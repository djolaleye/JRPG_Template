using System.Collections.Generic;
using UnityEngine;
using JRPG.Data;

namespace JRPG.Combat
{
    /// Owns status application, timing, and expiry. All status mutation goes through here so the rules
    /// (immunity, stacking, modifier bookkeeping, removal) live in exactly one place.
    ///
    ///
    /// Statuses are strictly battle-local: ClearAll runs at battle end and nothing
    /// is serialized.
    public sealed class StatusProcessor
    {
        private readonly DataRegistry _data;

        public StatusProcessor(DataRegistry data)
        {
            _data = data;
        }

        // ---- Application / removal ----------------------------------------------------------

        /// Applies a status, honouring immunity and the authored stack rule.
        public bool TryApply(CombatantInstance target, string statusId, string applierCombatantId,
            int durationOverride, int stacksToAdd, out string reason)
        {
            reason = null;
            
            if (target == null || string.IsNullOrEmpty(statusId)) { reason = "No target or status."; return false; }
            if (!_data.TryGet<StatusEffectData>(statusId, out var status) || status == null)
            {
                reason = $"Unknown status '{statusId}'.";
                return false;
            }
            if (target.profile != null && target.profile.IsImmuneToStatus(statusId))
            {
                reason = $"{target.displayName} is immune to {statusId}.";
                return false;
            }

            int duration = durationOverride > 0 ? durationOverride : status.defaultDuration;
            var existing = Find(target, statusId);

            if (existing != null)
            {
                switch (status.stackRule)
                {
                    case StatusStackRule.Ignore:
                        reason = "Already afflicted.";
                        return false;

                    case StatusStackRule.Refresh:
                        existing.remaining = Mathf.Max(existing.remaining, duration);
                        return true;

                    case StatusStackRule.Stack:
                        int newStacks = Mathf.Min(status.maxStacks, existing.stacks + Mathf.Max(1, stacksToAdd));
                        bool changed = newStacks != existing.stacks;

                        existing.stacks = newStacks;
                        existing.remaining = Mathf.Max(existing.remaining, duration);

                        if (changed) ReapplyModifiers(target, status, existing);

                        return true;

                    case StatusStackRule.Replace:
                        Remove(target, statusId);
                        break;
                }
            }

            var instance = new StatusEffectInstance
            {
                statusId = statusId,
                applierCombatantId = applierCombatantId,
                modifierSourceId = StatusEffectInstance.MakeSourceId(target.combatantId, statusId),
                durationType = status.durationType,
                remaining = duration,
                stacks = Mathf.Clamp(stacksToAdd, 1, status.maxStacks),
            };

            target.activeStatuses.Add(instance);
            ReapplyModifiers(target, status, instance);

            // OnApply timing fires immediately.
            if (status.HasTiming(StatusTiming.OnApply)) Tick(target, status, instance, null);

            return true;
        }

        public bool Remove(CombatantInstance target, string statusId)
        {
            var instance = Find(target, statusId);
            if (instance == null) return false;

            target.stats.RemoveModifiersFrom(instance.modifierSourceId);
            target.activeStatuses.Remove(instance);
            ClampResources(target);

            return true;
        }

        /// Removes every status in a dispel category (cleanse/dispel effects).
        public int RemoveByCategory(CombatantInstance target, StatusDispelCategory category)
        {
            int removed = 0;
            for (int i = target.activeStatuses.Count - 1; i >= 0; i--)
            {
                var inst = target.activeStatuses[i];

                if (!_data.TryGet<StatusEffectData>(inst.statusId, out var s) || s == null) continue;
                if (s.dispelCategory != category) continue;

                target.stats.RemoveModifiersFrom(inst.modifierSourceId);
                target.activeStatuses.RemoveAt(i);
                removed++;
            }
            if (removed > 0) ClampResources(target);

            return removed;
        }

        public void ClearAll(CombatantInstance target)
        {
            for (int i = 0; i < target.activeStatuses.Count; i++)
                target.stats.RemoveModifiersFrom(target.activeStatuses[i].modifierSourceId);
            
            target.activeStatuses.Clear();
            ClampResources(target);
        }

