using UnityEngine;
using JRPG.Data;

namespace JRPG.Combat
{
    public sealed class ResourceCostEvaluator : ICostEvaluator
    {
        private readonly CombatCostType _type;

        public ResourceCostEvaluator(CombatCostType type) { _type = type; }

        public CombatCostType Type => _type;

        public bool CanPay(CostContext ctx, CombatCost cost, out string reason)
        {
            reason = null;
            int current = Current(ctx.user);
            if (current >= cost.costAmount) return true;

            reason = $"Not enough {_type} ({current}/{cost.costAmount}).";
            return false;
        }

        public int Pay(CostContext ctx, CombatCost cost)
        {
            int before = Current(ctx.user);
            int after = Mathf.Max(0, before - cost.costAmount);
            Set(ctx.user, after);
            return before - after;                 // what was actually taken
        }

        public void Refund(CostContext ctx, CombatCost cost, int amountPaid)
        {
            int max = Max(ctx.user);
            Set(ctx.user, Mathf.Clamp(Current(ctx.user) + amountPaid, 0, max));
        }

        private int Current(CombatantInstance c) => _type switch
        {
            CombatCostType.MP => c.currentMP,
            CombatCostType.SP => c.currentSP,
            _ => c.currentHP,
        };

        private int Max(CombatantInstance c) => _type switch
        {
            CombatCostType.MP => c.MaxMP,
            CombatCostType.SP => c.MaxSP,
            _ => c.MaxHP,
        };

        private void Set(CombatantInstance c, int value)
        {
            switch (_type)
            {
                case CombatCostType.MP: c.currentMP = value; break;
                case CombatCostType.SP: c.currentSP = value; break;
                default: c.currentHP = value; break;
            }
        }
    }


    public sealed class ItemCostEvaluator : ICostEvaluator
    {
        public CombatCostType Type => CombatCostType.Item;

        public bool CanPay(CostContext ctx, CombatCost cost, out string reason)
        {
            reason = null;
            int need = Mathf.Max(1, cost.quantity);

            // The item menu already filters on usableInCombat; this refuses an ineligible item
            // however the submission arrived.
            if (!ItemCombatRules.IsUsableInCombat(ctx.data, cost.itemId))
            {
                reason = $"Item '{cost.itemId}' is not usable in combat.";
                return false;
            }
            if (ctx.inventory == null || !ctx.inventory.Has(cost.itemId, need))
            {
                reason = $"Missing item '{cost.itemId}' x{need}.";
                return false;
            }
            return true;
        }

        public int Pay(CostContext ctx, CombatCost cost)
        {
            int need = Mathf.Max(1, cost.quantity);
            ctx.inventory?.Remove(cost.itemId, need);
            return need;
        }

        public void Refund(CostContext ctx, CombatCost cost, int amountPaid)
        {
            if (amountPaid > 0) ctx.inventory?.Add(cost.itemId, amountPaid);
        }
    }

    
    public sealed class NoneCostEvaluator : ICostEvaluator
    {
        public CombatCostType Type => CombatCostType.None;
        public bool CanPay(CostContext ctx, CombatCost cost, out string reason) { reason = null; return true; }
        public int Pay(CostContext ctx, CombatCost cost) => 0;
        public void Refund(CostContext ctx, CombatCost cost, int amountPaid) { }
    }
}
