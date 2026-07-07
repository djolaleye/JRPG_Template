using System.Collections.Generic;
using JRPG.Core;

namespace JRPG.Combat
{
    /// Victory payload consumed by Progression phase (XP, rewards, level-ups).
    public class BattleResultData
    {
        public string battleId;
        public string encounterId;
        public BattleOutcome outcome = BattleOutcome.None;

        public List<string> defeatedEnemyIds = new();
        public List<string> survivingPartyCharacterIds = new();

        public int baseXP;
        public int currency;

        public List<ItemDropResult> itemDrops = new();

        public int turnCount;
        public float elapsedSeconds;
    }

    public struct ItemDropResult
    {
        public string itemId;
        public int quantity;
    }
}
