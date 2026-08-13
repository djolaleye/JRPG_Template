namespace JRPG.Core
{
    /// An encounter moved between Ready / InProgress / Complete. Carries both ends so a subscriber
    /// can tell a fresh completion from a crash-recovery reset without re-querying.
    public readonly struct EncounterStateChanged
    {
        public readonly string EncounterId;
        public readonly EncounterState Previous;
        public readonly EncounterState Next;

        public EncounterStateChanged(string encounterId, EncounterState previous, EncounterState next)
        {
            EncounterId = encounterId;
            Previous = previous;
            Next = next;
        }
    }
}
