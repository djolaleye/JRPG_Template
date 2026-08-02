using JRPG.Data;

namespace JRPG.Combat
{
    /// One active status on one combatant.
    public sealed class StatusEffectInstance
    {
        public string statusId;
        public string applierCombatantId;
        public string modifierSourceId;

        public StatusDurationType durationType;
        public int remaining;
        public int stacks = 1;

        public bool IsExpired =>
            durationType != StatusDurationType.WholeBattle &&
            durationType != StatusDurationType.UntilRemoved &&
            remaining <= 0;

        /// Deterministic tag so the same status from the same holder always reverses cleanly.
        public static string MakeSourceId(string combatantId, string statusId)
            => $"status:{combatantId}:{statusId}";
    }
}
