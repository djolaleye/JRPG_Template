using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Menu;

namespace JRPG.Progression.UI
{
    /// <summary>
    /// Post-battle results: what the battle paid out (EXP, items, currency) and what that does to each
    /// character, on one screen.
    ///
    /// <para>Merges what used to be two screens — a rewards list followed by an XP projection. They
    /// answer the same question ("what did I get?") and splitting them cost the player a confirm press
    /// to see the second half of one answer.</para>
    ///
    /// <para><b>This screen is the mutation point.</b> Everything here is still a projection; advancing
    /// is what calls <c>ApplyBattleResult</c>. That preserves the preview-first rule the flow has always
    /// had — nothing is granted until the player has seen it.</para>
    /// </summary>
    public sealed class ResultsMenuController : PostBattleMenuControllerBase
    {
        protected override IReadOnlyList<RowModel> BuildRows()
        {
            var rows = new List<RowModel>();
            var progression = Progression;
            if (progression == null) return rows;

            var rewards = progression.CurrentRewards;
            var preview = progression.CurrentPreview;
            var data = AppContext.Data as DataRegistry;

            // ---- EXP
            rows.Add(Header("hdr_exp", "EXP"));
            rows.Add(new RowModel
            {
                id = "exp_total",
                label = "  Earned",
                costText = (rewards?.totalXp ?? 0) + " EXP",
                enabled = false,
            });

            // Per-character projection: current level, gain, and the level it crosses into.
            if (preview != null)
            {
                foreach (var c in preview.characters)
                {
                    bool levels = c.projectedLevel > c.currentLevel;
                    rows.Add(new RowModel
                    {
                        id = "exp_" + c.characterId,
                        label = "  " + c.displayName,
                        auxText = levels
                            ? $"Lv {c.currentLevel} → {c.projectedLevel}   LEVEL UP!"
                            : $"Lv {c.currentLevel}",
                        costText = "+" + c.xpGained,
                        enabled = false,
                    });
                }
            }

            // ---- ITEM
            rows.Add(Header("hdr_item", "ITEM"));

            if (rewards == null || rewards.drops.Count == 0)
            {
                rows.Add(Info("item_none", "  None"));
            }
            else
            {
                for (int i = 0; i < rewards.drops.Count; i++)
                {
                    var drop = rewards.drops[i];
                    string name = drop.itemId;
                    Sprite icon = null;

                    if (data != null && data.TryGet<ItemData>(drop.itemId, out var item) && item != null)
                    {
                        if (!string.IsNullOrEmpty(item.displayName)) name = item.displayName;
                        icon = item.icon;
                    }

                    rows.Add(new RowModel
                    {
                        id = "drop_" + i,
                        label = "  " + name,
                        icon = icon,
                        quantityText = "× " + drop.quantity.ToString("00"),
                        enabled = false,
                    });
                }
            }

            // ---- CURRENCY. Shown only when there is some; there is no wallet to bank it into yet,
            // so a permanent "0" row would just be noise.
            if (rewards != null && rewards.currency > 0)
            {
                rows.Add(Header("hdr_currency", "CURRENCY"));
                rows.Add(new RowModel
                {
                    id = "currency",
                    label = "  Earned",
                    costText = rewards.currency.ToString(),
                    enabled = false,
                });
            }

            rows.Add(RowModel.Simple("ok", "OK", new ContinuePostBattleAction(), Context));
            return rows;
        }

        /// A section caption in the results list — not a choice, so it carries no disabled treatment.
        private static RowModel Header(string id, string text) => RowModel.Separator(id, text);
    }
}
