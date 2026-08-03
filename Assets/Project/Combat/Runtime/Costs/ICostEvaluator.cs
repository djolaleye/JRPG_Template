using JRPG.Data;
using JRPG.Services;

namespace JRPG.Combat
{
    /// Everything a cost evaluator needs.
    public sealed class CostContext
    {
        public CombatantInstance user;
        public CombatActionData action;
        public DataRegistry data;
        public IInventoryService inventory;
    }

    /// Validates, pays, and refunds one kind of CombatCost.
    public interface ICostEvaluator
    {
        CombatCostType Type { get; }

        bool CanPay(CostContext ctx, CombatCost cost, out string reason);

        /// Deducts the cost and returns what was actually taken, so Refund can undo exactly that much.
        int Pay(CostContext ctx, CombatCost cost);

        void Refund(CostContext ctx, CombatCost cost, int amountPaid);
    }
}
