using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace JRPG.Menu
{
    /// <summary>
    /// Bottom-anchored strip of <c>[glyph] Label</c> pairs advertising a screen's input affordances.
    ///
    /// <para>Dumb by design: it renders the <see cref="InputPrompt"/> list it is handed and nothing else.
    /// The screen calls <see cref="Show"/> (typically with its <see cref="MenuController.Prompts"/>).
    /// The only thing the bar decides for itself is which glyph column to read — keyboard or gamepad —
    /// based on the last device that actually drove an action.</para>
    ///
    /// <para>Entries are pooled: refreshing never instantiates or destroys once warmed up.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InputPromptBar : MonoBehaviour
    {
        /// <summary>Which glyph column the bar is currently rendering.</summary>
        public enum PromptScheme
        {
            Keyboard = 0,
            Gamepad = 1,
        }

        /// <summary>
        /// One action-name → glyph mapping. Placeholder text glyphs are expected here; swapping in real
        /// art later means changing these strings to sprite tags, not changing this widget.
        /// </summary>
        [Serializable]
        public class PromptGlyph
        {
            [Tooltip("Name of the action in the Menu action map, e.g. \"Submit\".")]
            public string actionName;

            [Tooltip("Glyph shown while the player is on keyboard/mouse.")]
            public string keyboardGlyph = "[?]";

            [Tooltip("Glyph shown while the player is on a gamepad.")]
            public string gamepadGlyph = "(?)";
        }

        [Header("Wiring")]
        [Tooltip("Parent the pooled entries live under. Usually carries a HorizontalLayoutGroup.")]
        [SerializeField] private RectTransform entryContainer;

        [Tooltip("Inactive child cloned per entry. Needs a TMP child named 'Glyph' and one named 'Label'.")]
        [SerializeField] private GameObject entryTemplate;

        [Header("Glyphs")]
        [SerializeField] private List<PromptGlyph> glyphs = new();

        [Tooltip("Used when an action name has no entry in the table above.")]
        [SerializeField] private string fallbackKeyboardGlyph = "[?]";
        [SerializeField] private string fallbackGamepadGlyph = "(?)";

        [Header("Behaviour")]
        [Tooltip("Hide the whole bar when Show() is given an empty or null list.")]
        [SerializeField] private bool hideWhenEmpty = true;

        [Tooltip("Follow the last-used device automatically. Off means SetScheme() is the only way to change columns.")]
        [SerializeField] private bool autoDetectScheme = true;

        private readonly List<Entry> _entries = new();
        private readonly List<InputPrompt> _current = new();

        private PromptScheme _scheme = PromptScheme.Keyboard;
        private bool _hooked;
        private bool _warnedMissingRefs;

        /// <summary>Glyph column currently in use.</summary>
        public PromptScheme Scheme => _scheme;

        /// <summary>Number of prompts currently rendered.</summary>
        public int PromptCount => _current.Count;

        // ---- lifecycle ---------------------------------------------------------------------------

        private void Awake()
        {
            if (entryContainer == null) entryContainer = transform as RectTransform;
            if (entryTemplate != null) entryTemplate.SetActive(false);
            _scheme = GuessInitialScheme();
        }

        private void OnEnable()
        {
            if (!autoDetectScheme || _hooked) return;
            InputSystem.onActionChange += HandleActionChange;
            _hooked = true;
        }

        private void OnDisable()
        {
            if (!_hooked) return;
            InputSystem.onActionChange -= HandleActionChange;
            _hooked = false;
        }

        // ---- public API --------------------------------------------------------------------------

        /// <summary>
        /// Replace everything on the bar with <paramref name="prompts"/>. Safe to call every frame:
        /// entries are pooled and only the text changes.
        /// </summary>
        public void Show(IReadOnlyList<InputPrompt> prompts)
        {
            _current.Clear();
            if (prompts != null)
            {
                for (int i = 0; i < prompts.Count; i++) _current.Add(prompts[i]);
            }

            Refresh();
        }

        /// <summary>Clears every entry (and hides the bar when <c>hideWhenEmpty</c>).</summary>
        public void Clear() => Show(null);

        /// <summary>
        /// Force a glyph column. Only useful when <c>autoDetectScheme</c> is off, or to preview a scheme
        /// from an options screen.
        /// </summary>
        public void SetScheme(PromptScheme scheme)
        {
            if (_scheme == scheme) return;
            _scheme = scheme;
            Refresh();
        }

        /// <summary>Resolves the glyph string an action would render with right now.</summary>
        public string GlyphFor(string actionName)
        {
            bool gamepad = _scheme == PromptScheme.Gamepad;

            if (!string.IsNullOrEmpty(actionName))
            {
                for (int i = 0; i < glyphs.Count; i++)
                {
                    var g = glyphs[i];
                    if (g == null) continue;
                    if (!string.Equals(g.actionName, actionName, StringComparison.OrdinalIgnoreCase)) continue;

                    string mapped = gamepad ? g.gamepadGlyph : g.keyboardGlyph;
                    if (!string.IsNullOrEmpty(mapped)) return mapped;
                    break;
                }
            }

            return gamepad ? fallbackGamepadGlyph : fallbackKeyboardGlyph;
        }

        // ---- rendering ---------------------------------------------------------------------------

        private void Refresh()
        {
            if (entryContainer == null) entryContainer = transform as RectTransform;

            if (entryTemplate == null || entryContainer == null)
            {
                WarnOnce("InputPromptBar is missing entryTemplate/entryContainer — nothing will render.");
                return;
            }

            if (hideWhenEmpty)
            {
                bool wantVisible = _current.Count > 0;
                // Never toggle our own GameObject (that would unhook input); toggle the strip instead.
                if (entryContainer.gameObject != gameObject && entryContainer.gameObject.activeSelf != wantVisible)
                    entryContainer.gameObject.SetActive(wantVisible);
            }

            EnsureCapacity(_current.Count);

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (i >= _current.Count)
                {
                    if (entry.Root != null && entry.Root.activeSelf) entry.Root.SetActive(false);
                    continue;
                }

                var prompt = _current[i];
                if (entry.Root != null && !entry.Root.activeSelf) entry.Root.SetActive(true);

                if (entry.Glyph != null)
                {
                    entry.Glyph.text = GlyphFor(prompt.ActionName);

                    // Glyphs are short tokens that must stay on one line. Keyboard placeholders like
                    // "[Enter]" and "[Esc]" are wider than the gamepad "(A)" the slot was sized for, and
                    // wrapping split them across two lines ("[Ent" / "er]"). Both the flag and the width
                    // are re-applied per bind because the entries are pooled.
                    entry.Glyph.textWrappingMode = TextWrappingModes.NoWrap;
                    entry.Glyph.overflowMode = TextOverflowModes.Overflow;
                    FitGlyphWidth(entry);
                }

                if (entry.Label != null) entry.Label.text = prompt.Label ?? string.Empty;
            }
        }

        /// <summary>
        /// Gives the glyph slot a preferred width matching its text, so a wide keyboard token gets the
        /// room it needs and a narrow gamepad one does not leave a gap. Without this the slot keeps
        /// whatever fixed width the prefab authored and the glyph either clips or wraps.
        /// </summary>
        private static void FitGlyphWidth(Entry entry)
        {
            if (entry.Glyph == null) return;

            var layout = entry.Glyph.GetComponent<LayoutElement>();
            if (layout == null) layout = entry.Glyph.gameObject.AddComponent<LayoutElement>();

            layout.preferredWidth = entry.Glyph.GetPreferredValues(entry.Glyph.text).x;
        }

        private void EnsureCapacity(int count)
        {
            while (_entries.Count < count)
            {
                var go = Instantiate(entryTemplate, entryContainer);
                go.name = $"Prompt_{_entries.Count}";
                go.SetActive(true);
                _entries.Add(new Entry(go));
            }
        }

        // ---- device tracking ---------------------------------------------------------------------

        private static PromptScheme GuessInitialScheme()
        {
            // A gamepad plugged in with no keyboard present (console/handheld) starts on gamepad glyphs;
            // otherwise keyboard, and the first real input corrects it.
            if (Gamepad.current != null && Keyboard.current == null) return PromptScheme.Gamepad;
            return PromptScheme.Keyboard;
        }

        private void HandleActionChange(object obj, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed) return;

            var action = obj as InputAction;
            var device = action?.activeControl?.device;
            if (device == null) return;

            if (device is Gamepad) SetScheme(PromptScheme.Gamepad);
            else if (device is Keyboard || device is Mouse) SetScheme(PromptScheme.Keyboard);
        }

        // ---- helpers -----------------------------------------------------------------------------

        private void WarnOnce(string message)
        {
            if (_warnedMissingRefs) return;
            _warnedMissingRefs = true;
            Debug.LogWarning($"[JRPG.Menu] {message}", this);
        }

        private sealed class Entry
        {
            public readonly GameObject Root;
            public readonly TMP_Text Glyph;
            public readonly TMP_Text Label;

            public Entry(GameObject root)
            {
                Root = root;

                var glyphT = root.transform.Find("Glyph");
                var labelT = root.transform.Find("Label");

                Glyph = glyphT != null ? glyphT.GetComponent<TMP_Text>() : null;
                Label = labelT != null ? labelT.GetComponent<TMP_Text>() : null;

                if (Glyph != null && Label != null) return;

                // Name-independent fallback: first TMP is the glyph, second is the label.
                var texts = root.GetComponentsInChildren<TMP_Text>(true);
                if (Glyph == null && texts.Length > 0) Glyph = texts[0];
                if (Label == null && texts.Length > 1) Label = texts[1];
            }
        }
    }
}
