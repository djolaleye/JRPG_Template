using System;
using UnityEngine;

namespace JRPG.Core
{
    /// <summary>
    /// What one difficulty setting actually changes.
    ///
    /// <para>The two damage multipliers are named from the enemy's point of view, because that
    /// is the axis a designer tunes: how hard enemies hit, and how easily they go down. Party-on-party
    /// effects (healing an ally) are untouched by either.</para>
    ///
    /// <para><b>In Core, not Data.</b> <c>IDifficultyService</c> hands this to callers, and
    /// <c>JRPG.Services</c> references Core alone — so a profile living in Data could not appear on the
    /// interface. The ScriptableObject that authors a set of these stays in Data.</para>
    /// </summary>
    [Serializable]
    public struct DifficultyProfile
    {
        public Difficulty difficulty;

        [Min(0f)] public float xpMultiplier;

        [Tooltip("Multiplies damage dealt BY enemies to the party. Above 1 means enemies hit harder.")]
        [Min(0f)] public float enemyDamageDealtMultiplier;

        [Tooltip("Multiplies damage dealt to enemies by the party. Above 1 means enemies die faster.")]
        [Min(0f)] public float enemyDamageTakenMultiplier;

        public bool allowRetry;

        /// The neutral profile, used when nothing is authored for a difficulty. Deliberately a no-op
        /// so a missing asset plays as Normal rather than silently scaling something to zero.
        public static DifficultyProfile Default(Difficulty difficulty) => new()
        {
            difficulty = difficulty,
            xpMultiplier = 1f,
            enemyDamageDealtMultiplier = 1f,
            enemyDamageTakenMultiplier = 1f,
            allowRetry = true,
        };
    }
}
