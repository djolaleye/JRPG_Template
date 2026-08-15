using TMPro;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// The persistent money readout: a label and the current balance, rendered through
    /// <see cref="EconomySettings.Format"/> so the currency's name lives in authored data rather than
    /// in this component.
    ///
    /// <para><b>Only the spendable balance is shown.</b> <see cref="ICurrencyService.LifetimeEarned"/>
    /// is a statistic for tooling and achievements.</para>
    ///
    /// <para><b>The event refreshes, it does not inform.</b> The value always comes from the service —
    /// <see cref="CurrencyChanged"/> is only the cue to re-read it, so a screen opened mid-transaction
    /// or after a save restore shows the same number as one that watched every change.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WalletHeaderView : MonoBehaviour
    {
        [SerializeField] private TMP_Text amountText;

        [Tooltip("Optional caption drawn beside the amount. Left as authored when blank.")]
        [SerializeField] private TMP_Text captionText;

        [SerializeField] private string caption = "MONEY";

        private IEventBus _bus;

        private void OnEnable()
        {
            if (captionText != null && !string.IsNullOrEmpty(caption)) captionText.text = caption;

            _bus = AppContext.Bus;
            _bus?.Subscribe<CurrencyChanged>(OnCurrencyChanged);

            Refresh();
        }

        private void OnDisable()
        {
            _bus?.Unsubscribe<CurrencyChanged>(OnCurrencyChanged);
            _bus = null;
        }

        private void OnCurrencyChanged(CurrencyChanged _) => Refresh();

        /// <summary>Re-reads the wallet and repaints. Safe to call when no currency service exists.</summary>
        public void Refresh()
        {
            if (amountText == null) return;

            var services = AppContext.Services;
            if (services == null || !services.TryResolve<ICurrencyService>(out var wallet) || wallet == null)
            {
                amountText.text = string.Empty;
                return;
            }

            var settings = (AppContext.Data as DataRegistry)?.EconomySettings;

            amountText.text = settings != null
                ? settings.Format(wallet.Current)
                : wallet.Current.ToString("N0");
        }
    }
}
