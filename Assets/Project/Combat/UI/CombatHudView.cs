using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using JRPG.Core;
using JRPG.Data;
using JRPG.Menu;

namespace JRPG.Combat.UI
{
    /// <summary>
    /// The persistent battle HUD
    /// 
    /// </summary>
    public sealed class CombatHudView : MonoBehaviour
    {
        [Header("Party")]
        [Tooltip("Bottom-right vitals cluster. One widget per active party member.")]
        [SerializeField] private PartyStatusPanel partyPanel;

        [Header("Enemies")]
        [Tooltip("Top-left enemy roster: name, HP, and status/down state.")]
        [SerializeField] private TMP_Text enemyBanner;

        [Header("Turn order")]
        [SerializeField] private GameObject turnOrderRoot;
        [SerializeField] private TMP_Text turnOrderLabel;

        [Tooltip("Shown when the strip is hidden, so the toggle is discoverable rather than a secret.")]
        [SerializeField] private TMP_Text turnOrderHiddenHint;

        [Tooltip("How many upcoming turns to list before trailing off.")]
        [Min(1)][SerializeField] private int turnOrderLength = 6;

        [Header("Input")]
        [SerializeField] private InputActionAsset playerControls;
        [SerializeField] private string actionMapName = "Menu";
        [SerializeField] private string toggleActionName = "ToggleTurnOrder";

        [Tooltip("Everything that should vanish outside combat.")]
        [SerializeField] private CanvasGroup contentGroup;

        private static bool _turnOrderHidden;

        private readonly TurnOrderService _turnOrder = new();
        private InputActionMap _map;
        private InputAction _toggle;
        private bool _hooked;

        private static CombatService Combat => CombatFlowController.Current?.Combat;

        // ---- Lifecycle ----------------------------------------------------------------------------

        private void OnEnable()
        {
            HookInput(true);
            ApplyTurnOrderVisibility();
            Refresh();
        }

        private void OnDisable() => HookInput(false);

        private void LateUpdate() => Refresh();

        // ---- Rendering ----------------------------------------------------------------------------

        private void Refresh()
        {
            var combat = Combat;
            var battle = combat != null && combat.IsInBattle ? combat.CurrentBattle : null;

            // Persistent canvas: exists out of combat, so must take itself off screen
            // rather than leaving a stale roster up during exploration.
            if (contentGroup != null) contentGroup.alpha = battle == null ? 0f : 1f;
            if (battle == null) return;

            RefreshParty(battle);
            RefreshEnemies(battle);
            RefreshTurnOrder(battle);
        }


        private void RefreshParty(BattleContext battle)
        {
            if (partyPanel == null) return;

            var vitals = new List<CombatantVitals>(battle.partyCombatants.Count);
            for (int i = 0; i < battle.partyCombatants.Count; i++)
                vitals.Add(ToVitals(battle.partyCombatants[i]));

            partyPanel.Bind(vitals);
        }

        private static CombatantVitals ToVitals(CombatantInstance c) => new()
        {
            displayName = c.displayName,
            hp = c.currentHP,
            maxHP = c.stats.GetFinal(StatType.MaxHP),
            mp = c.currentMP,
            maxMP = c.stats.GetFinal(StatType.MaxMP),
            sp = c.currentSP,
            maxSP = c.stats.GetFinal(StatType.MaxSP),
            isDefeated = c.IsDefeated,
            portrait = null,
        };

