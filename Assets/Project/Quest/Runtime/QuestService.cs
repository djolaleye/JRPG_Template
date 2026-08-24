using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Dialogue;
using JRPG.Inventory;
using JRPG.Party;
using JRPG.Progression;
using JRPG.Save;
using JRPG.Services;

namespace JRPG.Quest
{
    /// <summary>
    /// The runtime authority for quests and bonds.
    ///
    /// <para><b>Discovery, acceptance and completion.</b> A Main quest
    /// passes through all three from story progression alone; a Side or Party quest stops at
    /// Available until the player accepts it. Nothing outside this service moves a quest between
    /// states, and nothing outside it grants a quest reward.</para>
    ///
    /// <para><b>Objective progress arrives as notifications.</b> The service subscribes to the events
    /// combat, dialogue and inventory publish and matches them against active objectives, so
    /// those systems carry no quest branches. Location and interaction objectives, which have no
    /// existing event, are reported explicitly through
    /// <see cref="RegisterObjectiveProgress"/>.</para>
    ///
    /// <para><b>Collection objectives are a projection.</b> Inventory remains the authority on how many
    /// of an item the player holds; the counter here is a cached view recomputed whenever it could
    /// have moved, so selling or using a quest item walks the objective back down.</para>
    /// </summary>
    public sealed class QuestService : IQuestService, ISaveable
    {
        private readonly DataRegistry _data;
        private readonly IEventBus _bus;
        private readonly IStoryStateService _story;
        private readonly IPartyService _party;
        private readonly IPartyRuntimeQueries _partyRuntime;
        private readonly IInventoryService _inventory;

        private readonly DialogueConditionEvaluator _conditions;
        private readonly QuestRewardApplier _rewards;

        private readonly Dictionary<string, QuestRuntimeState> _quests = new();
        private readonly Dictionary<string, int> _bondLevel = new();
        private readonly Dictionary<string, int> _bondProgress = new();

        /// Guards the reconciliation pass against re-entry: applying a completion reward can set a
        /// story flag, which is what reconciliation listens to.
        private bool _reconciling;

        public QuestService(DataRegistry data, IEventBus bus, IStoryStateService story,
                            IPartyService party, IPartyRuntimeQueries partyRuntime,
                            IInventoryService inventory, ICurrencyService currency = null,
                            ProgressionService progression = null)
        {
            _data = data;
            _bus = bus;
            _story = story;
            _party = party;
            _partyRuntime = partyRuntime;
            _inventory = inventory;

            // Passing itself as the quest service lets a quest gate on another quest. Safe despite
            // running inside this constructor: the evaluator only calls back into the read-side.
            _conditions = new DialogueConditionEvaluator(party, partyRuntime, inventory, story, data,
                                                        services: null, quests: this);
            _rewards = new QuestRewardApplier(inventory, currency, story, partyRuntime, progression, TryGrantBondProgress);

            if (_bus != null)
            {
                _bus.Subscribe<StoryFlagChanged>(OnStoryFlagChanged);
                _bus.Subscribe<DialogueCompleted>(OnDialogueCompleted);
                _bus.Subscribe<BattleResultPackaged>(OnBattleResultPackaged);
                _bus.Subscribe<ItemUsed>(OnItemUsed);
                _bus.Subscribe<GameLoaded>(OnGameLoaded);
            }

            ReconcileMainQuests();
        }

        /// Custom reward handlers are registered on the applier, which owns the extension seam.
        public QuestRewardApplier Rewards => _rewards;

        // ---- Definition lookup (concrete surface, used by UI and tooling) ----------------------

        public QuestData GetQuest(string questId)
            => !string.IsNullOrEmpty(questId) && _data != null && _data.QuestsById.TryGetValue(questId, out var q) ? q : null;

        public BondData GetBondData(string characterId)
            => !string.IsNullOrEmpty(characterId) && _data != null && _data.BondsByCharacterId.TryGetValue(characterId, out var b) ? b : null;

        /// <summary>
        /// Characters with authored bond data, in database order.
        ///
        /// <para>Recruited members only by default: a bond is a relationship with someone travelling
        /// with you, and listing a stranger's ladder would spoil who joins later. Tooling passes
        /// <paramref name="recruitedOnly"/> false to see the whole authored set.</para>
        /// </summary>
        public IReadOnlyList<string> GetBondCharacterIds(bool recruitedOnly = true)
        {
            var ids = new List<string>();
            if (_data == null) return ids;

            foreach (var kv in _data.BondsByCharacterId)
            {
                if (recruitedOnly && !IsBondCharacterAvailable(kv.Key)) continue;
                ids.Add(kv.Key);
            }

            return ids;
        }

