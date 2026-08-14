using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Data
{
    /// <summary>
    /// The authored tuning for every difficulty, in one asset referenced directly from
    /// <see cref="GameDatabase"/>.
    ///
    /// <para>This holds the tuning only. Which difficulty is selected is session state and lives
    /// on the difficulty service, where it is saved with the game — so retuning these numbers changes
    /// games already in progress.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "JRPG/Difficulty Settings", fileName = "DifficultySettings")]
    public class DifficultySettings : ScriptableObject
    {
        public List<DifficultyProfile> profiles = new();
        
        public DifficultyProfile Get(Difficulty difficulty)
        {
            for (int i = 0; i < profiles.Count; i++)
                if (profiles[i].difficulty == difficulty) return profiles[i];

            return DifficultyProfile.Default(difficulty);
        }
    }
}