        public void ClearAll(BattleContext battle)
        {
            if (battle == null) return;

            foreach (var c in battle.AllCombatants()) ClearAll(c);
        }

        // ---- Timing -------------------------------------------------------------------------

        public void Process(StatusTiming timing, CombatantInstance combatant, ActionResult result = null)
        {
            if (combatant == null || combatant.activeStatuses.Count == 0) return;

            // Iterate a copy: ticks can defeat the combatant and removal mutates the list.
            var snapshot = new List<StatusEffectInstance>(combatant.activeStatuses);

            for (int i = 0; i < snapshot.Count; i++)
            {
                var inst = snapshot[i];
                if (!_data.TryGet<StatusEffectData>(inst.statusId, out var status) || status == null) continue;
                if (!status.HasTiming(timing)) continue;

                Tick(combatant, status, inst, result);
            }

            // Duration bookkeeping happens on the combatant's own turn end (Turns) or at round end.
            if (timing == StatusTiming.TurnEnd || timing == StatusTiming.RoundEnd)
            {
                for (int i = combatant.activeStatuses.Count - 1; i >= 0; i--)
                {
                    var inst = combatant.activeStatuses[i];
                    bool counts = (timing == StatusTiming.TurnEnd && inst.durationType == StatusDurationType.Turns)
                               || (timing == StatusTiming.RoundEnd && inst.durationType == StatusDurationType.Rounds);
                    if (!counts) continue;

                    inst.remaining--;
                    if (!inst.IsExpired) continue;

                    combatant.stats.RemoveModifiersFrom(inst.modifierSourceId);
                    combatant.activeStatuses.RemoveAt(i);

                    result?.effects.Add(new EffectResult
                    {
                        targetCombatantId = combatant.combatantId,
                        effectType = "StatusExpired",
                        statusId = inst.statusId,
                        hpBefore = combatant.currentHP,
                        hpAfter = combatant.currentHP,
                    });
                }

                ClampResources(combatant);
            }
        }

        public void ProcessAll(StatusTiming timing, BattleContext battle, ActionResult result = null)
        {
            if (battle == null) return;

            foreach (var c in battle.AllCombatants()) Process(timing, c, result);
        }

        public void OnDamaged(CombatantInstance target, ActionResult result = null)
        {
            if (target == null) return;

            for (int i = target.activeStatuses.Count - 1; i >= 0; i--)
            {
                var inst = target.activeStatuses[i];
                if (!_data.TryGet<StatusEffectData>(inst.statusId, out var s) || s == null || !s.removeOnDamage) continue;
                
                target.stats.RemoveModifiersFrom(inst.modifierSourceId);
                target.activeStatuses.RemoveAt(i);

                result?.effects.Add(new EffectResult
                {
                    targetCombatantId = target.combatantId,
                    effectType = "StatusRemoved",
                    statusId = inst.statusId,
                });
            }
        }

        // ---- Queries used by the damage pipeline and submission validation -------------------

        public bool Has(CombatantInstance c, string statusId) => Find(c, statusId) != null;

        /// True when a status forbids this action.
        public bool IsActionBlocked(CombatantInstance c, CombatActionData action, out string reason)
        {
            reason = null;
            if (c == null || action == null) return false;

            for (int i = 0; i < c.activeStatuses.Count; i++)
            {
                if (!_data.TryGet<StatusEffectData>(c.activeStatuses[i].statusId, out var s) || s == null) continue;
                switch (s.actionRestriction)
                {
                    case StatusActionRestriction.AllActions:
                        reason = $"{c.displayName} cannot act ({s.displayName}).";
                        return true;
                    case StatusActionRestriction.MagicOnly:
                        if (action.category == CombatActionCategory.Skill)
                        {
                            reason = $"{c.displayName} cannot use skills ({s.displayName}).";
                            return true;
                        }
                        break;
                    case StatusActionRestriction.PhysicalOnly:
                        if (action.category == CombatActionCategory.Melee)
                        {
                            reason = $"{c.displayName} cannot attack ({s.displayName}).";
                            return true;
                        }
                        break;
                    case StatusActionRestriction.ItemsOnly:
                        if (action.category != CombatActionCategory.Item)
                        {
                            reason = $"{c.displayName} may only use items ({s.displayName}).";
                            return true;
                        }
                        break;
                }
            }

            return false;
        }

