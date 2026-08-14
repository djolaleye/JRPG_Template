using System.Collections;
using UnityEngine;
using JRPG.Combat;
using JRPG.Combat.Arena;
using JRPG.Core;
using JRPG.Data;
using JRPG.Menu;
using JRPG.Party;
using JRPG.Save;
using JRPG.Services;

using AppContext = JRPG.Core.AppContext;

namespace JRPG.Bootstrap
{
    /// <summary>
    /// Owns the exploration ⇄ combat transition end to end: freeze, capture, arena, scene load,
    /// camera handoff, activation, and the mirrored teardown.
    ///
    /// <para><b>Why one object.</b> The order of these steps is the feature. The return pose has to be
    /// captured before the encounter is marked InProgress; the encounter has to be marked before
    /// anything loads; the arena has to be staged before the camera is handed over; the battle must
    /// not start until the arena is ready. Spreading that across the trigger, the scene, and the
    /// combat service is how it stops being true.</para>
    ///
    /// <para><b>It does not run the battle.</b> Activation ends with the ordinary
    /// <c>ICombatService.StartBattleFromActiveParty</c> call — the engine still sets its own state,
    /// the existing HUD still opens on the first turn. No battle state is duplicated here.</para>
    ///
    /// <para><b>Teardown watches state rather than hooking three exits.</b> Victory (post-battle
    /// flow), defeat-and-leave (defeat controller) and escape (combat service) each leave
    /// <c>GameMode.Combat</c> by their own route. Subscribing to that one transition covers all three
    /// without editing any of them.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8000)]   // after GameBootstrap (-10000) and SceneFlowService (-9000)
    public sealed class CombatArenaDirector : MonoBehaviour, IEncounterTransitionService
    {
        public static CombatArenaDirector Current { get; private set; }

        [Header("Wiring")]
        [SerializeField] private string combatSceneName = "Combat";
        [SerializeField] private ScreenTransitionController transition;

        [Header("Pacing")]
        [Tooltip("How long the exploration camera holds on the encounter before the fade begins.")]
        [Min(0f)] [SerializeField] private float introHoldSeconds = 1f;

        [Tooltip("Length of the pull-back from the intro framing to the battle framing.")]
        [Min(0f)] [SerializeField] private float revealSeconds = 1.2f;

        [Tooltip("Share of the pull-back spent at the PartyReveal anchor before continuing to Battle.")]
        [Range(0f, 1f)] [SerializeField] private float partyRevealShare = 0.45f;

        [Tooltip("Floor on how long the screen stays covered, so a fast load is still a transition.")]
        [Min(0f)] [SerializeField] private float minimumCoveredSeconds = 0.45f;

        private CombatContext _context;
        private bool _busy;
        private bool _tearingDown;
        private bool _registered;

        private IEventBus _bus;

        public bool IsBusy => _busy || _tearingDown;
        public CombatContext ActiveContext => _context;

        // ---- Lifecycle --------------------------------------------------------------------------

        private void Awake()
        {
            Current = this;
            if (transition == null) transition = FindFirstObjectByType<ScreenTransitionController>(FindObjectsInactive.Include);
            Register();
        }

        private void OnEnable()
        {
            Register();

            _bus = AppContext.Bus;
            _bus?.Subscribe<GameStateChanged>(OnGameStateChanged);
            _bus?.Subscribe<PostBattleFlowCompleted>(OnPostBattleFlowCompleted);
            _bus?.Subscribe<GameLoaded>(OnGameLoaded);
        }

        private void OnDisable()
        {
            _bus?.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            _bus?.Unsubscribe<PostBattleFlowCompleted>(OnPostBattleFlowCompleted);
            _bus?.Unsubscribe<GameLoaded>(OnGameLoaded);

            if (Current == this) Current = null;
        }

        /// Registered, this lives on the persistent Startup scene, which never
        /// reloads, so the registry's throw-on-duplicate is not a hazard here.
        private void Register()
        {
            if (_registered || AppContext.Services == null) return;

            if (AppContext.Services.TryResolve<IEncounterTransitionService>(out var existing) && existing != null)
            {
                if (!ReferenceEquals(existing, this))
                    Debug.LogError("[JRPG.Arena] A second CombatArenaDirector is present; only the first " +
                                   "is registered. Remove the duplicate from the Startup scene.", this);

                _registered = ReferenceEquals(existing, this);
                return;
            }

            AppContext.Services.Register<IEncounterTransitionService>(this);
            _registered = true;
        }

