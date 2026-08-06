using System.Collections.Generic;
using UnityEngine;

namespace JRPG.Data
{
    /// <summary>
    /// How a line is presented, keyed by <see cref="DialogueImportance"/>.
    ///
    /// <para>Every presentation difference between importance levels lives here rather than in a
    /// <c>switch</c> in the presenter, so a project can retune the feel of Critical or Tutorial
    /// without touching UI code.</para>
    ///
    /// <para>Three routes exist, and <see cref="PresentationEntry"/> picks one per importance:
    /// the <b>passive overlay</b> (read-only banner, auto-dismissing, never takes input), the
    /// <b>notice card</b> (dimmed backdrop, heading/body/hint — tutorials and system notices), and the
    /// default <b>dialogue box</b> (speaker bust, name plate, body, choice stack).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "JRPG/Dialogue/Presentation Profile", fileName = "DialoguePresentationProfile")]
    public class DialoguePresentationProfile : ScriptableObject
    {
        [System.Serializable]
        public struct PresentationEntry
        {
            public DialogueImportance importance;

            [Tooltip("Route through the passive (read-only, auto-dismissing, non-blocking) overlay instead of the presenter.")]
            public bool usesPassiveOverlay;

            [Tooltip("Route through the NoticeCard instead of the " +
                     "standard dialogue box. For tutorials and system notices, which are not conversation.")]
            public bool usesNoticeCard;

            [Tooltip("Interactive path only: block navigation/cancel and emphasize (e.g. Critical).")]
            public bool blocksInput;

            [Tooltip("Passive overlay only: seconds before auto-dismiss (<= 0 uses the presenter default).")]
            public float autoDismissSeconds;

            [Tooltip("Optional style key a skinned presenter can switch on (e.g. \"critical\", \"tutorial\", \"system\").")]
            public string emphasisKey;

            [Tooltip("Draw a heavier frame around the box. Marks a line the player must not skim past.")]
            public bool framed;

            [Tooltip("Show the speaker bust. Off for lines with no personal speaker (system notices).")]
            public bool showsPortrait;

            [Tooltip("Body reveal speed, characters per second. 0 = no typewriter, the line appears whole.")]
            public float typewriterCharsPerSecond;

            [Tooltip("Seconds to wait on a fully-revealed linear line before advancing on its own. " +
                     "<= 0 means the player must advance it. Ignored when the node has choices.")]
            public float autoAdvanceSeconds;

            [Tooltip("Let the player fast-forward the typewriter and skip ahead. Off for Critical, which " +
                     "must be read at its own pace.")]
            public bool allowsSkip;
        }

        [SerializeField] private List<PresentationEntry> entries = new();

        /// <summary>
        /// Used when nothing is authored for an importance. A plain box, no
        /// auto-advance, skipping allowed.
        /// </summary>
        private static readonly PresentationEntry Default = new()
        {
            importance = DialogueImportance.Normal,
            usesPassiveOverlay = false,
            usesNoticeCard = false,
            blocksInput = false,
            autoDismissSeconds = 0f,
            emphasisKey = "",
            framed = false,
            showsPortrait = true,
            typewriterCharsPerSecond = 0f,
            autoAdvanceSeconds = 0f,
            allowsSkip = true,
        };

        public PresentationEntry Resolve(DialogueImportance importance)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].importance == importance) return entries[i];

            var d = Default;
            d.importance = importance;

            // Built-in fallbacks, so an unauthored profile still differentiates the levels that carry a
            // hard behavioural contract rather than a purely visual one.
            switch (importance)
            {
                case DialogueImportance.Passive:
                    d.usesPassiveOverlay = true;
                    d.showsPortrait = false;
                    break;
                case DialogueImportance.Critical:
                    d.blocksInput = true;
                    d.allowsSkip = false;
                    d.framed = true;
                    break;
                case DialogueImportance.Tutorial:
                case DialogueImportance.System:
                    d.usesNoticeCard = true;
                    d.showsPortrait = false;
                    break;
            }
            return d;
        }

#if UNITY_EDITOR
        /// <summary>Replaces the authored table. Used by the setup builder; editor-only.</summary>
        public void SetEntries(List<PresentationEntry> newEntries) => entries = newEntries ?? new List<PresentationEntry>();
#endif
    }
}