        /// <summary>
        /// Whether the character is in the party in any capacity. With no party service,
        /// every bond is treated as available rather than none.
        /// </summary>
        public bool IsBondCharacterAvailable(string characterId)
            => _party == null || _party.IsRecruited(characterId);

        public string GetBondDisplayName(string characterId)
        {
            var bond = GetBondData(characterId);
            if (bond != null && !string.IsNullOrEmpty(bond.displayName)) return bond.displayName;

            if (_data != null && _data.CharactersById.TryGetValue(characterId ?? string.Empty, out var character)
                && !string.IsNullOrEmpty(character.displayName))
                return character.displayName;

            return characterId;
        }

        public int GetMaxBondLevel(string characterId)
        {
            var bond = GetBondData(characterId);
            if (bond == null) return 0;

            int authored = bond.HighestAuthoredLevel;
            return authored > 0 ? Mathf.Min(authored, bond.maxLevel) : bond.maxLevel;
        }

        public bool IsBondLevelComplete(string characterId, int level) => GetBondLevel(characterId) >= level;

        /// The bond quest the character is working towards, or null when there is no next level.
        public QuestData GetNextBondQuest(string characterId)
        {
            var next = NextBondLevel(characterId);
            return next == null ? null : GetQuest(next.questId);
        }

        // ---- IQuestService: queries ------------------------------------------------------------

        public QuestState GetState(string questId)
            => _quests.TryGetValue(questId ?? string.Empty, out var state) ? state.state : QuestState.Hidden;

        public bool IsKnown(string questId) => GetState(questId) != QuestState.Hidden;
        public bool IsActive(string questId) => GetState(questId) == QuestState.Active;
        public bool IsComplete(string questId) => GetState(questId) == QuestState.Complete;

        public IReadOnlyList<string> GetQuestIds(QuestType type, QuestStateFilter filter)
        {
            var ids = new List<string>();
            if (_data == null) return ids;

            foreach (var kv in _data.QuestsById)
            {
                var quest = kv.Value;
                if (quest.questType != type) continue;
                if (Matches(GetState(quest.Id), filter)) ids.Add(quest.Id);
            }

            return ids;
        }

        private static bool Matches(QuestState state, QuestStateFilter filter) => filter switch
        {
            QuestStateFilter.All => true,
            QuestStateFilter.Known => state != QuestState.Hidden,
            QuestStateFilter.Open => state == QuestState.Active || state == QuestState.Available,
            QuestStateFilter.Available => state == QuestState.Available,
            QuestStateFilter.Active => state == QuestState.Active,
            QuestStateFilter.Complete => state == QuestState.Complete,
            QuestStateFilter.Failed => state == QuestState.Failed,
            _ => false,
        };

        public IReadOnlyList<string> GetObjectiveIds(string questId)
        {
            var ids = new List<string>();
            var quest = GetQuest(questId);
            if (quest?.objectives == null) return ids;

            for (int i = 0; i < quest.objectives.Count; i++)
                if (quest.objectives[i] != null) ids.Add(quest.objectives[i].objectiveId);

            return ids;
        }

        public bool TryGetObjectiveProgress(string questId, string objectiveId,
                                            out int current, out int required, out bool complete)
        {
            current = 0;
            required = 0;
            complete = false;

            var definition = FindObjectiveData(questId, objectiveId);
            if (definition == null) return false;

            required = definition.EffectiveRequiredAmount;

            if (_quests.TryGetValue(questId, out var runtime))
            {
                var objective = runtime.Find(objectiveId);
                if (objective != null)
                {
                    current = objective.currentAmount;
                    complete = objective.complete;
                }
            }

            return true;
        }

        // ---- IQuestService: transitions --------------------------------------------------------

