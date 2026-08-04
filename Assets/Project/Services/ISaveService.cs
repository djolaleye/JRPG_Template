using System.Collections.Generic;

namespace JRPG.Services
{
    public interface ISaveService
    {
        bool CanSave();
        bool Save(int slot);
        bool Load(int slot);
        bool SlotExists(int slot);

        /// <summary>
        /// Reads only the metadata header of a slot.
        /// </summary>
        SaveSlotInfo GetSlotInfo(int slot);

        /// Header info for every slot, 0..maxSlots-1, in index order.
        IReadOnlyList<SaveSlotInfo> ListSlots();

        /// Deletes a slot's file. False if the slot is out of range, already empty, or IO failed.
        bool DeleteSlot(int slot);
    }
}
