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

                rows.Add(Info(pending.characterId, $"{name}  —  points left: {progress.unspentAttributePoints}"));

                bool hasPoints = progress.unspentAttributePoints > 0;
                foreach (var stat in AttributePointDistributor.AllowedStats)
                {
                    int current = inst?.stats.GetFinal(stat) ?? 0;
                    rows.Add(new RowModel
                    {
                        id = pending.characterId + "_" + stat,
                        label = $"  {stat}",
                        quantityText = $"{current} > {current + 1}",
                        enabled = hasPoints,
                        action = new AllocatePointAction(pending.characterId, stat),
                        context = Context,
                    });
                }
            }

            rows.Add(new RowModel
            {
                id = "finish",
                label = "Finish",
                enabled = progression.CanCompletePostBattleFlow(),
                action = new ContinuePostBattleAction(requiresCompletable: true),
                context = Context,
            });
            return rows;
        }
    }
}
