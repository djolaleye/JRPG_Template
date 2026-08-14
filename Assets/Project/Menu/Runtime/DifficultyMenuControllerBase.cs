using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using JRPG.Core;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// Shared substrate for the two difficulty pickers: the one shown when starting a new game, and
    /// the one reached from the pause menu.
    ///
    /// <para>One row per <see cref="Difficulty"/>, in enum order, so the row index <i>is</i> the
    /// difficulty. The paired detail panel describes the focused
    /// setting in the player's terms.</para>
    ///
    /// <para><b>Subclasses differ only in what confirming means.</b> Starting a game and changing an
    /// existing one need the same list, the same description and the same modal; they part company at
    /// <see cref="Apply"/>.</para>
    /// </summary>
    public abstract class DifficultyMenuControllerBase : MenuController
    {
        [Tooltip("Description panel bound to the focused difficulty.")]
        [SerializeField] private DetailPanelController detailPanel;

        [Tooltip("Modal used for confirmation. Optional: without it the choice applies immediately.")]
        [SerializeField] protected ConfirmPromptController confirmPrompt;

        /// Difficulties as of the last rebuild, index-aligned with the rows.
        private readonly List<Difficulty> _rows = new();

        protected override bool ModalActive => confirmPrompt != null && confirmPrompt.IsOpen;

        protected bool TryGetService(out IDifficultyService difficulty)
        {
            difficulty = null;
            return Context?.Services != null && Context.Services.TryResolve(out difficulty) && difficulty != null;
        }

        // ---- Rows ---------------------------------------------------------------------------------

        protected override IReadOnlyList<RowModel> BuildRows()
        {
            _rows.Clear();

            if (!TryGetService(out var difficulty))
                return new[] { RowModel.Simple("no_service", "Difficulty service unavailable", null, Context, enabled: false) };

            var rows = new List<RowModel>(3);

            foreach (Difficulty value in Enum.GetValues(typeof(Difficulty)))
            {
                _rows.Add(value);

                // The current setting is marked rather than disabled: re-selecting it is harmless, and
                // a disabled row would read as "unavailable" instead of "already chosen".
                bool isCurrent = ShowsCurrent && value == difficulty.Current;

                rows.Add(new RowModel
                {
                    id = value.ToString().ToLowerInvariant(),
                    label = DisplayName(value),
                    auxText = isCurrent ? "Current" : null,
                    enabled = true,
                    action = new SelectDifficultyAction(value, this),
                    context = Context,
                });
            }

            return rows;
        }

        /// Whether the list marks the active setting. False on the new-game picker, where there is no
        /// "current" difficulty to speak of yet.
        protected virtual bool ShowsCurrent => true;

        public static string DisplayName(Difficulty difficulty) => difficulty switch
        {
            Difficulty.Easy => "Easy",
            Difficulty.Hard => "Hard",
            _ => "Normal",
        };

        /// <summary>
        /// Player-facing copy, in the register of the reference art: what the setting is <i>for</i>,
        /// not the multipliers behind it. The reassurance that difficulty can be changed later is part
        /// of the promise the pause-menu row keeps.
        /// </summary>
        public static string Blurb(Difficulty difficulty) => difficulty switch
        {
            Difficulty.Easy =>
                "For those looking for a casual experience. Battles are gentler and progress comes " +
                "faster.\n\nYou can change the difficulty at any time.",

            Difficulty.Hard =>
                "For those who want a real fight. Enemies hit harder and endure more, progress is " +
                "slower, and a lost battle cannot be retried.\n\nYou can change the difficulty at any time.",

            _ =>
                "For those who want balance between exciting gameplay and a riveting story.\n\n" +
                "You can change the difficulty at any time.",
        };

        protected override void OnHighlightChanged(int index, RowModel model)
        {
            if (detailPanel == null) return;

            if (index < 0 || index >= _rows.Count) { detailPanel.Clear(); return; }

            var value = _rows[index];
            detailPanel.ShowDetail(DisplayName(value), Blurb(value));
        }

        // ---- Selection ----------------------------------------------------------------------------

        /// <summary>
        /// Called when a row is chosen. Subclasses decide whether to confirm first and what applying
        /// actually does.
        /// </summary>
        public abstract void Select(Difficulty difficulty);

        /// Commits the choice. Shared because both screens set the service the same way.
        protected void Apply(Difficulty difficulty)
        {
            if (!TryGetService(out var service)) return;

            service.Set(difficulty);
        }

        /// Cancel is left to the subclass: backing out of a new game and backing out of a settings
        /// change mean different things.
        protected override void OnCancel(InputAction.CallbackContext ctx)
        {
            if (ModalActive) return;
            base.OnCancel(ctx);
        }

        public override IReadOnlyList<InputPrompt> Prompts { get; } = new[]
        {
            new InputPrompt("Submit", "Confirm"),
            new InputPrompt("Cancel", "Back"),
        };
    }

    /// Routes a chosen row back to the screen that built it, which owns what selection means.
    public sealed class SelectDifficultyAction : IMenuAction
    {
        private readonly Difficulty _difficulty;
        private readonly DifficultyMenuControllerBase _screen;

        public SelectDifficultyAction(Difficulty difficulty, DifficultyMenuControllerBase screen)
        {
            _difficulty = difficulty;
            _screen = screen;
        }

        public bool CanExecute(MenuContext c) => _screen != null;

        public void Execute(MenuContext c) => _screen.Select(_difficulty);

        public string GetDisabledReason(MenuContext c) => "Difficulty screen unavailable.";
    }
}
