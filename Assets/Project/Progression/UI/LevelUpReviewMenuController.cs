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

                rows.Add(RowModel.Separator(lu.characterId + "_" + lu.newLevel,
                    $"LEVEL UP!   {name}   Lv {lu.oldLevel} → {lu.newLevel}"));

                // One row per stat that moved, so each gain reads on its own line with its own "UP!"
                // rather than being crushed into a single run-on string.
                for (int i = 0; i < lu.statIncreases.Count; i++)
                {
                    var inc = lu.statIncreases[i];
                    rows.Add(new RowModel
                    {
                        id = $"{lu.characterId}_{lu.newLevel}_{inc.stat}",
                        label = "  " + inc.stat,
                        costText = $"+{inc.value:0.#}",
                        auxText = "UP!",
                        enabled = false,
                    });
                }

                if (lu.pointsGranted > 0)
                    rows.Add(new RowModel
                    {
                        id = $"{lu.characterId}_{lu.newLevel}_pts",
                        label = "  Attribute points",
                        costText = "+" + lu.pointsGranted,
                        enabled = false,
                    });
            }

            rows.Add(RowModel.Simple("continue", "Continue", new ContinuePostBattleAction(), Context));
            return rows;
        }
    }
}
