using System.Collections.Generic;
using UnityEngine;
using JRPG.Data;
using JRPG.Services;

namespace JRPG.Combat
{
    /// The only place that executes actions. Validates, pays costs, and applies effects against
    /// runtime combatants. It does not publish events, evaluate win/loss, or advance turns — that
    /// orchestration belongs to CombatService.
    public sealed class CombatActionResolver
    {
        private readonly IInventoryService _inventory;

        public CombatActionResolver(IInventoryService inventory)
        {
            _inventory = inventory;
        }

        public bool CanPayCosts(CombatantInstance user, CombatActionData action, out string reason)
        {
            reason = null;
            if (action.costs == null) return true;

            for (int i = 0; i < action.costs.Count; i++)
            {
                var cost = action.costs[i];
                switch (cost.type)
                {
                    case CombatCostType.MP:
                        if (user.currentMP < cost.mpAmount)
                        {
                            reason = $"Not enough MP ({user.currentMP}/{cost.mpAmount}).";
                            return false;
                        }
                        break;
                    case CombatCostType.Item:
                        int need = Mathf.Max(1, cost.quantity);
                        if (_inventory == null || !_inventory.Has(cost.itemId, need))
                        {
                            reason = $"Missing item '{cost.itemId}' x{need}.";
                            return false;
                        }
                        break;
                }
            }
            return true;
        }

        /// Validate → pay costs → apply effects. Targets must already be resolved/validated by the
        /// caller (CombatService) against the action's TargetRule.
        public ActionResult Resolve(CombatantInstance actor, CombatActionData action, List<CombatantInstance> targets)
        {
            var result = new ActionResult
            {
                success = true,
                actorCombatantId = actor.combatantId,
                actionId = action.Id,
            };

            if (!CanPayCosts(actor, action, out var reason))
                return ActionResult.Fail(actor.combatantId, action.Id, reason);

            PayCosts(actor, action);

            if (action.effects != null)
            {
                for (int i = 0; i < action.effects.Count; i++)
                    ApplyEffect(actor, action.effects[i], action, targets, result);
            }

            actor.hasActedThisRound = true;
            return result;
        }

        private void PayCosts(CombatantInstance user, CombatActionData action)
        {
            if (action.costs == null) return;
            for (int i = 0; i < action.costs.Count; i++)
            {
                var cost = action.costs[i];
                switch (cost.type)
                {
                    case CombatCostType.MP:
                        user.currentMP = Mathf.Max(0, user.currentMP - cost.mpAmount);
                        break;
                    case CombatCostType.Item:
                        _inventory?.Remove(cost.itemId, Mathf.Max(1, cost.quantity));
                        break;
                }
            }
        }

        private static void ApplyEffect(CombatantInstance actor, CombatEffect effect, CombatActionData action,
            List<CombatantInstance> targets, ActionResult result)
        {
            switch (effect.type)
            {
                case CombatEffectType.Guard:
                    actor.isGuarding = true;
                    actor.guardDamageMultiplier = effect.guardMultiplier <= 0f ? 0.5f : effect.guardMultiplier;
                    result.effects.Add(new EffectResult
                    {
                        targetCombatantId = actor.combatantId,
                        effectType = nameof(CombatEffectType.Guard),
                        amount = 0,
                        hpBefore = actor.currentHP,
                        hpAfter = actor.currentHP,
                    });
                    break;

                case CombatEffectType.Damage:
                    for (int i = 0; i < targets.Count; i++)
                        ApplyDamage(actor, effect, targets[i], result);
                    break;

                case CombatEffectType.Heal:
                    for (int i = 0; i < targets.Count; i++)
                        ApplyHeal(actor, effect, targets[i], result);
                    break;
            }
        }

        private static void ApplyDamage(CombatantInstance actor, CombatEffect effect, CombatantInstance target, ActionResult result)
        {
            float raw = effect.basePower
                        + actor.stats.GetFinal(effect.attackStat) * effect.statScale
                        - target.stats.GetFinal(effect.defenseStat) * 0.5f;
            int final = Mathf.Max(1, Mathf.RoundToInt(raw));

            if (target.isGuarding)
                final = Mathf.Max(1, Mathf.RoundToInt(final * target.guardDamageMultiplier));

            int before = target.currentHP;
            target.currentHP = Mathf.Max(0, target.currentHP - final);
            int after = target.currentHP;
            bool defeated = after <= 0;

            result.effects.Add(new EffectResult
            {
                targetCombatantId = target.combatantId,
                effectType = nameof(CombatEffectType.Damage),
                amount = final,
                hpBefore = before,
                hpAfter = after,
                wasDefeated = defeated,
            });

            if (defeated && !result.defeatedCombatantIds.Contains(target.combatantId))
                result.defeatedCombatantIds.Add(target.combatantId);
        }

        private static void ApplyHeal(CombatantInstance actor, CombatEffect effect, CombatantInstance target, ActionResult result)
        {
            int amount = Mathf.RoundToInt(effect.basePower + actor.stats.GetFinal(effect.scalingStat) * effect.statScale);
            int before = target.currentHP;
            target.currentHP = Mathf.Min(target.currentHP + amount, target.MaxHP);
            int after = target.currentHP;

            result.effects.Add(new EffectResult
            {
                targetCombatantId = target.combatantId,
                effectType = nameof(CombatEffectType.Heal),
                amount = after - before,
                hpBefore = before,
                hpAfter = after,
            });
        }
    }
}
