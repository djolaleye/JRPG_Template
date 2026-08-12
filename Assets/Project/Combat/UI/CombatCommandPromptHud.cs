using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using JRPG.Core;
using JRPG.Data;
using JRPG.Menu;
using JRPG.Services;

namespace JRPG.Combat.UI
{
    /// <summary>
    /// The battle command HUD: a cluster of <c>[glyph] VERB / sub-label</c> prompts bound
    /// straight to buttons.
    ///
    /// <para><b>Every prompt states its own availability</b> from the domain: affordability and status
    /// blocks come from <see cref="CombatService"/> / <c>StatusProcessor</c>, the escape chance from
    /// <see cref="EscapeResolver"/>. A verb the actor cannot use is dimmed and captioned with the
    /// reason rather than silently ignoring the button.</para>
    /// </summary>
    public sealed class CombatCommandPromptHud : MonoBehaviour
    {
        /// <summary>One rendered prompt row. Wired by the builder; all fields optional but the label.</summary>
        [System.Serializable]
        public sealed class PromptSlot
        {
            public string actionName;              // Menu-map action driving it, e.g. "Submit"
            public TMP_Text glyphLabel;
            public TMP_Text verbLabel;
            public TMP_Text subLabel;
            public CanvasGroup group;              // dimmed when the verb is unavailable
            [HideInInspector] public GameObject root;
        }

        private const string MeleeActionId = "attack_melee";
        private const string RangedActionId = "attack_ranged";
        private const string GuardActionId = "guard";
        private const string FleeActionId = "action_flee";

        [Header("Input")]
        [SerializeField] private InputActionAsset playerControls;
        [SerializeField] private string actionMapName = "Menu";

        [Header("Prompts")]
        [SerializeField] private PromptSlot attack = new() { actionName = "Submit" };
        [SerializeField] private PromptSlot shoot = new() { actionName = "PageL" };
        [SerializeField] private PromptSlot skill = new() { actionName = "Tab" };
        [SerializeField] private PromptSlot item = new() { actionName = "Change" };
        [SerializeField] private PromptSlot guard = new() { actionName = "Cancel" };
        [SerializeField] private PromptSlot flee = new() { actionName = "PageR" };

        [Header("Secondary strip")]
        [Tooltip("Non-primary verbs (turn order toggle, inspect).")]
        [SerializeField] private InputPromptBar secondaryPrompts;

        [Header("Glyphs")]
        [SerializeField] private string glyphSubmit = "[A]";
        [SerializeField] private string glyphCancel = "[B]";
        [SerializeField] private string glyphTab = "[Y]";
        [SerializeField] private string glyphChange = "[X]";
        [SerializeField] private string glyphPageL = "[L1]";
        [SerializeField] private string glyphPageR = "[R1]";

        [Header("Appearance")]
        [SerializeField] private float unavailableAlpha = 0.4f;

        private InputActionMap _map;
        private readonly Dictionary<string, InputAction> _actions = new();
        private bool _hooked;
        private double _listeningSince;

        private static CombatFlowController Flow => CombatFlowController.Current;
        private static CombatService Combat => Flow?.Combat;
        private static string ActorId => Flow?.CurrentActorId;

        private IMenuService Menus
            => AppContext.Services != null && AppContext.Services.TryResolve<IMenuService>(out var m) ? m : null;

        // ---- Lifecycle ----------------------------------------------------------------------------

        private void OnEnable()
        {
            HookInput(true);
            Refresh();
        }

        private void OnDisable() => HookInput(false);

        private void Update() => Refresh();

        // ---- Rendering ----------------------------------------------------------------------------

        private void Refresh()
        {
            var combat = Combat;
            var actorId = ActorId;
            if (combat == null || string.IsNullOrEmpty(actorId)) return;

            var actor = combat.CurrentBattle?.FindCombatant(actorId);

            Bind(attack, glyphSubmit, "ATTACK", "Use melee weapon", Availability(combat, actorId, MeleeActionId));

            bool canShoot = actor != null && combat.HasRangedWeapon(actor);
            SetSlotVisible(shoot, canShoot);
            if (canShoot)
                Bind(shoot, glyphPageL, "SHOOT", "Use ranged weapon", Availability(combat, actorId, RangedActionId));

            Bind(skill, glyphTab, "SKILL", "Use a skill", Availability(combat, actorId, null));
            Bind(item, glyphChange, "ITEM", "Use an item", Availability(combat, actorId, null));
            Bind(guard, glyphCancel, "GUARD", "Defend yourself", Availability(combat, actorId, GuardActionId));
            Bind(flee, glyphPageR, "FLEE", FleeSubLabel(combat), FleeAvailability(combat, actorId));
        }

        private void Bind(PromptSlot slot, string glyph, string verb, string sub, (bool ok, string reason) state)
        {
            if (slot == null) return;

            if (slot.glyphLabel != null) slot.glyphLabel.text = glyph;
            if (slot.verbLabel != null) slot.verbLabel.text = verb;

            // The disabled reason replaces the sub-label, so an unusable verb explains itself in place.
            if (slot.subLabel != null) slot.subLabel.text = state.ok ? sub : (state.reason ?? sub);
            if (slot.group != null) slot.group.alpha = state.ok ? 1f : unavailableAlpha;
        }

