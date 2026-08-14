using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;

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
        [Tooltip("Cap while this scope is pushed. Defaults to the unrestricted cap; author it lower " +
                 "to restrict the party for a story section.")]
        public int maxActiveMembers = PartyRules.MaxActiveMembers;
    }
}
