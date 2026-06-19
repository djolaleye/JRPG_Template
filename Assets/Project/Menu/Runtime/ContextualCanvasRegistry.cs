using System;
using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Menu
{
    [CreateAssetMenu(menuName = "JRPG/Contextual Canvas Registry", fileName = "ContextualCanvasRegistry")]
    public class ContextualCanvasRegistry : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string menuId;
            public GameObject canvasPrefab;
            public GameMode mode = GameMode.Exploration;
            public OverlayState overlay = OverlayState.None;
            public InputContext input = InputContext.Menu;
        }

        public List<Entry> entries = new();

        public Entry Find(string menuId)
        {
            if (string.IsNullOrEmpty(menuId)) return null;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].menuId == menuId) return entries[i];
            return null;
        }
    }
}
