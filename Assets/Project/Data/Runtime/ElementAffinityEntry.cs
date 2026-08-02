using System;

namespace JRPG.Data
{
    /// Used by characters, enemies, and equipment.
    /// Merged onto a combatant's CombatProfile at battle start.
    [Serializable]
    public struct ElementAffinityEntry
    {
        public Element element;
        public ElementAffinity affinity;
    }
}
