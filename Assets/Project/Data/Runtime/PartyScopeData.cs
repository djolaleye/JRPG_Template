using System.Collections.Generic;
using UnityEngine;

namespace JRPG.Data
{
    /// Template for temporary party restriction 
    /// Instantiated at runtime into a PartyScope value object that lives on the
    /// party scope stack
    [CreateAssetMenu(menuName = "JRPG/Party Scope Data", fileName = "PartyScopeData")]
    public class PartyScopeData : ScriptableObject
    {
        public string scopeId;
        public List<string> allowedCharacterIds = new();
        public List<string> requiredCharacterIds = new();
        public List<string> lockedCharacterIds = new();
        public int maxActiveMembers = 4;
    }
}
