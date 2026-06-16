using System.Collections.Generic;
using UnityEngine;

namespace JRPG.Data
{
    [CreateAssetMenu(menuName = "JRPG/Game Database", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        public List<CharacterData> characters = new();
        public List<EnemyData> enemies = new();

        // Later phases:
        // public List<ItemData> items;
        // public List<EquipmentData> equipment;
        // public List<CombatActionData> combatActions;
        // public List<StatusEffectData> statuses;
        // public List<DialogueGraph> dialogueGraphs;
        // public List<EncounterData> encounters;
    }
}
