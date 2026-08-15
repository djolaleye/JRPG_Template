using System.Collections.Generic;
using UnityEngine;
using JRPG.Characters;
using JRPG.Core;
using JRPG.Data;
using JRPG.Party;
using JRPG.Save;
using JRPG.Services;

namespace JRPG.Progression
{
    /// <summary>
    /// Owns the post-battle reward/growth flow and all progression bookkeeping. Subscribes to
    /// BattleResultPackaged, previews first, applies XP/level-ups on confirmation,
    /// tracks manual attribute points, and persists per-character progression as a save contributor.
    /// </summary>
    public sealed class ProgressionService : IProgressionService, ISaveable
    {
        private readonly DataRegistry _data;
        private readonly IEventBus _bus;
        private readonly IPartyService _party;
        private readonly IPartyRuntimeQueries _partyRuntime;
        private readonly IInventoryService _inventory;
        private readonly IDifficultyService _difficulty;
        private readonly ICurrencyService _currency;

        private readonly ProgressionEngine _engine;
        private readonly LevelUpApplier _applier = new();
        private readonly AttributePointDistributor _distributor;
        private readonly BattleRewardResolver _rewardResolver;
        private readonly SkillLearningService _skills;

        private readonly ProgressionRuntimeState _state = new();

        /// Seed for reward drop rolls, fixed so tests are repeatable.
        public int DropSeed { get; set; } = 20;

        public ProgressionService(DataRegistry data, IEventBus bus, IPartyService party,
            IPartyRuntimeQueries partyRuntime, IInventoryService inventory,
            IDifficultyService difficulty = null, ICurrencyService currency = null)
        {
            _data = data;
            _bus = bus;
            _party = party;
            _partyRuntime = partyRuntime;
            _inventory = inventory;

            // Optional: a harness that constructs progression without difficulty gets the neutral rate.
            _difficulty = difficulty;
            _currency = currency;

            _engine = new ProgressionEngine(data);
            _distributor = new AttributePointDistributor(bus);
            _rewardResolver = new BattleRewardResolver(data);
            _skills = new SkillLearningService(data);

            _bus.Subscribe<BattleResultPackaged>(OnBattleResultPackaged);
        }

        // ---- IProgressionService (lean surface) ----------------------------------------------

        public bool IsPostBattleFlowActive => _state.postBattleFlowActive;

        public bool HasPendingAttributeAllocations()
        {
            for (int i = 0; i < _state.pendingAllocations.Count; i++)
                if (_state.pendingAllocations[i].pointsRemaining > 0) return true;

            return false;
        }

        public bool CanCompletePostBattleFlow()
        {
            if (!_state.postBattleFlowActive) return true;

            return HasAppliedCurrentResult
                   && !HasPendingAttributeAllocations()
                   && !HasPendingSkillChoices();
        }

        // ---- Skill learning ------------------------------------------------------------------

        public bool HasPendingSkillChoices() => _state.pendingSkillChoices.Count > 0;

        /// <summary>Skills gained outright this flow — announcements, not decisions.</summary>
        public IReadOnlyList<LearnedSkill> LearnedSkills => _state.learnedSkills;

        /// <summary>
        /// True when the post-battle flow has anything skill-related to show: either a skill was
        /// gained outright, or one is waiting on a replacement decision. The flow controller uses this
        /// to decide whether the skill screen appears at all.
        /// </summary>
        public bool HasSkillOutcomes() => _state.learnedSkills.Count > 0 || _state.pendingSkillChoices.Count > 0;

        public PendingSkillChoice NextPendingSkillChoice()
            => _state.pendingSkillChoices.Count > 0 ? _state.pendingSkillChoices[0] : null;

        public IReadOnlyList<PendingSkillChoice> PendingSkillChoices => _state.pendingSkillChoices;

        public bool ResolveSkillChoice(string characterId, string discardSkillId)
        {
            PendingSkillChoice choice = null;
            for (int i = 0; i < _state.pendingSkillChoices.Count; i++)
                if (_state.pendingSkillChoices[i].characterId == characterId) { choice = _state.pendingSkillChoices[i]; break; }

            if (choice == null) return false;

            var inst = _partyRuntime.ResolveInstanceById(characterId);
            if (inst == null) return false;

            if (!_skills.ResolveReplacement(inst, choice.newSkillId, discardSkillId)) return false;

            _state.pendingSkillChoices.Remove(choice);
            _bus.Publish(new SkillLearned(characterId, choice.newSkillId, discardSkillId));

            return true;
        }

