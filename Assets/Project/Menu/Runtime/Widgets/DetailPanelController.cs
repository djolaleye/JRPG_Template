using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JRPG.Menu
{
    /// <summary>
    /// Description panel paired with a list — the Pokémon BAG blurb, the Persona 5 skill readout.
    ///
    /// <para><b>It does not subscribe to anything.</b> The screen owns the wiring: it listens to
    /// <see cref="MenuController.HighlightChanged"/>, decides what the focused row means, and pushes the
    /// resulting strings in. That keeps the panel reusable for rows, tabs, grid cells and anything else,
    /// and keeps domain lookups (item data, skill data) out of the widget.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DetailPanelController : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private TMP_Text footerLabel;
        [SerializeField] private Image iconImage;

        [Tooltip("Object toggled off when no icon is supplied. Defaults to the icon's own GameObject.")]
        [SerializeField] private GameObject iconRoot;

        [Tooltip("Object toggled off by Clear(). Defaults to this GameObject's first child container, " +
                 "or nothing if unassigned — in which case Clear() just blanks the text.")]
        [SerializeField] private GameObject contentRoot;

        [Header("Empty state")]
        [Tooltip("Shown by Clear() in the body slot. Blank leaves the panel empty.")]
        [SerializeField] private string emptyBodyText = "";

        [Tooltip("Glyph drawn in the icon slot when the caller supplies no sprite but the slot is visible.")]
        [SerializeField] private TMP_Text iconFallbackGlyph;

        private bool _warned;

        /// <summary>True between a <see cref="ShowDetail"/> and the next <see cref="Clear"/>.</summary>
        public bool HasContent { get; private set; }

        private void Awake()
        {
            if (iconRoot == null && iconImage != null) iconRoot = iconImage.gameObject;
        }

        /// <summary>
        /// Render one detail. <paramref name="icon"/> and <paramref name="footer"/> are optional; a null
        /// icon hides the icon slot (or falls back to a glyph) rather than drawing a white box.
        /// </summary>
        public void ShowDetail(string title, string body, Sprite icon = null, string footer = null)
        {
            if (!EnsureWired()) return;

            HasContent = true;
            if (contentRoot != null && !contentRoot.activeSelf) contentRoot.SetActive(true);

            if (titleLabel != null) titleLabel.text = title ?? string.Empty;
            if (bodyLabel != null) bodyLabel.text = body ?? string.Empty;

            if (footerLabel != null)
            {
                footerLabel.text = footer ?? string.Empty;
                footerLabel.gameObject.SetActive(!string.IsNullOrEmpty(footer));
            }

            ApplyIcon(icon);
        }

        /// <summary>Blank the panel — used when focus lands on nothing, or the list is empty.</summary>
        public void Clear()
        {
            if (!EnsureWired()) return;

            HasContent = false;

            if (titleLabel != null) titleLabel.text = string.Empty;
            if (bodyLabel != null) bodyLabel.text = emptyBodyText ?? string.Empty;
            if (footerLabel != null)
            {
                footerLabel.text = string.Empty;
                footerLabel.gameObject.SetActive(false);
            }

            ApplyIcon(null);

            if (contentRoot != null && string.IsNullOrEmpty(emptyBodyText)) contentRoot.SetActive(false);
        }

        private void ApplyIcon(Sprite icon)
        {
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
                iconImage.preserveAspect = true;
            }

            if (iconFallbackGlyph != null) iconFallbackGlyph.enabled = icon == null;

            if (iconRoot != null)
            {
                // Keep the slot alive when there is a textual fallback, so layout doesn't jump.
                bool wanted = icon != null || iconFallbackGlyph != null;
                if (iconRoot.activeSelf != wanted) iconRoot.SetActive(wanted);
            }
        }

        private bool EnsureWired()
        {
            if (titleLabel != null || bodyLabel != null) return true;

            if (!_warned)
            {
                _warned = true;
                Debug.LogWarning("[JRPG.Menu] DetailPanelController has no titleLabel or bodyLabel assigned — " +
                                 "ShowDetail/Clear are no-ops.", this);
            }
            return false;
        }
    }
}
