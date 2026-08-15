namespace JRPG.Core
{
    /// Why a wallet moved. Presentation, audio and analytics branch on this; the wallet itself does not.
    public enum CurrencyChangeReason
    {
        Unspecified = 0,
        Battle = 1,
        Shop = 2,
        Chest = 3,
        Quest = 4,
        Debug = 5,
    }

    /// <summary>
    /// The wallet changed. Carries the resulting balance and the delta so a subscriber can render
    /// either without re-querying — but the currency service remains the authority on the value, and
    /// nothing may treat this event as the balance itself.
    /// </summary>
    public readonly struct CurrencyChanged
    {
        public readonly int Current;
        public readonly int Delta;
        public readonly CurrencyChangeReason Reason;

        public CurrencyChanged(int current, int delta, CurrencyChangeReason reason)
        {
            Current = current;
            Delta = delta;
            Reason = reason;
        }
    }
}
