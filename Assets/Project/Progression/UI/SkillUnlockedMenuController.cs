using System.Collections.Generic;
using UnityEngine;
using JRPG.Menu;

namespace JRPG.Progression.UI
{
    /// <summary>
    /// Post-battle screen: a character was offered a skill. One skill per screen — the flow
    /// controller advances through them.
    ///
    /// <para>Purely informational: the only row that does anything is Continue.</para>
    /// </summary>
    public sealed class SkillUnlockedMenuController : PostBattleMenuControllerBase
    {
        [Tooltip("Optional. Shows the announced skill's description in full.")]
        [SerializeField] private DetailPanelController detailPanel;

        protected override IReadOnlyList<RowModel> BuildRows()
        {
            var rows = new List<RowModel>();

            var flow = PostBattleFlowController.Current;
            // The flow controller owns the cursor; a screen rebuilt out of step with it shows nothing
            // rather than the wrong skill.
            if (flow?.CurrentSkillAnnouncement is not { } entry) return rows;

            var data = Data;
            string description = SkillDescription(data, entry.SkillId);

            // The panel is the readable home for the blurb; the row keeps the aux copy so the screen
            // still reads correctly on a prefab with no panel wired.
            if (detailPanel != null)
                detailPanel.ShowDetail(
                    SkillName(data, entry.SkillId),
                    string.IsNullOrEmpty(description) ? "No description." : description,
                    icon: null,
                    footer: SkillCost(data, entry.SkillId));

            // No headline row: the prefab's Title already reads "NEW SKILL UNLOCKED!", and a second
            // header underneath it either repeated or contradicted it.
            rows.Add(new RowModel
            {
                id = "skill_" + entry.SkillId,
                label = SkillName(data, entry.SkillId),
                auxText = detailPanel != null ? null : description,
                costText = SkillCost(data, entry.SkillId),
                enabled = false,
            });

            // A pending choice records no level, so the owner line drops the suffix rather than
            // printing "Lv 0".
            string owner = CharacterName(data, entry.CharacterId);
            rows.Add(Info("owner", entry.AtLevel > 0 ? $"{owner} · Lv {entry.AtLevel}" : owner));

            // Says why the next screen is about to ask a question, instead of springing it.
            if (entry.RequiresChoice)
                rows.Add(Info("full", $"{owner}'s skill list is full."));

            // Only shown when there is more than one, so a single unlock is not captioned "1 of 1".
            int count = flow.SkillAnnouncementCount;
            if (count > 1)
                rows.Add(Info("counter", $"{flow.SkillAnnouncementIndex + 1} of {count}"));

            rows.Add(new RowModel
            {
                id = "continue",
                label = "Continue",
                enabled = true,
                action = new ContinuePostBattleAction(),
                context = Context,
            });

            return rows;
        }
    }
}
