using System.Collections.Generic;
using JRPG.Core;

namespace JRPG.Services
{
    /// <summary>
    /// The runtime authority for quests and bonds. Menus, dialogue, exploration and gameplay
    /// producers ask this service to change quest state.
    ///
    /// <para>Lean cross-assembly surface: ids, enums and primitives only. Queries that return
    /// authored <c>QuestData</c> / <c>BondData</c> live on the concrete <c>QuestService</c> in
    /// <c>JRPG.Quest</c>, which the quest UI resolves directly.</para>
    ///
    /// <para><b>Discovery and acceptance are separate operations.</b> A discovered quest is
    /// <see cref="QuestState.Available"/>, known but not undertaken. Main quests pass through that
    /// state in one step because story progression accepts them on the player's behalf; Side and
    /// Party quests wait. Nothing may treat Available and Active as interchangeable.</para>
    /// </summary>
    public interface IQuestService
    {
        // ---- Queries ---------------------------------------------------------------------------

        /// The quest's lifecycle state. Hidden for an unknown or undiscovered id, so a caller never
        /// has to distinguish "no such quest" from "not yet found" unless it wants to.
        QuestState GetState(string questId);

        bool IsKnown(string questId);
        bool IsActive(string questId);
        bool IsComplete(string questId);

        /// Quest ids of one family matching a filter, in authored order.
        IReadOnlyList<string> GetQuestIds(QuestType type, QuestStateFilter filter);

        /// Objective ids of a quest, in authored order. Empty for an unknown quest.
        IReadOnlyList<string> GetObjectiveIds(string questId);

        /// Current progress on one objective, and what it needs. False when either id is unknown.
        bool TryGetObjectiveProgress(string questId, string objectiveId, out int current, out int required, out bool complete);

        // ---- Transitions -----------------------------------------------------------------------

        /// <summary>
        /// Makes a quest known. Returns false when the id is unknown, the quest is already known, or
        /// its availability conditions are unmet. A Main quest continues straight to Active.
        /// </summary>
        bool DiscoverQuest(string questId);

        /// Accepts a discovered quest, moving Available to Active. False when it is not Available.
        bool TryAccept(string questId);

        /// Returns an Active optional quest to Available. Refused for Main quests, which are mandatory.
        bool TryAbandon(string questId);

        /// <summary>
        /// Completes an Active quest whose non-optional objectives are all met, applying its rewards
        /// exactly once. A repeat call on a completed quest is a no-op that returns false.
        /// </summary>
        bool TryComplete(string questId);

        // ---- Objective progress ----------------------------------------------------------------

        /// <summary>
        /// Reports a gameplay event to every Active quest with a matching objective. The producer
        /// says what happened; the service decides what, if anything, that advances.
        /// </summary>
        void RegisterObjectiveProgress(QuestObjectiveType type, string targetId, int amount = 1);

        /// Forces one objective complete regardless of its counter — the authored-exception path.
        bool SetObjectiveComplete(string questId, string objectiveId);

        // ---- Bonds -----------------------------------------------------------------------------

        int GetBondLevel(string characterId);
        int GetBondProgress(string characterId);

        /// Progress needed to unlock the next bond level's quest. 0 when there is no next level.
        int GetBondProgressRequired(string characterId);

        /// True when the next level's quest is unlocked and awaiting completion. While true, further
        /// bond progress for this character is refused — the quest is the gate.
        bool IsBondQuestUnlocked(string characterId);

        /// <summary>
        /// Grants bond progress. Returns false when the character has no bond data, is not eligible,
        /// or already has an unlocked bond quest waiting.
        /// </summary>
        bool TryGrantBondProgress(string characterId, int amount, BondProgressSource source = BondProgressSource.Unspecified);

        // ---- Lifecycle -------------------------------------------------------------------------

        void ResetForNewGame();
    }
}
