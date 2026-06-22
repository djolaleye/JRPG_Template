using System;

namespace JRPG.Data
{
    public enum TargetTeam
    {
        Self,
        Allies,
        Enemies
    }

    public enum TargetSelectionMode
    {
        Self,
        Single
    }

    /// Describes which combatants an action may target. The targeting system returns candidates from
    /// this rule; it never applies effects or mutates combat state.
    [Serializable]
    public class TargetRule
    {
        public TargetTeam team;
        public TargetSelectionMode selectionMode;
        public bool requireLiving = true;
        public bool allowSelf = true;
    }
}
