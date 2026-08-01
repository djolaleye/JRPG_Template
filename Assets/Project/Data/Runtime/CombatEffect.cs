using System;
using UnityEngine;

namespace JRPG.Data
{
    public enum CombatActionCategory
    {
        Melee,
        Guard,
        Item,
        Skill
    }

    public enum CombatEffectType
    {
        Damage,
        Heal,
        Guard,
        // Phase 11 (appended — existing serialized indices above must not shift):
        ApplyStatus,
        RemoveStatus,
        BuffStat,
        DebuffStat,
        ResourceChange,
        Revive,
        EscapeAttempt,
        ForcedTargetChange,
        TriggerDialogue,
        SetBattleFlag
    }

    /// Plain effect data interpreted by the combat resolver. The fields form a small union — the
    /// resolver reads only those relevant to <see cref="type"/>.
    [Serializable]
    public struct CombatEffect
    {
        public CombatEffectType type;

        // Damage / Heal shared.
        public int basePower;
        public float statScale;

        // Damage.
        public StatType attackStat;
        public StatType defenseStat;

        // Heal.
        public StatType scalingStat;

        // Guard.
        public float guardMultiplier;

        // ---- Phase 11 (appended; each effect type reads only its own fields) ----

        /// Damage: overrides the action's element when set to anything other than Physical/Neutral
        /// defaults are desired. Resolved by the elemental pipeline stage.
        public Element element;

        /// Probability this effect lands at all (0..1). <= 0 is treated as 1 (always) so existing
        /// authored effects keep firing.
        [Range(0f, 1f)] public float chance;

        // ApplyStatus / RemoveStatus.
        public string statusId;
        public int duration;
        public int stacks;

        // BuffStat / DebuffStat.
        public StatType buffStat;
        public ModifierType buffModifierType;
        public float buffValue;

        // ResourceChange.
        public CombatResource resourceType;
        public int resourceDelta;

        // TriggerDialogue / SetBattleFlag.
        public string stringArg;
        public bool boolArg;
    }
}