        // ---- Entry point ------------------------------------------------------------------------

        public bool RequestEncounter(string encounterId, Transform focus)
        {
            if (IsBusy)
            {
                Debug.Log("[JRPG.Arena] Encounter request ignored — a transition is already running.");
                return false;
            }

            var services = AppContext.Services;
            if (services == null) return false;

            if (services.TryResolve<ICombatService>(out var combat) && combat.IsInBattle)
            {
                Debug.Log("[JRPG.Arena] Encounter request ignored — already in battle.");
                return false;
            }

            if (!services.TryResolve<IWorldStateService>(out var world))
            {
                Debug.LogError("[JRPG.Arena] No IWorldStateService registered; encounters cannot be tracked.", this);
                return false;
            }

            if (!world.CanInitiate(encounterId))
            {
                Debug.Log($"[JRPG.Arena] Encounter '{encounterId}' is " +
                          $"{world.GetEncounterState(encounterId)} and cannot be started.");
                return false;
            }

            if (!services.TryResolve<ISceneFlowService>(out var flow))
            {
                Debug.LogError("[JRPG.Arena] No ISceneFlowService registered; the Combat scene cannot load.", this);
                return false;
            }

            if (!flow.IsSceneAvailable(combatSceneName))
            {
                Debug.LogError($"[JRPG.Arena] Combat scene '{combatSceneName}' is not in Build Settings.", this);
                return false;
            }

            var presentation = ExplorationPresentationSink.Current;
            if (presentation == null)
            {
                Debug.LogError("[JRPG.Arena] No exploration presentation agent in the scene; the player " +
                               "could not be returned after the battle.", this);
                return false;
            }

            if (!presentation.TryCapturePlayerPose(out var position, out var yaw)) return false;

            var returnContext = new BattleReturnContext
            {
                ExplorationSceneId = flow.CurrentContentScene,
                EncounterId = encounterId,
                PlayerPosition = position,
                PlayerRotationY = yaw,
            };

            services.TryResolve<IPartyRuntimeQueries>(out var party);
            var context = CombatContext.Build(encounterId, AppContext.Data as DataRegistry, party,
                                              returnContext, out var error);

            // Everything above is a read. A refusal here has changed nothing: the world is not frozen,
            // the encounter is still Ready, and no scene has been touched.
            if (context == null)
            {
                CombatContext.LogRefusal(error);
                return false;
            }

            _context = context;
            _busy = true;
            StartCoroutine(EnterCombatRoutine(world, flow, presentation, focus));
            return true;
        }

        // ---- Enter ------------------------------------------------------------------------------

        private IEnumerator EnterCombatRoutine(IWorldStateService world, ISceneFlowService flow,
                                               IExplorationPresentation presentation, Transform focus)
        {
            // Freeze first. Cutscene + Disabled kills the exploration action map, the Cinemachine input
            // axis, interaction, pause and any second encounter request in one assignment — the
            // exploration bridge already reacts to the input context.
            AppContext.State?.SetState(new LayeredState(GameMode.Cutscene, OverlayState.None, InputContext.Disabled));

            world.MarkInProgress(_context.EncounterId);

            // 1. Frame the encounter in the world, before anything technical happens.
            if (focus != null)
            {
                presentation.FocusOn(focus);
                if (introHoldSeconds > 0f) yield return new WaitForSecondsRealtime(introHoldSeconds);
            }

            // 2. Cover, and stay covered — the FadeOut/FadeIn pair, not Transition(), because the work
            // between them spans frames.
            yield return Cover();
            float coveredAt = Time.realtimeSinceStartup;

            // 3. Load behind the cover.
            bool loaded = false;
            flow.LoadOverlay(combatSceneName, true, () => loaded = true);
            while (!loaded) yield return null;

            var scene = CombatSceneController.Current;
            if (scene == null)
            {
                yield return AbortRoutine(world, flow, presentation,
                                          $"the Combat scene '{combatSceneName}' loaded without a CombatSceneController.");
                yield break;
            }

            // 4. Stage the arena and the bodies.
            scene.BeginInitialize();

            var staging = new ArenaStagingService(AppContext.Data as DataRegistry);
            if (!staging.Stage(_context, scene, out var stageError))
            {
                yield return AbortRoutine(world, flow, presentation, stageError);
                yield break;
            }

            // 5. Cameras onto the arena's anchors, still off.
            scene.ConfigureCamera(scene.StagedArena);
            if (scene.State == CombatSceneState.Failed)
            {
                yield return AbortRoutine(world, flow, presentation, "the combat camera could not be configured.");
                yield break;
            }

            scene.MarkReady();

            // 6. Handoff, under cover: exactly one presentation camera is live at every instant.
            scene.FocusCamera(ArenaAnchorKind.Intro);
            presentation.SetCameraActive(false);
            scene.SetCameraActive(true);

            // One frame for the brain to adopt the intro framing before it is revealed.
            yield return null;

            // 7. Hold the cover out to the floor, so a two-frame load still reads as a transition.
            float remaining = minimumCoveredSeconds - (Time.realtimeSinceStartup - coveredAt);
            if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);

