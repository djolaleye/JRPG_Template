using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using JRPG.Characters;

namespace JRPG.Menu
{
    /// <summary>
    /// Everything a status HUD needs to draw one combatant, as plain data.
    ///
    /// <para><b>This struct is the reason the widget is domain-agnostic.</b> Out of combat the caller
    /// fills it from a <see cref="CharacterRuntimeInstance"/>; in combat the combat layer fills it from
    /// its own combatant type. <c>JRPG.Menu</c> cannot see <c>JRPG.Combat</c> and must not — a widget that
    /// took a combatant interface would drag the combat assembly into every menu screen. A struct the
    /// caller fills costs one line at each call site and keeps the dependency arrow pointing the right
    /// way.</para>
    /// </summary>
    public struct CombatantVitals
    {
        public string displayName;
        public int hp;
        public int maxHP;
        public int mp;
        public int maxMP;
        public int sp;
        public int maxSP;
        public bool isDefeated;
        public Sprite portrait;

        /// <summary>Convenience projection for out-of-combat screens. Combat fills the struct itself.</summary>
        public static CombatantVitals From(CharacterRuntimeInstance inst, Sprite portrait = null, string displayName = null)
        {
            if (inst == null) return new CombatantVitals { displayName = displayName ?? "—", portrait = portrait };

            return new CombatantVitals
            {
                displayName = displayName ?? inst.DisplayName,
                hp = inst.currentHP,
                maxHP = inst.MaxHP,
                mp = inst.currentMP,
                maxMP = inst.MaxMP,
                sp = inst.currentSP,
                maxSP = inst.MaxSP,
                isDefeated = inst.currentHP <= 0,
                portrait = portrait,
            };
        }
    }

    /// <summary>
    /// The persistent corner HUD entry: portrait, name, and HP/MP/SP bars for one party member.
    ///
    /// <para>Defeated and critical states are marked with a <b>word</b> ("DOWN", "LOW") plus an overlay,
    /// not just a colour swap, so the state survives greyscale.</para>
    ///
    /// <para>Renders only. It never reads the party service, never resolves a character, never heals
    /// anyone — the screen pushes <see cref="CombatantVitals"/> in.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartyStatusWidget : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private Image portraitImage;

        [Tooltip("Shown when the vitals carry no portrait sprite — a name-plate instead of a white box.")]
        [SerializeField] private GameObject portraitFallbackRoot;

        [SerializeField] private TMP_Text portraitFallbackLabel;
        [SerializeField] private TMP_Text nameLabel;

        [Header("Bars")]
        [SerializeField] private StatBarView hpBar;
        [SerializeField] private StatBarView mpBar;
        [SerializeField] private StatBarView spBar;

        [Tooltip("Hide a bar's row entirely when its max is 0 (a character with no SP, say).")]
        [SerializeField] private bool hideUnusedBars = true;

        [SerializeField] private GameObject hpRoot;
        [SerializeField] private GameObject mpRoot;
        [SerializeField] private GameObject spRoot;

        [Header("State treatment (non-colour-only)")]
        [Tooltip("Word marking the state. Empty while healthy.")]
        [SerializeField] private TMP_Text statusLabel;

        [Tooltip("Overlay switched on while defeated — a scrim, hatch, or X. Colour is a bonus, not the signal.")]
        [SerializeField] private GameObject defeatedOverlay;

        [Tooltip("Marker switched on while critical but alive.")]
        [SerializeField] private GameObject criticalMarker;

        [SerializeField] private CanvasGroup group;

        [SerializeField] private string defeatedText = "DOWN";
        [SerializeField] private string criticalText = "LOW";

        [Range(0f, 1f)][SerializeField] private float criticalThreshold01 = 0.25f;
        [Range(0f, 1f)][SerializeField] private float defeatedAlpha = 0.45f;

        [Header("Empty slot")]
        [SerializeField] private string emptyName = "—";

        private CombatantVitals _vitals;
        private bool _hasBinding;
        private bool _warned;

        /// <summary>The last vitals bound. Default-valued before the first <see cref="Bind(CombatantVitals)"/>.</summary>
        public CombatantVitals Vitals => _vitals;

        /// <summary>True once something has been bound and not cleared.</summary>
        public bool HasBinding => _hasBinding;

        /// <summary>True while the bound member is at or under the critical HP threshold but not defeated.</summary>
        public bool IsCritical
        {
            get
            {
                if (!_hasBinding || _vitals.isDefeated) return false;
                if (_vitals.maxHP <= 0) return false;
                return (float)_vitals.hp / _vitals.maxHP <= criticalThreshold01;
            }
        }

        // ---- public API --------------------------------------------------------------------------

