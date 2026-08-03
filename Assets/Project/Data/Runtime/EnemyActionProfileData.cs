using System;
using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Data
{

    public enum AiConditionType
    {
        Always,
        SelfHpBelowPercent,
        SelfHpAbovePercent,
        AnyEnemyHpBelowPercent,
        RoundAtLeast,
        AllyCountAtLeast,
        SelfHasStatus,
        TargetHasStatus,
        TargetMissingStatus,
        SelfMissingStatus
    }


    public enum AiTargetRule
    {
        FirstLiving,
        LowestHp,
        HighestHp,
        Random,
        LastAttacker,
        SpecificEnemy,
        LowestHpAlly,
        TargetWeakness,
        AvoidResistance,
        StrongestAlly,
        Self
    }

    [Serializable]
    public struct AiCondition
    {
        public AiConditionType type;
        [Range(0f, 1f)] public float value;
        public int intValue;
        public string stringValue;
    }

    /// One candidate action the AI may take.
    [Serializable]
    public class AiActionEntry
    {
        public string actionId;

        [Tooltip("Relative likelihood among eligible, same-priority entries.")]
        [Min(0)] public int weight = 1;

        [Tooltip("Higher priority wins outright.")]
        public int priority;

        [Tooltip("All conditions must pass for this entry to be eligible.")]
        public List<AiCondition> conditions = new();

        public AiTargetRule targetRule = AiTargetRule.FirstLiving;

        [Tooltip("SpecificEnemy rule only: who to hit. Matched against the combatant id, the encounter " +
                 "slot id, or the source data id.")]
        public string specificTargetId;

        [Tooltip("Turns before this entry may be chosen again.")]
        [Min(0)] public int cooldownTurns;
    }

    /// A behaviour set that becomes active at or below an HP fraction.
    [Serializable]
    public class AiPhase
    {
        public string phaseId = "phase";

        [Tooltip("Active while the owner's HP fraction is at or below this (1 = always eligible). " +
                 "The lowest matching threshold wins, so order does not matter.")]
        [Range(0f, 1f)] public float hpThresholdPercent = 1f;

        [Tooltip("Optional trigger fired once when this phase becomes active.")]
        public string onEnterStoryFlag;

        public List<AiActionEntry> actions = new();
    }

    /// Authored enemy AI. Entries are filtered by condition,
    /// resolved by priority, then chosen by weight.
    [CreateAssetMenu(menuName = "JRPG/Combat/Enemy Action Profile", fileName = "EnemyActionProfile")]
    public class EnemyActionProfileData : GameDataBase
    {
        [Header("Scripted opening")]
        [Tooltip("Action ids used in order on the first turns, before normal selection begins. " +
                 "Boss choreography.")]
        public List<string> scriptedSequence = new();

        [Tooltip("Repeat the scripted sequence forever instead of falling through to the phases.")]
        public bool loopScriptedSequence;

        [Header("Phases (lowest matching HP threshold wins)")]
        public List<AiPhase> phases = new();

        [Header("Fallback")]
        [Tooltip("Used when nothing else is eligible or affordable.")]
        public string fallbackActionId = "attack_melee";

        [Tooltip("Optional per-profile RNG seed. 0 uses the battle's shared deterministic RNG.")]
        public int randomSeed;

        /// The phase governing an owner at the given HP fraction.
        public AiPhase ResolvePhase(float hpFraction)
        {
            AiPhase best = null;

            for (int i = 0; i < phases.Count; i++)
            {
                var p = phases[i];

                if (p == null || hpFraction > p.hpThresholdPercent) continue;
                if (best == null || p.hpThresholdPercent < best.hpThresholdPercent) best = p;
            }

            return best;
        }
    }
}
