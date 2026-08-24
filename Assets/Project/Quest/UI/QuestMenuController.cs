using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using JRPG.Core;
using JRPG.Data;
using JRPG.Menu;
using JRPG.Services;

namespace JRPG.Quest.UI
{
    /// <summary>
    /// The quest journal: Story, Missions and Bonds as three tabs of one screen.
    ///
    ///
    /// <para><b>Nothing is cached.</b> Every rebuild re-queries <see cref="IQuestService"/>, and
    /// collection objectives are refreshed against live inventory first, so the counts on screen are
    /// the counts the service would act on.</para>
    /// </summary>
    public sealed class QuestMenuController : MenuController
    {
        private enum Tab { Story, Missions, Bonds }

        private static readonly (string id, string label, Tab tab)[] TabDefs =
        {
            ("story",    "Story",    Tab.Story),
            ("missions", "Missions", Tab.Missions),
            ("bonds",    "Bonds",    Tab.Bonds),
        };

        [Header("Quest screen wiring (all optional)")]
        [SerializeField] private TabStripController tabStrip;
        [SerializeField] private DetailPanelController detailPanel;
        [SerializeField] private ConfirmPromptController confirmPrompt;

        /// One entry per row, index-aligned with the populator's rows. A quest row carries a quest id,
        /// a bond row a character id, and a separator neither.
        private readonly List<(string questId, string characterId)> _rowSubjects = new();

        private bool _tabsBuilt;

        protected override bool ModalActive => confirmPrompt != null && confirmPrompt.IsOpen;

        // ---- Lifecycle ----------------------------------------------------------------------------

        protected override void OnEnable()
        {
            BuildTabs();
            base.OnEnable();
        }

        private void BuildTabs()
        {
            if (tabStrip == null || _tabsBuilt) return;

            var defs = new List<TabDef>(TabDefs.Length);
            for (int i = 0; i < TabDefs.Length; i++) defs.Add(new TabDef(TabDefs[i].id, TabDefs[i].label));

            tabStrip.SetTabs(defs);
            tabStrip.TabChanged += OnTabChanged;
            _tabsBuilt = true;
        }

        private void OnDestroy()
        {
            if (tabStrip != null) tabStrip.TabChanged -= OnTabChanged;
        }

        private void OnTabChanged(int index) => RebuildAndFocus();

        protected override void OnPageLeft() => tabStrip?.Previous();
        protected override void OnPageRight() => tabStrip?.Next();

        protected override void OnNavigateHorizontal(int dir)
        {
            if (dir < 0) tabStrip?.Previous();
            else tabStrip?.Next();
        }

        private Tab ActiveTab()
        {
            if (tabStrip == null) return Tab.Story;

            int index = tabStrip.ActiveIndex;
            return index >= 0 && index < TabDefs.Length ? TabDefs[index].tab : Tab.Story;
        }

        // ---- Rows ---------------------------------------------------------------------------------

        protected override IReadOnlyList<RowModel> BuildRows()
        {
            _rowSubjects.Clear();

            var rows = new List<RowModel>();
            var service = ResolveService();
            if (service == null)
            {
                AddPlain(rows, "unavailable", "Quest service unavailable.");
                return rows;
            }

            // Inventory is the authority on collection counts; ask it before drawing them.
            service.RefreshCollectionObjectives();

            switch (ActiveTab())
            {
                case Tab.Missions:
                    BuildQuestRows(rows, service, QuestType.Side);
                    break;
                case Tab.Bonds:
                    BuildBondRows(rows, service);
                    break;
                default:
                    BuildQuestRows(rows, service, QuestType.Main);
                    break;
            }

            if (rows.Count == 0) AddPlain(rows, "empty", EmptyLabel());

            return rows;
        }

        private void BuildQuestRows(List<RowModel> rows, QuestService service, QuestType type)
        {
            AddSection(rows, "sec_active", "ACTIVE", service.GetQuestIds(type, QuestStateFilter.Active), service);
            AddSection(rows, "sec_available", "AVAILABLE", service.GetQuestIds(type, QuestStateFilter.Available), service);
            AddSection(rows, "sec_complete", "COMPLETED", service.GetQuestIds(type, QuestStateFilter.Complete), service);
        }

        private void AddSection(List<RowModel> rows, string sectionId, string heading,
                                IReadOnlyList<string> questIds, QuestService service)
        {
            if (questIds == null || questIds.Count == 0) return;

            rows.Add(RowModel.Separator(sectionId, heading));
            _rowSubjects.Add((null, null));

            for (int i = 0; i < questIds.Count; i++)
            {
                var quest = service.GetQuest(questIds[i]);
                if (quest == null) continue;

                rows.Add(BuildQuestRow(quest, service));
                _rowSubjects.Add((quest.Id, quest.characterId));
            }
        }

