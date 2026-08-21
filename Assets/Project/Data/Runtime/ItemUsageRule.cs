using System;
using System.Collections.Generic;

namespace JRPG.Data
{
    [Serializable]
    public class ItemUsageRule
    {
        public bool usableInCombat = true;
        public bool usableInExploration = true;
        public bool targetLivingAlliesOnly;
        public bool consumedOnUse = true;

        /// <summary>
        /// Story flags gating this item, all of which must be set (AND). Empty is an open gate.
        /// </summary>
        public List<string> requiredStoryFlags = new();

        // ---- Quest items --------------------------------------------------------------------------

        /// True when the only thing that may consume this item is its quest. Ordinary "use" from the
        /// inventory screen is refused; the quest flow calls the rule service directly.
        public bool questUseOnly;

        /// Quest this item belongs to.
        public string requiredQuestId;

        // ---- Exploration tools --------------------------------------------------------------------

        public ExplorationToolUseMode explorationToolUseMode = ExplorationToolUseMode.NeverConsumed;
    }
}
