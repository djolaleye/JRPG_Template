using UnityEngine;

namespace JRPG.Data
{
    /// <summary>
    /// Authored economy tuning and presentation, in one asset referenced directly from
    /// <see cref="GameDatabase"/>.
    /// It is a singleton with no stable id, so it is a field rather than an indexed list.
    ///
    /// <para><b>The currency is named here, not in code.</b> A template adopter renaming gold to yen,
    /// credits or macca edits this asset; nothing reads a hardcoded symbol. Only the wallet's numbers
    /// are session state — these are authored data, so a rename reaches games already in progress.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "JRPG/Economy Settings", fileName = "EconomySettings")]
    public class EconomySettings : ScriptableObject
    {
        [Header("Presentation")]
        public string currencyName = "Gold";
        public string currencySymbol = "G";

        [Tooltip("{0} = amount, {1} = symbol, {2} = full name. Default renders '1,250 G'.")]
        public string amountFormat = "{0:N0} {1}";

        [Header("Rules")]
        [Min(0)] public int startingCurrency;

        [Tooltip("Fallback fraction of an item's basePrice a shop pays when its sell rule names no " +
                 "multiplier of its own.")]
        [Min(0f)] public float defaultSellMultiplier = 0.5f;

        /// <summary>Player-facing rendering of an amount. The one place the symbol reaches the UI.</summary>
        public string Format(int amount)
        {
            var format = string.IsNullOrEmpty(amountFormat) ? "{0:N0} {1}" : amountFormat;

            return string.Format(format, amount, currencySymbol, currencyName);
        }
    }
}
