namespace JRPG.Services
{
    /// <summary>
    /// Lean cross-assembly surface: primitives + string context only, matching the
    /// ICombatService/IProgressionService precedent. The rich read API (render snapshots, choice view
    /// data, session) lives on the concrete DialogueService in JRPG.Dialogue; UI resolves that.
    /// </summary>
    public interface IDialogueService
    {
        bool IsDialogueActive { get; }

        bool CanStartDialogue(string graphId, DialogueStartContext context);
        void StartDialogue(string graphId, DialogueStartContext context);

        /// Advance a linear node (no-op on choice nodes; UI should call Choose there).
        void Advance();
        void Choose(string choiceId);
        void EndDialogue(DialogueEndReason reason);
        void ShowPassiveLine(string graphId, DialogueStartContext context, float autoDismissSeconds);
    }
}
