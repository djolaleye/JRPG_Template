using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Menu;
using JRPG.Services;

namespace JRPG.Combat.UI
{
    /// <summary>
    /// The battle ITEM list: what is in the bag that can be used here.
    ///
    /// <para><b>Ownership decides whether a row exists.</b> The action registry is not a bag, it only holds an
    /// authored action per item plus one <c>itemuse:</c> action synthesised for every combat-usable
    /// ItemData in the database. Listing one row per action would advertise every consumable in the
    /// game at <c>× 0</c>, including ones the player had never seen. A quantity of zero means the item is
    /// not carried, so it is not in this list at all.</para>
    ///
    /// <para>Affordability is a separate question: an item the player is
    /// carrying but cannot use right now (cooldown, a second cost it cannot pay) still gets a row, 
    /// greyed out with the resolver's own reason.</para>
    /// </summary>
    public sealed class CombatItemListController : MenuController
    {
        [Tooltip("Optional. Description panel bound to the focused item.")]
        [SerializeField] private DetailPanelController detailPanel;

        /// Items backing the current rows, index-aligned with them. Null entries are rows with no item.
        private readonly List<ItemData> _rowItems = new();

        protected override IReadOnlyList<RowModel> BuildRows()
        {
            _rowItems.Clear();
            var rows = new List<RowModel>();
            var flow = CombatFlowController.Current;
            var combat = flow?.Combat;
            var actorId = flow?.CurrentActorId;
            if (combat == null || string.IsNullOrEmpty(actorId) || Context?.Services == null) return rows;

            Context.Services.TryResolve<IInventoryService>(out var inv);
            var data = AppContext.Data as DataRegistry;

            foreach (var action in combat.GetAvailableActions(actorId))
            {
                if (action.category != CombatActionCategory.Item) continue;

                string itemId = CombatRowFormat.FirstItemCostId(action);
                if (string.IsNullOrEmpty(itemId)) continue;

                // Checked before the row is built, and fails closed: an unresolvable registry or item
                // hides the row rather than listing everything. CombatActionResolver enforces the same
                // rule at submission, so a row that slips through still cannot execute.
                if (!ItemCombatRules.IsUsableInCombat(data, itemId)) continue;

                // Not carried, not in the bag.
                int qty = inv?.GetQuantity(itemId) ?? 0;
                if (qty <= 0) continue;

                data.TryGet<ItemData>(itemId, out var item);

                bool enabled = combat.CanAfford(actorId, action.Id, out var reason);
                rows.Add(new RowModel
                {
                    id = action.Id,
                    label = CombatRowFormat.Label(action),
                    icon = item != null ? item.icon : null,
                    quantityText = $"× {qty}",
                    enabled = enabled,
                    action = new ChooseCombatActionAction(action.Id),
                    context = Context,
                    disabledReason = enabled ? null : reason,
                });

                _rowItems.Add(item);
            }

            if (rows.Count == 0)
            {
                _rowItems.Add(null);
                rows.Add(RowModel.Simple("empty", "No usable items.", null, Context, enabled: false,
                                         disabledReason: string.Empty));
            }

            return rows;
        }

        protected override void OnHighlightChanged(int index, RowModel model)
        {
            if (detailPanel == null) return;

            var item = index >= 0 && index < _rowItems.Count ? _rowItems[index] : null;
            if (item == null) { detailPanel.Clear(); return; }

            detailPanel.ShowDetail(
                string.IsNullOrEmpty(item.displayName) ? item.Id : item.displayName,
                string.IsNullOrEmpty(item.description) ? "No description." : item.description,
                item.icon);
        }

        public override IReadOnlyList<InputPrompt> Prompts { get; } = new[]
        {
            new InputPrompt("Submit", "Use"),
            new InputPrompt("Cancel", "Back"),
        };
    }
}
