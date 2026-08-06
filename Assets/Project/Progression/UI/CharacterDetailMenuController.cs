using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using JRPG.Characters;
using JRPG.Core;
using JRPG.Data;
using JRPG.Menu;
using JRPG.Services;

namespace JRPG.Progression.UI
{
    /// <summary>
    /// One character's readout: a fixed panel of numbers with a stat
    /// radar, and one navigable list.
    ///
    /// <para><b>Only the skills are navigable.</b> Level, vitals and stats are facts, not choices, so
    /// they render as a read-only block rather than as rows the cursor has to walk past to reach
    /// anything useful. The list is skills alone, and the detail panel follows the highlighted one.</para>
    ///
    /// <para><b>The detail panel has two modes.</b> Skill descriptions by default; the toggle input
    /// swaps it to the character's passives, which belong to the character rather than to any one row
    /// and so have nowhere else to live.</para>
    /// 
    /// </summary>
    public sealed class CharacterDetailMenuController : MenuController
    {
        [Tooltip("Read-only block: level, XP, vitals and the full stat list.")]
        [SerializeField] private TMP_Text summaryLabel;

        [Tooltip("Optional. Radar over the character's stat block.")]
        [SerializeField] private StatRadarView radar;

        [Tooltip("Optional. Shows the focused skill, or the character's passives while toggled.")]
        [SerializeField] private DetailPanelController detailPanel;

        /// Stats plotted on the radar, in axis order.
        private static readonly StatType[] RadarStats =
        {
            StatType.Strength, StatType.Magic, StatType.Defense,
            StatType.Resistance, StatType.Speed, StatType.Luck,
        };

        /// Skill actions backing the rows, index-aligned. Null for the empty-state row.
        private readonly List<CombatActionData> _rowSkills = new();

        /// Item id that granted the row's skill, or null when the character knows it outright.
        /// Index-aligned with <see cref="_rowSkills"/>.
        private readonly List<string> _rowSources = new();

        /// False = focused skill's description; true = the character's passives.
        private bool _showPassives;

        protected override void OnEnable()
        {
            _showPassives = false;
            base.OnEnable();

            RefreshSummary();
            RefreshRadar();
        }

        // ---- Skill list ------------------------------------------------------------------------------

        protected override IReadOnlyList<RowModel> BuildRows()
        {
            _rowSkills.Clear();
            _rowSources.Clear();
            var rows = new List<RowModel>();

            var subject = Context?.Subject;
            if (subject == null)
            {
                _rowSkills.Add(null);
                _rowSources.Add(null);
                rows.Add(RowModel.Simple("none", "No character selected.", null, Context, enabled: false,
                                         disabledReason: string.Empty));
                return rows;
            }

            var data = AppContext.Data as DataRegistry;

            // Learned skills first, in the order the character learned them.
            var known = new HashSet<string>();
            for (int i = 0; i < subject.skillIds.Count; i++)
            {
                if (!known.Add(subject.skillIds[i])) continue;
                AddSkillRow(rows, data, subject.skillIds[i], grantedBy: null);
            }

            // Then anything the character's gear unlocks.
            foreach (var unlocked in EquipmentUnlocks(subject.SourceDataId))
            {
                // Only Skill-category unlocks belong in a skills list; gear can also unlock other
                // categories.
                if (data == null || !data.TryGet<CombatActionData>(unlocked.actionId, out var action)
                    || action == null || action.category != CombatActionCategory.Skill)
                    continue;

                // Already learned
                if (known.Contains(unlocked.actionId)) continue;

                AddSkillRow(rows, data, unlocked.actionId, grantedBy: unlocked.sourceItemId);
            }

            if (rows.Count == 0)
            {
                _rowSkills.Add(null);
                _rowSources.Add(null);

                rows.Add(RowModel.Simple("empty", "No skills learned.", null, Context, enabled: false,
                                         disabledReason: string.Empty));
            }

            return rows;
        }

        private void AddSkillRow(List<RowModel> rows, DataRegistry data, string skillId, string grantedBy)
        {
            CombatActionData action = null;
            data?.TryGet(skillId, out action);

            rows.Add(new RowModel
            {
                id = skillId,
                label = action != null && !string.IsNullOrEmpty(action.displayName) ? action.displayName : skillId,
                auxText = action != null ? CostLine(action) : string.Empty,
                costText = grantedBy != null ? "EQUIP" : string.Empty,
                enabled = true,
                action = null,
                context = Context,
            });

            _rowSkills.Add(action);
            _rowSources.Add(grantedBy);
        }

