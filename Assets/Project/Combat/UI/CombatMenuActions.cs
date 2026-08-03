using UnityEngine;
using JRPG.Menu;

namespace JRPG.Combat.UI
{
    /// Chooses a combat action for the current actor and advances to target selection — or straight to
    /// confirmation for self-target actions (e.g. Guard), which have nothing to pick. Never mutates
    /// combat: nothing reaches the engine until the player confirms.
    public sealed class ChooseCombatActionAction : IMenuAction
    {
        private readonly string _actionId;
        public ChooseCombatActionAction(string actionId) { _actionId = actionId; }

        public bool CanExecute(MenuContext c)
        {
            var flow = CombatFlowController.Current;
            var combat = flow?.Combat;
            var actorId = flow?.CurrentActorId;
            if (combat == null || string.IsNullOrEmpty(actorId)) return false;
            return combat.CanAfford(actorId, _actionId);
        }

        public void Execute(MenuContext c)
        {
            var flow = CombatFlowController.Current;
            var combat = flow?.Combat;
            var actorId = flow?.CurrentActorId;
            if (combat == null || string.IsNullOrEmpty(actorId)) return;

            flow.PendingActionId = _actionId;

            if (CombatRowFormat.IsAutoTargeted(_actionId))
            {
                // Self / All / Random are resolved engine-side by the targeting system, so there is
                // nothing for the player to pick — record the preview set and skip to confirmation.
                if (CombatRowFormat.IsSelfTarget(_actionId))
                    flow.SetPendingTargets(new[] { actorId });
                else
                    flow.SetPendingTargets(PreviewAutoTargets(combat, actorId, _actionId));

                c.Menus?.Open("combat_confirm", null);
            }
            else
            {
                flow.SetPendingTargets(null);
                c.Menus?.Open("combat_target", null);
            }
        }

        public string GetDisabledReason(MenuContext c) => "Action unavailable or unaffordable.";

        /// Preview set shown on the confirm screen for auto-resolved actions. The engine re-resolves
        /// at submission, so this is display only (a Random action may land elsewhere).
        private static string[] PreviewAutoTargets(CombatService combat, string actorId, string actionId)
        {
            var valid = combat.GetValidTargets(actorId, actionId);
            var ids = new string[valid.Count];
            for (int i = 0; i < valid.Count; i++) ids[i] = valid[i].combatantId;
            return ids;
        }
    }

    /// Records the chosen target and advances to confirmation. Submission happens only once the player
    /// confirms — see <see cref="ConfirmCombatActionAction"/>.
    public sealed class ChooseCombatTargetAction : IMenuAction
    {
        private readonly string _targetCombatantId;
        public ChooseCombatTargetAction(string targetCombatantId) { _targetCombatantId = targetCombatantId; }

        public bool CanExecute(MenuContext c)
        {
            var flow = CombatFlowController.Current;
            return flow?.Combat != null
                && !string.IsNullOrEmpty(flow.CurrentActorId)
                && !string.IsNullOrEmpty(flow.PendingActionId)
                && !string.IsNullOrEmpty(_targetCombatantId);
        }

        public void Execute(MenuContext c)
        {
            var flow = CombatFlowController.Current;
            if (flow?.Combat == null) return;

            flow.SetPendingTargets(new[] { _targetCombatantId });
            c.Menus?.Open("combat_confirm", null);
        }

        public string GetDisabledReason(MenuContext c) => "No pending action or target.";
    }

    /// The only path from the menus into the engine. Revalidates at the moment of submission (costs and
    /// targets may have moved since selection) and honours the returned ActionResult — a rejected submit
    /// keeps the confirm screen open rather than closing the menus and stranding the turn.
    public sealed class ConfirmCombatActionAction : IMenuAction
    {
        public bool CanExecute(MenuContext c)
        {
            var flow = CombatFlowController.Current;
            var combat = flow?.Combat;
            if (combat == null || string.IsNullOrEmpty(flow.CurrentActorId) || string.IsNullOrEmpty(flow.PendingActionId))
                return false;

            return combat.PreviewAction(flow.CurrentActorId, flow.PendingActionId, flow.PendingTargetIds).usable;
        }

        public void Execute(MenuContext c)
        {
            var flow = CombatFlowController.Current;
            var combat = flow?.Combat;
            if (combat == null) return;

            // Already submitted (pending state is cleared on success) — swallow the repeat rather than
            // bothering the engine with an empty action id.
            if (string.IsNullOrEmpty(flow.PendingActionId)) return;

            var result = combat.SubmitAction(flow.CurrentActorId, flow.PendingActionId, flow.PendingTargetIds);
            if (!result.success)
            {
                // Stay put so the player can cancel or re-pick; closing here would leave the actor's
                // turn live with no menu open and no way back in.
                Debug.LogWarning($"[JRPG.Combat.UI] Action submission rejected: {result.failureReason}");
                return;
            }

            flow.ClearPendingSelection();
            c.Menus?.CloseAll();
        }

        public string GetDisabledReason(MenuContext c)
        {
            var flow = CombatFlowController.Current;
            var combat = flow?.Combat;
            if (combat == null || string.IsNullOrEmpty(flow?.PendingActionId)) return "No pending action.";

            var reason = combat.PreviewAction(flow.CurrentActorId, flow.PendingActionId, flow.PendingTargetIds).blockedReason;
            return string.IsNullOrEmpty(reason) ? "Action unavailable." : reason;
        }
    }
}
