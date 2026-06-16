using System;

namespace JRPG.Data
{
    [Serializable]
    public struct StatEntry
    {
        public StatType stat;
        public int value;

        public StatEntry(StatType stat, int value)
        {
            this.stat = stat;
            this.value = value;
        }
    }
}