        public bool DiscoverQuest(string questId)
        {
            var quest = GetQuest(questId);
            if (quest == null)
            {
                Debug.LogWarning($"[JRPG.Quest] DiscoverQuest('{questId}') — no such quest.");
                return false;
            }

            if (IsKnown(questId)) return false;
            if (!_conditions.EvaluateAll(quest.availabilityConditions, null)) return false;

            var runtime = EnsureRuntime(quest);
            runtime.state = QuestState.Available;
            _bus?.Publish(new QuestDiscovered(quest.Id, quest.questType));

            // The story is not offered to the player, so a Main quest continues straight through.
            if (quest.questType == QuestType.Main) Activate(quest, runtime);

            return true;
        }

        public bool TryAccept(string questId)
        {
            var quest = GetQuest(questId);
            if (quest == null) return false;

            if (!_quests.TryGetValue(questId, out var runtime) || runtime.state != QuestState.Available)
                return false;

            Activate(quest, runtime);
            return true;
        }

        private void Activate(QuestData quest, QuestRuntimeState runtime)
        {
            runtime.state = QuestState.Active;
            _bus?.Publish(new QuestAccepted(quest.Id, quest.questType));

            // A quest may already be satisfied the moment it is taken — the item is in the bag, the
            // flag is set. Evaluating now means the player is not sent to do what they have done.
            RefreshDerivedObjectives(quest, runtime);
            TryAutoComplete(quest, runtime);
        }

        public bool TryAbandon(string questId)
        {
            var quest = GetQuest(questId);
            if (quest == null) return false;

            if (quest.questType == QuestType.Main || quest.mandatory)
            {
                Debug.LogWarning($"[JRPG.Quest] '{questId}' is mandatory and cannot be abandoned.");
                return false;
            }

            if (!_quests.TryGetValue(questId, out var runtime) || runtime.state != QuestState.Active)
                return false;

            // Back to Available, not Hidden: the player still knows the quest exists, and un-knowing it
            // would let a one-shot discovery be lost for the rest of the game.
            runtime.state = QuestState.Available;
            ResetObjectives(quest, runtime);
            _bus?.Publish(new QuestAbandoned(quest.Id));

            return true;
        }

        public bool TryComplete(string questId)
        {
            var quest = GetQuest(questId);
            if (quest == null) return false;

            if (!_quests.TryGetValue(questId, out var runtime)) return false;
            if (runtime.state != QuestState.Active) return false;

            RefreshDerivedObjectives(quest, runtime);
            if (!AreRequiredObjectivesMet(quest, runtime)) return false;

            // Everything that could refuse the transaction is asked before anything is granted.
            if (!_rewards.CanReceiveAll(quest.completionRewards, out var blocked))
            {
                Debug.LogWarning($"[JRPG.Quest] '{questId}' not completed — {blocked}");
                return false;
            }

            if (!runtime.rewardsApplied)
            {
                _rewards.ApplyAll(quest.completionRewards, quest.characterId);
                runtime.rewardsApplied = true;
            }

            runtime.state = QuestState.Complete;
            _bus?.Publish(new QuestCompleted(quest.Id, quest.questType));

            // Bond advancement is part of the same transaction: the level is what the quest was for.
            if (quest.questType == QuestType.Party) AdvanceBond(quest);

            if (!string.IsNullOrEmpty(quest.completionFlag)) _story?.SetBool(quest.completionFlag, true);
            if (!string.IsNullOrEmpty(quest.followUpQuestId)) DiscoverQuest(quest.followUpQuestId);

            return true;
        }

        // ---- IQuestService: objective progress -------------------------------------------------

        public bool RegisterObjectiveProgress(QuestObjectiveType type, string targetId, int amount = 1)
        {
            if (amount <= 0) return false;

            bool advancedAnything = false;

            foreach (var runtime in ActiveSnapshot())
            {
                if (runtime.state != QuestState.Active) continue;

                var quest = GetQuest(runtime.questId);
                if (quest?.objectives == null) continue;

                bool moved = false;

                for (int i = 0; i < quest.objectives.Count; i++)
                {
                    var definition = quest.objectives[i];
                    if (definition == null || definition.type != type) continue;
                    if (!string.Equals(definition.targetId, targetId)) continue;

                    moved |= Advance(quest, runtime, definition, amount);
                }

                if (moved) TryAutoComplete(quest, runtime);
                advancedAnything |= moved;
            }

            return advancedAnything;
        }

