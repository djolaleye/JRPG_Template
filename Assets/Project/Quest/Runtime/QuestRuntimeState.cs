using System;
using System.Collections.Generic;
using JRPG.Core;

namespace JRPG.Quest
{
    /// One objective's live progress. Ids and primitives only — this round-trips through the save.
    [Serializable]
    public sealed class QuestObjectiveRuntimeState
    {
        public string objectiveId;
        public int currentAmount;
        public bool complete;
    }

    /// <summary>
    /// One quest instance's live state. The authored <c>QuestData</c> says what the quest is; this
    /// says where the player has got to with it.
    ///
    /// <para><c>rewardsApplied</c> is the duplicate-payout guard: it is set inside the completion
    /// transaction and checked before any reward is granted, so a reload, a re-entered screen or a
    /// second <c>TryComplete</c> cannot pay twice.</para>
    /// </summary>
    [Serializable]
    public sealed class QuestRuntimeState
    {
        public string questId;
        public QuestState state = QuestState.Hidden;
        public List<QuestObjectiveRuntimeState> objectives = new();
        public bool rewardsApplied;

        public QuestObjectiveRuntimeState Find(string objectiveId)
        {
            if (string.IsNullOrEmpty(objectiveId)) return null;

            for (int i = 0; i < objectives.Count; i++)
                if (objectives[i].objectiveId == objectiveId) return objectives[i];

            return null;
        }

        public bool IsKnown => state != QuestState.Hidden;
    }
}
