using System.Collections.Generic;
using UnityEngine;
using JRPG.Characters;

namespace JRPG.Menu
{
    /// <summary>
    /// Container that binds a list of party members and pools one <see cref="PartyStatusWidget"/> per
    /// slot — the corner HUD as a whole, where the widget is a single row of it.
    ///
    /// <para>Same architectural rule as the widget: it renders what it is handed. It never asks a party
    /// service who is in the party; the screen (or the combat presenter) resolves that and pushes a list
    /// of <see cref="CombatantVitals"/> in.</para>
    ///
    /// <para>Lives in its own file because Unity creates exactly one MonoScript per <c>.cs</c>, so a
    /// MonoBehaviour sharing a file with another cannot be serialized onto a prefab.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartyStatusPanel : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Parent for the pooled member widgets. Usually a Vertical/HorizontalLayoutGroup.")]
        [SerializeField] private RectTransform memberContainer;

        [Tooltip("Inactive child cloned per member. Must carry a PartyStatusWidget.")]
        [SerializeField] private PartyStatusWidget memberTemplate;

        [Header("Behaviour")]
        [Tooltip("Keep this many slots visible even when fewer members are bound, drawn blank.")]
        [Min(0)][SerializeField] private int minimumSlots;

        [SerializeField] private MenuAnimationProfile animationProfile;

        private readonly List<PartyStatusWidget> _widgets = new();
        private readonly List<CombatantVitals> _buffer = new();

        private int _activeCount;
        private bool _warned;

        /// <summary>Number of slots currently visible (bound members plus padding).</summary>
        public int ActiveCount => _activeCount;

        /// <summary>Live view of the pooled widgets. Useful for cursor/highlight work over the HUD.</summary>
        public IReadOnlyList<PartyStatusWidget> Widgets => _widgets;

        private void Awake()
        {
            if (memberContainer == null) memberContainer = transform as RectTransform;
            if (memberTemplate != null) memberTemplate.gameObject.SetActive(false);
        }

        /// <summary>Bind a whole party. Slots are pooled; a re-bind at the same count allocates nothing.</summary>
        public void Bind(IReadOnlyList<CombatantVitals> members)
        {
            int count = members?.Count ?? 0;
            int slots = Mathf.Max(count, minimumSlots);

            if (!EnsurePool(slots)) return;

            for (int i = 0; i < _widgets.Count; i++)
            {
                var w = _widgets[i];
                if (w == null) continue;

                bool visible = i < slots;
                if (w.gameObject.activeSelf != visible) w.gameObject.SetActive(visible);
                if (!visible) continue;

                if (animationProfile != null) w.SetAnimationProfile(animationProfile);

                if (i < count) w.Bind(members[i]);
                else w.Clear();
            }

            _activeCount = slots;
        }

        /// <summary>
        /// Convenience overload for out-of-combat screens. Portraits stay the caller's business —
        /// resolving a character's portrait is a data lookup, and widgets do not do data lookups.
        /// </summary>
        public void Bind(IReadOnlyList<CharacterRuntimeInstance> members, IReadOnlyList<Sprite> portraits = null)
        {
            _buffer.Clear();
            if (members != null)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    Sprite portrait = portraits != null && i < portraits.Count ? portraits[i] : null;
                    _buffer.Add(CombatantVitals.From(members[i], portrait));
                }
            }
            Bind(_buffer);
        }

        /// <summary>Hide every slot.</summary>
        public void Clear()
        {
            for (int i = 0; i < _widgets.Count; i++)
            {
                if (_widgets[i] != null && _widgets[i].gameObject.activeSelf)
                    _widgets[i].gameObject.SetActive(false);
            }
            _activeCount = 0;
        }

        /// <summary>The widget in slot <paramref name="index"/>, or null when out of range.</summary>
        public PartyStatusWidget SlotAt(int index)
            => index >= 0 && index < _widgets.Count ? _widgets[index] : null;

        /// <summary>Share one animation profile with every pooled slot.</summary>
        public void SetAnimationProfile(MenuAnimationProfile profile)
        {
            animationProfile = profile;
            for (int i = 0; i < _widgets.Count; i++)
            {
                if (_widgets[i] != null) _widgets[i].SetAnimationProfile(profile);
            }
        }

        private bool EnsurePool(int slots)
        {
            if (memberContainer == null) memberContainer = transform as RectTransform;

            if (memberTemplate == null || memberContainer == null)
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("[JRPG.Menu] PartyStatusPanel is missing memberTemplate/memberContainer — " +
                                     "Bind() renders nothing.", this);
                }
                return false;
            }

            while (_widgets.Count < slots)
            {
                var w = Instantiate(memberTemplate, memberContainer);
                w.name = $"Member_{_widgets.Count}";
                w.gameObject.SetActive(true);
                _widgets.Add(w);
            }

            return true;
        }
    }
}
