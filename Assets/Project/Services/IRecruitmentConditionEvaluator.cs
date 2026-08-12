namespace JRPG.Services
{
    /// <summary>
    /// Contract for resolving "is this story flag / event ID satisfied?" — used by PartyService
    /// to decide if a Met character is eligible to become Recruitable.
    /// The dialogue/world will provide the real implementation backed by the story flag store;
    /// in the meantime, JRPG.Party ships a simple in-memory implementation.
    /// </summary>
    public interface IRecruitmentConditionEvaluator
    {
        bool IsConditionMet(string conditionId);
    }
}
