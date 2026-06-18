namespace JRPG.Core
{
    public readonly struct CharacterRecruited
    {
        public readonly string Id;
        public CharacterRecruited(string id) { Id = id; }
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
