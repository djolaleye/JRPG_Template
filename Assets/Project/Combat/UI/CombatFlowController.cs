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
    /// with the engine. Also renders a minimal battle-status text (full HUD is Phase 12).
    public sealed class CombatFlowController : MonoBehaviour
    {
        public static CombatFlowController Current { get; private set; }

        [Tooltip("Seconds to wait before an enemy acts, so turns are readable.")]
        [SerializeField] private float enemyTurnDelay = 0.6f;
        [Tooltip("Optional: auto-start this encounter on Start (isolated testing). Leave empty to wait for a trigger.")]
        [SerializeField] private string autoStartEncounterId = "";
        [SerializeField] private TMP_Text statusText;

        /// Cross-menu selection state for the command → action → target → confirm flow. UI-only: the
        /// engine is not told anything until ConfirmCombatActionAction submits.
        public string PendingActionId { get; set; }
        public IReadOnlyList<string> PendingTargetIds => _pendingTargetIds;

        private readonly List<string> _pendingTargetIds = new();

        public void SetPendingTargets(IEnumerable<string> combatantIds)
        {
            _pendingTargetIds.Clear();
            if (combatantIds != null) _pendingTargetIds.AddRange(combatantIds);
        }

        public void ClearPendingSelection()
        {
            PendingActionId = null;
            _pendingTargetIds.Clear();
        }

        private IEventBus _bus;
        private IMenuService _menus;
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
                AppContext.Services.TryResolve(out _menus);

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
        }

        // ---- Turn loop ----

        private void Update()
        {
            if (!_turnPending) return;
            _turnPending = false;

            var combat = Combat;
            if (combat == null || !combat.IsInBattle) return;

            var actor = combat.CurrentBattle.currentActor;
            if (actor == null) return;

            RefreshStatus();

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

        private IEnumerator EnemyTurnRoutine(string enemyId)
        {
            yield return new WaitForSeconds(enemyTurnDelay);
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
            => $"{c.displayName}  HP {c.currentHP}/{c.MaxHP}  MP {c.currentMP}/{c.MaxMP}{(c.isGuarding ? "  [Guard]" : "")}{(c.IsDefeated ? "  [Down]" : "")}";
    }
}