        /// Ensures an instance carries the skills its level entitles it to (after party rebuild/load).
        public void SeedSkills(CharacterRuntimeInstance character) => _skills.SeedSkills(character);

        // ---- Rich API (resolved concretely by the UI layer) ----------------------------------

        public BattleResultData CurrentResult => _state.lastProcessedBattleResult;
        public ResolvedRewards CurrentRewards { get; private set; }
        public ProgressionPreview CurrentPreview { get; private set; }
        public IReadOnlyList<LevelUpResult> PendingLevelUps => _state.pendingLevelUps;
        public bool HasAppliedCurrentResult { get; private set; }

        public IReadOnlyList<PendingAttributeAllocation> GetPendingAttributeAllocations() => _state.pendingAllocations;

        private void OnBattleResultPackaged(BattleResultPackaged battleResult)
        {
            if (battleResult.Outcome != BattleOutcome.Victory) return;

            if (battleResult.Result is not BattleResultData result)
            {
                Debug.LogError("[JRPG.Progression] BattleResultPackaged carried no usable result.");
                return;
            }

            BeginPostBattleFlow(result);
        }

        public void BeginPostBattleFlow(BattleResultData result)
        {
            if (_state.postBattleFlowActive)
            {
                Debug.LogWarning("[JRPG.Progression] BeginPostBattleFlow ignored — flow already active.");
                return;
            }

            _state.postBattleFlowActive = true;
            _state.lastProcessedBattleResult = result;
            _state.pendingLevelUps.Clear();
            _state.pendingAllocations.Clear();
            // Without this the next battle re-announces the skills the last one granted.
            _state.learnedSkills.Clear();
            HasAppliedCurrentResult = false;

            // Preview first, mutate on confirm
            CurrentRewards = _rewardResolver.Resolve(result, DropSeed, XpMultiplier);
            CurrentPreview = _engine.BuildPreview(result, CurrentRewards, _party, _partyRuntime);

            _bus.Publish(new PostBattleFlowStarted(result.battleId));
        }

        public ProgressionPreview PreviewBattleResult(BattleResultData result)
            => _engine.BuildPreview(result, _rewardResolver.Resolve(result, DropSeed, XpMultiplier),
                                    _party, _partyRuntime);

        /// Difficulty's XP rate, or 1 when no difficulty service is wired.
        private float XpMultiplier => _difficulty?.CurrentProfile.xpMultiplier ?? 1f;

