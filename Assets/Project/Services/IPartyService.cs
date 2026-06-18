using System.Collections.Generic;
using JRPG.Core;

namespace JRPG.Services
{
    /// <summary>
    /// Roster authority — id-only and enum APIs that any assembly can use without referencing
    /// JRPG.Characters. The runtime-instance subset lives on IPartyRuntimeQueries in JRPG.Party.
    /// </summary>
    public interface IPartyService
    {
        // Membership
        bool HasCharacter(string id);
        bool IsRecruited(string id);
        CharacterRosterState GetState(string id);
        void SetState(string id, CharacterRosterState state); // emits PartyChanged
        void Recruit(string id);                              // Recruitable -> Recruited; emits CharacterRecruited

        // Composition
        bool IsActiveCombatMember(string id);
        IReadOnlyList<string> GetActivePartyIds();
        IReadOnlyList<string> GetReservePartyIds();
        bool TrySetActive(string id, int slot);   // honors maxActiveMembers + current PartyScope
        bool MoveToReserve(string id);

        // Locks
        bool IsLocked(string id);
        bool Lock(string id);
        bool Unlock(string id);

        // Recruitment eligibility
        bool EvaluateRecruitable(string id);
        bool TryPromoteToRecruitable(string id);

        // Dialogue-facing
        bool IsCharacterPresentForDialogue(string id);
        bool CanCharacterComment(string id);

        // Story restrictions
        void PushScope(string scopeId,
                       IReadOnlyList<string> allowedIds,
                       IReadOnlyList<string> requiredIds,
                       IReadOnlyList<string> lockedIds,
                       int maxActiveMembers);
        void PopScope();
    }
}
