using System;
using JRPG.Core;

namespace JRPG.Save
{
    /// The selected difficulty. One enum value — the tuning it maps to is authored data, not save
    /// state, so a designer can retune a running game's difficulty without invalidating saves.
    [Serializable]
    public class DifficultySaveData : SaveDataBase
    {
        public Difficulty difficulty = Difficulty.Normal;
    }
}