        /// <summary>
        /// <b>Every quest row is focusable, including finished ones.</b> A row the player cannot act on
        /// still has a description, objectives and rewards worth reading, and a journal whose completed
        /// entries cannot be selected is a journal that cannot be read. Rows with nothing to do simply
        /// carry no action, which <see cref="MenuController.ExecuteRow"/> treats as a no-op.
        /// </summary>
        private RowModel BuildQuestRow(QuestData quest, QuestService service)
        {
            var state = service.GetState(quest.Id);

            IMenuAction action = null;

            if (state == QuestState.Available)
            {
                action = new AcceptQuestAction(quest.Id);
            }
            else if (state == QuestState.Active && !quest.autoComplete && ObjectivesMet(quest, service))
            {
                // A hand-in quest whose objectives are done is the only active row that does anything;
                // everything else finishes itself the moment its objectives land.
                action = new TurnInQuestAction(quest.Id, true);
            }

            return new RowModel
            {
                id = quest.Id,
                label = quest.DisplayTitle,
                auxText = RowStatus(quest, service, state),
                enabled = true,
                action = action,
                context = RowContext(quest.Id, quest.characterId),
            };
        }

        private void BuildBondRows(List<RowModel> rows, QuestService service)
        {
            var characterIds = service.GetBondCharacterIds();

            for (int i = 0; i < characterIds.Count; i++)
            {
                string characterId = characterIds[i];
                int level = service.GetBondLevel(characterId);
                int max = service.GetMaxBondLevel(characterId);

                var nextQuest = service.GetNextBondQuest(characterId);
                var nextState = nextQuest == null ? QuestState.Hidden : service.GetState(nextQuest.Id);

                IMenuAction action = null;
                string status;

                if (nextQuest == null)
                {
                    status = "Bond complete";
                }
                else if (nextState == QuestState.Available)
                {
                    action = new AcceptQuestAction(nextQuest.Id);
                    status = "Quest available";
                }
                else if (nextState == QuestState.Active)
                {
                    status = $"{nextQuest.DisplayTitle} under way";
                }
                else
                {
                    // The threshold is stated rather than hidden. It lives in the row's own aux text
                    // rather than a disabled-reason affix, because the row is selectable and that
                    // affix only renders while a row is disabled.
                    status = $"Next at {service.GetBondProgress(characterId)} / " +
                             $"{service.GetBondProgressRequired(characterId)}";
                }

                string levelText = max > 0 ? $"Bond {level} / {max}" : $"Bond {level}";

                rows.Add(new RowModel
                {
                    id = characterId,
                    label = service.GetBondDisplayName(characterId),
                    auxText = $"{levelText}   ·   {status}",
                    enabled = true,
                    action = action,
                    context = RowContext(nextQuest?.Id, characterId),
                });

                _rowSubjects.Add((nextQuest?.Id, characterId));
            }
        }

        private void AddPlain(List<RowModel> rows, string id, string label)
        {
            rows.Add(RowModel.Simple(id, label, null, Context, enabled: false, disabledReason: string.Empty));
            _rowSubjects.Add((null, null));
        }

        private string EmptyLabel() => ActiveTab() switch
        {
            Tab.Missions => "No missions yet.",
            Tab.Bonds => "No bonds yet.",
            _ => "No story quests yet.",
        };

        private MenuContext RowContext(string questId, string characterId) => new()
        {
            Services = Context?.Services,
            Menus = Context?.Menus,
            Subject = Context?.Subject,
            Payload = string.IsNullOrEmpty(questId) ? characterId : questId,
        };

        // ---- Abandon ------------------------------------------------------------------------------

        /// <summary>
        /// Abandon lives on the Tab verb rather than a row of its own: it applies to whichever quest is
        /// focused, and giving it a row would put a destructive action in the same column as the
        /// harmless ones.
        /// </summary>
        protected override void OnTab()
        {
            if (ModalActive || confirmPrompt == null) return;

            var service = ResolveService();
            var quest = FocusedQuest(service);
            if (service == null || quest == null) return;
            if (quest.questType == QuestType.Main || quest.mandatory) return;
            if (service.GetState(quest.Id) != QuestState.Active) return;

            var action = new AbandonQuestAction(quest.Id);
            var context = RowContext(quest.Id, quest.characterId);

            confirmPrompt.Ask($"Abandon \"{quest.DisplayTitle}\"? Progress on it is lost.",
                              "Abandon", "Keep",
                              () => { action.Execute(context); RebuildAndFocus(); });
        }

        // ---- Detail panel -------------------------------------------------------------------------