        /// <summary>
        /// What the banner shows is driven by what the pending action is about to hit, not by the roster:
        ///
        /// <list type="bullet">
        /// <item>a single-target enemy action shows <b>only the enemy under the cursor</b>;</item>
        /// <item>an All/Random action shows <b>every living enemy, stacked</b>, because they are all
        /// about to be hit and the player needs to weigh the whole set;</item>
        /// <item>a Self or ally-targeted action <b>hides the banner</b> — no enemy is involved, so naming
        /// one would be misleading.</item>
        /// </list>
        ///
        /// <para>Defeated enemies are never listed: they cannot be targeted, so advertising them
        /// implies otherwise.</para>
        /// </summary>
        private void RefreshEnemies(BattleContext battle)
        {
            if (enemyBanner == null) return;

            var rule = PendingTargetRule();
            var mode = ResolveBannerMode(rule);

            if (mode == EnemyBannerMode.Hidden) { enemyBanner.text = string.Empty; return; }

            var data = AppContext.Data as DataRegistry;

            if (mode == EnemyBannerMode.All)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < battle.enemyCombatants.Count; i++)
                {
                    var e = battle.enemyCombatants[i];
                    if (e.IsDefeated) continue;

                    sb.AppendLine(Describe(e, data, emphasised: true));
                }

                enemyBanner.text = sb.ToString().TrimEnd();
                return;
            }