        /// Confirms the current flow's rewards: grants drops, applies XP shares, resolves level-ups
        /// and stat growth, and queues manual attribute points.
        public void ApplyBattleResult()
        {
            if (!_state.postBattleFlowActive || _state.lastProcessedBattleResult == null)
            {
                Debug.LogWarning("[JRPG.Progression] ApplyBattleResult ignored — no active flow.");
                return;
            }

            if (HasAppliedCurrentResult) return;
            HasAppliedCurrentResult = true;

            var result = _state.lastProcessedBattleResult;
            _rewardResolver.Grant(CurrentRewards, _inventory);

            // Currency is banked here, beside the drops, and nowhere else. BattleRewardResolver stays a
            // pure function that only *reports* the amount, so the results screen can preview the payout
            // without the player being paid for looking at it.
            if (_currency != null && CurrentRewards != null && CurrentRewards.currency > 0)
                _currency.Add(CurrentRewards.currency, CurrencyChangeReason.Battle);

            foreach (var characterId in _engine.GetXpRecipients(_party))
            {
                var inst = _partyRuntime.ResolveInstanceById(characterId);
                if (inst == null) continue;

                int gained = _engine.ComputeXpShare(characterId, CurrentRewards.totalXp, result, _party);
                if (gained <= 0) continue;

                var progress = GetProgressForCharacter(characterId);
                int startingLevel = inst.level;

                inst.currentXp += gained;
                progress.currentXp = inst.currentXp;

                _bus.Publish(new XpApplied(characterId, gained, inst.currentXp));

                var levelUps = _engine.EvaluateLevelUps(characterId, startingLevel, inst.currentXp);

                for (int i = 0; i < levelUps.Count; i++)
                {
                    var lu = levelUps[i];
                    _applier.ApplyLevel(inst, lu);
                    progress.currentLevel = inst.level;
                    if (lu.pointsGranted > 0) progress.unspentAttributePoints += lu.pointsGranted;
                    _state.pendingLevelUps.Add(lu);

                    // Skills unlocked by this level. Two outcomes: learned outright, or the list was
                    // full and the player owes a decision. Both are recorded — the announcement screen
                    // needs the first, and it was previously computed and thrown away.
                    var learned = new List<string>();
                    var overflow = _skills.ApplyLevelUpLearning(inst, lu.oldLevel, lu.newLevel, learned);

                    for (int s = 0; s < learned.Count; s++)
                        _state.learnedSkills.Add(new LearnedSkill
                        {
                            characterId = characterId,
                            skillId = learned[s],
                            atLevel = lu.newLevel,
                        });

                    for (int s = 0; s < overflow.Count; s++)
                        _state.pendingSkillChoices.Add(new PendingSkillChoice
                        {
                            characterId = characterId,
                            newSkillId = overflow[s],
                            currentSkillIds = new List<string>(inst.skillIds),
                        });

                    _bus.Publish(new LevelUpOccurred(characterId, lu.oldLevel, lu.newLevel));
                }

                if (progress.unspentAttributePoints > 0)
                    _state.pendingAllocations.Add(new PendingAttributeAllocation
                    {
                        characterId = characterId,
                        pointsRemaining = progress.unspentAttributePoints,
                    });
            }
        }

        public IReadOnlyList<LevelUpResult> EvaluateLevelUps(string characterId, int startingLevel, int finalXp)
            => _engine.EvaluateLevelUps(characterId, startingLevel, finalXp);

        public bool TryAllocateAttributePoint(string characterId, StatType stat)
        {
            var inst = _partyRuntime.ResolveInstanceById(characterId);
            var progress = GetProgressForCharacter(characterId);
            if (!_distributor.TryAllocate(inst, progress, stat)) return false;

            for (int i = 0; i < _state.pendingAllocations.Count; i++)
                if (_state.pendingAllocations[i].characterId == characterId)
                    _state.pendingAllocations[i].pointsRemaining = progress.unspentAttributePoints;

            return true;
        }

        // ---- Read-only queries for progression screens -----------------------------------------

        /// <summary>
        /// Total XP the character still needs to reach their next level, using the same growth asset
        /// and <see cref="ProgressionCurveData"/> threshold table the apply path uses. Returns 0 when
        /// the character is unknown, has no growth/curve data, or is already at the curve's max level.
        /// </summary>
        public int GetXpToNextLevel(string characterId)
        {
            var inst = _partyRuntime.ResolveInstanceById(characterId);
            if (inst == null) return 0;

            var growth = _engine.FindGrowth(characterId);
            if (growth == null) return 0;
            if (!_data.TryGet<ProgressionCurveData>(growth.progressionCurveId, out var curve) || curve == null)
                return 0;

            // The curve is the authority on which level this XP total buys; the instance's own level
            // can be ahead of it (scripted/level-set characters), so take the higher of the two.
            int level = Mathf.Max(inst.level, curve.GetLevelForTotalXp(inst.currentXp));
            if (level >= curve.maxLevel) return 0;

            int remaining = curve.GetTotalXpRequiredForLevel(level + 1) - inst.currentXp;
            return remaining > 0 ? remaining : 0;
        }

