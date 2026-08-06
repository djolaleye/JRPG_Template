using System;
using System.Collections.Generic;
using UnityEngine;

namespace JRPG.Inventory
{
    [Serializable]
    public struct StartingEntry
    {
        public string itemId;
        public int quantity;
    }

    /// <summary>
    /// A piece of gear a character begins the game already wearing.
    /// </summary>
    [Serializable]
    public struct StartingEquipEntry
    {
        public string characterId;
        public string itemId;
    }

    [CreateAssetMenu(menuName = "JRPG/Starting Inventory Config", fileName = "StartingInventoryConfig")]
    public class StartingInventoryConfig : ScriptableObject
    {
        public List<StartingEntry> entries = new();
        public List<StartingEquipEntry> startingEquipment = new();
    }
}