            yield return Uncover();

            // 8. Pull back through the party reveal to the battle framing.
            yield return RevealRoutine(scene);

            // 9. Hand over to the engine. Unchanged: it sets its own combat state and the existing HUD
            // opens on the first party turn.
            scene.Activate();

            if (AppContext.Services.TryResolve<ICombatService>(out var combat))
                combat.StartBattleFromActiveParty(_context.EncounterId);
            else
                Debug.LogError("[JRPG.Arena] No ICombatService registered; the arena is staged but no " +
                               "battle can start.", this);

            _busy = false;
        }

        /// The Cinemachine blend does the movement; this only decides when each anchor takes priority.
        private IEnumerator RevealRoutine(CombatSceneController scene)
        {
            if (revealSeconds <= 0f)
            {
                scene.FocusCamera(ArenaAnchorKind.Battle);
                yield break;
            }

            float toReveal = revealSeconds * partyRevealShare;

            scene.FocusCamera(ArenaAnchorKind.PartyReveal);
            if (toReveal > 0f) yield return new WaitForSecondsRealtime(toReveal);

            scene.FocusCamera(ArenaAnchorKind.Battle);
            yield return new WaitForSecondsRealtime(revealSeconds - toReveal);
        }

        /// <summary>
        /// Unwinds a failed setup: the encounter goes back to Ready, the overlay is dropped, the world
        /// is handed back to the player. A failure must never leave an encounter stuck InProgress or
        /// the screen black.
        /// </summary>
        private IEnumerator AbortRoutine(IWorldStateService world, ISceneFlowService flow,
                                         IExplorationPresentation presentation, string reason)
        {
            Debug.LogError($"[JRPG.Arena] Encounter '{_context?.EncounterId}' aborted — {reason}", this);

            var scene = CombatSceneController.Current;
            if (scene != null) scene.BeginEnding();

            bool unloaded = false;
            flow.UnloadOverlay(() => unloaded = true);
            while (!unloaded) yield return null;

            presentation.SetCameraActive(true);
            presentation.ReleaseFocus();

            world.ResetEncounter(_context?.EncounterId);
            _context = null;
            _busy = false;

            AppContext.State?.SetState(new LayeredState(GameMode.Exploration, OverlayState.None, InputContext.Exploration));
            yield return Uncover();
        }

        // ---- Exit -------------------------------------------------------------------------------

        /// <summary>
        /// The encounter is Complete only once the post-battle flow has finished — not when the battle
        /// ends, and never when a scene merely loads. Published from
        /// <c>ProgressionService.CompletePostBattleFlow</c>, which runs before the flow controller
        /// restores exploration state, so this always lands ahead of teardown.
        /// </summary>
        private void OnPostBattleFlowCompleted(PostBattleFlowCompleted e)
        {
            if (_context == null) return;
            if (AppContext.Services == null || !AppContext.Services.TryResolve<IWorldStateService>(out var world)) return;

            world.MarkComplete(_context.EncounterId);
        }

        private void OnGameStateChanged(GameStateChanged e)
        {
            // Leaving combat by any route — victory, escape, or defeat-and-leave — is the teardown
            // signal. Retry after defeat never leaves Combat, so it correctly does not tear down.
            if (e.Previous.Mode != GameMode.Combat || e.Current.Mode == GameMode.Combat) return;
            if (_context == null || _tearingDown) return;

            // Going to the main menu means the whole world is being replaced by a scene swap that
            // owns the screen and unloads the overlay on its way. Running the normal teardown against
            // it would fight for the fader and then restore Exploration state over the title screen.
            if (e.Current.Mode == GameMode.MainMenu)
            {
                Abandon("the session returned to the title screen");
                return;
            }

            _tearingDown = true;
            StartCoroutine(ExitCombatRoutine());
        }

