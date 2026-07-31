namespace JRPG.Services
{
    /// A read-only, non-blocking dialogue overlay. Displays a resolved line without pushing a
    /// menu frame, changing LayeredState, or taking input — so it can appear during combat without
    /// disturbing the command/target flow. Implemented by a scene MonoBehaviour in JRPG.Dialogue.UI.
    public interface IPassiveDialoguePresenter
    {
        /// Queue a line for display; the presenter shows queued lines one at a time and auto-dismisses.
        /// seconds <= 0 means "use the presenter's default duration".
        void ShowLine(string speaker, string body, float seconds);
    }

    /// Static locator for the active passive-dialogue overlay. Set by
    /// the presenter in OnEnable and cleared in OnDisable; null when no passive overlay exists in-scene.
    public static class PassiveDialogueSink
    {
        public static IPassiveDialoguePresenter Current { get; set; }
    }
}
