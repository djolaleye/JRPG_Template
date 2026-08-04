using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JRPG.Menu
{
    /// <summary>One tab in a <see cref="TabStripController"/>. Plain data; the screen owns what it means.</summary>
    [Serializable]
    public struct TabDef
    {
        public string id;
        public string displayName;
        public Sprite icon;

        public TabDef(string id, string displayName, Sprite icon = null)
        {
            this.id = id;
            this.displayName = displayName;
            this.icon = icon;
        }
    }

    /// <summary>
    /// Horizontal icon tabs with ◀ ▶ affordances and a name label for the active tab.
    ///
    /// <para>The active tab is marked: it scales up, its underline turns
    /// on, and its caption gains bracket markers. Survives greyscale or a colour-blind
    /// palette, which is the accessibility floor.</para>
    ///
    /// <para>Navigation wraps in both directions. Tab entries are pooled.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TabStripController : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Parent for the pooled tab entries. Usually carries a HorizontalLayoutGroup.")]
        [SerializeField] private RectTransform tabContainer;

        [Tooltip("Inactive child cloned per tab. Optional children by name: 'Icon' (Image), " +
                 "'Glyph' (TMP fallback when icon is null), 'Caption' (TMP), 'Underline' (GameObject).")]
        [SerializeField] private GameObject tabTemplate;

        [Tooltip("Displays the active tab's name beneath/beside the strip.")]
        [SerializeField] private TMP_Text activeNameLabel;

        [SerializeField] private GameObject leftArrow;
        [SerializeField] private GameObject rightArrow;

        [Header("Active treatment (non-colour-only)")]
        [Min(1f)][SerializeField] private float activeScale = 1.18f;
        [SerializeField] private bool showUnderlineOnActive = true;
        [SerializeField] private string activeCaptionPrefix = "[";
        [SerializeField] private string activeCaptionSuffix = "]";

        [Range(0f, 1f)][SerializeField] private float inactiveAlpha = 0.55f;

        [Header("Profiles")]
        [SerializeField] private MenuAnimationProfile animationProfile;

        private readonly List<TabDef> _tabs = new();
        private readonly List<Entry> _entries = new();

        private int _activeIndex = -1;
        private bool _warned;

        /// <summary>Fires whenever the active tab changes, with the new index.</summary>
        public event Action<int> TabChanged;

        /// <summary>Index of the active tab, or -1 when the strip is empty.</summary>
        public int ActiveIndex => _activeIndex;

        /// <summary>Number of tabs currently defined.</summary>
        public int TabCount => _tabs.Count;

        /// <summary>The active tab's definition. Default-valued when the strip is empty.</summary>
        public TabDef ActiveTab => _activeIndex >= 0 && _activeIndex < _tabs.Count ? _tabs[_activeIndex] : default;

        /// <summary>Id of the active tab, or null when the strip is empty.</summary>
        public string ActiveId => _activeIndex >= 0 && _activeIndex < _tabs.Count ? _tabs[_activeIndex].id : null;

        private void Awake()
        {
            if (tabContainer == null) tabContainer = transform as RectTransform;
            if (tabTemplate != null) tabTemplate.SetActive(false);
        }

        // ---- public API --------------------------------------------------------------------------

        /// <summary>
        /// Replace the tab set. Selection is preserved by id when possible, otherwise clamped to the
        /// first tab. Does not fire <see cref="TabChanged"/> unless the resolved index actually moved.
        /// </summary>
        public void SetTabs(IReadOnlyList<TabDef> tabs)
        {
            string previousId = ActiveId;

            _tabs.Clear();
            if (tabs != null)
            {
                for (int i = 0; i < tabs.Count; i++) _tabs.Add(tabs[i]);
            }

            BuildEntries();

            int resolved = -1;
            if (!string.IsNullOrEmpty(previousId)) resolved = IndexOf(previousId);
            if (resolved < 0 && _tabs.Count > 0) resolved = 0;

            bool moved = resolved != _activeIndex;
            _activeIndex = resolved;
            ApplyActiveVisuals();

            if (moved && _activeIndex >= 0) TabChanged?.Invoke(_activeIndex);
        }

        /// <summary>Activate a tab by index. Out-of-range indices are ignored.</summary>
        public void SetActive(int index) => SetActive(index, notify: true);

        /// <summary>Activate a tab by id. Returns false when no tab carries that id.</summary>
        public bool SetActiveById(string id)
        {
            int idx = IndexOf(id);
            if (idx < 0) return false;
            SetActive(idx, notify: true);
            return true;
        }

        /// <summary>Move one tab right, wrapping past the end.</summary>
        public void Next() => Step(+1);

        /// <summary>Move one tab left, wrapping past the start.</summary>
        public void Previous() => Step(-1);

        /// <summary>Index of the tab with the given id, or -1.</summary>
        public int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (string.Equals(_tabs[i].id, id, StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        // ---- internals ---------------------------------------------------------------------------

        private void Step(int dir)
        {
            if (_tabs.Count == 0) return;
            int n = _tabs.Count;
            int start = _activeIndex < 0 ? 0 : _activeIndex;
            int next = ((start + dir) % n + n) % n;   // always wraps
            SetActive(next, notify: true);
        }

        private void SetActive(int index, bool notify)
        {
            if (_tabs.Count == 0) return;
            if (index < 0 || index >= _tabs.Count) return;
            if (index == _activeIndex) return;

            _activeIndex = index;
            ApplyActiveVisuals();
            if (notify) TabChanged?.Invoke(_activeIndex);
        }

        private void BuildEntries()
        {
            if (tabContainer == null) tabContainer = transform as RectTransform;

            if (tabTemplate == null || tabContainer == null)
            {
                WarnOnce("TabStripController is missing tabTemplate/tabContainer — no tabs will render.");
                return;
            }

            while (_entries.Count < _tabs.Count)
            {
                var go = Instantiate(tabTemplate, tabContainer);
                go.name = $"Tab_{_entries.Count}";
                go.SetActive(true);
                _entries.Add(new Entry(go));
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (i >= _tabs.Count)
                {
                    if (e.Root != null && e.Root.activeSelf) e.Root.SetActive(false);
                    continue;
                }

                var def = _tabs[i];
                if (e.Root != null && !e.Root.activeSelf) e.Root.SetActive(true);
                e.Root.name = string.IsNullOrEmpty(def.id) ? $"Tab_{i}" : $"Tab_{def.id}";

                if (e.Icon != null)
                {
                    e.Icon.sprite = def.icon;
                    e.Icon.enabled = def.icon != null;
                    e.Icon.preserveAspect = true;
                }

                // With no art, the first letter of the name is the icon.
                if (e.Glyph != null)
                {
                    e.Glyph.enabled = def.icon == null;
                    e.Glyph.text = InitialOf(def.displayName);
                }

                if (e.Caption != null) e.Caption.text = def.displayName ?? string.Empty;
            }
        }

        private void ApplyActiveVisuals()
        {
            float scale = animationProfile != null
                ? 1f + animationProfile.Amplitude(activeScale - 1f)
                : activeScale;
            if (scale < 1f) scale = 1f;

            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.Root == null || !e.Root.activeSelf) continue;

                bool active = i == _activeIndex;

                if (e.Rect != null) e.Rect.localScale = active ? new Vector3(scale, scale, 1f) : Vector3.one;
                if (e.Group != null) e.Group.alpha = active ? 1f : Mathf.Clamp01(inactiveAlpha);
                if (e.Underline != null) e.Underline.SetActive(showUnderlineOnActive && active);

                if (e.Caption != null && i < _tabs.Count)
                {
                    string name = _tabs[i].displayName ?? string.Empty;
                    e.Caption.text = active ? $"{activeCaptionPrefix}{name}{activeCaptionSuffix}" : name;
                    e.Caption.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
                }
            }

            if (activeNameLabel != null)
                activeNameLabel.text = _activeIndex >= 0 && _activeIndex < _tabs.Count
                    ? _tabs[_activeIndex].displayName ?? string.Empty
                    : string.Empty;

            // Arrows always shown while more than one tab exists — navigation wraps, so neither end
            // is ever a dead stop.
            bool arrowsUseful = _tabs.Count > 1;
            if (leftArrow != null && leftArrow.activeSelf != arrowsUseful) leftArrow.SetActive(arrowsUseful);
            if (rightArrow != null && rightArrow.activeSelf != arrowsUseful) rightArrow.SetActive(arrowsUseful);
        }

        private static string InitialOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return "•";
            return name.Substring(0, 1).ToUpperInvariant();
        }

        private void WarnOnce(string message)
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning($"[JRPG.Menu] {message}", this);
        }

        private sealed class Entry
        {
            public readonly GameObject Root;
            public readonly RectTransform Rect;
            public readonly CanvasGroup Group;
            public readonly Image Icon;
            public readonly TMP_Text Glyph;
            public readonly TMP_Text Caption;
            public readonly GameObject Underline;

            public Entry(GameObject root)
            {
                Root = root;
                Rect = root.transform as RectTransform;

                Group = root.GetComponent<CanvasGroup>();
                if (Group == null) Group = root.AddComponent<CanvasGroup>();

                var iconT = root.transform.Find("Icon");
                var glyphT = root.transform.Find("Glyph");
                var captionT = root.transform.Find("Caption");
                var underlineT = root.transform.Find("Underline");

                Icon = iconT != null ? iconT.GetComponent<Image>() : null;
                Glyph = glyphT != null ? glyphT.GetComponent<TMP_Text>() : null;
                Caption = captionT != null ? captionT.GetComponent<TMP_Text>() : null;
                Underline = underlineT != null ? underlineT.gameObject : null;
            }
        }
    }
}
