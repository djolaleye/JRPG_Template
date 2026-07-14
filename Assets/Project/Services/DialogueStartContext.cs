namespace JRPG.Services
{
    /// String-only context passed when starting a dialogue. Safe to live in JRPG.Services (no data or
    /// runtime references), so triggers in any assembly can start dialogue through IDialogueService.
    public struct DialogueStartContext
    {
        public string initiatorId;      // player, npc, scripted_event
        public string speakerContextId; // e.g. "Blacksmith" — used by the current_speaker ref/token
        public string sceneId;
        public string sourceSystem;     // exploration, menu, ...
    }

    public enum DialogueEndReason
    {
        Completed,
        Cancelled,
        Interrupted
    }
}
