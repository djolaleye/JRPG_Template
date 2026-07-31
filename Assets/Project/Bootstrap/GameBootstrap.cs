using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Services;
using JRPG.Save;
using JRPG.Party;
using JRPG.Inventory;
using JRPG.Menu;
using JRPG.Combat;
using JRPG.Progression;
using JRPG.Dialogue;

namespace JRPG.Bootstrap
{
    /// <summary>
    /// Root — the only place concrete service implementations are wired up. Lives
    /// in the first-loaded scene. Services are constructed as locals and published exclusively through
    /// <see cref="AppContext"/> and the <see cref="ServiceRegistry"/>
    /// </summary>
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

        [Tooltip("Run bootstrap smoke probe (diagnostic logging of resolved services and state changes).")]
        [SerializeField] private bool logProbeOutput = true;

        [Tooltip("Transition straight into Exploration after boot. This is the interim entry point until " +
                 "the Phase 12 main-menu New Game flow exists.")]
        [SerializeField] private bool autoStartExploration = true;

        private void Awake()
        {
            var bus = new EventBus();
            var services = new ServiceRegistry();
            services.Register<IEventBus>(bus);
            services.Register<IServiceRegistry>(services);

            if (database == null)
            {
                Debug.LogError("[JRPG.Bootstrap] GameDatabase reference is missing.", this);
                return;
            }

            var data = new DataRegistry();
            try
            {
                data.Build(database);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[JRPG.Bootstrap] Database build failed: {e.Message}", this);
                return;
            }

            var state = new GameStateController(bus, new LayeredState(GameMode.MainMenu, OverlayState.None, InputContext.Menu));

            // Publish to JRPG.Core.AppContext so other assemblies can resolve services without a
            // direct reference on JRPG.Bootstrap.
            AppContext.Initialize(services, bus, state);
            AppContext.SetData(data);

            var saveContributors = new SaveRegistry();
            AppContext.SetSaveContributors(saveContributors);

            // Story-flag store. Doubles as the recruitment-condition evaluator so dialogue
            // Built before PartyService because PartyService consumes the evaluator.
            var story = new StoryStateService(bus);
            services.Register<IStoryStateService>(story);
            services.Register<IRecruitmentConditionEvaluator>(story);
            saveContributors.Register(story);

            var party = new PartyService(data, bus, protagonistId, story);
            services.Register<IPartyService>(party);
            saveContributors.Register(party);

            // Registration order matters — inventory before equipment
            // so equipment restore can return prior items to the inventory pool if needed.
            var inventory = new InventoryService(data, bus);
            services.Register<IInventoryService>(inventory);
            saveContributors.Register(inventory);

            // EquipmentManager resolves live instances by stable character id via ResolveInstanceById,
            // which covers active/reserve/guest members and rebuilds a cleared instance on load.
            var equipment = new EquipmentManager(data, inventory.Container, bus, charId => party.ResolveInstanceById(charId));
            services.Register<IEquipmentService>(equipment);
            saveContributors.Register(equipment);

            // Bake starting inventory (idempotent).
            StartingInventoryBaker.Bake(startingInventory, inventory.Container, data);

            // Combat service. Reads the active party (runtime instances) and consumes items via the
            // lean IInventoryService.
            var combat = new CombatService(bus, state, data, party, inventory);
            services.Register<ICombatService>(combat);

            // Progression service. Consumes BattleResultPackaged (which carries the packaged result,
            // owns the post-battle flow, and persists per-character level/XP/points.
            // Registered as a save contributor after Party to re-stamp level/XP onto the instances PartyService rebuilds at level 1.
            var progression = new ProgressionService(data, bus, party, party, inventory);
            services.Register<IProgressionService>(progression);
            saveContributors.Register(progression);

            // Dialogue service. Talks to party/inventory/combat/story through interfaces only, drives
            // the presenter via IMenuService, and hands off StartBattle after dialogue closes.
            var dialogue = new DialogueService(services, bus, state, data, party, inventory, story);
            services.Register<IDialogueService>(dialogue);

            // Menu service
            if (menuRegistry != null && menuParent != null)
            {
                var menus = new MenuService(menuRegistry, menuParent, state, bus);
                services.Register<IMenuService>(menus);
            }
            else
            {
                Debug.LogWarning("[JRPG.Bootstrap] MenuRegistry or menuParent missing — IMenuService not registered.", this);
            }

            // Save service (only if a config is assigned).
            if (saveConfig == null)
            {
                Debug.LogWarning("[JRPG.Bootstrap] SaveFileConfig is missing; save service not registered.", this);
            }
            else
            {
                var saveService = new SaveSystemCore(saveContributors, saveConfig, bus, state);
                services.Register<ISaveService>(saveService);
            }

            // Diagnostic only.
            if (logProbeOutput)
            {
                var probe = new BootstrapSmokeProbe(bus, services, data);
                probe.Run();
            }

            // Interim entry point: drop into Exploration so gameplay (and saving) is reachable. The
            // Phase 12 main-menu New Game flow will replace this with an explicit session-start path.
            if (autoStartExploration)
                state.SetState(new LayeredState(GameMode.Exploration, OverlayState.None, InputContext.Exploration));
        }
    }
}
