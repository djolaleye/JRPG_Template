using JRPG.Core;

namespace JRPG.Services
{
    /// <summary>
    /// The player's wallet — the single runtime authority for spendable currency. No menu, reward
    /// resolver or shop may hold a currency field of its own.
    ///
    /// <para><see cref="Current"/> is what can be spent and moves in
    /// both directions. <see cref="LifetimeEarned"/> only ever rises, and is a statistic rather than a
    /// balance. It is a <c>long</c>, so a very long game cannot overflow it, and is
    /// absent from ordinary wallet UI.</para>
    /// </summary>
    public interface ICurrencyService
    {
        int Current { get; }
        long LifetimeEarned { get; }

        bool CanAfford(int amount);

        /// <summary>Spends <paramref name="amount"/>. Returns false and changes nothing when it cannot be paid.</summary>
        bool TrySpend(int amount, CurrencyChangeReason reason = CurrencyChangeReason.Unspecified);

        /// <summary>Earns <paramref name="amount"/>, raising both the balance and lifetime earnings.</summary>
        void Add(int amount, CurrencyChangeReason reason = CurrencyChangeReason.Unspecified);

        void ResetForNewGame();
    }
}
