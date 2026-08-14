using System.Collections.Generic;
using JRPG.Core;

namespace JRPG.Party
{
    /// Runtime form of a party restriction. PartyScopeData (in JRPG.Data) is the template;
    /// what's pushed onto the scope stack is this mutable runtime value.
    public sealed class PartyScope
    {
        public string scopeId;
        public List<string> allowedCharacterIds = new();
        public List<string> requiredCharacterIds = new();
        public List<string> lockedCharacterIds = new();
        /// Defaults to the unrestricted cap; a scope authored below it is the point of a scope.
        public int maxActiveMembers = PartyRules.MaxActiveMembers;

        public PartyScope() { }

        public PartyScope(string scopeId,
                          IReadOnlyList<string> allowed,
                          IReadOnlyList<string> required,
                          IReadOnlyList<string> locked,
                          int maxActive)
        {
            this.scopeId = scopeId;
            if (allowed != null) allowedCharacterIds.AddRange(allowed);
            if (required != null) requiredCharacterIds.AddRange(required);
            if (locked != null) lockedCharacterIds.AddRange(locked);
            maxActiveMembers = maxActive;
        }
    }
}