        public float GetDamageDealtMultiplier(CombatantInstance c) => Aggregate(c, s => s.damageDealtMultiplier, multiply: true);
        public float GetDamageTakenMultiplier(CombatantInstance c) => Aggregate(c, s => s.damageTakenMultiplier, multiply: true);
        public float GetAccuracyModifier(CombatantInstance c) => Aggregate(c, s => s.accuracyModifier, multiply: false);
        public float GetEvasionModifier(CombatantInstance c) => Aggregate(c, s => s.evasionModifier, multiply: false);
        public float GetCritChanceModifier(CombatantInstance c) => Aggregate(c, s => s.critChanceModifier, multiply: false);

        // ---- Internals ----------------------------------------------------------------------

        private float Aggregate(CombatantInstance c, System.Func<StatusEffectData, float> selector, bool multiply)
        {
            float total = multiply ? 1f : 0f;
            if (c == null) return total;

            for (int i = 0; i < c.activeStatuses.Count; i++)
            {
                if (!_data.TryGet<StatusEffectData>(c.activeStatuses[i].statusId, out var s) || s == null) continue;
                
                float v = selector(s);
                // Stacks scale the contribution: multiplicative rules compound, additive ones add per stack.
                int stacks = Mathf.Max(1, c.activeStatuses[i].stacks);

                if (multiply) for (int k = 0; k < stacks; k++) total *= v;

                else total += v * stacks;
            }

            return total;
        }

        private StatusEffectInstance Find(CombatantInstance c, string statusId)
        {
            if (c == null) return null;

            for (int i = 0; i < c.activeStatuses.Count; i++)
                if (c.activeStatuses[i].statusId == statusId) return c.activeStatuses[i];
            
            return null;
        }

        private static void ReapplyModifiers(CombatantInstance target, StatusEffectData status, StatusEffectInstance inst)
        {
            target.stats.RemoveModifiersFrom(inst.modifierSourceId);

            for (int i = 0; i < status.statModifiers.Count; i++)
            {
                var m = status.statModifiers[i];
                target.stats.AddModifier(new StatModifier(
                    m.stat, m.modifierType, m.value * inst.stacks, inst.modifierSourceId, isPermanent: false));
            }

            target.stats.Recalculate();
            ClampResources(target);
        }

        private void Tick(CombatantInstance c, StatusEffectData status, StatusEffectInstance inst, ActionResult result)
        {
            int amount = status.tickAmount;

            if (status.tickPercentOfMax > 0f)
                amount += Mathf.RoundToInt(c.stats.GetFinal(StatType.MaxHP) * status.tickPercentOfMax);
            
            amount *= Mathf.Max(1, inst.stacks);
            
            if (amount == 0) return;

            int before = c.currentHP;
            switch (status.tickResource)
            {
                case CombatResource.HP:
                    c.currentHP = Mathf.Clamp(c.currentHP - amount, 0, c.MaxHP);
                    break;
                case CombatResource.MP:
                    c.currentMP = Mathf.Clamp(c.currentMP - amount, 0, c.MaxMP);
                    break;
                case CombatResource.SP:
                    c.currentSP = Mathf.Clamp(c.currentSP - amount, 0, c.MaxSP);
                    break;
            }

            result?.effects.Add(new EffectResult
            {
                targetCombatantId = c.combatantId,
                effectType = "StatusTick",
                statusId = inst.statusId,
                amount = Mathf.Abs(amount),
                hpBefore = before,
                hpAfter = c.currentHP,
                wasDefeated = c.IsDefeated,
                element = status.tickElement,
            });

            if (c.IsDefeated && result != null && !result.defeatedCombatantIds.Contains(c.combatantId))
                result.defeatedCombatantIds.Add(c.combatantId);
        }

        private static void ClampResources(CombatantInstance c)
        {
            c.currentHP = Mathf.Clamp(c.currentHP, 0, c.MaxHP);
            c.currentMP = Mathf.Clamp(c.currentMP, 0, c.MaxMP);
            c.currentSP = Mathf.Clamp(c.currentSP, 0, c.MaxSP);
        }
    }
}
