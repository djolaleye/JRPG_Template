using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Data
{
    [CreateAssetMenu(menuName = "JRPG/Enemy Data", fileName = "EnemyData")]
    public class EnemyData : GameDataBase
    {
        public List<StatEntry> baseStats = new();
        public int level = 1;
        public int baseXpReward;
        public int baseCurrencyReward;
        // Later: actionProfileId, dropTableId.
    }
}
