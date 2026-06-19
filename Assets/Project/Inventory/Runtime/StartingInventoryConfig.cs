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

    [CreateAssetMenu(menuName = "JRPG/Starting Inventory Config", fileName = "StartingInventoryConfig")]
    public class StartingInventoryConfig : ScriptableObject
    {
        public List<StartingEntry> entries = new();
    }
}
