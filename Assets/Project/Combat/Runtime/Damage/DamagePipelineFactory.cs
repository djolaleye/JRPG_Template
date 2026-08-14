using JRPG.Data;

namespace JRPG.Combat
{
    /// Builds the standard damage pipeline. Central place for the full stage list.
    ///
    /// Every stage is live except <see cref="DifficultyStage"/>, which is inert and holds its slot in
    /// the ordering so a difficulty multiplier can be dropped in without renumbering the rest.
    /// Some stages also go inert when their collaborator is null — see the parameter docs below.
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
        public static DamagePipeline CreateStandard(ElementInteractionMatrix elementMatrix = null,
            StatusProcessor status = null, CombatTuning tuning = null, PassiveRegistry passives = null)
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
                .Add(new DifficultyStage())                         // 9 (placeholder)
                .Add(new VarianceStage(tuning))                     // 9b
                .Add(new GuardStage())                              // 10
                .Add(new ClampRoundStage());                        // 11
        }
    }
}
