using System.Collections.Generic;
using JRPG.Core;
using JRPG.Data;
using JRPG.Menu;
using JRPG.Party;
using JRPG.Services;

namespace JRPG.Progression.UI
{
    /// Post-battle screen: a character learned a skill while already carrying the maximum, so the
    /// player picks which skill to give up. Declining the new skill is offered as an explicit row.
    ///
    public sealed class SkillChoiceMenuController : PostBattleMenuControllerBase
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

            var pending = progression.NextPendingSkillChoice();
            if (pending != null)
            {
                string who = data != null && data.TryGet<CharacterData>(pending.characterId, out var cd)
                    ? cd.displayName : pending.characterId;

                rows.Add(Info(pending.characterId, $"{who} learned {SkillName(data, pending.newSkillId)}!"));
                rows.Add(Info(pending.characterId + "_hint", "  Skill list is full — choose one to forget:"));

                // The skills currently held: picking one swaps it for the new skill.
                var inst = partyRuntime?.ResolveInstanceById(pending.characterId);
                var current = inst != null ? inst.skillIds : pending.currentSkillIds;

                for (int i = 0; i < current.Count; i++)
                {
                    string skillId = current[i];
                    rows.Add(new RowModel
                    {
                        id = pending.characterId + "_forget_" + skillId,
                        label = "  Forget " + SkillName(data, skillId),
                        quantityText = SkillCost(data, skillId),
                        enabled = true,
                        action = new ResolveSkillChoiceAction(pending.characterId, skillId),
                        context = Context,
                    });
                }

                // Declining: discard the NEW skill instead of anything already known.
                rows.Add(new RowModel
                {
                    id = pending.characterId + "_decline",
                    label = $"  Do not learn {SkillName(data, pending.newSkillId)}",
                    enabled = true,
                    action = new ResolveSkillChoiceAction(pending.characterId, pending.newSkillId),
                    context = Context,
                });
            }

            rows.Add(new RowModel
            {
                id = "finish",
                label = "Finish",
                enabled = !progression.HasPendingSkillChoices(),
                action = new ContinuePostBattleAction(requiresCompletable: false),
                context = Context,
            });
            return rows;
        }

        private static string SkillName(DataRegistry data, string skillId)
        {
            if (data != null && data.TryGet<CombatActionData>(skillId, out var a) && !string.IsNullOrEmpty(a.displayName))
                return a.displayName;
            return skillId;
        }

        private static string SkillCost(DataRegistry data, string skillId)
        {
            if (data == null || !data.TryGet<CombatActionData>(skillId, out var a) || a.costs == null) return "";
            for (int i = 0; i < a.costs.Count; i++)
                if (a.costs[i].type == CombatCostType.MP) return $"MP {a.costs[i].costAmount}";
            return "";
        }
    }
}
