using System.Collections.Generic;
using JRPG.Core;
using JRPG.Data;
using JRPG.Menu;
using JRPG.Party;
using JRPG.Services;

namespace JRPG.Progression.UI
{
    /// Post-battle screen 5: manual attribute allocation. One section per character with unspent
    /// points; each allowed stat is a row that spends one point (rows rebuild after every spend via
    /// the framework's post-execute rebuild). Finish is gated until every point is spent.
    public sealed class AttributeAllocationMenuController : PostBattleMenuControllerBase
    {
        protected override IReadOnlyList<RowModel> BuildRows()
        {
            var rows = new List<RowModel>();
            var progression = Progression;
            if (progression == null || Context?.Services == null) return rows;

            var data = AppContext.Data as DataRegistry;
            IPartyRuntimeQueries partyRuntime = null;
            if (Context.Services.TryResolve<IPartyService>(out var partySvc))
                partyRuntime = partySvc as IPartyRuntimeQueries;

            foreach (var pending in progression.GetPendingAttributeAllocations())
            {
                var progress = progression.GetProgressForCharacter(pending.characterId);
                var inst = partyRuntime?.ResolveInstanceById(pending.characterId);
                string name = data != null && data.TryGet<CharacterData>(pending.characterId, out var cd)
                    ? cd.displayName : pending.characterId;

                rows.Add(RowModel.Separator(pending.characterId,
                    $"{name}   Lv {progress.currentLevel}   —   {progress.unspentAttributePoints} POINTS AVAILABLE"));

                bool hasPoints = progress.unspentAttributePoints > 0;
                foreach (var stat in AttributePointDistributor.AllowedStats)
                {
                    // before → after from the domain's own projection rather than "current + 1": a point
                    // is a permanent Flat modifier, and derived stats do not necessarily move 1:1 with it.
                    var projected = progression.PreviewAttributePoint(pending.characterId, stat);
                    int current = inst?.stats.GetFinal(stat) ?? 0;

                    string delta = projected.allowed && projected.after != projected.before
                        ? $"{projected.before} → {projected.after}"
                        : current.ToString();

                    rows.Add(new RowModel
                    {
                        id = pending.characterId + "_" + stat,
                        label = $"  {stat}",
                        costText = delta,
                        enabled = hasPoints,
                        disabledReason = hasPoints ? null : "No points left.",
                        action = new AllocatePointAction(pending.characterId, stat),
                        context = Context,
                    });
                }
            }

            rows.Add(new RowModel
            {
                id = "finish",
                label = "Confirm",
                // Gated on points only. Gating on full completability would deadlock here, because a
                // pending skill decision belongs to the screen that comes AFTER this one.
                enabled = !progression.HasPendingAttributeAllocations(),
                disabledReason = "Spend all attribute points first.",
                action = new ContinuePostBattleAction(PostBattleGate.AttributePointsSpent),
                context = Context,
            });
            return rows;
        }
    }
}