        public bool SetObjectiveComplete(string questId, string objectiveId)
        {
            var quest = GetQuest(questId);
            var definition = FindObjectiveData(questId, objectiveId);
            if (quest == null || definition == null) return false;

            if (!_quests.TryGetValue(questId, out var runtime) || runtime.state != QuestState.Active)
                return false;

            var objective = EnsureObjective(runtime, definition);
            if (objective.complete) return false;

            objective.currentAmount = definition.EffectiveRequiredAmount;
            objective.complete = true;
            PublishObjective(quest, definition, objective);

            TryAutoComplete(quest, runtime);
            return true;
        }

        /// <summary>
        /// Recomputes every collection objective from live inventory. Called whenever the bag could
        /// have changed and by the quest screen before it rebuilds, so what the player sees matches
        /// what they are carrying.
        /// </summary>
        public void RefreshCollectionObjectives()
        {
            foreach (var runtime in ActiveSnapshot())
            {
                if (runtime.state != QuestState.Active) continue;

                var quest = GetQuest(runtime.questId);
                if (quest == null) continue;

                if (RefreshDerivedObjectives(quest, runtime)) TryAutoComplete(quest, runtime);
            }
        }

        /// <summary>
        /// Copy of the active quests, taken because completing one can discover a follow-up and so
        /// grow the ledger mid-walk.
        /// </summary>
        private List<QuestRuntimeState> ActiveSnapshot()
        {
            var snapshot = new List<QuestRuntimeState>(_quests.Count);

            foreach (var kv in _quests)
                if (kv.Value.state == QuestState.Active) snapshot.Add(kv.Value);

            return snapshot;
        }

        // ---- IQuestService: bonds --------------------------------------------------------------

        public int GetBondLevel(string characterId)
            => _bondLevel.TryGetValue(characterId ?? string.Empty, out var level) ? level : 0;

        public int GetBondProgress(string characterId)
            => _bondProgress.TryGetValue(characterId ?? string.Empty, out var progress) ? progress : 0;

        public int GetBondProgressRequired(string characterId)
        {
            var next = NextBondLevel(characterId);
            return next?.requiredBondProgress ?? 0;
        }

        public bool IsBondQuestUnlocked(string characterId)
        {
            var next = NextBondLevel(characterId);
            if (next == null) return false;
            if (GetBondProgress(characterId) < next.requiredBondProgress) return false;

            // Completed is impossible here — completing the quest raises the level, which moves
            // NextBondLevel on — so an unlocked quest is always one still to be done.
            return !string.IsNullOrEmpty(next.questId) && !IsComplete(next.questId);
        }

        public bool TryGrantBondProgress(string characterId, int amount,
                                         BondProgressSource source = BondProgressSource.Unspecified)
        {
            if (string.IsNullOrEmpty(characterId) || amount <= 0) return false;

            var bond = GetBondData(characterId);
            if (bond == null) return false;

            // No bond with someone who is not travelling with you.
            if (!IsBondCharacterAvailable(characterId)) return false;

            // The quest is the gate. While one is unlocked and unfinished, the meter stops — otherwise
            // a player could bank progress towards levels they have not earned the right to reach.
            if (IsBondQuestUnlocked(characterId)) return false;

            var next = NextBondLevel(characterId);
            if (next == null) return false;

            int current = GetBondProgress(characterId) + amount;
            _bondProgress[characterId] = current;

            bool unlocked = current >= next.requiredBondProgress
                            && _conditions.EvaluateAll(next.availabilityConditions, null);

            _bus?.Publish(new BondProgressChanged(characterId, current, amount, source, unlocked));

            // Crossing the threshold makes the stage's quest known.
            if (unlocked && !string.IsNullOrEmpty(next.questId)) DiscoverQuest(next.questId);

            return true;
        }

        private BondLevelData NextBondLevel(string characterId)
        {
            var bond = GetBondData(characterId);
            if (bond == null) return null;

            int next = GetBondLevel(characterId) + 1;
            if (next > GetMaxBondLevel(characterId)) return null;

            return bond.GetLevel(next);
        }