        /// <summary>
        /// Non-mutating projection of spending one attribute point on <paramref name="stat"/>: what the
        /// derived final would become. Mirrors <see cref="AttributePointDistributor.ReapplyManualModifier"/>
        /// on a cloned stat block, so it uses the real modifier math rather than a re-derivation.
        /// When the allocation is blocked, <c>after</c> equals <c>before</c> and <c>blockedReason</c> says why.
        /// </summary>
        public AttributePointPreview PreviewAttributePoint(string characterId, StatType stat)
        {
            var preview = new AttributePointPreview { stat = stat };

            var inst = _partyRuntime.ResolveInstanceById(characterId);
            if (inst == null)
            {
                preview.blockedReason = "Unknown character.";
                return preview;
            }

            preview.before = inst.stats.GetFinal(stat);
            preview.after = preview.before;

            if (!AttributePointDistributor.IsAllowed(stat))
            {
                preview.blockedReason = $"{stat} can't be raised with attribute points.";
                return preview;
            }

            var progress = GetProgressForCharacter(characterId);
            if (progress.unspentAttributePoints <= 0)
            {
                preview.blockedReason = "No unspent attribute points.";
                return preview;
            }

            // One point = the accumulated permanent Flat modifier for (character, stat) rebuilt at
            // points + 1. Applied to a clone so the live instance is untouched.
            progress.manuallyAllocatedPoints.TryGetValue(stat, out int points);
            string sourceId = AttributePointDistributor.ManualSourceId(characterId, stat);

            var projection = inst.stats.Clone();
            projection.RemoveModifiersFrom(sourceId);
            projection.AddModifier(new StatModifier(stat, ModifierType.Flat, points + 1, sourceId, isPermanent: true));
            projection.Recalculate();

            preview.after = projection.GetFinal(stat);
            preview.allowed = true;
            return preview;
        }

        public void CompletePostBattleFlow()
        {
            if (!_state.postBattleFlowActive) return;
            if (!CanCompletePostBattleFlow())
            {
                Debug.LogWarning("[JRPG.Progression] CompletePostBattleFlow blocked — unresolved attribute points.");
                return;
            }

            string battleId = _state.lastProcessedBattleResult?.battleId ?? string.Empty;
            _state.postBattleFlowActive = false;
            _state.pendingAllocations.Clear();

            _bus.Publish(new ProgressionCommitted(battleId));
            _bus.Publish(new PostBattleFlowCompleted(battleId));
        }

        /// Bookkeeping entry for a character, synced from the live instance on first access.
        public CharacterProgressRuntime GetProgressForCharacter(string characterId)
        {
            if (_state.charactersById.TryGetValue(characterId, out var progress)) return progress;

            var inst = _partyRuntime.ResolveInstanceById(characterId);
            var growth = _engine.FindGrowth(characterId);

            progress = new CharacterProgressRuntime
            {
                characterId = characterId,
                currentLevel = inst?.level ?? 1,
                currentXp = inst?.currentXp ?? 0,
                levelUpMode = growth?.levelUpMode ?? LevelUpMode.FixedGrowth,
            };

            _state.charactersById[characterId] = progress;
            return progress;
        }

        /// <summary>
        /// Clears progression bookkeeping: per-character level/XP/point records, the
        /// pending level-up, attribute-allocation and skill-choice queues, and the whole post-battle
        /// flow (active flag, last result, cached rewards/preview, applied latch). That is the entire
        /// mutable surface of this service — <see cref="ProgressionRuntimeState"/> plus the three
        /// auto-properties below it — so this reproduces the constructor's condition.
        ///
        /// Does not touch the characters' live level/XP/stat modifiers. Those belong to the
        /// runtime instances, which PartyService discards when it resets. Run this <i>after</i>
        /// PartyService.ResetForNewGame so <see cref="GetProgressForCharacter"/> can no longer
        /// re-seed a record off a stale instance.
        ///
        /// Idempotent and safe to call before anything has happened.
        /// </summary>
        public void ResetForNewGame()
        {
            _state.charactersById.Clear();
            _state.pendingLevelUps.Clear();
            _state.pendingAllocations.Clear();
            _state.pendingSkillChoices.Clear();
            _state.learnedSkills.Clear();
            _state.postBattleFlowActive = false;
            _state.lastProcessedBattleResult = null;
            CurrentRewards = null;
            CurrentPreview = null;
            HasAppliedCurrentResult = false;
        }

        // ---- ISaveable ------------------------------------------------------------------------

        public string SaveKey => "progression";

