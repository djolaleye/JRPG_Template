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

        private readonly ProgressionEngine _engine;
        private readonly LevelUpApplier _applier = new();
        private readonly AttributePointDistributor _distributor;
        private readonly BattleRewardResolver _rewardResolver;

        private readonly ProgressionRuntimeState _state = new();

        /// Seed for reward drop rolls, fixed so tests are repeatable.
        public int DropSeed { get; set; } = 20;

        public ProgressionService(DataRegistry data, IEventBus bus, IPartyService party,
            IPartyRuntimeQueries partyRuntime, IInventoryService inventory)
        {
            _data = data;
            _bus = bus;
            _party = party;
            _partyRuntime = partyRuntime;
            _inventory = inventory;

            _engine = new ProgressionEngine(data);
            _distributor = new AttributePointDistributor(bus);
            _rewardResolver = new BattleRewardResolver(data);

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
            
            return HasAppliedCurrentResult && !HasPendingAttributeAllocations();
        }

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
            HasAppliedCurrentResult = false;

            // Preview first, mutate on confirm
            CurrentRewards = _rewardResolver.Resolve(result, DropSeed);
            CurrentPreview = _engine.BuildPreview(result, CurrentRewards, _party, _partyRuntime);

            _bus.Publish(new PostBattleFlowStarted(result.battleId));
        }

        public ProgressionPreview PreviewBattleResult(BattleResultData result)
            => _engine.BuildPreview(result, _rewardResolver.Resolve(result, DropSeed), _party, _partyRuntime);

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
                
                payload.characters.Add(entry);
            }

            return payload;
        }

        /// Restore order: party restores roster first (instance cache resets to level 1),
        /// then re-stamps level/XP, re-applies growth + manual modifiers, and recalculates.
        public void RestoreState(SaveDataBase state)
        {
            if (state is not ProgressionSaveData payload) return;

            _state.charactersById.Clear();
            _state.pendingLevelUps.Clear();
            _state.pendingAllocations.Clear();
            _state.postBattleFlowActive = false;
            _state.lastProcessedBattleResult = null;
            CurrentRewards = null;
            CurrentPreview = null;
            HasAppliedCurrentResult = false;

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
}
