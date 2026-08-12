using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using JRPG.Core;
using JRPG.Services;

namespace JRPG.Combat.UI
{
    /// Drives the combat UI loop and carries the only piece of cross-menu state (the pending action id).
    /// Opens the command menu on a party turn and advances enemy turns after a short delay. All work is
    /// deferred out of the (synchronous) combat event handlers into Update/coroutines to avoid reentrancy
    /// with the engine.
    public sealed class CombatFlowController : MonoBehaviour
    {
        public static CombatFlowController Current { get; private set; }

        [Tooltip("Seconds to wait before an enemy acts, so turns are readable.")]
        [SerializeField] private float enemyTurnDelay = 1.6f;
        [Tooltip("Seconds the \"X is stunned!\" notice stays up before a blocked turn is skipped. Applies " +
                 "to party and enemy actors alike.")]
        [SerializeField] private float blockedTurnNoticeSeconds = 1.2f;
        [Tooltip("Optional: auto-start this encounter on Start (isolated testing). Leave empty to wait for a trigger.")]
        [SerializeField] private string autoStartEncounterId = "";

        [Tooltip("Seconds the escape notice stays up. Escaping otherwise snaps back to exploration with " +
                 "no acknowledgement that the attempt succeeded.")]
        [SerializeField] private float escapeNoticeSeconds = 1.6f;

        [SerializeField] private string escapeNoticeText = "Got away safely!";
        [SerializeField] private TMP_Text statusText;

        /// Cross-menu selection state for the command → action → target → confirm flow. UI-only: the
        /// engine is not told anything until ConfirmCombatActionAction submits.
        public string PendingActionId { get; set; }
        public IReadOnlyList<string> PendingTargetIds => _pendingTargetIds;

        private readonly List<string> _pendingTargetIds = new();

        /// <summary>
        /// The target the cursor is currently over on the target screen.
        ///
        /// <para>Distinct from <see cref="PendingTargetIds"/>, which only fills once the player has
        /// committed. The HUD needs the pre-commit value so the enemy banner can follow the cursor as it
        /// moves.</para>
        /// </summary>
        public string HighlightedTargetId { get; set; }

        public void SetPendingTargets(IEnumerable<string> combatantIds)
        {
            _pendingTargetIds.Clear();
            if (combatantIds != null) _pendingTargetIds.AddRange(combatantIds);
        }

        public void ClearPendingSelection()
        {
            PendingActionId = null;
            HighlightedTargetId = null;
            _pendingTargetIds.Clear();
        }

        private IEventBus _bus;
        private IMenuService _menus;
        private ICombatInterruptionService _interrupter;
        private CombatService _combat;

        private bool _turnPending;
        private string _lastActionSummary = "";

        public CombatService Combat => _combat ??= ResolveCombat();
        public string CurrentActorId => Combat?.CurrentBattle?.currentActor?.combatantId;

        private void Awake()
        {
            Current = this;
        }

        private void OnEnable()
        {
            _bus = AppContext.Bus;
            if (AppContext.Services != null)
            {
                AppContext.Services.TryResolve(out _menus);
                AppContext.Services.TryResolve(out _interrupter);
            }

            if (_bus != null)
            {
                _bus.Subscribe<BattleStarted>(OnBattleStarted);
                _bus.Subscribe<TurnStarted>(OnTurnStarted);
                _bus.Subscribe<BattleActionResolved>(OnActionResolved);
                _bus.Subscribe<BattleEnded>(OnBattleEnded);
            }
        }

        private void OnDisable()
        {
            if (_bus != null)
            {
                _bus.Unsubscribe<BattleStarted>(OnBattleStarted);
                _bus.Unsubscribe<TurnStarted>(OnTurnStarted);
                _bus.Unsubscribe<BattleActionResolved>(OnActionResolved);
                _bus.Unsubscribe<BattleEnded>(OnBattleEnded);
            }
            if (Current == this) Current = null;
        }

        private void Start()
        {
            if (!string.IsNullOrEmpty(autoStartEncounterId))
                Combat?.StartBattleFromActiveParty(autoStartEncounterId);
        }

        private CombatService ResolveCombat()
        {
            if (AppContext.Services != null && AppContext.Services.TryResolve<ICombatService>(out var svc))
                return svc as CombatService;
            return null;
        }

        // ---- Event handlers (defer real work to Update to avoid engine reentrancy) ----

        private void OnBattleStarted(BattleStarted e)
        {
            _lastActionSummary = "";
            RefreshStatus();
        }

        private void OnTurnStarted(TurnStarted e) => _turnPending = true;

        private void OnActionResolved(BattleActionResolved e)
        {
            _lastActionSummary = $"{e.ActorCombatantId} used {e.ActionId}";
            RefreshStatus();
        }

