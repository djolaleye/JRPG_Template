using System;
using System.Collections.Generic;
using JRPG.Core;
using JRPG.Data;
using JRPG.Services;
using JRPG.Characters;
using JRPG.Save;

namespace JRPG.Inventory
{
    /// <summary>
    /// Single authority for equipped state. Applies/removes <see cref="StatModifier"/>s on
    /// <see cref="CharacterRuntimeInstance"/>s using a deterministic <c>sourceId</c> so unequip
    /// reverts exactly. Saving only persists slot→itemId; modifiers are re-applied on load by
    /// re-applying the stat changes against the restored characters.
    /// </summary>
    public sealed class EquipmentManager : IEquipmentService, ISaveable
    {
        private readonly DataRegistry _registry;
        private readonly InventoryContainer _container;
        private readonly IEventBus _bus;
        private readonly Func<string, CharacterRuntimeInstance> _resolveInstance;
        private readonly Dictionary<string, EquipmentRuntimeState> _byChar = new();

        public string SaveKey => "equipment";

        public EquipmentManager(DataRegistry registry,
                                InventoryContainer container,
                                IEventBus bus,
                                Func<string, CharacterRuntimeInstance> resolveInstance)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _resolveInstance = resolveInstance ?? throw new ArgumentNullException(nameof(resolveInstance));
        }

        public IReadOnlyDictionary<string, EquipmentRuntimeState> All => _byChar;

        public EquipmentRuntimeState GetOrCreate(string charInstanceId)
        {
            if (!_byChar.TryGetValue(charInstanceId, out var currentState))
            {
                currentState = new EquipmentRuntimeState();
                _byChar[charInstanceId] = currentState;
            }
            return currentState;
        }

        /// <summary>
        /// Stat-modifier sourceId for a character/slot pair. Keyed by the character's stable data
        /// id (e.g. "char_hero") rather than the per-spawn runtime InstanceId, so equipment survives
        /// PartyService restoring its runtime instance cache during a Load.
        /// </summary>
        public static string MakeSourceId(string charId, EquipmentSlot slot)
            => $"equip:{charId}:{slot}";

        public bool Equip(CharacterRuntimeInstance target, string equipItemId, out string failureReason)
        {
            failureReason = null;
            if (target == null) { failureReason = "target null"; return false; }
            if (string.IsNullOrEmpty(equipItemId)) { failureReason = "itemId empty"; return false; }

            if (!_registry.TryGet<EquipmentData>(equipItemId, out var equip) || equip == null)
            {
                failureReason = "unknown equipment id";
                return false;
            }
            if (equip.allowedCharacterIds != null && equip.allowedCharacterIds.Count > 0
                && !equip.allowedCharacterIds.Contains(target.SourceDataId))
            {
                failureReason = "character not allowed";
                return false;
            }
            if (target.level < equip.requiredLevel)
            {
                failureReason = $"requires level {equip.requiredLevel}";
                return false;
            }
            if (_container.GetQuantity(equipItemId) <= 0)
            {
                failureReason = "not in inventory";
                return false;
            }

            // If slot occupied → prompt unequip first (returns the prior unit to inventory).
            var state = GetOrCreate(target.SourceDataId);
            if (state.slotToItemId.TryGetValue(equip.slot, out var prev) && !string.IsNullOrEmpty(prev))
            {
                if (!Unequip(target, equip.slot, out var ignore))
                {
                    failureReason = "couldn't free slot";
                    return false;
                }
            }

            _container.Remove(equipItemId, 1);
            state.slotToItemId[equip.slot] = equipItemId;

            var sourceId = MakeSourceId(target.SourceDataId, equip.slot);
            for (int i = 0; i < equip.statModifiers.Count; i++)
            {
                var m = equip.statModifiers[i];
                target.stats.AddModifier(new StatModifier(m.stat, m.modifierType, m.value, sourceId, false));
            }

            target.Recalculate();

            _bus.Publish(new EquipmentChanged(target.SourceDataId, equip.slot, equipItemId));
            return true;
        }

        public bool Unequip(CharacterRuntimeInstance target, EquipmentSlot slot, out string failureReason)
        {
            failureReason = null;
            if (target == null) { failureReason = "target null"; return false; }
            if (!_byChar.TryGetValue(target.SourceDataId, out var state)) return false;
            if (!state.slotToItemId.TryGetValue(slot, out var itemId) || string.IsNullOrEmpty(itemId)) return false;

            state.slotToItemId.Remove(slot);

            var sourceId = MakeSourceId(target.SourceDataId, slot);

            target.stats.RemoveModifiersFrom(sourceId);
            target.Recalculate();

            if (_registry.TryGet<EquipmentData>(itemId, out var equip) && equip != null)
                _container.Add(itemId, 1, equip);
            else
                _container.Add(itemId, 1, null);

            _bus.Publish(new EquipmentChanged(target.SourceDataId, slot, null));
            return true;
        }



        public string GetEquippedItemId(string charInstanceId, string slotName)
        {
            if (!_byChar.TryGetValue(charInstanceId, out var state)) return null;
            if (!Enum.TryParse(slotName, out EquipmentSlot slot)) return null;

            return state.slotToItemId.TryGetValue(slot, out var id) ? id : null;
        }



        public SaveDataBase CaptureState()
        {
            var dto = new EquipmentSaveData { version = SaveSystemCore.CurrentSaveVersion };

            foreach (var kv in _byChar)
            {
                var charEquipEntry = new CharacterEquipEntry { charInstanceId = kv.Key };
                foreach (var slotEnt in kv.Value.slotToItemId)
                {
                    if (string.IsNullOrEmpty(slotEnt.Value)) continue;
                    charEquipEntry.slots.Add(new SlotEntry { slot = slotEnt.Key, itemId = slotEnt.Value });
                }
                if (charEquipEntry.slots.Count > 0) dto.entries.Add(charEquipEntry);
            }

            return new EquipmentPayload { version = dto.version, data = dto };
        }

        public void RestoreState(SaveDataBase state)
        {
            if (state is not EquipmentPayload eqPayload || eqPayload.data == null) return;

            _byChar.Clear();

            for (int i = 0; i < eqPayload.data.entries.Count; i++)
            {
                var entry = eqPayload.data.entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.charInstanceId)) continue;

                var instance = _resolveInstance(entry.charInstanceId);
                if (instance == null) continue;

                var rtState = GetOrCreate(entry.charInstanceId);
                for (int j = 0; j < entry.slots.Count; j++)
                {
                    var slotEnt = entry.slots[j];
                    if (string.IsNullOrEmpty(slotEnt.itemId)) continue;
                    if (!_registry.TryGet<EquipmentData>(slotEnt.itemId, out var equip) || equip == null) continue;

                    rtState.slotToItemId[slotEnt.slot] = slotEnt.itemId;

                    var sourceId = MakeSourceId(entry.charInstanceId, slotEnt.slot);

                    for (int k = 0; k < equip.statModifiers.Count; k++)
                    {
                        var mod = equip.statModifiers[k];
                        instance.stats.AddModifier(new StatModifier(mod.stat, mod.modifierType, mod.value, sourceId, false));
                    }
                }
                instance.Recalculate();
            }
        }
    }
}
