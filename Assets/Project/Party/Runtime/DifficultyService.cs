using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Save;
using JRPG.Services;

namespace JRPG.Party
{
    /// <summary>
    /// Holds the selected difficulty and resolves it against the authored
    /// <see cref="DifficultySettings"/>, persisting under the save key <c>difficulty</c>.
    ///
    /// <para><b>The choice is saved; the tuning is not.</b> Only the enum goes into the file, so
    /// retuning the numbers affects games already in progress.</para>
    ///
    /// <para>Lives beside <c>WorldStateService</c> for the same reason: JRPG.Party is the assembly
    /// that already hosts save-contributing session services with references to Core, Data, Services
    /// and Save.</para>
    /// </summary>
    public sealed class DifficultyService : IDifficultyService, ISaveable
    {
        /// What a new game starts on if nothing picks otherwise.
        public const Difficulty DefaultDifficulty = Difficulty.Normal;

        private readonly IEventBus _bus;
        private readonly DataRegistry _data;

        public DifficultyService(IEventBus bus, DataRegistry data)
        {
            _bus = bus;
            _data = data;
        }

        public Difficulty Current { get; private set; } = DefaultDifficulty;

        public DifficultyProfile CurrentProfile => GetProfile(Current);

        public DifficultyProfile GetProfile(Difficulty difficulty)
            => _data?.DifficultySettings != null
                ? _data.DifficultySettings.Get(difficulty)
                : DifficultyProfile.Default(difficulty);

        public void Set(Difficulty difficulty)
        {
            if (Current == difficulty) return;

            var previous = Current;
            Current = difficulty;

            _bus?.Publish(new DifficultyChanged(previous, difficulty));
        }

        /// A new game starts at the default; the picker sets its own choice straight afterwards.
        public void ResetForNewGame() => Set(DefaultDifficulty);

        // ---- ISaveable ------------------------------------------------------------------------

        public string SaveKey => "difficulty";

        public SaveDataBase CaptureState() => new DifficultySaveData
        {
            version = SaveSystemCore.CurrentSaveVersion,
            difficulty = Current,
        };

        public void RestoreState(SaveDataBase state)
        {
            if (state is not DifficultySaveData payload) return;

            // Straight through Set so a restore publishes the same event a menu change does — anything
            // watching difficulty sees one signal, wherever the change came from.
            Set(payload.difficulty);
        }
    }
}
