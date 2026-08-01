namespace JRPG.Combat
{
    /// One ordered step of the damage calculation. Stages are small strategy objects so the pipeline can
    /// be reordered, extended, or replaced without touching action execution.
    public interface IDamageStage
    {
        string Name { get; } /// Used by the audit log to explain a damage number.

        void Apply(DamageContext ctx);
    }
}
