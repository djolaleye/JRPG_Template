using JRPG.Data;

namespace JRPG.Combat
{
    /// Builds the standard damage pipeline. Central place for the full stage list.
    ///
    /// Every stage is live. Several go inert when their collaborator is null — see the parameter docs
    /// below — which is how a harness builds a pipeline without statuses, passives or difficulty.
    public static class DamagePipelineFactory
    {
        /// <param name="elementMatrix">
        /// Elemental table (DataRegistry.ElementMatrix). When null the elemental slot stays a
        /// no-op, so combat still runs on a database with no matrix authored.
        /// </param>
        /// <param name="status">
        /// Status authority driving accuracy/crit/damage modifiers. When null those stages are inert.
        /// </param>
        /// <param name="tuning">Stat-contribution knobs; defaults are zero-impact (see CombatTuning).</param>
        /// <param name="passives">Runtime passive registry; when null the passive stage is inert.</param>
        /// <param name="difficulty">Difficulty tuning; when null the difficulty stage is inert.</param>
        public static DamagePipeline CreateStandard(ElementInteractionMatrix elementMatrix = null,
            StatusProcessor status = null, CombatTuning tuning = null, PassiveRegistry passives = null,
            JRPG.Services.IDifficultyService difficulty = null)
        {
            tuning ??= CombatTuning.Default;

            return new DamagePipeline()
                .Add(new BasePowerStage())                          // 1
                .Add(new AttackerStatStage())                       // 2
                .Add(new WeaponSlotStage())                         // 2b
                .Add(new AccuracyEvasionStage(status, tuning))      // 3
                .Add(new DefenseStage())                            // 4
                .Add(new ElementalStage(elementMatrix))             // 5
                .Add(new CriticalStage(status, tuning))             // 6
                .Add(new StatusModifierStage(status))               // 7
                .Add(new PassiveModifierStage(passives))            // 8
                .Add(new DifficultyStage(difficulty))               // 9
                .Add(new VarianceStage(tuning))                     // 9b
                .Add(new GuardStage())                              // 10
                .Add(new ClampRoundStage());                        // 11
        }
    }
}
