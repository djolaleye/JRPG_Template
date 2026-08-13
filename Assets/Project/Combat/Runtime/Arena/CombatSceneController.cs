using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using JRPG.Data;

namespace JRPG.Combat.Arena
{
    /// The Combat scene's lifecycle, advanced by explicit calls rather than by Unity execution order.
    /// Every transition is driven from the arena director.
    public enum CombatSceneState
    {
        Loaded,
        Initializing,
        ArenaConfigured,
        CombatantsConfigured,
        CameraConfigured,
        Ready,
        Active,
        Ending,
        Failed,
    }

    /// <summary>
    /// The Combat scene's single control surface: mount points, cameras, and the lifecycle state.
    ///
    /// <para><b>Inert on load.</b> Loading the scene does nothing — the camera is off, no arena is
    /// instantiated, and no battle starts. The director calls <see cref="BeginInitialize"/> and the
    /// staged <c>Mark…</c> transitions in order; skipping one is logged and refused.</para>
    ///
    /// <para><b>Discovered through a static sink</b>, like <c>PassiveDialogueSink</c>: this lives in a
    /// scene that loads and unloads, and <c>ServiceRegistry</c> throws on a duplicate registration
    /// and offers no unregister.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatSceneController : MonoBehaviour
    {
        /// Active controller, or null when the Combat scene is not resident.
        public static CombatSceneController Current { get; private set; }

        [Header("Mount points")]
        [Tooltip("The selected arena prefab is instantiated as a child of this transform.")]
        [SerializeField] private Transform arenaMount;

        [Tooltip("Staged combatant bodies are parented here, not under the arena — so an arena swap " +
                 "and a combatant respawn stay independent.")]
        [SerializeField] private Transform combatantRoot;

        [Tooltip("Body used for any combatant with no authored battlePrefab. Tinted by team.")]
        [SerializeField] private GameObject placeholderCombatant;

        [Header("Camera")]
        [Tooltip("The scene's presentation camera.")]
        [SerializeField] private Camera combatCamera;
        [SerializeField] private CinemachineBrain brain;
        [SerializeField] private CinemachineCamera introCamera;
        [SerializeField] private CinemachineCamera partyRevealCamera;
        [SerializeField] private CinemachineCamera battleCamera;

        private const int InactivePriority = 0;
        private const int ActivePriority = 20;

        public CombatSceneState State { get; private set; } = CombatSceneState.Loaded;

        /// True once every setup step has completed without error. The director will not activate
        /// combat before this.
        public bool ArenaReady => State is CombatSceneState.Ready or CombatSceneState.Active;

        public Transform ArenaMount => arenaMount;
        public Transform CombatantRoot => combatantRoot;
        public Camera CombatCamera => combatCamera;
        public GameObject PlaceholderCombatant => placeholderCombatant;

        /// The arena prefab instance currently staged, or null.
        public GameObject ArenaInstance { get; private set; }

        /// The staged arena's contract component. Distinct from the definition's prefab template.
        public ArenaRoot StagedArena { get; private set; }

        private readonly Dictionary<string, CombatantPresentation> _bodies = new();

        /// Staged bodies by combatant id — the same ids the engine uses, so a later floater or
        /// highlight can find a body without keeping a roster of its own.
        public IReadOnlyDictionary<string, CombatantPresentation> Bodies => _bodies;

        public CombatantPresentation FindBody(string combatantId)
            => !string.IsNullOrEmpty(combatantId) && _bodies.TryGetValue(combatantId, out var body) ? body : null;

        private void Awake()
        {
            if (brain == null && combatCamera != null) brain = combatCamera.GetComponent<CinemachineBrain>();

            // Inert on load: the exploration camera keeps the screen until the director says otherwise.
            SetCameraActive(false);
            ResetCameraPriorities();
        }

        private void OnEnable() => Current = this;

        private void OnDisable()
        {
            if (Current == this) Current = null;
        }

        // ---- Lifecycle ------------------------------------------------------------------------

        public void BeginInitialize()
        {
            if (!Require(CombatSceneState.Loaded, nameof(BeginInitialize))) return;
            State = CombatSceneState.Initializing;
        }

        public void MarkArenaConfigured()
        {
            if (!Require(CombatSceneState.Initializing, nameof(MarkArenaConfigured))) return;
            State = CombatSceneState.ArenaConfigured;
        }

        public void MarkCombatantsConfigured()
        {
            if (!Require(CombatSceneState.ArenaConfigured, nameof(MarkCombatantsConfigured))) return;
            State = CombatSceneState.CombatantsConfigured;
        }

        /// <summary>
        /// Snaps the three virtual cameras onto the arena's authored anchors and hands the intro
        /// framing priority.
        /// </summary>
        public void ConfigureCamera(ArenaRoot arena)
        {
            if (!Require(CombatSceneState.CombatantsConfigured, nameof(ConfigureCamera))) return;

            if (arena == null)
            {
                Fail("ConfigureCamera called with no arena.");
                return;
            }

            if (!Place(introCamera, arena.GetAnchor(ArenaAnchorKind.Intro), ArenaAnchorKind.Intro)) return;
            if (!Place(partyRevealCamera, arena.GetAnchor(ArenaAnchorKind.PartyReveal), ArenaAnchorKind.PartyReveal)) return;
            if (!Place(battleCamera, arena.GetAnchor(ArenaAnchorKind.Battle), ArenaAnchorKind.Battle)) return;

            ResetCameraPriorities();
            if (introCamera != null) introCamera.Priority = ActivePriority;

            State = CombatSceneState.CameraConfigured;
        }

        public void MarkReady()
        {
            if (!Require(CombatSceneState.CameraConfigured, nameof(MarkReady))) return;
            State = CombatSceneState.Ready;
        }

        public void Activate()
        {
            if (!Require(CombatSceneState.Ready, nameof(Activate))) return;
            State = CombatSceneState.Active;
        }

        public void BeginEnding()
        {
            State = CombatSceneState.Ending;
            SetCameraActive(false);
            ClearStaging();
        }

        // ---- Staged content --------------------------------------------------------------------

        /// Takes ownership of what the staging pass built, so teardown has a single place to look.
        public void AdoptStaging(GameObject arenaInstance, ArenaRoot stagedArena,
                                 IReadOnlyList<CombatantPresentation> bodies)
        {
            ArenaInstance = arenaInstance;
            StagedArena = stagedArena;

            _bodies.Clear();
            if (bodies == null) return;

            for (int i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                if (body == null || string.IsNullOrEmpty(body.CombatantId)) continue;

                // Two bodies claiming one id would silently shadow each other for every later lookup.
                if (!_bodies.TryAdd(body.CombatantId, body))
                    Debug.LogError($"[JRPG.Combat.Arena] Duplicate combatant id '{body.CombatantId}' " +
                                   "among the staged bodies.", body);
            }
        }

        /// <summary>
        /// Destroys the arena and every body. Called before restaging (a retry reuses this scene) and
        /// again on teardown, so it must be safe to run twice and safe to run on nothing.
        /// </summary>
        public void ClearStaging()
        {
            _bodies.Clear();
            StagedArena = null;

            if (ArenaInstance != null)
            {
                Destroy(ArenaInstance);
                ArenaInstance = null;
            }

            if (combatantRoot == null) return;

            for (int i = combatantRoot.childCount - 1; i >= 0; i--)
                Destroy(combatantRoot.GetChild(i).gameObject);
        }

        /// Aborts setup. The director reads <see cref="State"/> and unwinds rather than activating
        /// combat on a half-built arena.
        public void Fail(string reason)
        {
            State = CombatSceneState.Failed;
            SetCameraActive(false);
            Debug.LogError($"[JRPG.Combat.Arena] Combat scene setup failed: {reason}", this);
        }

        // ---- Camera ---------------------------------------------------------------------------

        /// <summary>Enables or disables the combat camera and its brain together. The director pairs
        /// this with disabling the exploration camera, so exactly one presentation camera is ever
        /// live.</summary>
        public void SetCameraActive(bool active)
        {
            if (combatCamera != null) combatCamera.enabled = active;
            if (brain != null) brain.enabled = active;
        }

        /// Hands priority to a specific stage of the reveal. The director sequences these; the blend
        /// itself is Cinemachine's.
        public void FocusCamera(ArenaAnchorKind kind)
        {
            ResetCameraPriorities();

            var target = kind switch
            {
                ArenaAnchorKind.Intro => introCamera,
                ArenaAnchorKind.PartyReveal => partyRevealCamera,
                ArenaAnchorKind.Battle => battleCamera,
                _ => null,
            };

            if (target != null) target.Priority = ActivePriority;
        }

        private void ResetCameraPriorities()
        {
            if (introCamera != null) introCamera.Priority = InactivePriority;
            if (partyRevealCamera != null) partyRevealCamera.Priority = InactivePriority;
            if (battleCamera != null) battleCamera.Priority = InactivePriority;
        }

        private bool Place(CinemachineCamera camera, Transform anchor, ArenaAnchorKind kind)
        {
            if (camera == null)
            {
                Fail($"the Combat scene has no {kind} virtual camera wired.");
                return false;
            }

            if (anchor == null)
            {
                Fail($"the arena supplies no {kind} anchor.");
                return false;
            }

            camera.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            return true;
        }

        // ---- Guards ---------------------------------------------------------------------------

        private bool Require(CombatSceneState expected, string call)
        {
            if (State == expected) return true;

            Debug.LogError($"[JRPG.Combat.Arena] {call} expected state {expected} but the scene is " +
                           $"{State}. The lifecycle step was ignored.", this);
            return false;
        }
    }
}
