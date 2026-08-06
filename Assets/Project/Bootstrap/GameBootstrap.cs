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

        [Tooltip("Optional (Phase 10): importance→presentation mapping for mid-battle/contextual dialogue. " +
                 "If unset, a built-in fallback is used (Passive→passive overlay, Critical→blocks input).")]
        [SerializeField] private DialoguePresentationProfile dialoguePresentationProfile;

        [Tooltip("Stable id of the protagonist character. Seeded directly to Active and locked to Active/Reserve transitions.")]
        [SerializeField] private string protagonistId = "char_hero";

        [Tooltip("Content scene a fresh game starts in. Requested by ISessionService.NewGame() through " +
                 "ISceneFlowService, which SceneFlowService registers on the Startup scene.")]
        [SerializeField] private string newGameSceneName = "TestExplore";

        [Tooltip("Content scene ISessionService.ReturnToTitle goes back to. Must match the scene that " +
                 "hosts TitleScreenController.")]
        [SerializeField] private string titleSceneName = "Title";

        [Tooltip("Run bootstrap smoke probe (diagnostic logging of resolved services and state changes).")]
        [SerializeField] private bool logProbeOutput = true;

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
            // PartyService implements both roster surfaces. Register the runtime-instance surface
            // explicitly so consumers resolve IPartyRuntimeQueries directly instead of resolving
            // IPartyService and downcasting with `as`.
            services.Register<IPartyRuntimeQueries>(party);
            saveContributors.Register(party);

            // Registration order matters — inventory before equipment
            // so equipment restore can return prior items to the inventory pool if needed.
            // Story is passed so item visibility can honour ItemUsageRule.requiredStoryFlag; it is
            // already constructed above, ahead of the roster, for the recruitment evaluator.
            var inventory = new InventoryService(data, bus, story);
            services.Register<IInventoryService>(inventory);
            saveContributors.Register(inventory);

            // EquipmentManager resolves live instances by stable character id via ResolveInstanceById,
            // which covers active/reserve/guest members and rebuilds a cleared instance on load.
            var equipment = new EquipmentManager(data, inventory.Container, bus, charId => party.ResolveInstanceById(charId));
            services.Register<IEquipmentService>(equipment);
            saveContributors.Register(equipment);

            // Bake starting inventory + starting equipment (idempotent).
            StartingInventoryBaker.Bake(startingInventory, inventory.Container, data, equipment);

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

            // Phase 10 mid-battle interruption evaluator. Bridges combat↔dialogue: it uses CombatService
            // concretely but is exposed only as ICombatInterruptionService, and resolves IDialogueService
            // lazily (dialogue is constructed just above; combat below cannot depend on it directly).
            var interrupter = new CombatSequenceInterrupter(combat, data, services, bus)
            {
                PresentationProfile = dialoguePresentationProfile,
            };
            combat.Interrupter = interrupter;
            services.Register<ICombatInterruptionService>(interrupter);

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

            // Session lifecycle (New Game / Load Game / Return to Title). Constructed last because
            // it sequences resets across every stateful service above. ISceneFlowService (Phase 12.2),
            // ISaveService and IMenuService are resolved lazily off the registry inside it, so it is
            // safe for them to be registered later or not at all.
            var session = new SessionService(services, state, data,
                                             story, inventory, equipment, party, progression,
                                             startingInventory, newGameSceneName, titleSceneName);
            services.Register<ISessionService>(session);

            // Diagnostic only.
            if (logProbeOutput)
            {
                var probe = new BootstrapSmokeProbe(bus, services, data);
                probe.Run();
            }

        }
    }
}
