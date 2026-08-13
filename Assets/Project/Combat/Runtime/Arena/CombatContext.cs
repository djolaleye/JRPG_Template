using System.Collections.Generic;
using UnityEngine;
using JRPG.Characters;
using JRPG.Data;
using JRPG.Party;

namespace JRPG.Combat.Arena
{
    /// <summary>
    /// The handoff object between exploration and combat: everything the Combat scene needs to stage
    /// a battle, resolved once on the exploration side. Exploration creates it; combat consumes it.
    /// 
    /// <para><b>Nothing is duplicated.</b> The party comes from
    /// <see cref="IPartyRuntimeQueries.GetActiveCombatParty"/> and the roster from
    /// <see cref="EncounterData.ResolveRoster"/> — the same two sources
    /// <c>CombatService.StartBattle</c> reads, so presentation and engine cannot drift into two
    /// different ideas of who is fighting.</para>
    /// </summary>
    public sealed class CombatContext
    {
        public string EncounterId { get; private set; }
        public EncounterData Encounter { get; private set; }
        public CombatArenaDefinition Arena { get; private set; }
        public BattleReturnContext ReturnContext { get; private set; }

        public IReadOnlyList<CharacterRuntimeInstance> Party { get; private set; }
        public IReadOnlyList<EncounterEnemyEntry> EnemyRoster { get; private set; }

        public ArenaRoot ArenaTemplate { get; private set; }

        private CombatContext() { }

        /// <summary>
        /// Resolves an encounter into a complete context, or fails with a message naming the ids
        /// involved. Called before the world is frozen or anything is loaded, so a refusal costs
        /// nothing.
        /// </summary>
        public static CombatContext Build(string encounterId, DataRegistry data, IPartyRuntimeQueries party,
                                          BattleReturnContext returnContext, out string error)
        {
            if (string.IsNullOrEmpty(encounterId))
            {
                error = "No encounter id supplied.";
                return null;
            }

            if (data == null || party == null)
            {
                error = $"Encounter '{encounterId}' cannot be staged — the data registry or party service is missing.";
                return null;
            }

            if (!data.TryGet<EncounterData>(encounterId, out var encounter) || encounter == null)
            {
                error = $"Unknown encounter '{encounterId}'.";
                return null;
            }

            if (string.IsNullOrEmpty(encounter.arenaId))
            {
                error = $"Encounter '{encounterId}' names no arena.";
                return null;
            }

            if (!data.TryGet<CombatArenaDefinition>(encounter.arenaId, out var arena) || arena == null)
            {
                error = $"Encounter '{encounterId}' references missing arena '{encounter.arenaId}'.";
                return null;
            }

            var context = new CombatContext
            {
                EncounterId = encounterId,
                Encounter = encounter,
                Arena = arena,
                ReturnContext = returnContext,
                Party = party.GetActiveCombatParty(),
                EnemyRoster = encounter.ResolveRoster(),
                ArenaTemplate = arena.arenaPrefab != null
                    ? arena.arenaPrefab.GetComponentInChildren<ArenaRoot>(true)
                    : null,
            };

            return context.Validate(out error) ? context : null;
        }

        /// <summary>
        /// Full pre-flight check: the arena exists and is structurally sound, both sides have
        /// combatants, and the arena can seat every one of them. Run before the scene load, and again
        /// before staging.
        /// </summary>
        public bool Validate(out string error)
        {
            if (Arena == null)
            {
                error = $"Encounter '{EncounterId}' has no arena.";
                return false;
            }

            if (Arena.arenaPrefab == null)
            {
                error = $"Arena '{Arena.Id}' has no prefab assigned.";
                return false;
            }

            if (ArenaTemplate == null)
            {
                error = $"Arena '{Arena.Id}' prefab '{Arena.arenaPrefab.name}' carries no ArenaRoot component.";
                return false;
            }

            if (!ArenaTemplate.Validate(out var structural))
            {
                error = $"Arena '{Arena.Id}': {structural}";
                return false;
            }

            if (Party == null || Party.Count == 0)
            {
                error = $"Encounter '{EncounterId}' has no active party members to stage.";
                return false;
            }

            if (EnemyRoster == null || EnemyRoster.Count == 0)
            {
                error = $"Encounter '{EncounterId}' has an empty enemy roster.";
                return false;
            }

            if (ReturnContext == null || !ReturnContext.IsValid)
            {
                error = $"Encounter '{EncounterId}' has no valid return context — the player could not be sent back.";
                return false;
            }

            if (!ArenaSpawnRules.TryResolveSlots(Arena.Id, ArenaTemplate, ArenaTeam.Party, Party.Count, out _, out error))
                return false;

            if (!ArenaSpawnRules.TryResolveSlots(Arena.Id, ArenaTemplate, ArenaTeam.Enemy, EnemyRoster.Count, out _, out error))
                return false;

            error = null;
            return true;
        }

        public static void LogRefusal(string error) =>
            Debug.LogError($"[JRPG.Combat.Arena] Encounter refused: {error}");

        public override string ToString()
            => $"combat(encounter='{EncounterId}', arena='{Arena?.Id}', party={Party?.Count ?? 0}, enemies={EnemyRoster?.Count ?? 0})";
    }
}
