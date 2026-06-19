using System;

namespace JRPG.Data
{
    public enum ItemEffectType
    {
        Heal,
        RestoreMP,
        RestoreSP
    }

    [Serializable]
    public struct ItemEffect
    {
        public ItemEffectType type;
        public int amount;

        public ItemEffect(ItemEffectType type, int amount)
        {
            this.type = type;
            this.amount = amount;
        }
    }
}
