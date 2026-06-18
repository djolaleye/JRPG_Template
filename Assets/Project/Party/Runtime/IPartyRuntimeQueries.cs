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
    }
}
