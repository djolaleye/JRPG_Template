using System.Collections.Generic;
using UnityEngine;
using JRPG.Data;

namespace JRPG.Combat
{
    /// What a single action actually paid, so it can be undone exactly.
    public struct PaidCost
    {
        public CombatCost cost;
        public int amountPaid;
    }

    /// Maps CombatCostType → evaluator and performs atomic payment.
    ///
    /// An action either pays every cost and executes, or pays nothing. Validation runs
    /// across the whole cost list before a single resource is touched, and if execution later aborts,
    /// Rollback restores exactly what was taken (including inventory items).
    public sealed class CostRegistry
    {
        private readonly Dictionary<CombatCostType, ICostEvaluator> _evaluators = new();

        public CostRegistry Register(ICostEvaluator evaluator)
        {
            if (evaluator != null) _evaluators[evaluator.Type] = evaluator;
            return this;
        }

        /// True only when EVERY cost in the list can be paid — checked before anything is spent.
        public bool CanPayAll(CostContext ctx, CombatActionData action, out string reason)
        {
            reason = null;
            if (action?.costs == null) return true;

            for (int i = 0; i < action.costs.Count; i++)
            {
                var cost = action.costs[i];
                if (!_evaluators.TryGetValue(cost.type, out var evaluator))
                {
                    Debug.LogWarning($"[JRPG.Combat] No cost evaluator for '{cost.type}' — treated as free.");
                    continue;
                }
                if (!evaluator.CanPay(ctx, cost, out reason)) return false;
            }
            return true;
        }

        /// Pays every cost, recording each deduction.
        public List<PaidCost> PayAll(CostContext ctx, CombatActionData action)
        {
            var paid = new List<PaidCost>();
            if (action?.costs == null) return paid;

            for (int i = 0; i < action.costs.Count; i++)
            {
                var cost = action.costs[i];
                if (!_evaluators.TryGetValue(cost.type, out var evaluator)) continue;
                paid.Add(new PaidCost { cost = cost, amountPaid = evaluator.Pay(ctx, cost) });
            }

            return paid;
        }

        /// Undoes a PayAll, exactly. Used when execution aborts after payment.
        public void Rollback(CostContext ctx, List<PaidCost> paid)
        {
            if (paid == null) return;

            for (int i = paid.Count - 1; i >= 0; i--)
            {
                if (!_evaluators.TryGetValue(paid[i].cost.type, out var evaluator)) continue;
                
                evaluator.Refund(ctx, paid[i].cost, paid[i].amountPaid);
            }
        }

        public static CostRegistry CreateStandard()
        {
            return new CostRegistry()
                .Register(new NoneCostEvaluator())
                .Register(new ResourceCostEvaluator(CombatCostType.MP))
                .Register(new ResourceCostEvaluator(CombatCostType.SP))
                .Register(new ResourceCostEvaluator(CombatCostType.HP))
                .Register(new ItemCostEvaluator());
        }
    }
}
