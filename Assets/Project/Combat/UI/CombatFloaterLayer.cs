using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;

namespace JRPG.Combat.UI
{
    /// <summary>
    /// Floating damage / heal / miss numbers, driven by <see cref="BattleEffectsResolved"/>.
    ///
    /// <para>Every field the resolver fills is surfaced: amount, critical, missed, absorbed, immune and
    /// element. Before this existed the pipeline computed all of it and the player saw a number change
    /// on a bar, if that.</para>
    ///
    /// <para><b>Only effects that moved HP get a floater</b> — see <see cref="ShouldFloat"/>. Guard and
    /// status-only actions have no number to show, and floating their zero was noise.</para>
    ///
    /// <para><b>Each floater carries the target's resulting HP</b> (<c>hpAfter / maxHP</c>). The number
    /// alone says how hard the hit landed; the pair says whether it mattered — and it is the only place
    /// enemy HP is shown outside the target list.</para>
    ///
    /// <para><b>Positioning is HUD-relative, not world-relative.</b> Combatants have no scene transforms
    /// in this project — battles resolve against data, not against actors in a field — so a floater is
    /// anchored to the element that represents its combatant: the party member's status widget, or the
    /// enemy's slot along the enemy row. If combatant actors are added later, only
    /// <see cref="CombatHudView.AnchorFor"/> has to change.</para>
    /// </summary>
    public sealed class CombatFloaterLayer : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Parent for spawned floaters. Should span the screen.")]
        [SerializeField] private RectTransform floaterRoot;

        [Tooltip("Inactive TMP_Text cloned per floater.")]
        [SerializeField] private TMP_Text floaterTemplate;

        [Header("Motion")]
        [Min(0.05f)][SerializeField] private float lifetime = 0.9f;
        [SerializeField] private float riseDistance = 90f;

        [Tooltip("Seconds between floaters when one action hits several targets, so a multi-hit reads " +
                 "as a sequence instead of one overlapping smear.")]
        [Min(0f)][SerializeField] private float staggerSeconds = 0.08f;

        [Header("Colours")]
        [SerializeField] private Color damageColor = new(1f, 0.94f, 0.90f);
        [SerializeField] private Color criticalColor = new(1f, 0.80f, 0.25f);
        [SerializeField] private Color healColor = new(0.45f, 1f, 0.62f);
        [SerializeField] private Color missColor = new(0.78f, 0.80f, 0.88f);
        [SerializeField] private Color immuneColor = new(0.62f, 0.72f, 0.95f);

        [Header("Sizes")]
        [SerializeField] private float baseFontSize = 44f;
        [SerializeField] private float criticalFontSize = 60f;

        private IEventBus _bus;
        private readonly List<TMP_Text> _pool = new();

        private void OnEnable()
        {
            _bus = AppContext.Bus;
            _bus?.Subscribe<BattleEffectsResolved>(OnEffects);

            if (floaterTemplate != null) floaterTemplate.gameObject.SetActive(false);
        }

        private void OnDisable() => _bus?.Unsubscribe<BattleEffectsResolved>(OnEffects);

        /// <summary>
        /// Fires for party and enemy actions alike — enemy turns resolve through the same
        /// <c>SubmitAction</c>, so an enemy hitting the party floats a number exactly as the reverse does.
        /// </summary>
        private void OnEffects(BattleEffectsResolved e)
        {
            if (!e.Success || e.Effects == null || e.Effects.Count == 0) return;
            if (!isActiveAndEnabled) return;

            StartCoroutine(SpawnSequence(e));
        }

        private IEnumerator SpawnSequence(BattleEffectsResolved e)
        {
            var battle = CombatFlowController.Current?.Combat?.CurrentBattle;

            for (int i = 0; i < e.Effects.Count; i++)
            {
                if (!ShouldFloat(e.Effects[i])) continue;

                Spawn(e.Effects[i], battle);

                // The stagger belongs between two floaters that actually appear; pacing it off the raw
                // effect index would leave a gap wherever a suppressed effect used to be.
                if (staggerSeconds > 0f && HasLaterFloater(e, i))
                    yield return new WaitForSeconds(staggerSeconds);
            }
        }

