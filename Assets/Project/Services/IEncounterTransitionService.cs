using UnityEngine;

namespace JRPG.Services
{
    /// <summary>
    /// The single entry point from the world into a battle: everything between "the player attacked
    /// this thing" and "the battle is running in its arena".
    ///
    /// <para><b>Why exploration does not start battles directly any more.</b> Calling
    /// <c>ICombatService.StartBattleFromActiveParty</c> from a trigger skips the freeze, the return
    /// context, the encounter state transition, the arena and the transition — all of which have to
    /// happen in one specific order. One implementation owns that order.</para>
    /// </summary>
    public interface IEncounterTransitionService
    {
        /// True while a transition (either direction) is in flight. A second request is refused.
        bool IsBusy { get; }

        /// <summary>
        /// Starts <paramref name="encounterId"/>. <paramref name="focus"/> is what the exploration
        /// camera frames before the fade — usually the world object that was attacked; null skips the
        /// framing beat.
        ///
        /// <para>Returns false, changing nothing, when the encounter is not <c>Ready</c>, a battle or
        /// transition is already running, or the encounter cannot resolve a valid arena.</para>
        /// </summary>
        bool RequestEncounter(string encounterId, Transform focus);
    }
}
