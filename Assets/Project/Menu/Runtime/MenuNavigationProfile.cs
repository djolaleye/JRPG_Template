using UnityEngine;

namespace JRPG.Menu
{
    /// <summary>
    /// Shared navigation/cursor feel for every menu screen. Authored once as an asset rather than
    /// re-serialized per prefab, so retuning navigation does not mean touching a dozen prefabs.
    ///
    /// Read by <see cref="RowUIController"/> (cursor + state treatment) and <see cref="MenuController"/>
    /// (wrap/repeat). Accessibility toggles in Phase 12.12 write to the runtime overrides here rather
    /// than to each screen.
    /// </summary>
    [CreateAssetMenu(menuName = "JRPG/UI/Menu Navigation Profile", fileName = "MenuNavigationProfile")]
    public class MenuNavigationProfile : ScriptableObject
    {
        [Header("Navigation")]
        [Tooltip("Wrap from the last row back to the first (and vice versa).")]
        public bool wrapNavigation = true;

        [Tooltip("Seconds a direction must be held before it starts repeating. 0 disables repeat.")]
        [Min(0f)] public float holdRepeatDelay = 0.4f;

        [Tooltip("Seconds between repeats once repeating has started.")]
        [Min(0.01f)] public float holdRepeatInterval = 0.08f;

        [Header("Cursor")]
        [Tooltip("Optional caret/cursor sprite shown beside the focused row. Null renders the " +
                 "textual fallback below.")]
        public Sprite cursorSprite;

        [Tooltip("Text drawn in the cursor slot when cursorSprite is null.")]
        public string cursorFallbackGlyph = ">";

        [Tooltip("Local offset applied to the cursor relative to the focused row.")]
        public Vector2 cursorOffset = new(-18f, 0f);

        [Header("Focus indicator")]
        [Tooltip("Outline thickness on the focused row. Part of the non-colour-only focus treatment, " +
                 "so focus stays legible for colour-blind players and in high-contrast mode.")]
        [Min(0f)] public float focusOutlineWidth = 2f;

        public Color focusOutlineColor = new(1f, 1f, 1f, 0.9f);

        [Header("Mouse")]
        [Tooltip("Pointer hover moves focus, mirroring keyboard/gamepad selection.")]
        public bool hoverSelects = true;
    }
}