        /// <summary>
        /// Raises the character's bond one stage as part of a Party quest's completion, and pays that
        /// stage's benefits. Levels never skip.
        /// </summary>
        private void AdvanceBond(QuestData quest)
        {
            var bond = GetBondData(quest.characterId);
            if (bond == null)
            {
                Debug.LogWarning($"[JRPG.Quest] Party quest '{quest.Id}' has no bond data for '{quest.characterId}'.");
                return;
            }

            int target = quest.bondLevel > 0 ? quest.bondLevel : LevelForQuest(bond, quest.Id);
            int current = GetBondLevel(quest.characterId);

            if (target != current + 1)
            {
                Debug.LogWarning($"[JRPG.Quest] '{quest.Id}' targets bond level {target} but " +
                                 $"'{quest.characterId}' is at {current}. Bond levels never skip.");
                return;
            }

            _bondLevel[quest.characterId] = target;
            _bus?.Publish(new BondLevelChanged(quest.characterId, current, target));

            var stage = bond.GetLevel(target);
            if (stage != null)
                _rewards.ApplyAll(stage.benefits, quest.characterId, BondProgressSource.QuestReward);

            // The next stage may already be paid for: progress is cumulative and was still accruing
            // before this quest unlocked, so re-check rather than making the player earn it twice.
            var following = NextBondLevel(quest.characterId);
            if (following != null && GetBondProgress(quest.characterId) >= following.requiredBondProgress
                && !string.IsNullOrEmpty(following.questId))
                DiscoverQuest(following.questId);
        }

        private static int LevelForQuest(BondData bond, string questId)
        {
            if (bond.levels == null) return 0;

            for (int i = 0; i < bond.levels.Count; i++)
                if (bond.levels[i] != null && bond.levels[i].questId == questId) return bond.levels[i].level;

            return 0;
        }

        // ---- Reconciliation --------------------------------------------------------------------

        /// <summary>
        /// Discovers every Main quest whose availability conditions now hold. Idempotent: a quest
        /// already known is skipped, and a completed one is never revisited - 
        /// safe to run on every story flag change and after every load.
        /// </summary>
        public void ReconcileMainQuests()
        {
            if (_data == null || _reconciling) return;

            _reconciling = true;
            try
            {
                foreach (var kv in _data.QuestsById)
                {
                    var quest = kv.Value;
                    if (quest.questType != QuestType.Main) continue;
                    if (IsKnown(quest.Id)) continue;

                    DiscoverQuest(quest.Id);
                }
            }
            finally
            {
                _reconciling = false;
            }
        }

        // ---- Event handlers --------------------------------------------------------------------

        private void OnStoryFlagChanged(StoryFlagChanged evt)
        {
            RegisterObjectiveProgress(QuestObjectiveType.StoryCondition, evt.FlagId);
            ReconcileMainQuests();
        }

        private void OnDialogueCompleted(DialogueCompleted evt)
        {
            RegisterObjectiveProgress(QuestObjectiveType.TalkToCharacter, evt.GraphId);
            RefreshCollectionObjectives();
        }

        private void OnItemUsed(ItemUsed evt)
        {
            RegisterObjectiveProgress(QuestObjectiveType.UseItem, evt.ItemId);
            RefreshCollectionObjectives();
        }

        private void OnGameLoaded(GameLoaded evt)
        {
            // Story flags in the file may already satisfy quests the ledger has no record of — a save
            // written before the quest existed, or before it was authored.
            ReconcileMainQuests();
            RefreshCollectionObjectives();
        }

        private void OnBattleResultPackaged(BattleResultPackaged evt)
        {
            if (evt.Outcome != BattleOutcome.Victory || evt.Result == null) return;

            var defeated = evt.Result.DefeatedEnemyIds;
            if (defeated != null)
                for (int i = 0; i < defeated.Count; i++)
                    RegisterObjectiveProgress(QuestObjectiveType.DefeatEnemy, defeated[i]);

            RefreshCollectionObjectives();
            GrantBattleParticipationBonds();
        }

        /// <summary>
        /// The bond drip - time spent fighting alongside someone. Authored on
        /// <see cref="BondSettings"/> so a project can turn it off entirely and leave dialogue and
        /// quest rewards as the only sources.
        /// </summary>
        private void GrantBattleParticipationBonds()
        {
            var settings = _data?.BondSettings;
            if (settings == null || settings.battleParticipationProgress <= 0 || _party == null) return;

            var active = _party.GetActivePartyIds();
            for (int i = 0; i < active.Count; i++)
                TryGrantBondProgress(active[i], settings.battleParticipationProgress,
                                     BondProgressSource.BattleParticipation);

            if (!settings.reserveMembersEarnFromBattle) return;

            var reserve = _party.GetReservePartyIds();
            for (int i = 0; i < reserve.Count; i++)
                TryGrantBondProgress(reserve[i], settings.battleParticipationProgress,
                                     BondProgressSource.BattleParticipation);
        }

