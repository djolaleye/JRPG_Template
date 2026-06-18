using System.Collections.Generic;
using JRPG.Services;

namespace JRPG.Party
{
    /// <summary>
    /// In-memory recruitment-condition evaluator. Holds a flat HashSet of satisfied condition IDs.
    /// Used until a real story-flag store comes online with the dialogue/world phase.
    /// </summary>
    public sealed class SimpleRecruitmentConditionStore : IRecruitmentConditionEvaluator
    {
        private readonly HashSet<string> _satisfied = new();

        public IReadOnlyCollection<string> Satisfied => _satisfied;

        public bool IsConditionMet(string conditionId)
        {
            // Empty / null is treated as a vacuous "true" so callers can pass placeholder ids without crashing.
            if (string.IsNullOrEmpty(conditionId)) return true;
            return _satisfied.Contains(conditionId);
        }

        public bool Set(string conditionId, bool satisfied)
        {
            if (string.IsNullOrEmpty(conditionId)) return false;
            return satisfied ? _satisfied.Add(conditionId) : _satisfied.Remove(conditionId);
        }

        public void Clear() => _satisfied.Clear();
    }
}