        private static void SetSlotVisible(PromptSlot slot, bool visible)
        {
            var go = slot?.root != null ? slot.root : slot?.verbLabel?.transform.parent.gameObject;
            if (go != null && go.activeSelf != visible) go.SetActive(visible);
        }

        /// <summary>Domain-sourced availability. A null action id is a submenu, always reachable.</summary>
        private (bool ok, string reason) Availability(CombatService combat, string actorId, string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return (true, null);

            return combat.CanAfford(actorId, actionId, out var reason) ? (true, null) : (false, reason);
        }

        private (bool ok, string reason) FleeAvailability(CombatService combat, string actorId)
        {
            var escape = CurrentEscape(combat);
            if (escape.HasValue && !escape.Value.allowed)
                return (false, string.IsNullOrEmpty(escape.Value.blockedReason) ? "Cannot escape." : escape.Value.blockedReason);

            return Availability(combat, actorId, FleeActionId);
        }

        private string FleeSubLabel(CombatService combat)
        {
            var escape = CurrentEscape(combat);
            if (!escape.HasValue) return "Escape the battle";

            return escape.Value.allowed
                ? $"Escape the battle  {Mathf.RoundToInt(escape.Value.chance * 100f)}%"
                : "Escape the battle";
        }

        private EscapeResolver.EscapeCheck? CurrentEscape(CombatService combat)
        {
            var battle = combat?.CurrentBattle;
            if (battle == null) return null;

            EncounterData encounter = null;
            if (AppContext.Data is DataRegistry data && !string.IsNullOrEmpty(battle.encounterId))
                data.TryGet(battle.encounterId, out encounter);

            return EscapeResolver.Evaluate(battle, encounter);
        }

        // ---- Input --------------------------------------------------------------------------------

        private void HookInput(bool subscribe)
        {
            // Editor-only
            if (!Application.isPlaying) return;

            if (playerControls == null) return;

            if (_map == null)
            {
                _map = playerControls.FindActionMap(actionMapName, false);
                if (_map == null)
                {
                    Debug.LogError($"[JRPG.Combat.UI] Input map '{actionMapName}' not found.");
                    return;
                }

                foreach (var name in new[] { "Submit", "Cancel", "Tab", "PageL", "PageR", "Change" })
                {
                    var action = _map.FindAction(name, false);
                    if (action != null) _actions[name] = action;
                    else Debug.LogWarning($"[JRPG.Combat.UI] Menu map has no '{name}' action — that prompt is inert.");
                }
            }

            if (subscribe)
            {
                if (_hooked) return;
                foreach (var kv in _actions) kv.Value.performed += OnAction;
                _hooked = true;
                _listeningSince = MenuInputMap.Now;

                // Share the ref-count rather than enabling directly, so this HUD cannot leave the Menu
                // map live for exploration after the battle.
                MenuInputMap.Acquire(_map);
            }
            else
            {
                if (!_hooked) return;
                foreach (var kv in _actions) kv.Value.performed -= OnAction;
                _hooked = false;
                MenuInputMap.Release(_map);
            }
        }

        private void OnAction(InputAction.CallbackContext ctx)
        {
            // The press that opened this HUD is not a command for it.
            if (MenuInputMap.IsStalePress(ctx, _listeningSince)) return;

            var combat = Combat;
            var actorId = ActorId;
            if (combat == null || string.IsNullOrEmpty(actorId)) return;

            switch (ctx.action.name)
            {
                case "Submit": Choose(MeleeActionId); break;
                case "Cancel": Choose(GuardActionId); break;
                case "PageR": Choose(FleeActionId); break;
                case "Tab": Open("combat_skills"); break;
                case "Change": Open("combat_items"); break;

                case "PageL":
                    var actor = combat.CurrentBattle?.FindCombatant(actorId);
                    if (actor != null && combat.HasRangedWeapon(actor)) Choose(RangedActionId);
                    break;
            }
        }

        /// <summary>
        /// Routes through the same <see cref="ChooseCombatActionAction"/> the list menu used, so target
        /// selection, auto-target handling and confirmation behave identically. This HUD changes how a
        /// verb is picked, not what picking it does.
        /// </summary>
        private void Choose(string actionId)
        {
            var combat = Combat;
            var actorId = ActorId;
            if (combat == null) return;

            if (!combat.CanAfford(actorId, actionId, out var reason))
            {
                if (!string.IsNullOrEmpty(reason))
                    Debug.Log($"[JRPG.Combat.UI] {actionId} unavailable: {reason}");
                return;
            }

            new ChooseCombatActionAction(actionId).Execute(BuildContext());
        }

        private void Open(string menuId) => Menus?.Open(menuId, null);

        private MenuContext BuildContext() => new()
        {
            Services = AppContext.Services,
            Menus = Menus,
        };
    }
}