        protected override void OnHighlightChanged(int index, RowModel model)
        {
            if (detailPanel == null) return;

            var service = ResolveService();
            if (service == null || index < 0 || index >= _rowSubjects.Count) { detailPanel.Clear(); return; }

            var (questId, characterId) = _rowSubjects[index];

            if (ActiveTab() == Tab.Bonds && !string.IsNullOrEmpty(characterId))
            {
                ShowBondDetail(service, characterId);
                return;
            }

            var quest = service.GetQuest(questId);
            if (quest == null) { detailPanel.Clear(); return; }

            detailPanel.ShowDetail(quest.DisplayTitle, BuildQuestBody(quest, service), null,
                                   BuildQuestFooter(quest, service));
        }

        private void ShowBondDetail(QuestService service, string characterId)
        {
            int level = service.GetBondLevel(characterId);
            int max = service.GetMaxBondLevel(characterId);

            var body = new StringBuilder();
            body.AppendLine($"Bond Level {level} / {max}");
            body.AppendLine($"Progress {service.GetBondProgress(characterId)} / " +
                            $"{service.GetBondProgressRequired(characterId)}");

            var next = service.GetNextBondQuest(characterId);
            if (next == null)
            {
                body.AppendLine();
                body.AppendLine("Every bond stage is complete.");
            }
            else
            {
                body.AppendLine();
                body.AppendLine($"Next: {next.DisplayTitle}  ({Describe(service.GetState(next.Id))})");

                if (!string.IsNullOrEmpty(next.description)) body.AppendLine(next.description);

                AppendObjectives(body, next, service);

                var bond = service.GetBondData(characterId);
                var stage = bond?.GetLevel(level + 1);
                if (stage != null && stage.benefits != null && stage.benefits.Count > 0)
                {
                    body.AppendLine();
                    body.AppendLine("Unlocks:");
                    for (int i = 0; i < stage.benefits.Count; i++)
                        body.AppendLine("  " + DescribeReward(stage.benefits[i]));
                }
            }

            detailPanel.ShowDetail(service.GetBondDisplayName(characterId), body.ToString(), null,
                                   CompletedBenefits(service, characterId, level));
        }

        /// The stages already earned, so the screen keeps a history rather than only a next step.
        private string CompletedBenefits(QuestService service, string characterId, int level)
        {
            if (level <= 0) return "No bond stages completed yet.";

            var bond = service.GetBondData(characterId);
            if (bond == null) return null;

            var parts = new List<string>(level);
            for (int i = 1; i <= level; i++)
            {
                var stage = bond.GetLevel(i);
                if (stage == null) continue;

                parts.Add(string.IsNullOrEmpty(stage.displayTitle) ? $"Lv {i}" : $"Lv {i} {stage.displayTitle}");
            }

            return parts.Count == 0 ? null : "Earned:  " + string.Join("  ·  ", parts);
        }

        private string BuildQuestBody(QuestData quest, QuestService service)
        {
            var body = new StringBuilder();

            if (!string.IsNullOrEmpty(quest.description)) body.AppendLine(quest.description);

            AppendObjectives(body, quest, service);

            if (quest.completionRewards != null && quest.completionRewards.Count > 0)
            {
                body.AppendLine();
                body.AppendLine("Rewards:");
                for (int i = 0; i < quest.completionRewards.Count; i++)
                    body.AppendLine("  " + DescribeReward(quest.completionRewards[i]));
            }

            return body.ToString();
        }

        private void AppendObjectives(StringBuilder body, QuestData quest, QuestService service)
        {
            if (quest.objectives == null || quest.objectives.Count == 0) return;

            body.AppendLine();
            body.AppendLine("Objectives:");

            for (int i = 0; i < quest.objectives.Count; i++)
            {
                var objective = quest.objectives[i];
                if (objective == null) continue;

                service.TryGetObjectiveProgress(quest.Id, objective.objectiveId, out int current,
                                                out int required, out bool complete);

                string text = string.IsNullOrEmpty(objective.description)
                    ? DescribeObjective(objective)
                    : objective.description;

                // Counted objectives show the count; boolean ones would only ever read 0/1.
                string progress = objective.IsBoolean || required <= 1 ? string.Empty : $"  ({current}/{required})";
                string mark = complete ? "[x]" : "[ ]";
                string optional = objective.optional ? "  (optional)" : string.Empty;

                body.AppendLine($"  {mark} {text}{progress}{optional}");
            }
        }

