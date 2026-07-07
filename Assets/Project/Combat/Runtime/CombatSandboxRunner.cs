using System.Collections.Generic;
using System.Text;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Combat
{
    /// Debug harness. Drives and inspects battles from the CombatSandbox scene with no UI.
    /// Resolves the combat service through AppContext (so JRPG.Combat does not depend on JRPG.Bootstrap)
    /// and exposes context-menu actions on the component.
    public sealed class CombatSandboxRunner : MonoBehaviour
    {
        [Header("Encounter")]
        [SerializeField] private string encounterId = "encounter_test_slimes";
        [SerializeField] private bool useActiveParty = true;

        [Tooltip("Optional explicit party stable ids (used when 'Use Active Party' is off).")]
        [SerializeField] private List<string> partyIds = new();
        [Tooltip("Optional explicit enemy stable ids (overrides the encounter's enemy list when set).")]
        [SerializeField] private List<string> enemyIds = new();

        [Header("Manual submission")]
        [SerializeField] private string selectedActionId = "attack_melee";
        [SerializeField] private string selectedTargetId = "";

        private CombatService _combat;
        private bool _subscribed;

        private CombatService Combat
        {
            get
            {
                if (_combat == null && AppContext.Services != null &&
                    AppContext.Services.TryResolve<JRPG.Services.ICombatService>(out var svc))
                    _combat = svc as CombatService;
                return _combat;
            }
        }

        private void OnEnable()
        {
            var bus = AppContext.Bus;
            if (bus == null || _subscribed) return;
            bus.Subscribe<BattleStarted>(OnBattleStarted);
            bus.Subscribe<TurnStarted>(OnTurnStarted);
            bus.Subscribe<BattleActionResolved>(OnActionResolved);
            bus.Subscribe<BattleEnded>(OnBattleEnded);
            bus.Subscribe<BattleResultPackaged>(OnResultPackaged);
            _subscribed = true;
        }

        private void OnDisable()
        {
            var bus = AppContext.Bus;
            if (bus == null || !_subscribed) return;
            bus.Unsubscribe<BattleStarted>(OnBattleStarted);
            bus.Unsubscribe<TurnStarted>(OnTurnStarted);
            bus.Unsubscribe<BattleActionResolved>(OnActionResolved);
            bus.Unsubscribe<BattleEnded>(OnBattleEnded);
            bus.Unsubscribe<BattleResultPackaged>(OnResultPackaged);
            _subscribed = false;
        }

        // ---- Event logging ------------------------------------------------------------------

        private void OnBattleStarted(BattleStarted e) => Debug.Log($"[Sandbox] BattleStarted battle={e.BattleId} encounter={e.EncounterId}");
        private void OnTurnStarted(TurnStarted e) => Debug.Log($"[Sandbox] TurnStarted actor={e.CombatantId}");
        private void OnActionResolved(BattleActionResolved e) => Debug.Log($"[Sandbox] ActionResolved actor={e.ActorCombatantId} action={e.ActionId} success={e.Success}");
        private void OnBattleEnded(BattleEnded e) => Debug.Log($"[Sandbox] BattleEnded battle={e.BattleId} outcome={e.Outcome}");
        private void OnResultPackaged(BattleResultPackaged e) => Debug.Log($"[Sandbox] BattleResultPackaged battle={e.BattleId} outcome={e.Outcome}");

        // ---- Context-menu actions -----------------------------------------------------------

        [ContextMenu("Start Battle")]
        public void StartBattle()
        {
            if (Combat == null) { Debug.LogError("[Sandbox] ICombatService not available. Is GameBootstrap in the scene?"); return; }

            Combat.StartBattle(new BattleStartRequest
            {
                encounterId = encounterId,
                useActivePartyFromPartyService = useActiveParty,
                partyCharacterIds = useActiveParty ? null : partyIds,
                enemyIds = enemyIds != null && enemyIds.Count > 0 ? enemyIds : null,
            });
            PrintState();
        }

        [ContextMenu("Print State")]
        public void PrintState()
        {
            var ctx = Combat?.CurrentBattle;
            if (ctx == null) { Debug.Log("[Sandbox] No active battle."); return; }

            var sb = new StringBuilder();
            sb.AppendLine($"[Sandbox] Phase={ctx.phase} Round={ctx.roundNumber} Actor={ctx.currentActor?.combatantId}");
            sb.AppendLine($"  Turn queue: {string.Join(", ", ctx.turnQueue)}");
            sb.AppendLine("  Party:");
            foreach (var c in ctx.partyCombatants) sb.AppendLine($"    {Describe(c)}");
            sb.AppendLine("  Enemies:");
            foreach (var c in ctx.enemyCombatants) sb.AppendLine($"    {Describe(c)}");
            Debug.Log(sb.ToString());
        }

        private static string Describe(CombatantInstance c)
            => $"{c.combatantId} HP {c.currentHP}/{c.MaxHP} MP {c.currentMP}/{c.MaxMP} SPD {c.Speed}{(c.isGuarding ? " [Guard]" : "")}{(c.IsDefeated ? " [Defeated]" : "")}";

        [ContextMenu("List Actions (current actor)")]
        public void ListActions()
        {
            var ctx = Combat?.CurrentBattle;
            if (ctx?.currentActor == null) { Debug.Log("[Sandbox] No current actor."); return; }
            var actions = Combat.GetAvailableActions(ctx.currentActor.combatantId);
            var ids = new List<string>();
            foreach (var a in actions) ids.Add(a.Id);
            Debug.Log($"[Sandbox] Actions for {ctx.currentActor.combatantId}: {string.Join(", ", ids)}");
        }

        [ContextMenu("List Targets (current actor + selected action)")]
        public void ListTargets()
        {
            var ctx = Combat?.CurrentBattle;
            if (ctx?.currentActor == null) { Debug.Log("[Sandbox] No current actor."); return; }
            var targets = Combat.GetValidTargets(ctx.currentActor.combatantId, selectedActionId);
            var ids = new List<string>();
            foreach (var t in targets) ids.Add(t.combatantId);
            Debug.Log($"[Sandbox] Targets for {selectedActionId}: {string.Join(", ", ids)}");
        }

        [ContextMenu("Submit Selected Action")]
        public void SubmitSelected()
        {
            var ctx = Combat?.CurrentBattle;
            if (ctx?.currentActor == null) { Debug.Log("[Sandbox] No current actor."); return; }
            var targetIds = string.IsNullOrEmpty(selectedTargetId) ? null : new List<string> { selectedTargetId };
            var result = Combat.SubmitAction(ctx.currentActor.combatantId, selectedActionId, targetIds);
            LogResult(result);
            PrintState();
        }

        [ContextMenu("Submit Melee On First Enemy")]
        public void SubmitMeleeOnFirstEnemy()
        {
            var ctx = Combat?.CurrentBattle;
            if (ctx?.currentActor == null) { Debug.Log("[Sandbox] No current actor."); return; }

            string targetId = null;
            foreach (var e in ctx.enemyCombatants)
                if (!e.IsDefeated) { targetId = e.combatantId; break; }

            var result = Combat.SubmitAction(ctx.currentActor.combatantId, "attack_melee",
                targetId == null ? null : new List<string> { targetId });
            LogResult(result);
            PrintState();
        }

        /// Drives an entire battle synchronously: party combatants melee the first living enemy,
        /// enemies act via their AI. Useful for one-click validation of the full loop.
        [ContextMenu("Auto-Play To End")]
        public void AutoPlayToEnd()
        {
            if (Combat == null) { Debug.LogError("[Sandbox] ICombatService not available."); return; }
            if (!Combat.IsInBattle) StartBattle();

            int guard = 0;
            while (Combat.IsInBattle && guard++ < 500)
            {
                var actor = Combat.CurrentBattle.currentActor;
                if (actor == null) break;

                if (actor.team == CombatantTeam.Party)
                {
                    string targetId = null;
                    foreach (var e in Combat.CurrentBattle.enemyCombatants)
                        if (!e.IsDefeated) { targetId = e.combatantId; break; }
                    var result = Combat.SubmitAction(actor.combatantId, "attack_melee",
                        targetId == null ? null : new List<string> { targetId });
                    LogResult(result);
                }
                else
                {
                    Combat.AdvanceEnemyTurn();
                }
            }

            PrintState();
            PrintLastResult();
        }

        [ContextMenu("Force Enemy Turn")]
        public void ForceEnemyTurn()
        {
            if (Combat == null) return;
            Combat.AdvanceEnemyTurn();
            PrintState();
        }

        [ContextMenu("Print Last Result")]
        public void PrintLastResult()
        {
            var r = Combat?.LastResult;
            if (r == null) { Debug.Log("[Sandbox] No battle result yet."); return; }
            Debug.Log($"[Sandbox] Result outcome={r.outcome} baseXP={r.baseXP} currency={r.currency} " +
                      $"turns={r.turnCount} elapsed={r.elapsedSeconds:0.00}s " +
                      $"defeatedEnemies=[{string.Join(", ", r.defeatedEnemyIds)}] " +
                      $"survivors=[{string.Join(", ", r.survivingPartyCharacterIds)}]");
        }

        private static void LogResult(ActionResult result)
        {
            if (result == null) { Debug.Log("[Sandbox] (null result)"); return; }
            if (!result.success) { Debug.LogWarning($"[Sandbox] Action failed: {result.failureReason}"); return; }

            var sb = new StringBuilder();
            sb.AppendLine($"[Sandbox] {result.actorCombatantId} used {result.actionId}:");
            foreach (var e in result.effects)
                sb.AppendLine($"    {e.effectType} -> {e.targetCombatantId}: {e.amount} (HP {e.hpBefore}->{e.hpAfter}){(e.wasDefeated ? " DEFEATED" : "")}");
            if (result.battleEnded) sb.AppendLine($"    Battle ended: {result.battleOutcome}");
            Debug.Log(sb.ToString());
        }
    }
}