        /// <summary>Render one combatant. The only binding entry point combat needs.</summary>
        public void Bind(CombatantVitals vitals)
        {
            _vitals = vitals;
            _hasBinding = true;

            EnsureWiredWarning();

            string name = string.IsNullOrEmpty(vitals.displayName) ? emptyName : vitals.displayName;
            if (nameLabel != null) nameLabel.text = name;

            ApplyPortrait(vitals.portrait, name);

            BindBar(hpBar, hpRoot, "HP", vitals.hp, vitals.maxHP, alwaysShow: true);
            BindBar(mpBar, mpRoot, "MP", vitals.mp, vitals.maxMP, alwaysShow: false);
            BindBar(spBar, spRoot, "SP", vitals.sp, vitals.maxSP, alwaysShow: false);

            ApplyState();
        }

        /// <summary>Convenience binding for out-of-combat screens.</summary>
        public void Bind(CharacterRuntimeInstance inst, Sprite portrait = null)
            => Bind(CombatantVitals.From(inst, portrait));

        /// <summary>Convenience binding with an explicit display name (data lookups stay on the screen).</summary>
        public void Bind(CharacterRuntimeInstance inst, string displayName, Sprite portrait = null)
            => Bind(CombatantVitals.From(inst, portrait, displayName));

        /// <summary>Blank the slot — an empty party position rather than a stale member.</summary>
        public void Clear()
        {
            _vitals = default;
            _hasBinding = false;

            if (nameLabel != null) nameLabel.text = emptyName;
            ApplyPortrait(null, emptyName);

            BindBar(hpBar, hpRoot, "HP", 0, 0, alwaysShow: true);
            BindBar(mpBar, mpRoot, "MP", 0, 0, alwaysShow: false);
            BindBar(spBar, spRoot, "SP", 0, 0, alwaysShow: false);

            if (statusLabel != null) { statusLabel.text = string.Empty; statusLabel.enabled = false; }
            if (defeatedOverlay != null) defeatedOverlay.SetActive(false);
            if (criticalMarker != null) criticalMarker.SetActive(false);
            if (group != null) group.alpha = 1f;
        }

        /// <summary>Share one animation profile down into the child bars.</summary>
        public void SetAnimationProfile(MenuAnimationProfile profile)
        {
            if (hpBar != null) hpBar.SetAnimationProfile(profile);
            if (mpBar != null) mpBar.SetAnimationProfile(profile);
            if (spBar != null) spBar.SetAnimationProfile(profile);
        }

        // ---- internals ---------------------------------------------------------------------------

        private void ApplyPortrait(Sprite portrait, string name)
        {
            bool hasPortrait = portrait != null;

            if (portraitImage != null)
            {
                portraitImage.sprite = portrait;
                portraitImage.enabled = hasPortrait;
                portraitImage.preserveAspect = true;
            }

            if (portraitFallbackRoot != null) portraitFallbackRoot.SetActive(!hasPortrait);

            if (portraitFallbackLabel != null && !hasPortrait)
                portraitFallbackLabel.text = Initials(name);
        }

        private void BindBar(StatBarView bar, GameObject root, string caption, int value, int max, bool alwaysShow)
        {
            bool show = alwaysShow || !hideUnusedBars || max > 0;

            if (root != null && root.activeSelf != show) root.SetActive(show);
            if (bar == null) return;

            if (!show) return;

            bar.SetCaption(caption);
            bar.SetValue(value, max);   // max == 0 is handled inside StatBarView
        }

        private void ApplyState()
        {
            bool defeated = _vitals.isDefeated || (_vitals.maxHP > 0 && _vitals.hp <= 0);
            bool critical = !defeated && IsCritical;

            if (statusLabel != null)
            {
                string text = defeated ? defeatedText : critical ? criticalText : string.Empty;
                statusLabel.text = text;
                statusLabel.enabled = !string.IsNullOrEmpty(text);
            }

            if (defeatedOverlay != null) defeatedOverlay.SetActive(defeated);
            if (criticalMarker != null) criticalMarker.SetActive(critical);

            if (group != null) group.alpha = defeated ? Mathf.Clamp01(defeatedAlpha) : 1f;
        }

        private static string Initials(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";

            // First letters of up to two words: "Wren Ashford" -> "WA".
            string trimmed = name.Trim();
            if (trimmed.Length == 0) return "?";

            int space = trimmed.IndexOf(' ');
            if (space <= 0 || space + 1 >= trimmed.Length)
                return trimmed.Substring(0, 1).ToUpperInvariant();

            return (trimmed.Substring(0, 1) + trimmed.Substring(space + 1, 1)).ToUpperInvariant();
        }

        private void EnsureWiredWarning()
        {
            if (_warned) return;
            if (nameLabel != null || hpBar != null) return;

            _warned = true;
            Debug.LogWarning("[JRPG.Menu] PartyStatusWidget has no nameLabel or hpBar assigned — " +
                             "Bind() renders nothing.", this);
        }
    }
}
