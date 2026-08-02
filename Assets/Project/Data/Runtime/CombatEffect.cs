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

        [Header("Damage / Heal")] 
        public int basePower;
        public float statScale;

        [Header("Damage")]
        public StatType attackStat;
        public StatType defenseStat;
        public Element element;

        [Header("Heal")]
        public StatType scalingStat;

        [Header("Guard")]
        public float guardMultiplier;

        [Header("Accuracy")]
        [Range(0f, 1f)] public float executionChance;
        [Range(0f, 1f)] public float hitChance;

        [Header("Status")]
        public string statusId;
        public int duration;
        public int stacks;

        [Header("Buff / Debuff")]
        public StatType buffStat;
        public ModifierType buffModifierType;
        public float buffValue;

        [Header("Resource Change")]
        public CombatResource resourceType;
        public int resourceDelta;

        [Header("Dialogue / Story Flag")]
        public string stringArg;
        public bool boolArg;
    }
}