        public SaveDataBase CaptureState()
        {
            var payload = new ProgressionSaveData();
            foreach (var kv in _state.charactersById)
            {
                var progress = kv.Value;
                // The live instance is the session authority for level/XP.
                var inst = _partyRuntime.ResolveInstanceById(kv.Key);
                var entry = new CharacterProgressEntry
                {
                    characterId = kv.Key,
                    level = inst?.level ?? progress.currentLevel,
                    currentXp = inst?.currentXp ?? progress.currentXp,
                    unspentAttributePoints = progress.unspentAttributePoints,
                };

                foreach (var pointKv in progress.manuallyAllocatedPoints)
                    entry.manuallyAllocatedPoints.Add(new StatPointEntry { statId = pointKv.Key.ToString(), points = pointKv.Value });

                // The chosen loadout is a player decision — level alone cannot reproduce it.
                if (inst != null) entry.skillIds.AddRange(inst.skillIds);

                payload.characters.Add(entry);
            }

            return payload;
        }

        /// Restore order: party restores roster first (instance cache resets to level 1),
        /// then re-stamps level/XP, re-applies growth + manual modifiers, and recalculates.
        public void RestoreState(SaveDataBase state)
        {
            if (state is not ProgressionSaveData payload) return;

            // A restore starts from a blank slate for exactly the same reasons a new game does,
            // so the wipe has one definition.
            ResetForNewGame();

            for (int i = 0; i < payload.characters.Count; i++)
            {
                var entry = payload.characters[i];
                var inst = _partyRuntime.ResolveInstanceById(entry.characterId);
                if (inst == null)
                {
                    Debug.LogWarning($"[JRPG.Progression] Restore: unknown character '{entry.characterId}' — skipped.");
                    continue;
                }

                var progress = new CharacterProgressRuntime
                {
                    characterId = entry.characterId,
                    currentLevel = entry.level,
                    currentXp = entry.currentXp,
                    unspentAttributePoints = entry.unspentAttributePoints,
                };
                var growth = _engine.FindGrowth(entry.characterId);
                if (growth != null) progress.levelUpMode = growth.levelUpMode;

                inst.level = entry.level;
                inst.currentXp = entry.currentXp;

                inst.skillIds.Clear();
                for (int s = 0; s < entry.skillIds.Count && !inst.IsSkillListFull; s++)
                    inst.TryLearnSkill(entry.skillIds[s]);
                    
                _skills.SeedSkills(inst);

                // Re-apply per-level growth (universal resources + fixed stats) for every level
                // reached. Runs for all modes so manual-allocation characters get their resource
                // growth back too. Uses the same source of truth as the apply path.
                for (int level = 2; level <= entry.level; level++)
                {
                    string sourceId = LevelUpApplier.GrowthSourceId(entry.characterId, level);
                    inst.stats.RemoveModifiersFrom(sourceId);
                    foreach (var ge in _engine.GetLevelStatIncreases(growth, level))
                        inst.stats.AddModifier(new StatModifier(ge.stat, ModifierType.Flat, ge.value, sourceId, isPermanent: true));
                }

                // Re-apply manual allocations.
                for (int p = 0; p < entry.manuallyAllocatedPoints.Count; p++)
                {
                    var pe = entry.manuallyAllocatedPoints[p];
                    if (!System.Enum.TryParse<StatType>(pe.statId, out var stat))
                    {
                        Debug.LogWarning($"[JRPG.Progression] Restore: unknown stat '{pe.statId}' for '{entry.characterId}'.");
                        continue;
                    }
                    progress.manuallyAllocatedPoints[stat] = pe.points;
                    AttributePointDistributor.ReapplyManualModifier(inst, entry.characterId, stat, pe.points);
                }

                inst.stats.Recalculate();
                inst.currentHP = inst.stats.GetFinal(StatType.MaxHP);
                inst.currentMP = inst.stats.GetFinal(StatType.MaxMP);
                inst.currentSP = inst.stats.GetFinal(StatType.MaxSP);
                inst.Recalculate();

                _state.charactersById[entry.characterId] = progress;
            }
        }
    }

    /// <summary>
    /// Before/after projection for spending one manual attribute point, produced by
    /// <see cref="ProgressionService.PreviewAttributePoint"/>. Purely a view model — nothing reads it back.
    /// </summary>
    public struct AttributePointPreview
    {
        public StatType stat;
        /// Current derived final for <see cref="stat"/>.
        public int before;
        /// Projected derived final after one point. Equals <see cref="before"/> when not allowed.
        public int after;
        /// True when a point could actually be spent on this stat right now.
        public bool allowed;
        /// Player-facing explanation when <see cref="allowed"/> is false; null otherwise.
        public string blockedReason;
    }
}
