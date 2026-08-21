using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// "How many do you want to throw away?" — a stepper, a Discard row and a way out, for the item the
    /// inventory screen was focused on.
    /// </summary>
    public sealed class ItemDiscardMenuController : MenuController
    {
        [Header("Discard screen")]
        [SerializeField] private StepperRowController quantityStepper;
        [SerializeField] private DetailPanelController detailPanel;
        [SerializeField] private ConfirmPromptController confirmPrompt;

        private ItemData _item;
        private int _owned;

        protected override bool ModalActive => confirmPrompt != null && confirmPrompt.IsOpen;

        protected override void OnEnable()
        {
            // Context is adopted by the base OnEnable, so the stepper is configured from BuildRows
            // instead — the first rebuild happens immediately afterwards.
            base.OnEnable();
        }

        // ---- Rows -----------------------------------------------------------------------------------

        protected override IReadOnlyList<RowModel> BuildRows()
        {
            var rows = new List<RowModel>(2);

            _item = null;
            _owned = 0;

            var itemId = Context?.SelectedItemId;
            if (string.IsNullOrEmpty(itemId) || Context?.Services == null)
            {
                rows.Add(RowModel.Simple("none", "Nothing selected.", null, Context, enabled: false));
                rows.Add(RowModel.Simple("back", "Back", new CloseMenuAction(), Context));
                return rows;
            }

            var data = AppContext.Data as DataRegistry;
            if (data != null) data.TryGet(itemId, out _item);

            Context.Services.TryResolve<IInventoryService>(out var inventory);
            _owned = inventory != null ? inventory.GetQuantity(itemId) : 0;

            ConfigureStepper();
            RefreshDetail();

            int amount = SelectedAmount();
            var action = new DiscardItemAction(amount);
            bool allowed = action.CanExecute(Context);

            rows.Add(new RowModel
            {
                id = "discard",
                label = amount > 1 ? $"Throw away ×{amount}" : "Throw away",
                icon = _item != null ? _item.icon : null,
                quantityText = _owned > 0 ? $"Have {_owned}" : null,
                enabled = allowed,
                action = action,
                context = Context,
                disabledReason = allowed ? null : action.GetDisabledReason(Context),
            });

            rows.Add(RowModel.Simple("back", "Back", new CloseMenuAction(), Context));

            return rows;
        }

        /// <summary>
        /// Runs the discard behind the confirmation modal, then leaves: the amount the player chose is
        /// gone, so there is nothing further to do on this screen.
        /// </summary>
        protected override void OnSubmit(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
        {
            if (ModalActive) return;

            int index = HighlightedIndex;
            if (index != 0 || confirmPrompt == null || _item == null)
            {
                base.OnSubmit(ctx);
                return;
            }

            int amount = SelectedAmount();
            var action = new DiscardItemAction(amount);
            if (!action.CanExecute(Context)) { base.OnSubmit(ctx); return; }

            confirmPrompt.Ask($"Throw away {ItemName()} ×{amount}? This cannot be undone.",
                              "Throw away", "Cancel",
                              () =>
                              {
                                  action.Execute(Context);
                                  Context?.Menus?.Close();
                              });
        }

        // ---- Quantity -------------------------------------------------------------------------------

        private void ConfigureStepper()
        {
            if (quantityStepper == null) return;

            int max = Mathf.Max(1, _owned);

            // Configure re-labels and re-clamps; the current value survives when it is still in range,
            // so a rebuild triggered by the stepper itself does not snap the player back to 1.
            if (quantityStepper.Max != max || quantityStepper.Min != 1)
            {
                quantityStepper.Configure(Label(), Mathf.Clamp(quantityStepper.Value, 1, max), 1, max);
                quantityStepper.ValueChanged -= OnQuantityChanged;
                quantityStepper.ValueChanged += OnQuantityChanged;
            }
        }

        private void OnDestroy()
        {
            if (quantityStepper != null) quantityStepper.ValueChanged -= OnQuantityChanged;
        }

        private void OnQuantityChanged(int _) => RebuildAndFocus();

        private int SelectedAmount()
        {
            if (quantityStepper == null) return _owned > 0 ? 1 : 0;

            return Mathf.Clamp(quantityStepper.Value, 1, Mathf.Max(1, _owned));
        }

        protected override void OnNavigateHorizontal(int dir) => quantityStepper?.Nudge(dir);
        protected override void OnPageLeft() => quantityStepper?.Nudge(-1);
        protected override void OnPageRight() => quantityStepper?.Nudge(+1);

        // ---- Presentation ---------------------------------------------------------------------------

        private void RefreshDetail()
        {
            if (detailPanel == null) return;

            if (_item == null) { detailPanel.Clear(); return; }

            detailPanel.ShowDetail(ItemName(),
                                   string.IsNullOrEmpty(_item.description) ? "No description." : _item.description,
                                   _item.icon,
                                   $"Have {_owned}");
        }

        private string Label() => $"Amount";

        private string ItemName()
        {
            if (_item == null) return Context?.SelectedItemId ?? "item";

            return string.IsNullOrEmpty(_item.displayName) ? _item.Id : _item.displayName;
        }

        public override IReadOnlyList<InputPrompt> Prompts { get; } = new[]
        {
            new InputPrompt("Submit", "Discard"),
            new InputPrompt("PageL", "Fewer"),
            new InputPrompt("PageR", "More"),
            new InputPrompt("Cancel", "Back"),
        };
    }
}
