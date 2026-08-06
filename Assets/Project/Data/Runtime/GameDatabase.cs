using System.Collections.Generic;
using UnityEngine;

namespace JRPG.Data
{
    [CreateAssetMenu(menuName = "JRPG/Game Database", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        public List<CharacterData> characters = new();
        public List<EnemyData> enemies = new();

        /// <summary>
        /// Items and equipment share one list — EquipmentData : ItemData, so polymorphic lookups
        /// resolve through the same dictionary in DataRegistry.
        /// </summary>
        public List<ItemData> items = new();

        public List<CombatActionData> combatActions = new();
        public List<EncounterData> encounters = new();

        public List<ProgressionCurveData> progressionCurves = new();
        public List<CharacterGrowthData> characterGrowth = new();
        public List<DialogueGraphData> dialogueGraphs = new();
        public List<BattleTriggerData> battleTriggers = new();
        public List<StatusEffectData> statuses = new();
        public List<PassiveData> passives = new();

        public List<EnemyActionProfileData> enemyActionProfiles = new();
        public ElementInteractionMatrix elementMatrix;
    }
}
