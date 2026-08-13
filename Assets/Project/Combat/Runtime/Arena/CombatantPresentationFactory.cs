using UnityEngine;
using JRPG.Characters;
using JRPG.Data;

namespace JRPG.Combat.Arena
{
    /// <summary>
    /// Builds the bodies staged on arena spawn slots.
    ///
    /// <para><b>Authored prefab, else placeholder.</b> <c>CharacterData.battlePrefab</c> and
    /// <c>EnemyData.battlePrefab</c> are optional on purpose — a null falls back to a tinted capsule,
    /// so the whole arena lifecycle is playable and testable before any character art exists, and
    /// dropping real art in later is a field assignment with no code change.</para>
    /// </summary>
    public sealed class CombatantPresentationFactory
    {
        /// Party blue / enemy red. Only ever applied to the placeholder, never to authored art.
        private static readonly Color PartyTint = new(0.35f, 0.55f, 0.95f);
        private static readonly Color EnemyTint = new(0.9f, 0.35f, 0.28f);

        private readonly DataRegistry _data;
        private readonly GameObject _placeholder;

        public CombatantPresentationFactory(DataRegistry data, GameObject placeholder)
        {
            _data = data;
            _placeholder = placeholder;
        }

        public CombatantPresentation CreateParty(CharacterRuntimeInstance character, Transform slot,
                                                 int slotIndex, Transform parent)
        {
            GameObject prefab = null;
            if (_data != null && _data.TryGet<CharacterData>(character.SourceDataId, out var charData) && charData != null)
                prefab = charData.battlePrefab;

            string displayName = string.IsNullOrEmpty(character.displayName)
                ? character.SourceDataId
                : character.displayName;

            return Create(prefab, slot, parent,
                          CombatantFactory.PartyCombatantId(character.SourceDataId),
                          ArenaTeam.Party, slotIndex, displayName);
        }

        public CombatantPresentation CreateEnemy(EncounterEnemyEntry entry, EnemyData enemy, Transform slot,
                                                 int slotIndex, Transform parent)
        {
            // Same name the combat HUD shows: the authored per-slot override first, so two copies of
            // one enemy read as "Slime A" and "Slime B" on the field as well as in the menus.
            string displayName = !string.IsNullOrEmpty(entry.displayNameOverride)
                ? entry.displayNameOverride
                : enemy != null && !string.IsNullOrEmpty(enemy.displayName) ? enemy.displayName : entry.enemyId;

            return Create(enemy != null ? enemy.battlePrefab : null, slot, parent,
                          CombatantFactory.EnemyCombatantId(entry.slotId),
                          ArenaTeam.Enemy, slotIndex, displayName);
        }

        private CombatantPresentation Create(GameObject prefab, Transform slot, Transform parent,
                                             string combatantId, ArenaTeam team, int slotIndex, string displayName)
        {
            bool usedPlaceholder = prefab == null;
            var source = usedPlaceholder ? _placeholder : prefab;

            if (source == null)
            {
                Debug.LogError($"[JRPG.Combat.Arena] No body for '{combatantId}' — the combatant has no " +
                               "battlePrefab and no placeholder prefab is wired on the Combat scene.");
                return null;
            }

            // Parented under the scene's presentation root rather than the arena, so swapping the arena
            // and respawning combatants stay independent operations.
            var instance = Object.Instantiate(source, slot.position, slot.rotation, parent);

            var presentation = instance.GetComponent<CombatantPresentation>();
            if (presentation == null) presentation = instance.AddComponent<CombatantPresentation>();

            presentation.Initialize(combatantId, team, slotIndex, displayName);

            if (usedPlaceholder)
                presentation.ApplyTint(team == ArenaTeam.Party ? PartyTint : EnemyTint);

            return presentation;
        }
    }
}
