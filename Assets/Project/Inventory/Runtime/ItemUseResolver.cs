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

        /// <summary>
        /// Non-mutating form of the <see cref="TryUse"/> validation ladder: may this item be used on this
        /// target, right now?
        ///
        /// <para>Extracted so UI can grey a row and say why without re-deriving the rules — the reason
        /// strings here are the ones the player sees. <see cref="TryUse"/> runs the same check, so a row
        /// that somehow slips through still cannot execute.</para>
        /// </summary>
        public bool CanUse(string itemId, CharacterRuntimeInstance target, LayeredState state, out string reason)
        {
            reason = null;

            if (string.IsNullOrEmpty(itemId)) { reason = "No item."; return false; }
            if (!_registry.TryGet<ItemData>(itemId, out var item) || item == null) { reason = "Unknown item."; return false; }
            if (_container.GetQuantity(itemId) <= 0) { reason = "None left."; return false; }

            if (item.linkedEffects == null || item.linkedEffects.Count == 0)
            {
                reason = item.category switch
                {
                    ItemCategory.Equipment => "Equip this from the equipment screen.",
                    ItemCategory.KeyItem => "Key item — used automatically.",
                    ItemCategory.QuestItem => "Quest item.",
                    ItemCategory.Material => "Crafting material.",
                    _ => "Nothing happens.",
                };
                return false;
            }

            var rule = item.usageRule;
            if (rule != null)
            {
                if (state.Mode == GameMode.Combat && !rule.usableInCombat) { reason = "Can't use in battle."; return false; }
                if (state.Mode == GameMode.Exploration && !rule.usableInExploration) { reason = "Can't use here."; return false; }
                if (rule.targetLivingAlliesOnly && target != null && target.currentHP <= 0) { reason = "Target is down."; return false; }
            }

            return true;
        }

        public bool TryUse(string itemId, CharacterRuntimeInstance target, LayeredState state, out string failureReason)
        {
            failureReason = null;

            if (target == null) { failureReason = "target null"; return false; }
            if (!CanUse(itemId, target, state, out failureReason)) return false;

            _registry.TryGet<ItemData>(itemId, out var item);
            var rule = item.usageRule;

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
