using System.Collections.Generic;
using UnityEngine;
using JRPG.Characters;
using JRPG.Core;
using JRPG.Data;
using JRPG.Party;
using JRPG.Services;

namespace JRPG.Progression
{
    /// XP distribution, level threshold crossings, and previews.
    public sealed class ProgressionEngine
    {
        // XP distribution rules (doc §8.7).
        public const float ActiveSurvivorShare = 1.0f;
        public const float ActiveDefeatedShare = 0.0f;
        public const float ReserveShare = 0.5f;
        public const float GuestShare = 0.0f;

        // Universal per-level resource growth. Applied to every character on every level-up,
        // regardless of level-up mode or growth curve, and independently of the fixed stat table.
        public const int AutoMaxHpPerLevel = 10;
        public const int AutoMaxMpPerLevel = 5;
        public const int AutoMaxSpPerLevel = 5;

        private readonly DataRegistry _data;

        public ProgressionEngine(DataRegistry data)
        {
            _data = data;
        }

        /// XP share for one character given the battle result and their roster state.
        public int ComputeXpShare(string characterId, int totalXp, BattleResultData result, IPartyService party)
        {
            var state = party.GetState(characterId);

            switch (state)
            {
                case CharacterRosterState.Active:
                    bool survived = result.survivingPartyCharacterIds.Contains(characterId);
                    return Mathf.RoundToInt(totalXp * (survived ? ActiveSurvivorShare : ActiveDefeatedShare));
                case CharacterRosterState.Reserve:
                    return Mathf.RoundToInt(totalXp * ReserveShare);
                case CharacterRosterState.Guest:
                    return Mathf.RoundToInt(totalXp * GuestShare);
                default:
                    return 0;
            }
        }

        /// All roster members eligible for an XP share
        public List<string> GetXpRecipients(IPartyService party)
        {
            var ids = new List<string>();

            foreach (var id in party.GetActivePartyIds()) ids.Add(id);
            foreach (var id in party.GetReservePartyIds()) if (!ids.Contains(id)) ids.Add(id);

            return ids;
        }

        /// Level-ups produced by moving a character from startingLevel to the level implied by
        /// finalXp on their growth curve. Empty when no threshold is crossed or data is missing.
        public List<LevelUpResult> EvaluateLevelUps(string characterId, int startingLevel, int finalXp)
        {
            var results = new List<LevelUpResult>();
            var growth = FindGrowth(characterId);
            if (growth == null) return results;

            if (!_data.TryGet<ProgressionCurveData>(growth.progressionCurveId, out var curve))
            {
                Debug.LogWarning($"[JRPG.Progression] Growth '{growth.Id}' references missing curve '{growth.progressionCurveId}'.");
                return results;
            }

            if (growth.levelUpMode == LevelUpMode.RandomVariance)
            {
                Debug.LogWarning($"[JRPG.Progression] '{growth.Id}': RandomVariance not implemented.");
                return results;
            }

            int finalLevel = curve.GetLevelForTotalXp(finalXp);
            for (int level = startingLevel + 1; level <= finalLevel; level++)
            {
                var res = new LevelUpResult
                {
                    characterId = characterId,
                    oldLevel = level - 1,
                    newLevel = level,
                    pointsGranted = UsesManualPoints(growth.levelUpMode) ? growth.attributePointsPerLevel : 0,
                };

                res.statIncreases.AddRange(GetLevelStatIncreases(growth, level));
                results.Add(res);
            }
            return results;
        }

        /// Stat increases granted when reaching <paramref name="level"/>: the universal resource
        /// growth (always, for every character) plus the growth asset's fixed non-resource stat
        /// entries (FixedGrowth/Hybrid only). Resource stats in the fixed table are ignored so
        /// MaxHP/MP/SP are grown exactly once, independently of the stat table. Shared by the apply
        /// path (EvaluateLevelUps) and the save-restore path so both stay in lock-step.
        public List<StatGrowthEntry> GetLevelStatIncreases(CharacterGrowthData growth, int level)
        {
            var list = new List<StatGrowthEntry>
            {
                new() { level = level, stat = StatType.MaxHP, value = AutoMaxHpPerLevel },
                new() { level = level, stat = StatType.MaxMP, value = AutoMaxMpPerLevel },
                new() { level = level, stat = StatType.MaxSP, value = AutoMaxSpPerLevel },
            };

            if (growth != null && UsesFixedGrowth(growth.levelUpMode))
            {
                for (int i = 0; i < growth.fixedGrowthPerLevel.Count; i++)
                {
                    var e = growth.fixedGrowthPerLevel[i];
                    if (e.level == level && !IsResourceStat(e.stat)) list.Add(e);
                }
            }
            return list;
        }

        public static bool IsResourceStat(StatType stat)
            => stat == StatType.MaxHP || stat == StatType.MaxMP || stat == StatType.MaxSP;

        /// Non-mutating preview of applying `rewards.totalXp` across the current roster.
        public ProgressionPreview BuildPreview(BattleResultData result, ResolvedRewards rewards,
            IPartyService party, IPartyRuntimeQueries partyRuntime)
        {
            var preview = new ProgressionPreview { battleId = result.battleId, totalXp = rewards.totalXp };

            foreach (var id in GetXpRecipients(party))
            {
                var inst = partyRuntime.ResolveInstanceById(id);
                if (inst == null) continue;

                int gained = ComputeXpShare(id, rewards.totalXp, result, party);
                int projected = inst.level;
                var growth = FindGrowth(id);

                if (growth != null && _data.TryGet<ProgressionCurveData>(growth.progressionCurveId, out var curve))
                    projected = Mathf.Max(inst.level, curve.GetLevelForTotalXp(inst.currentXp + gained));

                preview.characters.Add(new CharacterXpPreview
                {
                    characterId = id,
                    displayName = _data.TryGet<CharacterData>(id, out var cd) ? cd.displayName : id,
                    currentLevel = inst.level,
                    currentXp = inst.currentXp,
                    xpGained = gained,
                    projectedLevel = projected,
                });
            }
            return preview;
        }


        public CharacterGrowthData FindGrowth(string characterId)
        {
            if (_data.TryGet<CharacterData>(characterId, out var cd)
                && !string.IsNullOrEmpty(cd.growthDataId)
                && _data.TryGet<CharacterGrowthData>(cd.growthDataId, out var byId))
                return byId;

            foreach (var kv in _data.CharacterGrowthById)
                if (kv.Value.characterId == characterId) return kv.Value;
                
            return null;
        }

        public static bool UsesFixedGrowth(LevelUpMode mode)
            => mode == LevelUpMode.FixedGrowth || mode == LevelUpMode.Hybrid;

        public static bool UsesManualPoints(LevelUpMode mode)
            => mode == LevelUpMode.ManualAllocation || mode == LevelUpMode.Hybrid;
    }
}
