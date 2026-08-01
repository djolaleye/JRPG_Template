namespace JRPG.Combat
{
    /// Builds the standard damage pipeline. Central place for the full stage list.
    ///
    /// Stages marked "placeholder" are currently inert, holding their slot in the ordering; later sub-phases
    /// swap them so ordering never has to be re-derived:
    ///   EquipmentStage → 11.4, AccuracyEvasionStage → 11.3+, ElementalStage → 11.2,
    ///   CriticalStage → 11.3+, StatusModifierStage → 11.3, PassiveModifierStage → 11.4.
    public static class DamagePipelineFactory
    {
        public static DamagePipeline CreateStandard()
        {
            return new DamagePipeline()
                .Add(new BasePowerStage())        // 1
                .Add(new AttackerStatStage())     // 2
                .Add(new EquipmentStage())        // 3  (placeholder)
                .Add(new AccuracyEvasionStage())  // 4  (placeholder)
                .Add(new DefenseStage())          // 5
                .Add(new ElementalStage())        // 6  (placeholder)
                .Add(new CriticalStage())         // 7  (placeholder)
                .Add(new StatusModifierStage())   // 8  (placeholder)
                .Add(new PassiveModifierStage())  // 9  (placeholder)
                .Add(new DifficultyStage())       // 10 (placeholder)
                .Add(new GuardStage())            // 11
                .Add(new ClampRoundStage());      // 12
        }
    }
}
