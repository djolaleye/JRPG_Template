using System.Collections.Generic;
using JRPG.Core;

namespace JRPG.Party
{
    /// <summary>
    /// Authority for membership. Holds IDs only; runtime instances are resolved through the character factory + data registry by id.
    /// </summary>
    public sealed class PartyRuntimeState
    {
        public readonly Dictionary<string, CharacterRosterState> stateByCharacterId = new();
        public readonly List<string> activeOrder = new();
        public readonly List<string> reserveOrder = new();

        /// <summary>
        /// Locked characters cannot be moved between roster states until unlocked.
        /// </summary>
        public readonly HashSet<string> lockedIds = new();

        public int maxActiveMembers = 3;
        public int maxTotalParty = 8;

        public readonly Stack<ScopeSnapshot> scopeStack = new();
    }

    /// Snapshot captured when a PartyScope is pushed; restored on pop.
    public sealed class ScopeSnapshot
    {
        public PartyScope scope;
        public List<string> savedActiveOrder = new();
        public List<string> savedReserveOrder = new();
        public Dictionary<string, CharacterRosterState> savedStates = new();
        public HashSet<string> savedLockedIds = new();
        public int savedMaxActive;
    }
}
