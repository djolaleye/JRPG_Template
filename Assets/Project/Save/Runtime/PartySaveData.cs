using System;
using System.Collections.Generic;
using JRPG.Core;

namespace JRPG.Save
{
    [Serializable]
    public struct RosterEntry
    {
        public string id;
        public CharacterRosterState state;
    }

    [Serializable]
    public class ScopeSnapshotDto
    {
        public string scopeId;
        public List<string> allowedCharacterIds = new();
        public List<string> requiredCharacterIds = new();
        public List<string> lockedCharacterIds = new();
        public int scopeMaxActive;
        public List<string> savedActiveOrder = new();
        public List<string> savedReserveOrder = new();
        public List<RosterEntry> savedRoster = new();
        public List<string> savedLockedIds = new();
        public int savedMaxActive;
    }

    [Serializable]
    public class PartySaveData : SaveDataBase
    {
        public List<RosterEntry> roster = new();
        public List<string> activeOrder = new();
        public List<string> reserveOrder = new();
        public List<string> lockedIds = new();
        public int maxActiveMembers;
        public int maxTotalParty;
        public List<ScopeSnapshotDto> scopeStack = new(); // bottom-first
    }

    /// <summary>
    /// Wire format wrapper for the "party" SaveKey.
    /// </summary>
    [Serializable]
    public sealed class PartyPayload : SaveDataBase
    {
        public PartySaveData data = new();
    }
}
