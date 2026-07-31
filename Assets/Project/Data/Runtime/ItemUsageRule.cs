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

        // [Planned — Phase 12] Gate visibility/use on a story flag.
        //  ContextualFilterEngine has no story access, won't be wired until
        //  Phase 12 inventory presentation via the reserved `state` seam. Null/empty always passes.
        public string requiredStoryFlag;
    }
}