        // ---- Objective plumbing ----------------------------------------------------------------

        private QuestObjectiveData FindObjectiveData(string questId, string objectiveId)
        {
            var quest = GetQuest(questId);
            if (quest?.objectives == null || string.IsNullOrEmpty(objectiveId)) return null;

            for (int i = 0; i < quest.objectives.Count; i++)
                if (quest.objectives[i] != null && quest.objectives[i].objectiveId == objectiveId)
                    return quest.objectives[i];

            return null;
        }

        private bool Advance(QuestData quest, QuestRuntimeState runtime, QuestObjectiveData definition, int amount)
        {
            var objective = EnsureObjective(runtime, definition);
            if (objective.complete) return false;

            // An objective can carry its own gate — "only counts once you have met her".
            if (!_conditions.EvaluateAll(definition.conditions, null)) return false;

            int required = definition.EffectiveRequiredAmount;
            objective.currentAmount = Mathf.Min(required, objective.currentAmount + amount);
            objective.complete = objective.currentAmount >= required;

            PublishObjective(quest, definition, objective);
            return true;
        }

        /// <summary>
        /// Re-derives the objectives that are views of state the player can lose as well as gain:
        /// collection counts from inventory, story conditions from flags. Returns true when anything moved.
        /// </summary>
        private bool RefreshDerivedObjectives(QuestData quest, QuestRuntimeState runtime)
        {
            if (quest?.objectives == null) return false;

            bool moved = false;

            for (int i = 0; i < quest.objectives.Count; i++)
            {
                var definition = quest.objectives[i];
                if (definition == null) continue;

                if (definition.type == QuestObjectiveType.CollectItem)
                {
                    if (_inventory == null || string.IsNullOrEmpty(definition.targetId)) continue;

                    var objective = EnsureObjective(runtime, definition);
                    int required = definition.EffectiveRequiredAmount;
                    int held = Mathf.Min(required, _inventory.GetQuantity(definition.targetId));

                    if (held == objective.currentAmount) continue;

                    objective.currentAmount = held;
                    objective.complete = held >= required;
                    PublishObjective(quest, definition, objective);
                    moved = true;
                }
                else if (definition.type == QuestObjectiveType.StoryCondition)
                {
                    if (_story == null || string.IsNullOrEmpty(definition.targetId)) continue;

                    var objective = EnsureObjective(runtime, definition);
                    bool satisfied = _story.GetBool(definition.targetId);

                    if (satisfied == objective.complete) continue;

                    objective.complete = satisfied;
                    objective.currentAmount = satisfied ? definition.EffectiveRequiredAmount : 0;
                    PublishObjective(quest, definition, objective);
                    moved = true;
                }
            }

            return moved;
        }

        private bool AreRequiredObjectivesMet(QuestData quest, QuestRuntimeState runtime)
        {
            if (quest?.objectives == null) return true;

            for (int i = 0; i < quest.objectives.Count; i++)
            {
                var definition = quest.objectives[i];
                if (definition == null || definition.optional) continue;

                var objective = runtime.Find(definition.objectiveId);
                if (objective == null || !objective.complete) return false;
            }

            return true;
        }

        private void TryAutoComplete(QuestData quest, QuestRuntimeState runtime)
        {
            if (!quest.autoComplete || runtime.state != QuestState.Active) return;
            if (!AreRequiredObjectivesMet(quest, runtime)) return;

            TryComplete(quest.Id);
        }

        private static QuestObjectiveRuntimeState EnsureObjective(QuestRuntimeState runtime, QuestObjectiveData definition)
        {
            var objective = runtime.Find(definition.objectiveId);
            if (objective != null) return objective;

            objective = new QuestObjectiveRuntimeState { objectiveId = definition.objectiveId };
            runtime.objectives.Add(objective);

            return objective;
        }

        private void PublishObjective(QuestData quest, QuestObjectiveData definition, QuestObjectiveRuntimeState objective)
            => _bus?.Publish(new QuestObjectiveUpdated(quest.Id, definition.objectiveId,
                                                       objective.currentAmount,
                                                       definition.EffectiveRequiredAmount,
                                                       objective.complete));

