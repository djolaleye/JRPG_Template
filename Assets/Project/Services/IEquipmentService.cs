namespace JRPG.Services
{
    /// <summary>
    /// Primitive cross-assembly surface for equipment lookups. Richer queries that take or return
    /// CharacterRuntimeInstance / EquipmentSlot live on IEquipmentManager in JRPG.Inventory.
    /// </summary>
    public interface IEquipmentService
    {
        /// <summary>Item id equipped in the given slot (string slot name) for a character instance, or null/empty.</summary>
        string GetEquippedItemId(string charInstanceId, string slotName);
    }
}
