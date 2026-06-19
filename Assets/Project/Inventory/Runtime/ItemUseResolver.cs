using System;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Characters;

namespace JRPG.Inventory
{
    /// <summary>
    /// Minimal prototype-grade item use: Heal / RestoreMP / RestoreSP. Clamps to runtime maxima.
    /// When combat lands, the resolver swaps to the full CombatEffectData pipeline while keeping
    /// the same <see cref="ItemUsed"/> event so listeners don't change.
    /// </summary>
    public sealed class ItemUseResolver
    {
        private readonly DataRegistry _registry;
        private readonly InventoryContainer _container;
        private readonly IEventBus _bus;

        public ItemUseResolver(DataRegistry registry, InventoryContainer container, IEventBus bus)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        public bool TryUse(string itemId, CharacterRuntimeInstance target, LayeredState state, out string failureReason)
        {
            failureReason = null;

            if (string.IsNullOrEmpty(itemId)) { failureReason = "itemId empty"; return false; }
            if (target == null) { failureReason = "target null"; return false; }
            if (!_registry.TryGet<ItemData>(itemId, out var item) || item == null) { failureReason = "unknown itemId"; return false; }

            if (_container.GetQuantity(itemId) <= 0) { failureReason = "out of stock"; return false; }

            var rule = item.usageRule;
            if (rule != null)
            {
                if (state.Mode == GameMode.Combat && !rule.usableInCombat) { failureReason = "not usable in combat"; return false; }
                if (state.Mode == GameMode.Exploration && !rule.usableInExploration) { failureReason = "not usable in exploration"; return false; }
                if (rule.targetLivingAlliesOnly && target.currentHP <= 0) { failureReason = "target is downed"; return false; }
            }

            // Apply effects.
            for (int i = 0; i < item.linkedEffects.Count; i++)
            {
                var fx = item.linkedEffects[i];
                ApplyEffect(target, fx);
            }
            
            target.Recalculate();

            if (rule == null || rule.consumedOnUse)
                _container.Remove(itemId, 1);

            _bus.Publish(new ItemUsed(itemId, target.InstanceId));
            return true;
        }

        private static void ApplyEffect(CharacterRuntimeInstance target, ItemEffect fx)
        {
            switch (fx.type)
            {
                case ItemEffectType.Heal:
                {
                    int max = target.stats.GetFinal(StatType.MaxHP);
                    target.currentHP = Mathf.Clamp(target.currentHP + fx.amount, 0, max);
                    break;
                }
                case ItemEffectType.RestoreMP:
                {
                    int max = target.stats.GetFinal(StatType.MaxMP);
                    target.currentMP = Mathf.Clamp(target.currentMP + fx.amount, 0, max);
                    break;
                }
                case ItemEffectType.RestoreSP:
                {
                    int max = target.stats.GetFinal(StatType.MaxSP);
                    target.currentSP = Mathf.Clamp(target.currentSP + fx.amount, 0, max);
                    break;
                }
            }
        }
    }
}
