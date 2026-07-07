using System.Collections.Generic;
using JRPG.Menu;

namespace JRPG.Progression.UI
{
    /// Post-battle screen 3: per-character XP projection. Confirm is the mutation point — the flow
    /// controller calls ProgressionService.ApplyBattleResult when this screen advances.
    public sealed class XpPreviewMenuController : PostBattleMenuControllerBase
    {
        protected override IReadOnlyList<RowModel> BuildRows()
        {
            var rows = new List<RowModel>();
            var preview = Progression?.CurrentPreview;
            if (preview == null) return rows;

            foreach (var c in preview.characters)
            {
                string projection = c.projectedLevel > c.currentLevel
                    ? $"Lv{c.currentLevel} > Lv{c.projectedLevel}!"
                    : $"Lv{c.currentLevel}";
                rows.Add(Info(c.characterId, $"{c.displayName}  {projection}"));
                rows.Add(Info(c.characterId + "_xp", $"    XP {c.currentXp} +{c.xpGained}"));
            }

            rows.Add(RowModel.Simple("confirm", "Confirm", new ContinuePostBattleAction(), Context));
            return rows;
        }
    }
}
