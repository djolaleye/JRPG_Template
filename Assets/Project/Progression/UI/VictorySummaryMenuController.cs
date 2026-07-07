using System.Collections.Generic;
using JRPG.Menu;

namespace JRPG.Progression.UI
{
    /// Post-battle screen 1: victory banner with battle facts and a Continue row.
    public sealed class VictorySummaryMenuController : PostBattleMenuControllerBase
    {
        protected override IReadOnlyList<RowModel> BuildRows()
        {
            var rows = new List<RowModel>();
            var result = Progression?.CurrentResult;
            if (result == null) return rows;

            rows.Add(Info("victory", "VICTORY!"));
            rows.Add(Info("encounter", $"Encounter: {(string.IsNullOrEmpty(result.encounterId) ? "(direct)" : result.encounterId)}"));
            rows.Add(Info("turns", $"Rounds: {result.turnCount}   Time: {result.elapsedSeconds:0.0}s"));
            rows.Add(RowModel.Simple("continue", "Continue", new ContinuePostBattleAction(), Context));
            return rows;
        }
    }
}
