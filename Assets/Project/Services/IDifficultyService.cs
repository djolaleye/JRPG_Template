using JRPG.Core;

namespace JRPG.Services
{
    /// <summary>
    /// The selected difficulty, and the tuning it resolves to.
    ///
    /// <para><b>Callers ask for the profile, not the enum.</b> Nothing outside this service should
    /// branch on <see cref="Difficulty"/> — the XP resolver, the damage pipeline and the defeat flow
    /// each read one field off <see cref="CurrentProfile"/>, so adding a difficulty is an authoring change.</para>
    /// </summary>
    public interface IDifficultyService
    {
        Difficulty Current { get; }

        /// Tuning for <see cref="Current"/>. Neutral (all multipliers 1, retries allowed) when the
        /// database authors nothing for it.
        DifficultyProfile CurrentProfile { get; }

        /// Changes the setting and publishes <see cref="DifficultyChanged"/>. Takes effect immediately;
        /// the pause menu is exploration-only, so a change can never land mid-battle.
        void Set(Difficulty difficulty);

        /// Tuning for a specific difficulty, for UI that previews what a setting would do.
        DifficultyProfile GetProfile(Difficulty difficulty);
    }
}
