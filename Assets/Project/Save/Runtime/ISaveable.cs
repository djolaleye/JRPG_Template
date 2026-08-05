using JRPG.Core;

namespace JRPG.Save
{
    public interface ISaveable
    {
        string SaveKey { get; }
        SaveDataBase CaptureState();
        void RestoreState(SaveDataBase state);
    }

    /// <summary>
    /// Optional companion to <see cref="ISaveable"/> for state that can only be applied once
    /// every contributor has restored.
    ///
    /// <para><b>Why this exists.</b> Restore is a rebuild: PartyService drops its runtime
    /// instances and they are recreated at level 1, 
    /// then ProgressionService re-stamps level/XP and replays growth modifiers on top.
    /// Anything written to an instance before progression runs is therefore either clamped to the level-1 maximum or overwritten outright — which is exactly what
    /// happened to current HP/MP/SP. Contributors that own such state stash it during
    /// <see cref="ISaveable.RestoreState"/> and apply it here, after the graph has settled.</para>
    ///
    /// <para>Ordering between multiple post-restore contributors is deliberately not defined: this hook
    /// is for state nothing else touches. Anything with a genuine inter-contributor dependency belongs
    /// in the restore order itself.</para>
    /// </summary>
    public interface ISaveablePostRestore
    {
        void PostRestore();
    }
}
