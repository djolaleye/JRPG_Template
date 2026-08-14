using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using JRPG.Menu;

namespace JRPG.Combat.UI
{
    /// Game-over prompt: Retry or Return currently
    public sealed class DefeatMenuController : MenuController
    {
        [SerializeField] private int modalSortingOrder = 200;

        protected override void OnEnable()
        {
            // MenuService parents this under menuParent, making it a nested canvas — which inherits the
            // parent's sorting unless overridden. overrideSorting cannot be authored on a prefab root
            var canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = modalSortingOrder;
            }

            base.OnEnable();
        }

        protected override IReadOnlyList<RowModel> BuildRows()
        {
            var rows = new List<RowModel>();
            var flow = DefeatFlowController.Current;
            bool canRetry = flow == null || flow.CanRetry;

            rows.Add(new RowModel
            {
                id = "retry",
                label = "Retry Battle",
                enabled = canRetry,
                action = new RetryBattleAction(),
                context = Context,
            });

            var loadLast = new LoadLastSaveAction();
            rows.Add(new RowModel
            {
                id = "load_last",
                label = "Load Last Save",
                enabled = loadLast.CanExecute(Context),
                action = loadLast,
                context = Context,
                disabledReason = loadLast.GetDisabledReason(Context),
            });

            // Always enabled, and last. Without it a first battle lost before the player ever saved —
            // with retry unavailable — would be a screen with no working exit.
            rows.Add(new RowModel
            {
                id = "return_title",
                label = "Return to Title",
                enabled = true,
                action = new ReturnToTitleFromDefeatAction(),
                context = Context,
            });

            return rows;
        }

        public override IReadOnlyList<InputPrompt> Prompts { get; } = new[]
        {
            new InputPrompt("Submit", "Confirm"),
        };

        /// No backing out of a game over.
        protected override void OnCancel(InputAction.CallbackContext ctx) { }
    }
}
