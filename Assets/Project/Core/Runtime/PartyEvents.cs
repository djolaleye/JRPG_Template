namespace JRPG.Core
{
    public readonly struct CharacterRecruited
    {
        public readonly string Id;
        public CharacterRecruited(string id) { Id = id; }
    }

    /// A single character's roster state changed, with the old and new values. Fired alongside the
    /// coarse <see cref="PartyChanged"/> so subscribers that care about a specific transition
    /// (recruitment UI, tutorials, quest triggers) don't have to diff the whole roster.
    public readonly struct CharacterRosterStateChanged
    {
        public readonly string Id;
        public readonly CharacterRosterState Previous;
        public readonly CharacterRosterState Next;
        public CharacterRosterStateChanged(string id, CharacterRosterState previous, CharacterRosterState next)
        {
            Id = id;
            Previous = previous;
            Next = next;
        }
    }

    public readonly struct PartyChanged
    {
        // Empty for now; subscribers query the party service for the current arrangement.
    }

    public readonly struct PartyScopeChanged
    {
        public readonly string ScopeId; // null/empty when scope was popped (back to default).
        public PartyScopeChanged(string scopeId) { ScopeId = scopeId; }
    }
}
