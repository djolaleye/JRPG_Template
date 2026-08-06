using System;
using System.Collections.Generic;
using JRPG.Core;
using JRPG.Data;
using JRPG.Services;
using JRPG.Characters;
using JRPG.Save;

namespace JRPG.Inventory
{
    /// <summary>One derived stat's before/after under a hypothetical equip. View model only.</summary>
    public struct EquipStatDelta
    {
        public StatType stat;
        public int before;
        public int after;

        public int Change => after - before;
    }

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

        /// <summary>
        /// Rewrites <see cref="CharacterRuntimeInstance.equippedItemIds"/> from the slot table, making
        /// this manager the single writer of both.
        ///
        /// <para><b>Reuired: </b><c>CombatantFactory.BuildCharacterProfile</c> reads
        /// <c>equippedItemIds</c>, so something must nothing wrote it to prevent a weapon's element affinities, status
        /// immunities, passives and <c>actionUnlockIds</c> from never reaching battle.</para>
        ///
        /// <para>Rebuilt wholesale rather than patched per-operation so the list cannot drift out of
        /// step with the slot table it mirrors.</para>
        /// </summary>
        private static void SyncEquippedIds(CharacterRuntimeInstance target, EquipmentRuntimeState state)
        {
            if (target == null) return;

            target.equippedItemIds ??= new List<string>();
            target.equippedItemIds.Clear();

            if (state == null) return;

            foreach (var kv in state.slotToItemId)
            {
                if (!string.IsNullOrEmpty(kv.Value)) target.equippedItemIds.Add(kv.Value);
            }
        }