        private void ResetObjectives(QuestData quest, QuestRuntimeState runtime)
        {
            runtime.objectives.Clear();
            RefreshDerivedObjectives(quest, runtime);
        }

        private QuestRuntimeState EnsureRuntime(QuestData quest)
        {
            if (_quests.TryGetValue(quest.Id, out var runtime)) return runtime;

            runtime = new QuestRuntimeState { questId = quest.Id };
            _quests[quest.Id] = runtime;

            return runtime;
        }

        // ---- Lifecycle -------------------------------------------------------------------------

        public void ResetForNewGame()
        {
            _quests.Clear();
            _bondLevel.Clear();
            _bondProgress.Clear();

            // A fresh game still has its opening Main quest: reconciliation is what assigns it, and
            // the story ledger it reads has already been cleared by this point in the reset order.
            ReconcileMainQuests();
        }

        // ---- ISaveable -------------------------------------------------------------------------

        public string SaveKey => "quests";

        public SaveDataBase CaptureState()
        {
            var payload = new QuestSaveData { version = SaveSystemCore.CurrentSaveVersion };

            foreach (var kv in _quests)
            {
                var runtime = kv.Value;
                if (!runtime.IsKnown) continue;

                var entry = new QuestSaveEntry
                {
                    questId = runtime.questId,
                    state = runtime.state,
                    rewardsApplied = runtime.rewardsApplied,
                    objectives = new List<QuestObjectiveSaveEntry>(runtime.objectives.Count),
                };

                for (int i = 0; i < runtime.objectives.Count; i++)
                {
                    var objective = runtime.objectives[i];
                    entry.objectives.Add(new QuestObjectiveSaveEntry
                    {
                        objectiveId = objective.objectiveId,
                        currentAmount = objective.currentAmount,
                        complete = objective.complete,
                    });
                }

                payload.quests.Add(entry);
            }

            foreach (var kv in _bondProgress)
                payload.bonds.Add(new BondSaveEntry
                {
                    characterId = kv.Key,
                    level = GetBondLevel(kv.Key),
                    progress = kv.Value,
                });

            // A character can hold a level with no progress recorded (debug set, or a level granted
            // before any progress was earned), so the level map is swept for anything the loop missed.
            foreach (var kv in _bondLevel)
            {
                if (_bondProgress.ContainsKey(kv.Key)) continue;
                payload.bonds.Add(new BondSaveEntry { characterId = kv.Key, level = kv.Value, progress = 0 });
            }

            return payload;
        }

        public void RestoreState(SaveDataBase state)
        {
            if (state is not QuestSaveData payload) return;

            _quests.Clear();
            _bondLevel.Clear();
            _bondProgress.Clear();

            if (payload.quests != null)
                for (int i = 0; i < payload.quests.Count; i++)
                {
                    var entry = payload.quests[i];
                    if (string.IsNullOrEmpty(entry.questId)) continue;

                    // A quest removed from the database since the save was written is dropped rather
                    // than restored into a ledger that can never resolve it.
                    if (GetQuest(entry.questId) == null)
                    {
                        Debug.LogWarning($"[JRPG.Quest] Saved quest '{entry.questId}' is not in the database — dropped.");
                        continue;
                    }

                    var runtime = new QuestRuntimeState
                    {
                        questId = entry.questId,
                        state = entry.state,
                        rewardsApplied = entry.rewardsApplied,
                    };

                    if (entry.objectives != null)
                        for (int o = 0; o < entry.objectives.Count; o++)
                            runtime.objectives.Add(new QuestObjectiveRuntimeState
                            {
                                objectiveId = entry.objectives[o].objectiveId,
                                currentAmount = entry.objectives[o].currentAmount,
                                complete = entry.objectives[o].complete,
                            });

                    _quests[entry.questId] = runtime;
                }

            if (payload.bonds != null)
                for (int i = 0; i < payload.bonds.Count; i++)
                {
                    var entry = payload.bonds[i];
                    if (string.IsNullOrEmpty(entry.characterId)) continue;

                    _bondLevel[entry.characterId] = Mathf.Max(0, entry.level);
                    _bondProgress[entry.characterId] = Mathf.Max(0, entry.progress);
                }

            // Reconciliation runs from the GameLoaded handler rather than here: the story contributor
            // may not have restored yet, and this service must not depend on contributor order.
        }
    }
}
