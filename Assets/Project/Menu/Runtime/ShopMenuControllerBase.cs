using TMPro;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Economy;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// Shared substrate for the buy and sell screens: the vendor they are working with, the quantity
    /// widget, the running total, and the confirmation modal.
    ///
    /// <para><b>The quantity is a widget, not a row</b> — the same decision the discard screen made:
    /// the amount and its total sit beside the catalog while the list
    /// keeps the focus. Left/right and the page actions drive it.</para>
    ///
    /// <para><b>Money is spent behind a confirmation.</b> The shared modal defaults to Cancel and
    /// suppresses row input while open, so one press can never both answer it and re-fire the row.</para>
    /// </summary>
    public abstract class ShopMenuControllerBase : MenuController
    {
        [Header("Shop screen")]
        [SerializeField] protected StepperRowController quantityStepper;
        [SerializeField] protected DetailPanelController detailPanel;
        [SerializeField] protected ConfirmPromptController confirmPrompt;

        [Tooltip("Running total for the current selection. Optional.")]
        [SerializeField] protected TMP_Text totalText;

        protected override bool ModalActive => confirmPrompt != null && confirmPrompt.IsOpen;

        /// <summary>The vendor this screen is working with, from the payload the shop object passed down.</summary>
        protected string ShopId => ShopMenuPayload.From(Context)?.ShopId;

        /// <summary>Section being browsed; empty for a basic shop's flat catalog.</summary>
        protected string SectionId => ShopMenuPayload.From(Context)?.SectionId;

        protected ShopService Shops
            => Context?.Services != null && Context.Services.TryResolve<IShopService>(out var svc)
                ? svc as ShopService
                : null;

        protected EconomySettings Economy => (AppContext.Data as DataRegistry)?.EconomySettings;

        /// <summary>Currency rendered the way the whole game renders it.</summary>
        protected string Money(int amount)
            => Economy != null ? Economy.Format(amount) : amount.ToString("N0");

        // ---- Quantity ------------------------------------------------------------------------------

        /// <summary>
        /// Re-labels and re-clamps the stepper for the focused row. The value survives when it is still
        /// in range, so moving between two affordable rows does not snap the amount back to 1.
        /// </summary>
        protected void ConfigureQuantity(int max)
        {
            if (quantityStepper == null) return;

            int upper = Mathf.Max(1, max);
            if (quantityStepper.Max == upper && quantityStepper.Min == 1) return;

            quantityStepper.Configure("Qty.", Mathf.Clamp(quantityStepper.Value, 1, upper), 1, upper);
            quantityStepper.ValueChanged -= OnQuantityChanged;
            quantityStepper.ValueChanged += OnQuantityChanged;
        }

        protected int SelectedQuantity()
            => quantityStepper != null ? Mathf.Max(1, quantityStepper.Value) : 1;

        private void OnQuantityChanged(int _) => RebuildAndFocus();

        protected virtual void OnDestroy()
        {
            if (quantityStepper != null) quantityStepper.ValueChanged -= OnQuantityChanged;
        }

        protected override void OnNavigateHorizontal(int dir) => quantityStepper?.Nudge(dir);
        protected override void OnPageLeft() => quantityStepper?.Nudge(-1);
        protected override void OnPageRight() => quantityStepper?.Nudge(+1);

        /// <summary>Writes the running total, or clears it when the selection has no price.</summary>
        protected void ShowTotal(string label, int total)
        {
            if (totalText == null) return;

            totalText.text = total > 0 ? $"{label}  {Money(total)}" : string.Empty;
        }
    }
}
