using System.Collections.Generic;
using UnityEngine;
using JRPG.Data;
using JRPG.Menu;

namespace JRPG.Combat.UI
{
    /// <summary>
    /// The current actor's Skill-category actions, greying out unaffordable ones.
    ///
    /// <para>The focused skill's authored description is pushed to the paired detail panel: the combat
    /// row prefab has no aux slot, so the blurb has nowhere else to go.</para>
    /// </summary>
    public sealed class CombatSkillListController : MenuController
    {
        [Tooltip("Optional. Description panel bound to the focused skill.")]
        [SerializeField] private DetailPanelController detailPanel;

        /// Actions backing the current rows, index-aligned with them. Null entries are separators.
        private readonly List<CombatActionData> _rowActions = new();

        protected override IReadOnlyList<RowModel> BuildRows()
        {
            _rowActions.Clear();
            var rows = new List<RowModel>();
            var flow = CombatFlowController.Current;
            var combat = flow?.Combat;
            var actorId = flow?.CurrentActorId;
            if (combat == null || string.IsNullOrEmpty(actorId)) return rows;

            var actor = combat.CurrentBattle?.FindCombatant(actorId);
            var profile = actor?.profile;

            var available = new Dictionary<string, CombatActionData>();
            var order = new List<string>();
            foreach (var action in combat.GetAvailableActions(actorId))
            {
                if (action.category != CombatActionCategory.Skill) continue;
                if (available.ContainsKey(action.Id)) continue;

                available[action.Id] = action;
                order.Add(action.Id);
            }

            if (available.Count == 0) return rows;

            if (profile == null || profile.skillIds.Count == 0)
            {
                for (int i = 0; i < order.Count; i++)
                    rows.Add(SkillRow(available[order[i]], actor, combat, actorId, grantedBy: null));
                return rows;
            }

            // Known first, in the order the character learned them.
            var emitted = new HashSet<string>();
            for (int i = 0; i < profile.skillIds.Count; i++)
            {
                var id = profile.skillIds[i];
                if (!available.TryGetValue(id, out var action) || !emitted.Add(id)) continue;

                rows.Add(SkillRow(action, actor, combat, actorId, grantedBy: null));
            }

            // Then everything left over, reachable only because something they are wearing unlocked it.
            var granted = new List<string>();
            for (int i = 0; i < order.Count; i++)
                if (!emitted.Contains(order[i])) granted.Add(order[i]);

            if (granted.Count == 0) return rows;

            // The separator is a row too, so the parallel list needs a slot for it or every entry
            // after this point would describe the wrong skill.
            _rowActions.Add(null);
            rows.Add(RowModel.Separator("sep_from_equipment", "FROM EQUIPMENT"));

            for (int i = 0; i < granted.Count; i++)
                rows.Add(SkillRow(available[granted[i]], actor, combat, actorId,
                                  grantedBy: profile.GetUnlockSource(granted[i])));

            return rows;
        }

        protected override void OnHighlightChanged(int index, RowModel model)
        {
            if (detailPanel == null) return;

            var action = index >= 0 && index < _rowActions.Count ? _rowActions[index] : null;
            if (action == null) { detailPanel.Clear(); return; }

            detailPanel.ShowDetail(
                string.IsNullOrEmpty(action.displayName) ? action.Id : action.displayName,
                string.IsNullOrEmpty(action.description) ? "No description." : action.description,
                icon: null,
                footer: CombatRowFormat.CostText(action));
        }

        public override IReadOnlyList<InputPrompt> Prompts { get; } = new[]
        {
            new InputPrompt("Cancel", "Back"),
            new InputPrompt("Submit", "Confirm"),
        };

        private RowModel SkillRow(CombatActionData action, CombatantInstance actor,
                                  CombatService combat, string actorId, string grantedBy)
        {
            // A cooling-down skill stays visible but disabled, showing the turns remaining
            int cooldown = actor?.GetCooldown(action.Id) ?? 0;
            string cost = CombatRowFormat.CostText(action);
            if (cooldown > 0) cost = string.IsNullOrEmpty(cost) ? $"CD {cooldown}" : $"{cost}   CD {cooldown}";

            // The granting item is named inline rather than on a second line: the combat row prefab
            // (MenuRow) has no aux slot, and the richer MenuRow_Basic puts a full-width disabled-reason
            // affix straight through the label and the multi-cost column. Attribution still has to
            // survive several items granting into the same group, so it goes in the label.
            string label = CombatRowFormat.Label(action);
            if (!string.IsNullOrEmpty(grantedBy)) label += "  ·  " + CombatRowFormat.ItemName(grantedBy);

            // Recorded here rather than at each call site, so a row can never be added without its
            // matching entry.
            _rowActions.Add(action);

            return new RowModel
            {
                id = action.Id,
                label = label,
                costText = cost,
                enabled = combat.CanAfford(actorId, action.Id),
                action = new ChooseCombatActionAction(action.Id),
                context = Context
            };
        }
    }
}
