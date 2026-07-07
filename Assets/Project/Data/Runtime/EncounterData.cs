using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Data
{
    /// Static configuration for a battle. It never stores defeated enemies, selected actions,
    /// current HP, or battle results — those are battle-local runtime state.
    [CreateAssetMenu(menuName = "JRPG/Combat/Encounter", fileName = "Encounter")]
    public class EncounterData : GameDataBase
    {
        public List<string> enemyIds = new();
        public string battleSceneId;
        public bool escapable;
    }
}
