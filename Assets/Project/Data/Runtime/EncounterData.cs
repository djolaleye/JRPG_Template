using System;
using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Data
{
    /// One enemy slot in an encounter. `slotId` gives each instance an authored identity so two copies
    /// of the same EnemyData can be told apart by battle triggers, AI, and scripted dialogue.
    [Serializable]
    public struct EncounterEnemyEntry
    {
        public string enemyId;

        [Tooltip("Optional stable id for this instance (e.g. 'slime_left'). Must be unique within the " +
                 "encounter. Leave empty to auto-generate {enemyId}_{index}.")]
        public string slotId;

        [Tooltip("Optional display name override, so duplicates read as 'Slime A' / 'Slime B'.")]
        public string displayNameOverride;
    }

    /// Static configuration for a battle.
    [CreateAssetMenu(menuName = "JRPG/Combat/Encounter", fileName = "Encounter")]
    public class EncounterData : GameDataBase
    {
        [Tooltip("Enemy roster with per-instance identity.")]
        public List<EncounterEnemyEntry> enemies = new();

        [Tooltip("Legacy flat roster. Used only when 'enemies' is empty.")]
        public List<string> enemyIds = new();

        public string battleSceneId;

        [Header("Escape rules")]
        [Tooltip("Whether fleeing is permitted at all.")]
        public bool escapable;

        [Tooltip("Boss fights forbid escape outright, regardless of the flag above.")]
        public bool isBoss;

        [Tooltip("Divides the computed escape chance — higher means harder to flee (1 = normal).")]
        [Min(0.1f)] public float escapeDifficulty = 1f;

        /// Ids of BattleTriggerData assets evaluated during this encounter (mid-battle dialogue interruptions).
        public List<string> battleTriggerIds = new();

        /// The resolved roster: the rich list when authored, else the legacy ids upgraded in place.
        /// Slot ids are filled in deterministically so every instance always has one.
        public List<EncounterEnemyEntry> ResolveRoster()
        {
            var roster = new List<EncounterEnemyEntry>();

            if (enemies != null && enemies.Count > 0)
            {
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];

                    if (string.IsNullOrEmpty(e.enemyId)) continue;
                    if (string.IsNullOrEmpty(e.slotId)) e.slotId = $"{e.enemyId}_{i}";
                    
                    roster.Add(e);
                }
                return roster;
            }

            if (enemyIds != null)
                for (int i = 0; i < enemyIds.Count; i++)
                {
                    if (string.IsNullOrEmpty(enemyIds[i])) continue;
                    roster.Add(new EncounterEnemyEntry { enemyId = enemyIds[i], slotId = $"{enemyIds[i]}_{i}" });
                }

            return roster;
        }
    }
}
