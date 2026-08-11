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

            // Skills gained with room to spare: an announcement, not a decision. Without this the
            // player would only discover them by opening a menu later.
            var learned = progression.LearnedSkills;
            if (learned.Count > 0)
            {
                rows.Add(RowModel.Separator("hdr_learned", "NEW SKILL UNLOCKED!"));

                for (int i = 0; i < learned.Count; i++)
                {
                    var entry = learned[i];
                    string owner = CharacterName(data, entry.characterId);

                    rows.Add(new RowModel
                    {
                        id = "learned_" + entry.characterId + "_" + entry.skillId,
                        label = "  " + SkillName(data, entry.skillId),
                        auxText = $"{owner} · Lv {entry.atLevel}   {SkillDescription(data, entry.skillId)}",
                        costText = SkillCost(data, entry.skillId),
                        enabled = false,
                    });
                }
            }

            var pending = progression.NextPendingSkillChoice();
            if (pending != null)
            {
                string who = CharacterName(data, pending.characterId);

                rows.Add(RowModel.Separator("hdr_replace", "SKILL LIST FULL"));
                rows.Add(Info(pending.characterId, $"{who} learned {SkillName(data, pending.newSkillId)}!"));
                rows.Add(Info(pending.characterId + "_hint", "  Choose one to forget:"));

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
                label = "Continue",
                enabled = !progression.HasPendingSkillChoices(),
                action = new ContinuePostBattleAction(PostBattleGate.SkillChoicesResolved),
                context = Context,
            });
            return rows;
        }

        private static string CharacterName(DataRegistry data, string characterId)
        {
            if (data != null && data.TryGet<CharacterData>(characterId, out var cd) && cd != null
                && !string.IsNullOrEmpty(cd.displayName))
                return cd.displayName;
            return characterId;
        }

        private static string SkillDescription(DataRegistry data, string skillId)
        {
            if (data != null && data.TryGet<CombatActionData>(skillId, out var a) && a != null
                && !string.IsNullOrEmpty(a.description))
                return a.description;
            return string.Empty;
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
