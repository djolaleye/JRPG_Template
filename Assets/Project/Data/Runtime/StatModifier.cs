using System;

namespace JRPG.Data
{
    [Serializable]
    public struct StatModifier
    {
        public StatType stat;
        public ModifierType modifierType;
        public float value;
        public string sourceId;
        public bool isPermanent;

        public StatModifier(StatType stat, ModifierType modifierType, float value, string sourceId, bool isPermanent)
        {
            this.stat = stat;
            this.modifierType = modifierType;
            this.value = value;
            this.sourceId = sourceId;
            this.isPermanent = isPermanent;
        }
    }
}
