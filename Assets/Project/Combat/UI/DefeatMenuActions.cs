using UnityEngine;
using JRPG.Core;
using JRPG.Menu;
using JRPG.Services;

namespace JRPG.Combat.UI
{
    /// Replays the encounter that was just lost, restoring the party to the state it entered with.
    public sealed class RetryBattleAction : IMenuAction
    {
        public bool CanExecute(MenuContext c)
        {
            var flow = DefeatFlowController.Current;
            return flow != null && flow.CanRetry;
        }

        public void Execute(MenuContext c)
        {
            // Close the defeat screen first: the retry starts a battle synchronously, and the combat UI
            // must open its command menu onto a clean stack.
            c.Menus?.CloseAll();
            DefeatFlowController.Current?.RequestRetry();
        }

        public string GetDisabledReason(MenuContext c) => "This battle cannot be retried.";
    }

    /// <summary>
    /// Restores the most recent save.
    /// </summary>
    public sealed class LoadLastSaveAction : IMenuAction
    {
        public bool CanExecute(MenuContext c) => FindNewestSlot(c?.Services) >= 0;

        public void Execute(MenuContext c)
        {
            int slot = FindNewestSlot(c?.Services);
            if (slot < 0) return;

            c.Menus?.CloseAll();
            DefeatFlowController.Current?.RequestLoadLastSave(slot);
        }

        public string GetDisabledReason(MenuContext c) => "No save file to load.";

        /// <summary>
        /// The most recently written slot, or -1 when nothing is saved.
        ///
        /// <para><c>savedAtUtcTicks == 0</c> means <i>unknown</i> on pre-v3 saves, not "the epoch", so
        /// a zero never beats a real timestamp. Among slots that are all unknown — or genuinely tied —
        /// the highest index wins, which is the closest thing to "most recent" the file offers.</para>
        /// </summary>
        public static int FindNewestSlot(IServiceRegistry services)
        {
            if (services == null || !services.TryResolve<ISaveService>(out var save) || save == null) return -1;

            int best = -1;
            long bestTicks = -1;

            var slots = save.ListSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                var info = slots[i];
                if (!info.exists) continue;

                if (info.savedAtUtcTicks > bestTicks || (info.savedAtUtcTicks == bestTicks && info.slot > best))
                {
                    bestTicks = info.savedAtUtcTicks;
                    best = info.slot;
                }
            }

            return best;
        }
    }

    /// <summary>
    /// The guaranteed exit. Present so a defeat with no save and no retryable battle — a first battle
    /// entered from a dialogue exit, for instance — still has somewhere to go.
    /// </summary>
    public sealed class ReturnToTitleFromDefeatAction : IMenuAction
    {
        public bool CanExecute(MenuContext c) => true;

        public void Execute(MenuContext c)
        {
            c.Menus?.CloseAll();
            DefeatFlowController.Current?.RequestReturnToTitle();
        }

        public string GetDisabledReason(MenuContext c) => "";
    }
}
