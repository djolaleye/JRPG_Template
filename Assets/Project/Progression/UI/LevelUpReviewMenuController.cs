using System.Collections.Generic;
using System.Text;
using JRPG.Core;
using JRPG.Data;
using JRPG.Menu;

namespace JRPG.Progression.UI
{
    /// Post-battle screen 4: applied level-ups — old/new level, stat increases, points granted.
    public sealed class LevelUpReviewMenuController : PostBattleMenuControllerBase
    {
        protected override IReadOnlyList<RowModel> BuildRows()
        {
            var rows = new List<RowModel>();
            var progression = Progression;
            if (progression == null) return rows;

            var data = AppContext.Data as DataRegistry;
            foreach (var lu in progression.PendingLevelUps)
            {
                string name = data != null && data.TryGet<CharacterData>(lu.characterId, out var cd)
                    ? cd.displayName : lu.characterId;
                rows.Add(Info(lu.characterId + "_" + lu.newLevel, $"{name}  Lv{lu.oldLevel} > Lv{lu.newLevel}"));

                if (lu.statIncreases.Count > 0)
                {
                    var sb = new StringBuilder("    ");
                    for (int i = 0; i < lu.statIncreases.Count; i++)
                    {
                        if (i > 0) sb.Append("  ");
                        sb.Append($"{lu.statIncreases[i].stat} +{lu.statIncreases[i].value:0.#}");
                    }
                    rows.Add(Info(lu.characterId + "_" + lu.newLevel + "_stats", sb.ToString()));
                }
                if (lu.pointsGranted > 0)
                    rows.Add(Info(lu.characterId + "_" + lu.newLevel + "_pts", $"    Attribute points +{lu.pointsGranted}"));
            }

            rows.Add(RowModel.Simple("continue", "Continue", new ContinuePostBattleAction(), Context));
            return rows;
        }
    }
}
