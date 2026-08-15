using System;
using JRPG.Core;

namespace JRPG.Save
{
    /// The wallet: the spendable balance and the lifetime-earned statistic, as primitives only.
    [Serializable]
    public class CurrencySaveData : SaveDataBase
    {
        public int current;
        public long lifetimeEarned;
    }
}
