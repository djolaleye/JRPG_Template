using System.Collections.Generic;
using JRPG.Menu;

namespace JRPG.Combat.UI
{
    /// Lists valid targets for the pending action (respecting the action's TargetRule via the engine).
    public sealed class CombatTargetMenuController : MenuController
    {
        protected override IReadOnlyList<RowModel> BuildRows()
        {
            var rows = new List<RowModel>();
            var flow = CombatFlowController.Current;
            var combat = flow?.Combat;
            var actorId = flow?.CurrentActorId;
            var actionId = flow?.PendingActionId;
            if (combat == null || string.IsNullOrEmpty(actorId) || string.IsNullOrEmpty(actionId)) return rows;

            foreach (var t in combat.GetValidTargets(actorId, actionId))
            {
                rows.Add(new RowModel
                {
                    id = t.combatantId,
                    label = $"{t.displayName}  HP {t.currentHP}/{t.MaxHP}",
                    enabled = true,
                    action = new ChooseCombatTargetAction(t.combatantId),
                    context = Context
                });
            }
            return rows;
        }

        public override IReadOnlyList<InputPrompt> Prompts { get; } = new[]
        {
            new InputPrompt("Navigate", "Target"),
            new InputPrompt("Cancel", "Back"),
            new InputPrompt("Submit", "Confirm"),
        };

        /// <summary>
        /// Left/right step the target list, exactly as up/down do.
        ///
        /// <para>One selection model drives both axes — which also keeps
        /// <see cref="OnHighlightChanged"/> as the single place the HUD banner is updated from.</para>
        /// </summary>
        protected override void OnNavigateHorizontal(int dir) => MoveSelection(dir);

        /// <summary>
        /// Publishes the cursor's position to the flow controller so the HUD's enemy banner can follow
        /// it. Read-only bookkeeping.
        /// </summary>
        protected override void OnHighlightChanged(int index, RowModel model)
        {
            var flow = CombatFlowController.Current;
            if (flow != null) flow.HighlightedTargetId = model.id;
        }

        protected override void OnDisable()
        {
            // The banner must not keep pointing at a target on a screen that is gone.
            var flow = CombatFlowController.Current;
            if (flow != null) flow.HighlightedTargetId = null;

            base.OnDisable();
        }
    }
}
