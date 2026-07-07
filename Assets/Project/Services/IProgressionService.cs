namespace JRPG.Services
{
    /// <summary>
    /// Lean cross-assembly surface: primitives only, matching the IPartyService/IInventoryService
    /// precedent. The rich progression API (previews, level-up results, allocation by StatType)
    /// lives on the concrete ProgressionService in JRPG.Progression; UI resolves that directly.
    /// </summary>
    public interface IProgressionService
    {
        /// True from the moment a victory result is accepted until the post-battle flow completes.
        /// While true, the post-battle flow owns the layered state — nothing else should return the
        /// game to exploration.
        bool IsPostBattleFlowActive { get; }

        bool HasPendingAttributeAllocations();

        /// False while required manual attribute points remain unspent (the doc's blocking rule).
        bool CanCompletePostBattleFlow();
    }
}
