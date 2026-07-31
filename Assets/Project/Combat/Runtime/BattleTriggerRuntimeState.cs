using System.Collections.Generic;
using JRPG.Data;

namespace JRPG.Combat
{
    /// A single interactive interruption waiting to (or currently) play. Snapshotted from BattleTriggerData
    /// so the authored asset is never mutated.
    public struct PendingInterruption
    {
        public string triggerId;
        public string dialogueGraphId;
        public DialogueImportance importance;
        public bool blocksCombatInput;
        public string interruptedCombatantId;   // battle-local id
        public string interruptedSourceDataId;  // for speaker resolution
        public string followUpStoryFlag;
        public string followUpCombatActionId;
        public string followUpEncounterId;
    }

    /// Battle-local runtime firing state for battle triggers. Owned by BattleContext; never written back
    /// to any authored asset. Cleared implicitly when the battle (and its context) ends.
    public sealed class BattleTriggerRuntimeState
    {
        /// One-shot triggers that have already fired this battle.
        public readonly HashSet<string> firedOneShotIds = new();

        /// Interactive interruptions matched at the current point, not yet shown.
        public readonly Queue<PendingInterruption> pending = new();

        public BattleTriggerTiming currentEvaluationPoint;

        /// True while an interactive interruption is playing and combat is held.
        public bool interruptionActive;
        public PendingInterruption activeInterruption;

        /// The hold is set synchronously, but dialogue is opened
        /// one frame later after submitting menu action's teardown
        public bool dialogueOpenPending;

        /// True while a turn-transition tail (win/loss check + advance) is deferred until the current
        /// batch of interruptions resolves. Turn-start/battle-start holds leave this false.
        public bool holdingTurnTransition;
        public ActionResult heldResult;

        public string interruptedCombatantId;
        public CombatPhase resumePhase;
        public int roundNumber;
    }
}
