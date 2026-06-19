using System;

namespace JRPG.Data
{
    [Serializable]
    public class ItemUsageRule
    {
        public bool usableInCombat = true;
        public bool usableInExploration = true;
        public bool targetLivingAlliesOnly;
        public bool consumedOnUse = true;

        // TODO(dialogue/world phase): wire this against the real story-flag store.
        // Currently authored but unevaluated; null/empty always passes.
        public string requiredStoryFlag;
    }
}
