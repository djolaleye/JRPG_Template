using System.Text;
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
                }
            }
            return sb.ToString();
        }

        public static string FirstItemCostId(CombatActionData a)
        {
            if (a.costs == null) return null;
            for (int i = 0; i < a.costs.Count; i++)
                if (a.costs[i].type == CombatCostType.Item) return a.costs[i].itemId;
            return null;
        }

        private static void Append(StringBuilder sb, string s)
        {
            if (sb.Length > 0) sb.Append("  ");
            sb.Append(s);
        }
    }
}