        /// <summary>
        /// Drops the battle without touching the screen or the game state, for the cases where
        /// something else is already replacing the world. Releases the exploration camera because the
        /// enter path disabled it, and that scene may still be resident for a few more frames.
        /// </summary>
        private void Abandon(string reason)
        {
            StopAllCoroutines();
            _busy = false;
            _tearingDown = false;
            _context = null;

            var scene = CombatSceneController.Current;
            if (scene != null) scene.BeginEnding();

            var presentation = ExplorationPresentationSink.Current;
            if (presentation != null)
            {
                presentation.SetCameraActive(true);
                presentation.ReleaseFocus();
            }

            Debug.Log($"[JRPG.Arena] Battle abandoned — {reason}.");
        }

        private void OnGameLoaded(GameLoaded e)
        {
            // A load replaces the world wholesale.
            // The overlay itself is dropped by the scene-flow guard on the content swap, which
            // also owns the screen — so this abandons rather than tearing down.
            if (_context != null) Abandon("a save was loaded");
        }

        private IEnumerator ExitCombatRoutine()
        {
            var context = _context;
            var services = AppContext.Services;

            yield return Cover();

            var scene = CombatSceneController.Current;
            if (scene != null) scene.BeginEnding();

            if (services != null && services.TryResolve<ISceneFlowService>(out var flow))
            {
                bool unloaded = false;
                flow.UnloadOverlay(() => unloaded = true);
                while (!unloaded) yield return null;
            }

            var presentation = ExplorationPresentationSink.Current;
            if (presentation != null)
            {
                presentation.SetCameraActive(true);
                presentation.ReleaseFocus();

                if (context?.ReturnContext != null)
                    presentation.RestorePlayerPose(context.ReturnContext.PlayerPosition,
                                                   context.ReturnContext.PlayerRotationY);
            }

            // Anything that did not reach Complete is fightable again: defeat-and-leave and escape both
            // land here, and neither resolved the encounter.
            if (services != null && services.TryResolve<IWorldStateService>(out var world) && context != null)
            {
                if (world.GetEncounterState(context.EncounterId) != EncounterState.Complete)
                    world.ResetEncounter(context.EncounterId);
            }

            _context = null;

            // The exiting flow already asked for exploration state; re-asserting it is how input is
            // guaranteed back even if that flow set something else on the way out.
            AppContext.State?.SetState(new LayeredState(GameMode.Exploration, OverlayState.None, InputContext.Exploration));

            // One frame so the restored pose is applied before the screen comes back.
            yield return null;

            yield return Uncover();
            _tearingDown = false;
        }

        // ---- Screen -----------------------------------------------------------------------------

        /// <summary>
        /// Longest we will wait on a fade callback.
        ///
        /// <para><b>Why a timeout at all.</b> <c>ScreenTransitionController</c> is interrupt-and-retarget:
        /// a second caller — a scene swap, say — cancels the first request and <i>drops</i> its pending
        /// callbacks rather than firing them. Waiting on a callback that will never come strands this
        /// coroutine mid-teardown, holding a battle context forever. Continuing after the timeout is
        /// always better than not continuing at all: whoever superseded us owns the screen and will
        /// uncover it.</para>
        /// </summary>
        private const float FadeCallbackTimeout = 3f;

        private IEnumerator Cover() => AwaitFade(done => transition.FadeOut(done), "fade out");

        private IEnumerator Uncover() => AwaitFade(done => transition.FadeIn(done), "fade in");

        private IEnumerator AwaitFade(System.Action<System.Action> begin, string label)
        {
            if (transition == null) yield break;

            bool done = false;
            begin(() => done = true);

            float deadline = Time.realtimeSinceStartup + FadeCallbackTimeout;
            while (!done)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Debug.LogWarning($"[JRPG.Arena] The {label} callback never arrived — another caller " +
                                     "took the screen. Continuing.", this);
                    yield break;
                }

                yield return null;
            }
        }
    }
}
