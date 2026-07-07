using System.Collections.Generic;
using JRPG.Core;
using JRPG.Data;
using JRPG.Menu;

namespace JRPG.Progression.UI
{
    /// Post-battle screen 2: resolved rewards — XP, currency (display only in Phase 8), and the
    /// item drops that will be granted on confirmation.
    public sealed class RewardReviewMenuController : PostBattleMenuControllerBase
    {
        protected override IReadOnlyList<RowModel> BuildRows()
        {
            var rows = new List<RowModel>();
            var rewards = Progression?.CurrentRewards;
            if (rewards == null) return rows;

            rows.Add(Info("xp", $"XP Gained: {rewards.totalXp}"));
            rows.Add(Info("currency", $"Currency: {rewards.currency} (not yet banked)"));

            if (rewards.drops.Count == 0)
            {
                rows.Add(Info("nodrops", "Items: none"));
            }
            else
            {
                var data = AppContext.Data as DataRegistry;
                for (int i = 0; i < rewards.drops.Count; i++)
                {
                    var d = rewards.drops[i];
                    string name = data != null && data.TryGet<ItemData>(d.itemId, out var item)
                        ? item.displayName : d.itemId;
                    rows.Add(new RowModel { id = "drop_" + i, label = name, quantityText = "x" + d.quantity, enabled = false });
                }
            }

            rows.Add(RowModel.Simple("continue", "Continue", new ContinuePostBattleAction(), Context));
            return rows;
        }
    }
}
