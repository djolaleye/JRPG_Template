using System.Collections.Generic;

namespace JRPG.Services
{
    /// <summary>
    /// One combat action a character can use only because of something they are wearing, paired with the
    /// item responsible.
    ///
    /// <para>The source is carried alongside the id because "where did this come from?" is the first
    /// thing a player asks about an ability that is not in their skill list.</para>
    /// </summary>
    public readonly struct UnlockedAction
    {
        public readonly string actionId;
        public readonly string sourceItemId;

        public UnlockedAction(string actionId, string sourceItemId)
        {
            this.actionId = actionId;
            this.sourceItemId = sourceItemId;
        }
    }

    /// <summary>
    /// Primitive cross-assembly surface for equipment lookups. Richer queries that take or return
    /// CharacterRuntimeInstance / EquipmentSlot live on IEquipmentManager in JRPG.Inventory.
    /// </summary>
    public interface IEquipmentService
    {
        /// <summary>Item id equipped in the given slot (string slot name) for a character instance, or null/empty.</summary>
        string GetEquippedItemId(string charInstanceId, string slotName);

        /// <summary>
        /// Actions this character's equipped gear unlocks (<c>EquipmentData.actionUnlockIds</c>), with the
        /// item each came from. Empty when nothing equipped grants anything.
        /// </summary>
        IReadOnlyList<UnlockedAction> GetUnlockedActions(string charId);
    }
}
