using System.Collections.Generic;
using JRPG.Characters;
using JRPG.Data;

namespace JRPG.Combat
{
    /// Converts persistent runtime characters and enemy assets into battle-local combatants.
    /// Kept separate from the state machine so conversion rules are inspectable and testable.
    public sealed class CombatantFactory
    {
        private readonly DataRegistry _data;

        public CombatantFactory(DataRegistry data = null)
        {
            _data = data;
        }

        /// Party combatant. Clones the character's stat block so temporary combat modifiers cannot
        /// leak into exploration state. Current HP/MP/SP are copied from the live instance and are
        /// committed back at battle end via sourceRuntimeId.
        public CombatantInstance FromCharacter(CharacterRuntimeInstance character)
        {
            var stats = character.stats.Clone();
            stats.Recalculate();

            var combatant = new CombatantInstance
            {
                combatantId = $"party_{character.SourceDataId}",
                sourceDataId = character.SourceDataId,
                sourceRuntimeId = character.InstanceId,
                team = CombatantTeam.Party,
                displayName = ResolveCharacterDisplayName(character),
                stats = stats,
                currentHP = character.currentHP,
                currentMP = character.currentMP,
                currentSP = character.currentSP,
                level = character.level,
            };

            BuildCharacterProfile(combatant, character);
            return combatant;
        }

        /// Enemy combatant built from an authored encounter slot, so duplicates of the same EnemyData
        /// get distinct, authored identities (slotId / display name).
        public CombatantInstance FromEnemySlot(EnemyData enemy, EncounterEnemyEntry slot, int enemyIndex)
        {
            var combatant = FromEnemy(enemy, enemyIndex);

            if (!string.IsNullOrEmpty(slot.slotId))
            {
                combatant.encounterSlotId = slot.slotId;
                combatant.combatantId = $"enemy_{slot.slotId}";
            }
            if (!string.IsNullOrEmpty(slot.displayNameOverride))
                combatant.displayName = slot.displayNameOverride;

            return combatant;
        }

        /// Enemy combatant. Battle-local and discarded when combat ends; resources start full.
        public CombatantInstance FromEnemy(EnemyData enemy, int enemyIndex)
        {
            var stats = new StatBlockRuntime();
            for (int i = 0; i < enemy.baseStats.Count; i++)
            {
                var bs = enemy.baseStats[i];
                stats.SetBase(bs.stat, bs.value);
            }
            stats.Recalculate();

            var combatant = new CombatantInstance
            {
                combatantId = $"enemy_{enemy.Id}_{enemyIndex}",
                sourceDataId = enemy.Id,
                sourceRuntimeId = string.Empty,
                team = CombatantTeam.Enemy,
                displayName = string.IsNullOrEmpty(enemy.displayName) ? enemy.Id : enemy.displayName,
                stats = stats,
                currentHP = stats.GetFinal(StatType.MaxHP),
                currentMP = stats.GetFinal(StatType.MaxMP),
                currentSP = stats.GetFinal(StatType.MaxSP),
                level = enemy.level,
            };

            combatant.profile.AddAffinities(enemy.elementAffinities);

            AddRange(combatant.profile.statusImmunities, enemy.statusImmunityIds);
            AddRange(combatant.profile.passiveEffectIds, enemy.passiveEffectIds);
            combatant.profile.AddSkills(enemy.skillIds);

            return combatant;
        }

        /// Authored display name for a party member, matching how enemies resolve theirs. Falls back to
        /// the stable data id when there is no registry or no authored name (so combat never shows blank).
        private string ResolveCharacterDisplayName(CharacterRuntimeInstance character)
        {
            if (_data != null
                && _data.TryGet<CharacterData>(character.SourceDataId, out var charData)
                && charData != null
                && !string.IsNullOrEmpty(charData.displayName))
                return charData.displayName;

            return character.SourceDataId;
        }

        /// Innate character traits + everything currently equipped, merged once.
        private void BuildCharacterProfile(CombatantInstance combatant, CharacterRuntimeInstance character)
        {
            if (_data == null) return;

            if (_data.TryGet<CharacterData>(character.SourceDataId, out var charData) && charData != null)
            {
                combatant.profile.AddAffinities(charData.elementAffinities);

                AddRange(combatant.profile.statusImmunities, charData.statusImmunityIds);
                AddRange(combatant.profile.passiveEffectIds, charData.passiveEffectIds);

                // The instance's own list is the authority (it carries level-up learning and the
                // player's discard choices); the authored defaults are only a fallback for an
                // instance that was never seeded.
                combatant.profile.AddSkills(character.skillIds.Count > 0 ? character.skillIds : charData.defaultSkillIds);
            }

            var equipped = character.equippedItemIds;
            if (equipped == null) return;

            for (int i = 0; i < equipped.Count; i++)
            {
                if (string.IsNullOrEmpty(equipped[i])) continue;
                if (!_data.TryGet<EquipmentData>(equipped[i], out var equip) || equip == null) continue;

                combatant.profile.AddAffinities(equip.elementAffinities);

                AddRange(combatant.profile.statusImmunities, equip.statusImmunityIds);
                AddRange(combatant.profile.passiveEffectIds, equip.passiveEffectIds);

                if (equip.actionUnlockIds != null)
                    for (int u = 0; u < equip.actionUnlockIds.Count; u++)
                        combatant.profile.AddUnlockedAction(equip.actionUnlockIds[u], equip.Id);

                RecordWeaponSlot(combatant.profile, equip);
            }
        }

        /// <summary>
        /// Files a weapon's flat stat contribution under its slot on the profile.
        ///
        /// <para>Needed because both weapons fold into one stat block: by the time combat starts, a
        /// sword's Strength and a bow's Strength are indistinguishable there. A Ranged action has to
        /// trade one for the other, which means knowing each separately.</para>
        ///
        /// <para>Only <see cref="ModifierType.Flat"/> modifiers are recorded.</para>
        /// </summary>
        private static void RecordWeaponSlot(CombatProfile profile, EquipmentData equip)
        {
            bool isRanged = equip.slot == EquipmentSlot.RangedWeapon;
            if (!isRanged && equip.slot != EquipmentSlot.MeleeWeapon) return;

            profile.SetWeaponItem(isRanged, equip.Id);

            if (equip.HasWeaponBasePower) profile.SetWeaponBasePower(isRanged, equip.weaponBasePower);

            if (equip.statModifiers == null) return;

            for (int i = 0; i < equip.statModifiers.Count; i++)
            {
                var m = equip.statModifiers[i];
                if (m.modifierType != ModifierType.Flat) continue;

                profile.AddWeaponStat(isRanged, m.stat, m.value);
            }
        }

        private static void AddRange(HashSet<string> set, List<string> values)
        {
            if (values == null) return;

            for (int i = 0; i < values.Count; i++)
                if (!string.IsNullOrEmpty(values[i])) set.Add(values[i]);
        }

        private static void AddRange(List<string> list, List<string> values)
        {
            if (values == null) return;

            for (int i = 0; i < values.Count; i++)
                if (!string.IsNullOrEmpty(values[i]) && !list.Contains(values[i])) list.Add(values[i]);
        }
    }
}
