using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Save;
using JRPG.Services;

namespace JRPG.Economy
{
    /// <summary>
    /// The wallet. Every currency change in the game passes through here.
    ///
    /// <para><b>Earning and spending are not symmetric.</b> <see cref="Add"/> raises the balance and
    /// the lifetime total; <see cref="TrySpend"/> lowers the balance alone. Lifetime earnings are a
    /// record of what the game paid out, not a running balance, so nothing subtracts from them.</para>
    ///
    /// <para><b>Overflow is refused, never wrapped.</b> Overflow addition is
    /// clamped and logged instead.</para>
    /// </summary>
    public sealed class CurrencyService : ICurrencyService, ISaveable
    {
        private readonly IEventBus _bus;
        private readonly DataRegistry _data;

        private int _current;
        private long _lifetimeEarned;
        private const int WALLET_MAXIMUM = 99999999;

        /// <param name="data">
        /// Optional. Supplies <see cref="EconomySettings.startingCurrency"/> for
        /// <see cref="ResetForNewGame"/>. Null starts a new game at zero, which is what a harness
        /// built without a database gets.
        /// </param>
        public CurrencyService(IEventBus bus, DataRegistry data = null)
        {
            _bus = bus;
            _data = data;
            _current = StartingCurrency;
        }

        private int StartingCurrency
            => _data != null && _data.EconomySettings != null
                ? Mathf.Max(0, _data.EconomySettings.startingCurrency)
                : 0;

        // ---- ICurrencyService ------------------------------------------------------------------

        public int Current => _current;
        public long LifetimeEarned => _lifetimeEarned;

        public bool CanAfford(int amount) => amount >= 0 && _current >= amount;

        public void Add(int amount, CurrencyChangeReason reason = CurrencyChangeReason.Unspecified)
        {
            if (amount == 0) return;

            if (amount < 0)
            {
                Debug.LogWarning($"[JRPG.Economy] Add({amount}) rejected — earning is never negative. " +
                                 "Use TrySpend for outgoing currency.");
                return;
            }

            int widened = _current + amount;
            int granted = amount;

            if (widened > WALLET_MAXIMUM)
            {
                granted = WALLET_MAXIMUM - _current;
                Debug.LogWarning($"[JRPG.Economy] Add({amount}) clamped to {granted}: the wallet is at its ceiling.");
            }

            _current += granted;
            _lifetimeEarned += granted;

            _bus?.Publish(new CurrencyChanged(_current, granted, reason));
        }

        public bool TrySpend(int amount, CurrencyChangeReason reason = CurrencyChangeReason.Unspecified)
        {
            if (amount < 0)
            {
                Debug.LogWarning($"[JRPG.Economy] TrySpend({amount}) rejected — spending is never negative.");
                return false;
            }

            if (amount == 0) return true;
            if (!CanAfford(amount)) return false;

            _current -= amount;

            _bus?.Publish(new CurrencyChanged(_current, -amount, reason));

            return true;
        }

        public void ResetForNewGame()
        {
            _current = StartingCurrency;
            _lifetimeEarned = 0L;

            _bus?.Publish(new CurrencyChanged(_current, 0, CurrencyChangeReason.Unspecified));
        }

        // ---- ISaveable -------------------------------------------------------------------------

        public string SaveKey => "currency";

        public SaveDataBase CaptureState() => new CurrencySaveData
        {
            version = SaveSystemCore.CurrentSaveVersion,
            current = _current,
            lifetimeEarned = _lifetimeEarned,
        };

        public void RestoreState(SaveDataBase state)
        {
            if (state is not CurrencySaveData payload) return;

            _current = Mathf.Max(0, payload.current);
            _lifetimeEarned = payload.lifetimeEarned < 0 ? 0L : payload.lifetimeEarned;

            _bus?.Publish(new CurrencyChanged(_current, 0, CurrencyChangeReason.Unspecified));
        }
    }
}