        /// <summary>
        /// Non-mutating form of the <see cref="Equip"/> validation ladder: answers "would this equip
        /// succeed?" without changing any state, so equip screens can grey out rows and show the reason.
        /// <see cref="Equip"/> calls this so the checks (and their order) have exactly one definition.
        /// The only step not covered here is freeing an occupied slot, which is inherently a mutation
        /// and stays in <see cref="Equip"/>.
        /// </summary>
        public bool CanEquip(CharacterRuntimeInstance target, string equipItemId, out string failureReason)
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
                failureReason = $"{ResolveCharacterName(target)} can't equip this.";
                return false;
            }

            if (target.level < equip.requiredLevel)
            {
                failureReason = $"Requires level {equip.requiredLevel}.";
                return false;
            }

            // Respect the character's own equippable slots. Empty/absent list = no restriction
            if (_registry.TryGet<CharacterData>(target.SourceDataId, out var charData)
                && charData.allowedSlots != null && charData.allowedSlots.Count > 0
                && !charData.allowedSlots.Contains(equip.slot))
            {
                failureReason = $"{ResolveCharacterName(target)} has no {equip.slot} slot.";
                return false;
            }

            if (_container.GetQuantity(equipItemId) <= 0)
            {
                failureReason = "Not in inventory.";
                return false;
            }

            return true;
        }

        public bool Equip(CharacterRuntimeInstance target, string equipItemId, out string failureReason)
        {
            if (!CanEquip(target, equipItemId, out failureReason)) return false;

            // CanEquip already proved this resolves.
            _registry.TryGet<EquipmentData>(equipItemId, out var equip);

            // If slot occupied → prompt unequip first (returns the prior unit to inventory).
            var state = GetOrCreate(target.SourceDataId);
            if (state.slotToItemId.TryGetValue(equip.slot, out var prev) && !string.IsNullOrEmpty(prev))
            {
                if (!Unequip(target, equip.slot, out var ignore))
                {
                    failureReason = $"Couldn't unequip the current {equip.slot}.";
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
            SyncEquippedIds(target, state);

            _bus.Publish(new EquipmentChanged(target.SourceDataId, equip.slot, equipItemId));
            return true;
        }

        /// <summary>
        /// <see cref="Equip"/> for a caller that has only a character id — it resolves the instance
        /// through this manager's own lookup delegate.
        ///
        /// <para>Exists for authored/system-driven equips (starting equipment) where there is no screen
        /// holding a <see cref="CharacterRuntimeInstance"/>. Routes through <see cref="Equip"/> so the
        /// validation ladder, inventory bookkeeping and stat modifiers are identical to a player equip —
        /// no second path that could drift.</para>
        /// </summary>
        public bool EquipById(string charId, string equipItemId, out string failureReason)
        {
            failureReason = null;
            if (string.IsNullOrEmpty(charId)) { failureReason = "characterId empty"; return false; }

            var target = _resolveInstance(charId);
            if (target == null) { failureReason = $"No character '{charId}' in the roster."; return false; }

            return Equip(target, equipItemId, out failureReason);
        }

        public bool Unequip(CharacterRuntimeInstance target, EquipmentSlot slot, out string failureReason)
        {
            failureReason = null;
            if (target == null) { failureReason = "target null"; return false; }
            if (!_byChar.TryGetValue(target.SourceDataId, out var state)) { failureReason = $"{ResolveCharacterName(target)} has nothing equipped."; return false; }
            if (!state.slotToItemId.TryGetValue(slot, out var itemId) || string.IsNullOrEmpty(itemId)) { failureReason = $"{ResolveCharacterName(target)}'s {slot} slot is empty."; return false; }

            state.slotToItemId.Remove(slot);

            var sourceId = MakeSourceId(target.SourceDataId, slot);

            target.stats.RemoveModifiersFrom(sourceId);
            target.Recalculate();
            SyncEquippedIds(target, state);

            if (_registry.TryGet<EquipmentData>(itemId, out var equip) && equip != null)
                _container.Add(itemId, 1, equip);
            else
                _container.Add(itemId, 1, null);

            _bus.Publish(new EquipmentChanged(target.SourceDataId, slot, null));
            return true;
        }


        /// <summary>
        /// Returns to the constructor's condition: nothing equipped by anyone.
        ///
        /// Clearing <c>_byChar</c> alone is not enough. Equipping writes <c>equip:&lt;charId&gt;:&lt;slot&gt;</c>
        /// stat modifiers onto the live <see cref="CharacterRuntimeInstance"/>, and those live on the
        /// instance, not here — so this mirrors <see cref="Unequip"/>'s
        /// <c>RemoveModifiersFrom</c> + <c>Recalculate</c> for every recorded character/slot pair
        /// before dropping the table. Otherwise a stale bonus could ride into the new session on any
        /// instance still referenced elsewhere.
        ///
        ///  Run this before the party service drops its instance cache, so the modifier removal lands on
        /// the instances that are actually live.
        ///
        /// Idempotent and safe to call before anything has happened.
        /// </summary>
        public void ResetForNewGame()
        {
            foreach (var kv in _byChar)
            {
                var state = kv.Value;
                if (state == null || state.slotToItemId.Count == 0) continue;

                var instance = _resolveInstance(kv.Key);
                if (instance == null) continue;

                foreach (var slotEnt in state.slotToItemId)
                    instance.stats.RemoveModifiersFrom(MakeSourceId(kv.Key, slotEnt.Key));

                instance.Recalculate();

                SyncEquippedIds(instance, null);
            }

            _byChar.Clear();
        }



        /// Player-facing name for failure prose. Falls back to the raw data id when the character
        /// has no authored displayName (or is not in the registry at all).
        private string ResolveCharacterName(CharacterRuntimeInstance target)
        {
            if (target == null) return string.Empty;
            if (_registry.TryGet<CharacterData>(target.SourceDataId, out var charData)
                && charData != null && !string.IsNullOrEmpty(charData.displayName))
                return charData.displayName;

            return target.SourceDataId;
        }

        /// <summary>Item currently in <paramref name="slot"/> for this character, or null.</summary>
        public string GetEquipped(string charId, EquipmentSlot slot)
        {
            if (string.IsNullOrEmpty(charId) || !_byChar.TryGetValue(charId, out var state)) return null;
            return state.slotToItemId.TryGetValue(slot, out var id) ? id : null;
        }

        /// <summary>
        /// Before/after derived stats for equipping <paramref name="equipItemId"/>, without changing
        /// anything. Only stats that move are returned.
        ///
        /// <para>Computed on a <see cref="StatBlockRuntime.Clone"/>.
        /// Accounts for the item that would be <i>displaced</i> from
        /// the slot</para>
        /// </summary>
        public IReadOnlyList<EquipStatDelta> PreviewEquip(CharacterRuntimeInstance target, string equipItemId)
        {
            var deltas = new List<EquipStatDelta>();
            if (target == null) return deltas;
            if (!_registry.TryGet<EquipmentData>(equipItemId, out var equip) || equip == null) return deltas;

            var projected = target.stats.Clone();

            // Drop whatever occupies the slot today.
            string displaced = GetEquipped(target.SourceDataId, equip.slot);
            if (!string.IsNullOrEmpty(displaced))
                projected.RemoveModifiersFrom(MakeSourceId(target.SourceDataId, equip.slot));

            var sourceId = MakeSourceId(target.SourceDataId, equip.slot);
            for (int i = 0; i < equip.statModifiers.Count; i++)
            {
                var m = equip.statModifiers[i];
                projected.AddModifier(new StatModifier(m.stat, m.modifierType, m.value, sourceId, false));
            }

            projected.Recalculate();

            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                int before = target.stats.GetFinal(stat);
                int after = projected.GetFinal(stat);
                if (before != after) deltas.Add(new EquipStatDelta { stat = stat, before = before, after = after });
            }

            return deltas;
        }

        /// <summary>
        /// Actions unlocked by everything this character has equipped, with the item responsible.
        ///
        /// <para>Read from the slot table, which is this manager's own record, rather than from the
        /// instance's mirrored id list.</para>
        /// </summary>
        public IReadOnlyList<UnlockedAction> GetUnlockedActions(string charId)
        {
            var unlocked = new List<UnlockedAction>();
            if (string.IsNullOrEmpty(charId) || !_byChar.TryGetValue(charId, out var state)) return unlocked;

            // One item can unlock several actions, and two items can unlock the same one; de-duplicate by
            // action id.
            var seen = new HashSet<string>();

            foreach (var kv in state.slotToItemId)
            {
                if (string.IsNullOrEmpty(kv.Value)) continue;
                if (!_registry.TryGet<EquipmentData>(kv.Value, out var equip) || equip?.actionUnlockIds == null) continue;

                for (int i = 0; i < equip.actionUnlockIds.Count; i++)
                {
                    var actionId = equip.actionUnlockIds[i];
                    if (string.IsNullOrEmpty(actionId) || !seen.Add(actionId)) continue;

                    unlocked.Add(new UnlockedAction(actionId, kv.Value));
                }
            }

            return unlocked;
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
                if (instance == null)
                {
                    // A payload referencing an unknown character would silently lose its gear. Surface
                    // it: with ResolveInstanceById this should only happen for an id no longer in the
                    // roster (e.g. removed from the database between save and load).
                    UnityEngine.Debug.LogWarning($"[JRPG.Inventory] Equipment restore skipped '{entry.charInstanceId}' — no such character in the current roster.");
                    continue;
                }

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
                SyncEquippedIds(instance, rtState);
            }
        }
    }
}