        private void OnBattleEnded(BattleEnded e)
        {
            _turnPending = false;
            ClearPendingSelection();
            _menus?.CloseAll();
            _lastActionSummary = $"Battle ended: {e.Outcome}";
            RefreshStatus();


            if (e.Outcome == BattleOutcome.Escaped)
                PassiveDialogueSink.Current?.ShowLine(null, escapeNoticeText, escapeNoticeSeconds);
        }

        // ---- Turn loop ----

        private void Update()
        {
            // While an interactive interruption is holding combat, do not pump the turn loop. Open
            // (deferred) interruption dialogue — one frame after the submitting menu action's
            // teardown. Leave _turnPending set so the current turn resumes automatically once the dialogue resolves.
            if (_interrupter != null && _interrupter.IsInterruptionActive)
            {
                _interrupter.PumpPendingInterruptionDialogue();
                return;
            }

            if (!_turnPending) return;
            _turnPending = false;

            var combat = Combat;
            if (combat == null || !combat.IsInBattle) return;

            var actor = combat.CurrentBattle.currentActor;
            if (actor == null) return;

            RefreshStatus();

            // A combatant under a total action restriction (stun/sleep/freeze) never reaches the command
            // menu or the AI. Checked before the team split so both sides behave identically.
            if (combat.IsTurnBlocked(actor, out var blockedMessage))
            {
                StartCoroutine(BlockedTurnRoutine(actor.combatantId, blockedMessage));
                return;
            }

            if (actor.team == CombatantTeam.Party)
            {
                if (_menus != null && _menus.ActiveMenuId != "combat_command")
                    _menus.Open("combat_command", null);
            }
            else
            {
                StartCoroutine(EnemyTurnRoutine(actor.combatantId));
            }
        }

        /// Shows the blocked-turn notice, then hands the turn back to combat to be consumed. The delay is
        /// purely so the player can read why nothing happened; all the rules (status ticks, expiry,
        /// win/loss, advance) live in CombatService.SkipBlockedTurn.
        private IEnumerator BlockedTurnRoutine(string actorId, string message)
        {
            _lastActionSummary = message;
            RefreshStatus();

            // Read-only overlay: no menu frame, no LayeredState change, no input capture — so the
            // command hub is never opened for this turn and nothing has to be torn down afterwards.
            PassiveDialogueSink.Current?.ShowLine(null, message, blockedTurnNoticeSeconds);

            yield return new WaitForSeconds(blockedTurnNoticeSeconds);

            // An interruption that began during the notice owns combat now; re-arm so this turn is
            // re-evaluated (and skipped again) once the dialogue resolves.
            if (_interrupter != null && _interrupter.IsInterruptionActive) { _turnPending = true; yield break; }

            var combat = Combat;
            if (combat != null && combat.IsInBattle && combat.CurrentBattle.currentActor?.combatantId == actorId)
                combat.SkipBlockedTurn(actorId);
        }

        private IEnumerator EnemyTurnRoutine(string enemyId)
        {
            yield return new WaitForSeconds(enemyTurnDelay);

            // If an interruption became active during the delay, defer: re-arm so the turn resumes
            // after the dialogue resolves.
            if (_interrupter != null && _interrupter.IsInterruptionActive) { _turnPending = true; yield break; }

            var combat = Combat;
            if (combat != null && combat.IsInBattle && combat.CurrentBattle.currentActor?.combatantId == enemyId)
                combat.AdvanceEnemyTurn();
        }

        // ---- Minimal status text ----

        private void RefreshStatus()
        {
            if (statusText == null) return;
            var combat = Combat;
            var ctx = combat?.CurrentBattle;
            if (ctx == null) { statusText.text = ""; return; }

            var sb = new StringBuilder();
            sb.AppendLine($"Turn: {ctx.currentActor?.displayName ?? "-"}   Round {ctx.roundNumber}");
            if (!string.IsNullOrEmpty(_lastActionSummary)) sb.AppendLine(_lastActionSummary);
            sb.AppendLine("— Party —");
            foreach (var c in ctx.partyCombatants) sb.AppendLine(Line(c));
            sb.AppendLine("— Enemies —");
            foreach (var c in ctx.enemyCombatants) sb.AppendLine(Line(c));
            statusText.text = sb.ToString();
        }

        private static string Line(CombatantInstance c)
        {
            var sb = new StringBuilder();
            sb.Append($"{c.displayName}  HP {c.currentHP}/{c.MaxHP}  MP {c.currentMP}/{c.MaxMP}  SP {c.currentSP}/{c.MaxSP}");
            if (c.isGuarding) sb.Append("  [GUARDING]");

            for (int i = 0; i < c.activeStatuses.Count; i++)
            {
                var s = c.activeStatuses[i];
                sb.Append($"  [{s.statusId.Replace("status_", "")}");
                if (s.stacks > 1) sb.Append($" x{s.stacks}");
                sb.Append(']');
            }

            if (c.IsDefeated) sb.Append("  [DOWN]");
            return sb.ToString();
        }
    }
}
