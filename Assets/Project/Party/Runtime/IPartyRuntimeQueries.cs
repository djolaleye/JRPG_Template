using System.Collections.Generic;
using JRPG.Characters;

namespace JRPG.Party
{
    /// Runtime-instance queries that can't live on IPartyService because JRPG.Services may not
    /// reference JRPG.Characters. Consumers that need CharacterRuntimeInstance reference JRPG.Party
    /// and resolve this interface directly off the PartyService.
    public interface IPartyRuntimeQueries
    {
        IReadOnlyList<CharacterRuntimeInstance> GetActiveCombatParty();
        CharacterRuntimeInstance GetPartyMemberInSlot(int index);
        IReadOnlyList<CharacterRuntimeInstance> GetActiveSpeakerCandidates();

        /// Live runtime instance for any known character stable id (active, reserve, or guest),
        /// created lazily on first request. Null when the id is unknown to the data registry.
        /// Needed by progression to award reserve XP and re-apply growth on save restore.
        CharacterRuntimeInstance ResolveInstanceById(string characterId);
    }
}