        /// <summary>
        /// Whether an effect has a number worth floating.
        ///
        /// <para><b>Decided by HP movement, not by effect type.</b> Guard, status application, stat
        /// buffs and debuffs all resolve to an <c>amount</c> of zero, and floating "0" over a target
        /// says nothing except that something happened. The alternative — listing the effect types to
        /// suppress — would need every one of <c>StatusTick</c>, <c>PassiveRegen</c>,
        /// <c>StatusExpired</c>, <c>EscapeFailed</c> and friends enumerated, since
        /// <see cref="EffectResult.effectType"/> is a free-form tag rather than the enum, and would
        /// silently miss the next tag someone adds.</para>
        ///
        /// <para>A miss or an immunity is HP-neutral but still reported: those are the outcome of an
        /// attack, and saying nothing would be indistinguishable from the attack never happening.</para>
        /// </summary>
        private static bool ShouldFloat(EffectResult fx)
            => fx.hpAfter != fx.hpBefore || fx.missed || fx.immune;

        private static bool HasLaterFloater(BattleEffectsResolved e, int index)
        {
            for (int i = index + 1; i < e.Effects.Count; i++)
                if (ShouldFloat(e.Effects[i])) return true;

            return false;
        }

        private void Spawn(EffectResult fx, BattleContext battle)
        {
            if (floaterRoot == null || floaterTemplate == null) return;

            var target = battle?.FindCombatant(fx.targetCombatantId);

            var label = Rent();
            label.text = Compose(fx, target);
            label.color = ColorFor(fx);
            label.fontSize = fx.critical && !fx.missed ? criticalFontSize : baseFontSize;

            var rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = CombatHudView.Current != null
                ? CombatHudView.Current.AnchorFor(fx.targetCombatantId)
                : Vector2.zero;

            label.gameObject.SetActive(true);
            StartCoroutine(Animate(label, rt.anchoredPosition));
        }

        /// <summary>
        /// The floater's text: the headline outcome, then the target's resulting HP so the hit can be
        /// weighed rather than merely seen.
        /// </summary>
        private static string Compose(EffectResult fx, CombatantInstance target)
        {
            string headline;

            if (fx.missed) headline = "MISS";
            else if (fx.immune) headline = "IMMUNE";
            else if (fx.absorbed) headline = $"+{Mathf.Abs(fx.amount)} ABSORB";
            else if (IsHeal(fx)) headline = $"+{Mathf.Abs(fx.amount)}";
            else headline = fx.critical ? $"{Mathf.Abs(fx.amount)}!" : $"{Mathf.Abs(fx.amount)}";

            if (target == null) return headline;

            int maxHp = target.stats != null ? target.stats.GetFinal(StatType.MaxHP) : 0;
            int hp = Mathf.Max(0, fx.hpAfter);

            // A miss changes nothing, so repeating the unchanged HP would be noise.
            if (fx.missed) return headline;

            return $"{headline}\n<size=60%>{hp}/{maxHp}</size>";
        }

        /// <summary>
        /// Heals are identified by the effect moving HP upward rather than by the effect type string, so
        /// any future restorative effect floats correctly without being enumerated here.
        /// </summary>
        private static bool IsHeal(EffectResult fx) => fx.hpAfter > fx.hpBefore;

        private Color ColorFor(EffectResult fx)
        {
            if (fx.missed) return missColor;
            if (fx.immune) return immuneColor;
            if (fx.absorbed || IsHeal(fx)) return healColor;

            return fx.critical ? criticalColor : damageColor;
        }

        private IEnumerator Animate(TMP_Text label, Vector2 from)
        {
            float t = 0f;
            var rt = label.rectTransform;
            var baseColor = label.color;

            while (t < lifetime)
            {
                // Unscaled: battles can resolve while the rest of the game is paused.
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / lifetime);

                rt.anchoredPosition = from + new Vector2(0f, riseDistance * k);

                // Hold full opacity for the first half so the number is readable before it starts to go.
                var c = baseColor;
                c.a = k < 0.5f ? 1f : 1f - (k - 0.5f) * 2f;
                label.color = c;

                yield return null;
            }

            label.gameObject.SetActive(false);
        }

        /// <summary>Pooled: a busy round must not instantiate a label per hit.</summary>
        private TMP_Text Rent()
        {
            for (int i = 0; i < _pool.Count; i++)
                if (_pool[i] != null && !_pool[i].gameObject.activeSelf) return _pool[i];

            var clone = Instantiate(floaterTemplate, floaterRoot);
            clone.gameObject.SetActive(false);
            _pool.Add(clone);

            return clone;
        }
    }
}
