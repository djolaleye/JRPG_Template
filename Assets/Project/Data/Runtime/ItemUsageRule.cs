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
        public string requiredStoryFlag;
    }
}
