using System.Collections.Generic;
using UnityEngine;

namespace JRPG.Data
{
    /// Data-driven mapping from DialogueImportance to how a line is presented
    [CreateAssetMenu(menuName = "JRPG/Dialogue/Presentation Profile", fileName = "DialoguePresentationProfile")]
    public class DialoguePresentationProfile : ScriptableObject
    {
        [System.Serializable]
        public struct PresentationEntry
        {
            public DialogueImportance importance;
            [Tooltip("Route through the passive (read-only, auto-dismissing, non-blocking) overlay instead of the presenter.")]
            public bool usesPassiveOverlay;
            [Tooltip("Interactive path only: block navigation/cancel and emphasize (e.g. Critical).")]
            public bool blocksInput;
            [Tooltip("Passive overlay only: seconds before auto-dismiss (<= 0 uses the presenter default).")]
            public float autoDismissSeconds;
            [Tooltip("Optional style key a skinned presenter can switch on (e.g. \"critical\", \"tutorial\", \"system\").")]
            public string emphasisKey;
        }

        [SerializeField] private List<PresentationEntry> entries = new();

        private static readonly PresentationEntry Default = new()
        {
            importance = DialogueImportance.Normal,
            usesPassiveOverlay = false,
            blocksInput = false,
            autoDismissSeconds = 0f,
            emphasisKey = ""
        };

        public PresentationEntry Resolve(DialogueImportance importance)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].importance == importance) return entries[i];
                
            var d = Default;
            d.importance = importance;
            // Sensible built-in fallback for Passive when no explicit entry is authored.
            if (importance == DialogueImportance.Passive) d.usesPassiveOverlay = true;
            if (importance == DialogueImportance.Critical) d.blocksInput = true;
            return d;
        }
    }
}
