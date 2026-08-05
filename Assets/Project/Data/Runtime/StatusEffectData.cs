using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Data
{
    public enum StatusDurationType
    {
        Turns,        // decremented on the afflicted combatant's own turn end
        Rounds,       // decremented at round end
        WholeBattle,  // lasts until the battle ends
        UntilRemoved  // only a dispel/removal effect clears it
    }

    public enum StatusStackRule
    {
        Ignore,   // keep the existing instance untouched
        Refresh,  // reset the remaining duration
        Stack,    // add a stack (up to maxStacks) and refresh
        Replace   // discard the old instance and apply fresh
    }

    public enum StatusDispelCategory
    {
        None,
        Buff,
        Debuff,
        Ailment,
        Curse
    }

    public enum StatusActionRestriction
    {
        None,
        AllActions,   // stun/sleep/freeze
        PhysicalOnly, // may not use physical actions
        MagicOnly,    // silence — may not use skills
        ItemsOnly
    }

    /// When a status does its work.
    [System.Flags]
    public enum StatusTiming
    {
        None        = 0,
        OnApply     = 1 << 0,
        TurnStart   = 1 << 1,
        BeforeAction= 1 << 2,
        AfterAction = 1 << 3,
        TurnEnd     = 1 << 4,
        RoundEnd    = 1 << 5,
        OnDamage    = 1 << 6,
        OnBattleEnd = 1 << 7
    }

    /// Authored status effect. Runtime state lives on
    /// StatusEffectInstance attached to a combatant.
    [CreateAssetMenu(menuName = "JRPG/Combat/Status Effect", fileName = "StatusEffect")]
    public class StatusEffectData : GameDataBase
    {
        [Header("Duration & stacking")]
        public StatusDurationType durationType = StatusDurationType.Turns;
        [Min(0)] public int defaultDuration = 3;
        public StatusStackRule stackRule = StatusStackRule.Ignore;
        [Min(1)] public int maxStacks = 1;
        public StatusDispelCategory dispelCategory = StatusDispelCategory.Ailment;

        [Header("Timing")]
        [Tooltip("Points where this status is processed.")]
        public StatusTiming timing = StatusTiming.TurnEnd;

        [Header("Stat modifiers")]
        [Tooltip("Applied to the afflicted combatant's stat block, tagged status:{combatantId}:{statusId}.")]
        public List<StatModifier> statModifiers = new();

        [Header("Periodic tick")]
        [Tooltip("Damage dealt (positive) or healing done (negative) each time the status ticks.")]
        public int tickAmount;
        [Tooltip("Fraction of the target's MaxHP dealt/healed per tick, added to tickAmount.")]
        [Range(0f, 1f)] public float tickPercentOfMax;
        public CombatResource tickResource = CombatResource.HP;
        public Element tickElement = Element.Neutral;

        [Header("Combat modifiers")]
        public float damageDealtMultiplier = 1f;
        public float damageTakenMultiplier = 1f;
        public float accuracyModifier;
        public float evasionModifier;
        public float critChanceModifier;

        [Header("Restrictions & removal")]
        public StatusActionRestriction actionRestriction = StatusActionRestriction.None;
        [Tooltip("Cleared as soon as the afflicted combatant takes damage.")]
        public bool removeOnDamage;

        [Header("Presentation")]
        [Tooltip("Passive line shown when this status costs the combatant their entire turn (i.e. " +
                 "actionRestriction is AllActions). {0} is the combatant's display name, e.g. " +
                 "\"{0} is stunned!\". Empty falls back to the generic restriction reason.")]
        public string blockedTurnMessage;
        public string indicatorIconId;
        [Tooltip("Hex colour a HUD can tint the indicator with.")]
        public string indicatorColorHex = "#FFFFFF";

        public bool HasTiming(StatusTiming t) => (timing & t) != 0;
    }
}
