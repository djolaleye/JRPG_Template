using System.Collections.Generic;
using JRPG.Core;

namespace JRPG.Combat
{
    /// Victory payload consumed by Phase 8 (XP, rewards, level-ups). Phase 6 populates it with fixed
    /// or enemy-derived test values; the milestone is that victory produces a correctly shaped payload
    /// emitted through the event bus.
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
