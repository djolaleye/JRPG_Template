using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Services;
using JRPG.Save;
using JRPG.Party;

namespace JRPG.Bootstrap
{

    /// Single composition root. The only place concrete service implementations are wired up.
    /// Lives in the first-loaded scene.

    [DefaultExecutionOrder(-10000)]
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameDatabase database;
        [SerializeField] private SaveFileConfig saveConfig;
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
