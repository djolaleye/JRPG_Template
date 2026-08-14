using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Menu;
using JRPG.Party;
using JRPG.Services;

namespace JRPG.Progression.UI
{
    /// <summary>
    /// Post-battle screen: a character learned a skill while already carrying the maximum, so the
    /// player picks which skill to give up. Declining the new skill is offered as an explicit row.
    ///
    /// <para>Purely the decision. Skills gained with room to spare are announced by
    /// <see cref="SkillUnlockedMenuController"/>, which runs before this screen.</para>
    /// </summary>
    public sealed class SkillChoiceMenuController : PostBattleMenuControllerBase
    {
        [Tooltip("Optional. Description panel bound to the focused skill.")]
        [SerializeField] private DetailPanelController detailPanel;

        /// Skill ids backing the current rows, index-aligned. Null entries are rows that describe no
        /// skill (the header, the hint, Continue) and clear the panel instead.
        private readonly List<string> _rowSkillIds = new();

        protected override IReadOnlyList<RowModel> BuildRows()
        {
            _rowSkillIds.Clear();
            var rows = new List<RowModel>();
            var progression = Progression;
            if (progression == null || Context?.Services == null) return rows;

            var data = Data;
            IPartyRuntimeQueries partyRuntime = null;
            if (Context.Services.TryResolve<IPartyService>(out var partySvc))
                partyRuntime = partySvc as IPartyRuntimeQueries;

            var pending = progression.NextPendingSkillChoice();
            if (pending != null)
            {
                string who = CharacterName(data, pending.characterId);

                rows.Add(RowModel.Separator("hdr_replace", "SKILL LIST FULL"));
                _rowSkillIds.Add(null);

                // The new skill is what the header line is about, so focusing it describes that.
                rows.Add(Info(pending.characterId, $"{who} learned {SkillName(data, pending.newSkillId)}!"));
                _rowSkillIds.Add(pending.newSkillId);

                rows.Add(Info(pending.characterId + "_hint", "  Choose one to forget:"));
                _rowSkillIds.Add(null);

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

                    // So the player can read what they are about to give up before giving it up.
                    _rowSkillIds.Add(skillId);
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
                _rowSkillIds.Add(pending.newSkillId);
            }

            rows.Add(new RowModel
            {
                id = "finish",
                label = "Continue",
                enabled = !progression.HasPendingSkillChoices(),
                action = new ContinuePostBattleAction(PostBattleGate.SkillChoicesResolved),
                context = Context,
            });
            _rowSkillIds.Add(null);

            return rows;
        }

        protected override void OnHighlightChanged(int index, RowModel model)
        {
            if (detailPanel == null) return;

            string skillId = index >= 0 && index < _rowSkillIds.Count ? _rowSkillIds[index] : null;
            if (string.IsNullOrEmpty(skillId)) { detailPanel.Clear(); return; }

            var data = Data;
            string description = SkillDescription(data, skillId);

            detailPanel.ShowDetail(
                SkillName(data, skillId),
                string.IsNullOrEmpty(description) ? "No description." : description,
                icon: null,
                footer: SkillCost(data, skillId));
        }
    }
}
