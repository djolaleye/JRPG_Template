using System.Collections.Generic;
using JRPG.Core;

namespace JRPG.Services
{
    /// Narrow cross-assembly surface for the dialogue <-> combat interruption handshake.
    /// Implemented by the combat interrupter and resolved lazily by the dialogue system.
    public interface ICombatInterruptionService
    {
        /// True while an interactive interruption dialogue is holding combat. UI/turn pumps gate on this.
        bool IsInterruptionActive { get; }

        /// Called by the dialogue system when an interruption dialogue completes; resumes combat at the
        /// exact safe transition point that was held.
        void NotifyInterruptionDialogueEnded();

        /// Called each frame an interruption is active. Opens the deferred
        /// interruption dialogue once, on a clean menu stack (see one-frame-defer note on implementation).
        void PumpPendingInterruptionDialogue();

        // ---- Combat-resolution commands routable from dialogue exits/commands ----
        void ResumeBattle();
        void EndBattleWithOutcome(BattleOutcome outcome);
        void StartFollowUpBattle(string encounterId);
        void QueueCombatAction(string combatantId, string actionId, IReadOnlyList<string> targetIds);
        void SetEnemyActionProfile(string profileId);
        void SetBattleTrigger(string triggerId, bool active);
    }
}
