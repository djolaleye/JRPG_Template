using System;
using UnityEngine;

namespace JRPG.Data
{
    public enum TargetTeam
    {
        Self,
        Allies,
        Enemies,
        All
    }

    /// How many of the eligible candidates an action hits, and who picks them.
    public enum TargetSelectionMode
    {
        Self,
        Single,
        All,
        Random
    }

    /// Describes which combatants an action may target.
    [Serializable]
    public class TargetRule
    {
        public TargetTeam team;
        public TargetSelectionMode selectionMode;

        [Tooltip("Random mode: how many distinct targets to pick.")]
        [Min(1)] public int maxTargets = 1;

        [Header("Filters")]
        [Tooltip("Exclude defeated combatants.")]
        public bool requireLiving = true;

        [Tooltip("Only defeated combatants are eligible.")]
        public bool requireDefeated;

        public bool allowSelf = true;

        [Header("Conditional restrictions")]
        [Tooltip("Only combatants currently afflicted with this status id are eligible.")]
        public string requiredStatusId;

        [Tooltip("Only combatants at or below this fraction of MaxHP are eligible.")]
        [Range(0f, 1f)] public float maxHpFraction;

        /// True when the engine resolves the whole target set itself, so the UI must not ask the
        /// player to pick one.
        public bool IsAutoResolved =>
            selectionMode == TargetSelectionMode.Self
            || selectionMode == TargetSelectionMode.All
            || selectionMode == TargetSelectionMode.Random;
    }
}
