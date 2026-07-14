namespace JRPG.Services
{
    /// <summary>
    /// Lightweight persistent story-flag store. Backs dialogue conditions/commands and (through
    /// IRecruitmentConditionEvaluator) party recruitment gating. Grows into quests later.
    /// </summary>
    public interface IStoryStateService
    {
        bool GetBool(string flagId);
        void SetBool(string flagId, bool value);

        int GetInt(string flagId);
        void SetInt(string flagId, int value);

        bool HasCompletedDialogue(string graphId);
        void MarkDialogueCompleted(string graphId);

        bool WasChoiceSelected(string choiceId);
        void MarkChoiceSelected(string choiceId);
    }
}
