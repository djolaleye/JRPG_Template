using System.Collections.Generic;
using JRPG.Core;
using JRPG.Save;
using JRPG.Services;

namespace JRPG.Dialogue
{
    /// In-memory story-flag store that persists via the save system. Also serves as the
    /// IRecruitmentConditionEvaluator, so dialogue SetStoryFlag commands directly gate party
    /// recruitment. Publishes StoryFlagChanged.
    public sealed class StoryStateService : IStoryStateService, IRecruitmentConditionEvaluator, ISaveable
    {
        private readonly IEventBus _bus;

        private readonly Dictionary<string, bool> _boolFlags = new();
        private readonly Dictionary<string, int> _intFlags = new();
        private readonly HashSet<string> _completedGraphs = new();
        private readonly HashSet<string> _selectedChoices = new();

        public StoryStateService(IEventBus bus)
        {
            _bus = bus;
        }

        // ---- IStoryStateService ---------------------------------------------------------------

        public bool GetBool(string flagId) => !string.IsNullOrEmpty(flagId) && _boolFlags.TryGetValue(flagId, out var v) && v;

        public void SetBool(string flagId, bool value)
        {
            if (string.IsNullOrEmpty(flagId)) return;
            _boolFlags[flagId] = value;
            _bus?.Publish(new StoryFlagChanged(flagId));
        }

        public int GetInt(string flagId) => !string.IsNullOrEmpty(flagId) && _intFlags.TryGetValue(flagId, out var v) ? v : 0;

        public void SetInt(string flagId, int value)
        {
            if (string.IsNullOrEmpty(flagId)) return;
            _intFlags[flagId] = value;
            _bus?.Publish(new StoryFlagChanged(flagId));
        }

        public bool HasCompletedDialogue(string graphId) => !string.IsNullOrEmpty(graphId) && _completedGraphs.Contains(graphId);
        public void MarkDialogueCompleted(string graphId) { if (!string.IsNullOrEmpty(graphId)) _completedGraphs.Add(graphId); }

        public bool WasChoiceSelected(string choiceId) => !string.IsNullOrEmpty(choiceId) && _selectedChoices.Contains(choiceId);
        public void MarkChoiceSelected(string choiceId) { if (!string.IsNullOrEmpty(choiceId)) _selectedChoices.Add(choiceId); }

        /// <summary>
        /// Wipes the entire story ledger — bool flags, int flags, completed dialogue graphs and
        /// selected choice ids. Those four collections are the whole of this service's state, so
        /// this reproduces the constructor's condition.
        ///
        /// Run this before PartyService.ResetForNewGame, since this service doubles as the
        /// <see cref="IRecruitmentConditionEvaluator"/> the party consults for recruitment
        /// eligibility.
        ///
        /// Idempotent and safe to call before anything has happened.
        /// </summary>
        public void ResetForNewGame()
        {
            _boolFlags.Clear();
            _intFlags.Clear();
            _completedGraphs.Clear();
            _selectedChoices.Clear();
        }

        // ---- IRecruitmentConditionEvaluator ---------------------------------------------------

        /// Empty/null id is vacuously true (matches the placeholder store), so characters with no
        /// recruitment condition stay eligible.
        public bool IsConditionMet(string conditionId) => string.IsNullOrEmpty(conditionId) || GetBool(conditionId);

        // ---- ISaveable ------------------------------------------------------------------------

        public string SaveKey => "story";

        public SaveDataBase CaptureState()
        {
            var payload = new StorySaveData();
            foreach (var kv in _boolFlags) payload.boolFlags.Add(new BoolFlagEntry { id = kv.Key, value = kv.Value });
            foreach (var kv in _intFlags) payload.intFlags.Add(new IntFlagEntry { id = kv.Key, value = kv.Value });
            payload.completedDialogueGraphs.AddRange(_completedGraphs);
            payload.selectedChoiceIds.AddRange(_selectedChoices);
            return payload;
        }

        public void RestoreState(SaveDataBase state)
        {
            if (state is not StorySaveData payload) return;

            // A restore starts from a blank ledger for the same reason a new game does.
            ResetForNewGame();

            for (int i = 0; i < payload.boolFlags.Count; i++) _boolFlags[payload.boolFlags[i].id] = payload.boolFlags[i].value;
            for (int i = 0; i < payload.intFlags.Count; i++) _intFlags[payload.intFlags[i].id] = payload.intFlags[i].value;
            for (int i = 0; i < payload.completedDialogueGraphs.Count; i++) _completedGraphs.Add(payload.completedDialogueGraphs[i]);
            for (int i = 0; i < payload.selectedChoiceIds.Count; i++) _selectedChoices.Add(payload.selectedChoiceIds[i]);
        }
    }
}
