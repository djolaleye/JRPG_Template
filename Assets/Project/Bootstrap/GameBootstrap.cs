using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Services;
using JRPG.Save;
using JRPG.Party;
using JRPG.Inventory;
using JRPG.Menu;
using JRPG.Combat;

namespace JRPG.Bootstrap
{

    /// Single composition root. The only place concrete service implementations are wired up.
    /// Lives in the first-loaded scene.

    [DefaultExecutionOrder(-10000)]
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameDatabase database;
        [SerializeField] private SaveFileConfig saveConfig;
        [SerializeField] private StartingInventoryConfig startingInventory;
        [SerializeField] private ContextualCanvasRegistry menuRegistry;
        [SerializeField] private Transform menuParent;

        [Tooltip("Stable id of the protagonist character. Seeded directly to Active and locked to Active/Reserve transitions.")]
        [SerializeField] private string protagonistId = "char_hero";
        [SerializeField] private bool logProbeOutput = true;

        public static ServiceRegistry Services { get; private set; }
        public static DataRegistry Data { get; private set; }
        public static GameStateController State { get; private set; }
        public static SaveRegistry SaveContributors { get; private set; }
        public static CharacterHolder Characters { get; private set; }
        public static PartyService Party { get; private set; }
        public static SimpleRecruitmentConditionStore RecruitmentConditions { get; private set; }
        public static InventoryService Inventory { get; private set; }
        public static EquipmentManager Equipment { get; private set; }
        public static MenuService Menus { get; private set; }
        public static CombatService Combat { get; private set; }

        private void Awake()
        {
            var bus = new EventBus();
            Services = new ServiceRegistry();
            Services.Register<IEventBus>(bus);
            Services.Register<IServiceRegistry>(Services);

            if (database == null)
            {
                Debug.LogError("[JRPG.Bootstrap] GameDatabase reference is missing.", this);
                return;
            }

            Data = new DataRegistry();
            try
            {
                Data.Build(database);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[JRPG.Bootstrap] Database build failed: {e.Message}", this);
                return;
            }

            State = new GameStateController(bus, new LayeredState(GameMode.MainMenu, OverlayState.None, InputContext.Menu));

            // Publish to JRPG.Core.AppContext so other assemblies can resolve services without a
            // direct reference on JRPG.Bootstrap (Bootstrap remains the only place that constructs them).
            AppContext.Initialize(Services, bus, State);
            AppContext.SetData(Data);

            SaveContributors = new SaveRegistry();
            Characters = new CharacterHolder(Data);
            SaveContributors.Register(Characters);
            AppContext.SetSaveContributors(SaveContributors);

            // Recruitment-condition evaluator: in-memory stub for now. Will be replaced by the
            // story-flag store once the dialogue/world phase lands.
            RecruitmentConditions = new SimpleRecruitmentConditionStore();
            Services.Register<IRecruitmentConditionEvaluator>(RecruitmentConditions);

            Party = new PartyService(Data, bus, protagonistId, RecruitmentConditions);
            Services.Register<IPartyService>(Party);
            SaveContributors.Register(Party);

            // Registration order matters — inventory before equipment
            // so equipment restore can return prior items to the inventory pool if needed.
            Inventory = new InventoryService(Data, bus);
            Services.Register<IInventoryService>(Inventory);
            SaveContributors.Register(Inventory);

            // EquipmentManager looks up live runtime instances by stable character id (SourceDataId).
            // This survives PartyService rebuilding its instance cache on Load, since SourceDataId is
            // authored and never regenerated.
            Equipment = new EquipmentManager(
                Data,
                Inventory.Container,
                bus,
                charId =>
                {
                    var active = Party.GetActiveCombatParty();
                    for (int i = 0; i < active.Count; i++)
                        if (active[i].SourceDataId == charId) return active[i];
                    return null;
                });
            Services.Register<IEquipmentService>(Equipment);
            SaveContributors.Register(Equipment);

            // Bake starting inventory (idempotent — skips if container has items, e.g. after a save load).
            StartingInventoryBaker.Bake(startingInventory, Inventory.Container, Data);

            // Combat service. Reads the active party (runtime instances) and consumes items via the
            // lean IInventoryService. Battle state is transient; party HP/MP/SP is committed back to
            // CharacterRuntimeInstance at battle end, so no separate combat save contributor is needed.
            Combat = new CombatService(bus, State, Data, Party, Inventory);
            Services.Register<ICombatService>(Combat);

            // Menu service.
            if (menuRegistry != null && menuParent != null)
            {
                Menus = new MenuService(menuRegistry, menuParent, State, bus);
                Services.Register<IMenuService>(Menus);
            }
            else
            {
                Debug.LogWarning("[JRPG.Bootstrap] MenuRegistry or menuParent missing — IMenuService not registered.", this);
            }

            if (saveConfig == null)
            {
                Debug.LogWarning("[JRPG.Bootstrap] SaveFileConfig is missing; save service not registered.", this);
            }
            else
            {
                var saveService = new SaveSystemCore(SaveContributors, saveConfig, bus, State);
                Services.Register<ISaveService>(saveService);
            }

            if (logProbeOutput)
            {
                var probe = new BootstrapSmokeProbe(bus, Services, Data);
                probe.Run();
                State.SetState(new LayeredState(GameMode.Exploration, OverlayState.None, InputContext.Exploration));
            }
        }
    }
}