            var focus = FocusedEnemy(battle);
            enemyBanner.text = focus == null ? string.Empty : Describe(focus, data, emphasised: false);
        }

        private enum EnemyBannerMode { Single, All, Hidden }

        /// <summary>Target rule of the action being aimed, or null when nothing is pending.</summary>
        private static TargetRule PendingTargetRule()
        {
            var actionId = CombatFlowController.Current?.PendingActionId;
            if (string.IsNullOrEmpty(actionId)) return null;

            return AppContext.Data is DataRegistry data && data.TryGet<CombatActionData>(actionId, out var a)
                ? a?.targetRule
                : null;
        }

        private static EnemyBannerMode ResolveBannerMode(TargetRule rule)
        {
            // Nothing aimed yet: the default view is a single enemy.
            if (rule == null) return EnemyBannerMode.Single;

            if (rule.selectionMode == TargetSelectionMode.Self) return EnemyBannerMode.Hidden;
            if (rule.team == TargetTeam.Allies) return EnemyBannerMode.Hidden;

            if (rule.selectionMode == TargetSelectionMode.All
                || rule.selectionMode == TargetSelectionMode.Random) return EnemyBannerMode.All;

            return EnemyBannerMode.Single;
        }

        /// <summary>
        /// The enemy the banner speaks for: whatever the cursor is over, else the committed target, else
        /// the first living enemy so the banner is never blank at the start of a turn.
        /// </summary>
        private static CombatantInstance FocusedEnemy(BattleContext battle)
        {
            var flow = CombatFlowController.Current;

            var byCursor = Living(battle, flow?.HighlightedTargetId);
            if (byCursor != null) return byCursor;

            if (flow != null)
                for (int i = 0; i < flow.PendingTargetIds.Count; i++)
                {
                    var committed = Living(battle, flow.PendingTargetIds[i]);
                    if (committed != null) return committed;
                }

            for (int i = 0; i < battle.enemyCombatants.Count; i++)
                if (!battle.enemyCombatants[i].IsDefeated) return battle.enemyCombatants[i];

            return null;
        }

        /// <summary>Resolves an id to a living enemy; ally and defeated ids yield null.</summary>
        private static CombatantInstance Living(BattleContext battle, string combatantId)
        {
            if (string.IsNullOrEmpty(combatantId)) return null;

            var c = battle.FindCombatant(combatantId);
            return c != null && !c.IsDefeated && c.team == CombatantTeam.Enemy ? c : null;
        }

        private static string Describe(CombatantInstance e, DataRegistry data, bool emphasised)
        {
            var sb = new StringBuilder();

            sb.Append(emphasised ? $"<b>{e.displayName}</b>" : e.displayName);
            sb.Append("   Lv ").Append(e.level);

            var statuses = DescribeStatuses(e, data);
            if (!string.IsNullOrEmpty(statuses)) sb.Append("   ").Append(statuses);

            return sb.ToString();
        }

        /// <summary>
        /// Status names for a combatant, authored where available. Falls back to the raw id rather than
        /// hiding an effect that is genuinely on the combatant.
        /// </summary>
        private static string DescribeStatuses(CombatantInstance c, DataRegistry data)
        {
            if (c.activeStatuses == null || c.activeStatuses.Count == 0) return null;

            var parts = new List<string>(c.activeStatuses.Count);
            for (int i = 0; i < c.activeStatuses.Count; i++)
            {
                var id = c.activeStatuses[i]?.statusId;
                if (string.IsNullOrEmpty(id)) continue;

                parts.Add(data != null && data.TryGet<StatusEffectData>(id, out var sd) && sd != null
                          && !string.IsNullOrEmpty(sd.displayName)
                    ? sd.displayName
                    : id);
            }

            return parts.Count == 0 ? null : "[" + string.Join(", ", parts) + "]";
        }

        /// <summary>
        /// Renders <c>[current actor] + what remains of this round + next round's projection</c>.
        ///
        /// <para>The live queue alone is not enough: it is consumed as the round advances, so late in a
        /// round the strip would shrink to nothing and then jump. <see cref="TurnOrderService.ProjectNextRound"/>
        /// supplies the continuation.</para>
        /// </summary>
        private void RefreshTurnOrder(BattleContext battle)
        {
            if (_turnOrderHidden || turnOrderLabel == null) return;

            var order = new List<string>(turnOrderLength);

            if (battle.currentActor != null && !battle.currentActor.IsDefeated)
                order.Add(battle.currentActor.combatantId);

            foreach (var id in battle.turnQueue)
            {
                if (order.Count >= turnOrderLength) break;
                order.Add(id);
            }

            bool wrapped = false;
            if (order.Count < turnOrderLength)
            {
                foreach (var id in _turnOrder.ProjectNextRound(battle))
                {
                    if (order.Count >= turnOrderLength) break;
                    if (!wrapped) { wrapped = true; order.Add(null); }   // null marks the round break
                    order.Add(id);
                }
            }

            var sb = new StringBuilder();
            for (int i = 0; i < order.Count; i++)
            {
                if (order[i] == null) { sb.Append("  •  "); continue; }

                var c = battle.FindCombatant(order[i]);
                if (c == null) continue;

                if (sb.Length > 0 && !sb.ToString().EndsWith("  ")) sb.Append("  ›  ");

                // The actor whose turn it is reads as the head of the strip, not as one of the queue.
                bool isCurrent = i == 0 && battle.currentActor != null && c == battle.currentActor;
                sb.Append(isCurrent ? $"<b>{c.displayName}</b>" : c.displayName);
            }

            turnOrderLabel.text = sb.ToString();
        }

        // ---- Turn-order toggle --------------------------------------------------------------------

        private void ApplyTurnOrderVisibility()
        {
            if (turnOrderRoot != null) turnOrderRoot.SetActive(!_turnOrderHidden);
            if (turnOrderHiddenHint != null) turnOrderHiddenHint.gameObject.SetActive(_turnOrderHidden);
        }

        private void OnToggle(InputAction.CallbackContext ctx)
        {
            _turnOrderHidden = !_turnOrderHidden;
            ApplyTurnOrderVisibility();
        }

        private void HookInput(bool subscribe)
        {
            if (playerControls == null) return;

            if (_map == null)
            {
                _map = playerControls.FindActionMap(actionMapName, false);
                if (_map == null) return;
                _toggle = _map.FindAction(toggleActionName, false);

                if (_toggle == null)
                    Debug.LogWarning($"[JRPG.Combat.UI] Menu map has no '{toggleActionName}' — the turn-order strip cannot be hidden.");
            }

            if (_toggle == null) return;

            if (subscribe)
            {
                if (_hooked) return;
                _toggle.performed += OnToggle;
                _hooked = true;

                // No Acquire here: the command HUD owns the map's enabled state. This only listens, so
                // taking a second reference would keep the Menu map alive after the HUD closes.
            }
            else
            {
                if (!_hooked) return;
                _toggle.performed -= OnToggle;
                _hooked = false;
            }
        }
    }
}