        /// <summary>
        /// Gear-unlocked actions for this character, from the equipment service.
        /// </summary>
        private IReadOnlyList<UnlockedAction> EquipmentUnlocks(string charId)
        {
            if (Context?.Services == null
                || !Context.Services.TryResolve<IEquipmentService>(out var equipment)
                || equipment == null)
                return System.Array.Empty<UnlockedAction>();

            return equipment.GetUnlockedActions(charId);
        }

        private static string CostLine(CombatActionData action)
        {
            if (action.costs == null || action.costs.Count == 0) return string.Empty;

            var parts = new List<string>(action.costs.Count);
            for (int i = 0; i < action.costs.Count; i++)
            {
                var c = action.costs[i];
                if (c.type is CombatCostType.Item or CombatCostType.None) continue;
                parts.Add($"{c.costAmount} {c.type}");
            }

            return string.Join("  ", parts);
        }

        // ---- Read-only summary -----------------------------------------------------------------------

        private void RefreshSummary()
        {
            if (summaryLabel == null) return;

            var subject = Context?.Subject;
            if (subject == null) { summaryLabel.text = string.Empty; return; }

            var progression = Context?.Services != null
                              && Context.Services.TryResolve<IProgressionService>(out var svc)
                ? svc as ProgressionService
                : null;

            var sb = new StringBuilder();
            sb.AppendLine($"<b>{subject.DisplayName}</b>    Lv {subject.level}");

            if (progression != null)
            {
                int toNext = progression.GetXpToNextLevel(subject.SourceDataId);
                sb.AppendLine(toNext > 0 ? $"XP {subject.currentXp}    Next: {toNext}" : $"XP {subject.currentXp}    Max level");
                sb.AppendLine($"Growth: {progression.GetProgressForCharacter(subject.SourceDataId).levelUpMode}");
            }
            else
            {
                sb.AppendLine($"XP {subject.currentXp}");
            }

            sb.AppendLine();
            sb.AppendLine($"HP {subject.currentHP} / {subject.MaxHP}");
            sb.AppendLine($"MP {subject.currentMP} / {subject.MaxMP}");
            sb.AppendLine($"SP {subject.currentSP} / {subject.MaxSP}");
            sb.AppendLine();

            foreach (StatType stat in System.Enum.GetValues(typeof(StatType)))
            {
                // The three pools are already spelled out above with their current values.
                if (stat is StatType.MaxHP or StatType.MaxMP or StatType.MaxSP) continue;
                sb.AppendLine($"{stat}  {subject.stats.GetFinal(stat)}");
            }

            summaryLabel.text = sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Normalises each plotted stat against the largest of them, so the shape shows the character's
        /// balance. An absolute scale would need a project-wide stat ceiling.
        /// </summary>
        private void RefreshRadar()
        {
            if (radar == null) return;

            var subject = Context?.Subject;
            if (subject == null) { radar.ClearAxes(); return; }

            var labels = new List<string>(RadarStats.Length);
            var raw = new List<float>(RadarStats.Length);
            float max = 1f;

            for (int i = 0; i < RadarStats.Length; i++)
            {
                int value = subject.stats.GetFinal(RadarStats[i]);
                labels.Add(RadarStats[i].ToString());
                raw.Add(value);
                if (value > max) max = value;
            }

            var normalized = new List<float>(raw.Count);
            for (int i = 0; i < raw.Count; i++) normalized.Add(Mathf.Clamp01(raw[i] / max));

            radar.SetAxes(labels, normalized);
        }

        // ---- Detail panel ----------------------------------------------------------------------------

        /// <summary>Swaps the detail panel between the focused skill and the character's passives.</summary>
        protected override void OnTab()
        {
            _showPassives = !_showPassives;
            RefreshDetail(HighlightedIndex);
            RefreshPrompts();
        }

        protected override void OnHighlightChanged(int index, RowModel model) => RefreshDetail(index);

        private void RefreshDetail(int index)
        {
            if (detailPanel == null) return;

            if (_showPassives) { ShowPassives(); return; }

            var action = index >= 0 && index < _rowSkills.Count ? _rowSkills[index] : null;
            if (action == null) { detailPanel.Clear(); return; }

            var body = string.IsNullOrEmpty(action.description) ? "No description." : action.description;

            // Provenance for a gear-granted skill: it disappears when the item comes off, which the
            // player needs to know before planning around it.
            var source = index < _rowSources.Count ? _rowSources[index] : null;
            if (!string.IsNullOrEmpty(source)) body += $"\n\nGranted by {ItemName(source)}.";

            detailPanel.ShowDetail(
                string.IsNullOrEmpty(action.displayName) ? action.Id : action.displayName,
                body,
                null,
                CostLine(action));
        }

        private static string ItemName(string itemId)
        {
            var data = AppContext.Data as DataRegistry;
            if (data != null && data.TryGet<ItemData>(itemId, out var item) && item != null)
                return string.IsNullOrEmpty(item.displayName) ? item.Id : item.displayName;

            return itemId;
        }

        /// <summary>
        /// Passives granted by the character themselves plus everything they have equipped.
        ///
        /// <para>Names and descriptions come from <see cref="PassiveData"/>, keyed by the same id the
        /// passive registers under in code. A passive with no such asset falls back to its raw id rather
        /// than being hidden — the effect is real either way, and silently omitting it would misreport
        /// what the character has.</para>
        /// </summary>
        private void ShowPassives()
        {
            var subject = Context?.Subject;
            if (subject == null) { detailPanel.Clear(); return; }

            var data = AppContext.Data as DataRegistry;
            var innate = new List<string>();
            var fromGear = new List<string>();
            var immunities = new List<string>();

            if (data != null && data.TryGet<CharacterData>(subject.SourceDataId, out var charData) && charData != null)
            {
                innate.AddRange(charData.passiveEffectIds);
                immunities.AddRange(charData.statusImmunityIds);
            }

            if (subject.equippedItemIds != null && data != null)
            {
                for (int i = 0; i < subject.equippedItemIds.Count; i++)
                {
                    if (!data.TryGet<EquipmentData>(subject.equippedItemIds[i], out var equip) || equip == null) continue;

                    fromGear.AddRange(equip.passiveEffectIds);
                    immunities.AddRange(equip.statusImmunityIds);
                }
            }

            var sb = new StringBuilder();
            Section(sb, "Innate", innate, data, PassiveLine);
            Section(sb, "From equipment", fromGear, data, PassiveLine);
            Section(sb, "Status immunity", immunities, data, StatusLine);

            if (sb.Length == 0) sb.Append("No passives.");

            detailPanel.ShowDetail("Passives", sb.ToString().TrimEnd(), null, subject.DisplayName);
        }

        private static void Section(StringBuilder sb, string heading, List<string> ids, DataRegistry data,
                                    System.Func<DataRegistry, string, string> format)
        {
            if (ids == null || ids.Count == 0) return;

            sb.AppendLine(heading + ":");

            // Ids can repeat when two pieces of gear grant the same thing; listing it twice would read
            // as two separate effects.
            var seen = new HashSet<string>();
            for (int i = 0; i < ids.Count; i++)
            {
                if (string.IsNullOrEmpty(ids[i]) || !seen.Add(ids[i])) continue;
                sb.AppendLine("  • " + format(data, ids[i]));
            }

            sb.AppendLine();
        }

        /// "Fire Ward — halves incoming Fire damage", or the bare id when nothing is authored.
        private static string PassiveLine(DataRegistry data, string id)
        {
            if (data == null || !data.TryGet<PassiveData>(id, out var passive) || passive == null) return id;

            var name = string.IsNullOrEmpty(passive.displayName) ? id : passive.displayName;
            return string.IsNullOrEmpty(passive.description) ? name : $"{name} — {passive.description}";
        }

        private static string StatusLine(DataRegistry data, string id)
        {
            if (data == null || !data.TryGet<StatusEffectData>(id, out var status) || status == null) return id;

            return string.IsNullOrEmpty(status.displayName) ? id : status.displayName;
        }

        public override IReadOnlyList<InputPrompt> Prompts =>
            _showPassives ? PassivePrompts : SkillPrompts;

        private static readonly InputPrompt[] SkillPrompts =
        {
            new InputPrompt("Tab", "Passives"),
            new InputPrompt("Cancel", "Back"),
        };

        private static readonly InputPrompt[] PassivePrompts =
        {
            new InputPrompt("Tab", "Skills"),
            new InputPrompt("Cancel", "Back"),
        };
    }
}