        private string BuildQuestFooter(QuestData quest, QuestService service)
        {
            var parts = new List<string>(3) { Describe(service.GetState(quest.Id)) };

            if (!string.IsNullOrEmpty(quest.giverId)) parts.Add($"From: {CharacterName(quest.giverId)}");
            if (quest.questType == QuestType.Party && !string.IsNullOrEmpty(quest.characterId))
                parts.Add($"Bond: {CharacterName(quest.characterId)}");

            return string.Join("  ·  ", parts);
        }

        // ---- Text ---------------------------------------------------------------------------------

        private string RowStatus(QuestData quest, QuestService service, QuestState state)
        {
            if (state != QuestState.Active || quest.objectives == null || quest.objectives.Count == 0)
                return Describe(state);

            int done = 0;
            int total = 0;

            for (int i = 0; i < quest.objectives.Count; i++)
            {
                var objective = quest.objectives[i];
                if (objective == null || objective.optional) continue;

                total++;
                if (service.TryGetObjectiveProgress(quest.Id, objective.objectiveId, out _, out _, out bool complete)
                    && complete)
                    done++;
            }

            return total == 0 ? Describe(state) : $"{done} / {total}";
        }

        private static string Describe(QuestState state) => state switch
        {
            QuestState.Available => "Available",
            QuestState.Active => "In Progress",
            QuestState.Complete => "Complete",
            QuestState.Failed => "Failed",
            _ => "Unknown",
        };

        private string DescribeObjective(QuestObjectiveData objective) => objective.type switch
        {
            QuestObjectiveType.DefeatEnemy => $"Defeat {Name<EnemyData>(objective.targetId)}",
            QuestObjectiveType.CollectItem => $"Collect {Name<ItemData>(objective.targetId)}",
            QuestObjectiveType.UseItem => $"Use {Name<ItemData>(objective.targetId)}",
            QuestObjectiveType.TalkToCharacter => "Speak with someone",
            QuestObjectiveType.ReachLocation => "Reach the destination",
            QuestObjectiveType.Interact => "Investigate",
            QuestObjectiveType.StoryCondition => "Advance the story",
            _ => "Objective",
        };

        private string DescribeReward(QuestRewardData reward) => reward.type switch
        {
            QuestRewardType.Experience => $"{reward.amount} XP",
            QuestRewardType.Currency => $"{reward.amount} {CurrencyName()}",
            QuestRewardType.Item => $"{Name<ItemData>(reward.id)} × {reward.amount}",
            QuestRewardType.Skill => $"Skill: {Name<CombatActionData>(reward.id)}",
            QuestRewardType.BondProgress => $"{reward.amount} bond progress",
            QuestRewardType.StoryFlag => "Story progress",
            _ => "Reward",
        };

        private string CurrencyName()
        {
            var data = AppContext.Data as DataRegistry;
            var settings = data?.EconomySettings;

            return settings != null && !string.IsNullOrEmpty(settings.currencyName) ? settings.currencyName : "gold";
        }

        private string CharacterName(string id) => Name<CharacterData>(id);

        private string Name<T>(string id) where T : GameDataBase
        {
            if (string.IsNullOrEmpty(id)) return "—";

            var data = AppContext.Data as DataRegistry;
            if (data != null && data.TryGet<T>(id, out var asset) && asset != null
                && !string.IsNullOrEmpty(asset.displayName))
                return asset.displayName;

            return id;
        }

        // ---- Plumbing -----------------------------------------------------------------------------

        private QuestService ResolveService()
        {
            if (Context?.Services == null) return null;

            // The screen needs the definitions and the bond ledger, neither of which is on the lean
            // interface — the same concrete resolve the progression screens make.
            return Context.Services.TryResolve<IQuestService>(out var service) ? service as QuestService : null;
        }

        private QuestData FocusedQuest(QuestService service)
        {
            int index = HighlightedIndex;
            if (service == null || index < 0 || index >= _rowSubjects.Count) return null;

            return service.GetQuest(_rowSubjects[index].questId);
        }

        private bool ObjectivesMet(QuestData quest, QuestService service)
        {
            if (quest.objectives == null) return true;

            for (int i = 0; i < quest.objectives.Count; i++)
            {
                var objective = quest.objectives[i];
                if (objective == null || objective.optional) continue;

                if (!service.TryGetObjectiveProgress(quest.Id, objective.objectiveId, out _, out _, out bool complete)
                    || !complete)
                    return false;
            }

            return true;
        }

        protected override void OnSubmit(InputAction.CallbackContext ctx)
        {
            if (ModalActive) return;
            base.OnSubmit(ctx);
        }

        public override IReadOnlyList<InputPrompt> Prompts { get; } = new[]
        {
            new InputPrompt("Submit", "Select"),
            new InputPrompt("Cancel", "Back"),
            new InputPrompt("PageL", "Category"),
            new InputPrompt("Tab", "Abandon"),
        };
    }
}
