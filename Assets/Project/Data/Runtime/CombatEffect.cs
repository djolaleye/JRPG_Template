using System;
using UnityEngine;

namespace JRPG.Data
{
    public enum CombatActionCategory
    {
        Melee,
        Guard,
        Item,
        Skill,
        Flee,
        Ranged
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

        [Header("Status / Buff / Debuff")]
        [Tooltip("StatusEffectData id. For RemoveStatus, leave empty to dispel by category instead.")]
        public string statusId;

        [Tooltip("RemoveStatus only: when statusId is empty, every status in this category is removed.")]
        public StatusDispelCategory dispelCategory;

        [Header("Resource Change")]
        [Tooltip("Instantaneous change — a lingering drain/regen should be authored as a status " +
                 "with a tickResource instead.")]
        public CombatResource resourceType;
        public int resourceDelta;

        [Header("Dialogue / Story Flag")]
        public string stringArg;
        public bool boolArg;
    }
}
