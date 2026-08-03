using System.Text;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;

namespace JRPG.Combat.UI
{
    /// Shared row-formatting helpers for the combat action-list submenus.
    internal static class CombatRowFormat
    {
        public static string Label(CombatActionData a)
            => string.IsNullOrEmpty(a.displayName) ? a.Id : a.displayName;

        public static string CostText(CombatActionData a)
        {
            if (a.costs == null) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < a.costs.Count; i++)
            {
                var cost = a.costs[i];
                switch (cost.type)
                {
                    case CombatCostType.MP: Append(sb, $"MP {cost.costAmount}"); break;
                    case CombatCostType.SP: Append(sb, $"SP {cost.costAmount}"); break;
                    case CombatCostType.HP: Append(sb, $"HP {cost.costAmount}"); break;
                    case CombatCostType.Item: Append(sb, $"{ItemName(cost.itemId)} x{Mathf.Max(1, cost.quantity)}"); break;
                }
            }
            return sb.ToString();
        }

        public static string FirstItemCostId(CombatActionData a)
        {
            if (a.costs != null)
                for (int i = 0; i < a.costs.Count; i++)
                    if (a.costs[i].type == CombatCostType.Item) return a.costs[i].itemId;

            // Synthesised item actions carry the item id in their own id, which also covers items
            // authored as not-consumed-on-use (those have no item cost to read).
            return ItemActionSynthesizer.ItemIdFrom(a.Id);
        }

        /// Resolves authored action data for a menu row. The registry reaches the presentation layer
        /// only through AppContext (JRPG.Combat.UI has no composition root of its own).
        public static bool TryGetAction(string actionId, out CombatActionData action)
        {
            action = null;
            return !string.IsNullOrEmpty(actionId)
                && AppContext.Data is DataRegistry data
                && data.TryGet(actionId, out action);
        }

        public static bool IsSelfTarget(string actionId)
            => TryGetAction(actionId, out var action)
               && action.targetRule != null
               && action.targetRule.selectionMode == TargetSelectionMode.Self;

        /// True when the engine picks the targets itself (Self / All / Random).
        public static bool IsAutoTargeted(string actionId)
            => TryGetAction(actionId, out var action)
               && action.targetRule != null
               && action.targetRule.IsAutoResolved;

        /// Human-readable description of what an auto-resolved action will hit, for the confirm screen.
        public static string DescribeAutoTargets(string actionId)
        {
            if (!TryGetAction(actionId, out var action) || action.targetRule == null) return "";
            var rule = action.targetRule;
            switch (rule.selectionMode)
            {
                case TargetSelectionMode.Self: return "Self";
                case TargetSelectionMode.All:
                    return rule.team switch
                    {
                        TargetTeam.Allies => "All allies",
                        TargetTeam.Enemies => "All enemies",
                        TargetTeam.All => "All combatants",
                        _ => "Self",
                    };
                case TargetSelectionMode.Random:
                    string who = rule.team switch
                    {
                        TargetTeam.Allies => "ally",
                        TargetTeam.Enemies => "enemy",
                        TargetTeam.All => "combatant",
                        _ => "target",
                    };
                    return rule.maxTargets > 1 ? $"{rule.maxTargets} random {who}s" : $"Random {who}";
                default: return "";
            }
        }

        private static string ItemName(string itemId)
        {
            if (!string.IsNullOrEmpty(itemId)
                && AppContext.Data is DataRegistry data
                && data.TryGet<ItemData>(itemId, out var item)
                && !string.IsNullOrEmpty(item.displayName))
                return item.displayName;
            return string.IsNullOrEmpty(itemId) ? "Item" : itemId;
        }

        private static void Append(StringBuilder sb, string s)
        {
            if (sb.Length > 0) sb.Append("  ");
            sb.Append(s);
        }
    }
}
