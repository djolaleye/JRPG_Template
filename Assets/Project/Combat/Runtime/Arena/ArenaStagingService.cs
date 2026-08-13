using System.Collections.Generic;
using UnityEngine;
using JRPG.Data;

namespace JRPG.Combat.Arena
{
    /// <summary>
    /// Turns a <see cref="CombatContext"/> into a staged arena: the visual environment instantiated
    /// under the scene's mount, and a body on every spawn slot.
    ///
    /// <para><b>Validate, then build.</b> Everything that can be checked is checked before the first
    /// object is instantiated, so a failure leaves an empty mount rather than a half-built arena the
    /// caller has to unwind. Failures name both stable ids and both counts.</para>
    /// </summary>
    public sealed class ArenaStagingService
    {
        private readonly DataRegistry _data;

        public ArenaStagingService(DataRegistry data)
        {
            _data = data;
        }

        public bool Stage(CombatContext context, CombatSceneController scene, out string error)
        {
            if (context == null) { error = "No combat context."; return false; }

            if (scene == null)
            {
                error = $"Encounter '{context.EncounterId}' cannot be staged — the Combat scene is not loaded.";
                return false;
            }

            if (scene.ArenaMount == null || scene.CombatantRoot == null)
            {
                error = "The Combat scene has no ArenaMount or CombatPresentation root wired.";
                return false;
            }

            // Re-checked here rather than trusted from the pre-flight: the scene load sits between the
            // two.
            if (!context.Validate(out error)) return false;

            // A retry restages into the same scene, so anything left from the previous attempt goes
            // first — otherwise the second arena is instantiated inside the first.
            scene.ClearStaging();

            var arenaInstance = Object.Instantiate(context.Arena.arenaPrefab, scene.ArenaMount);
            arenaInstance.name = context.Arena.Id;
            arenaInstance.transform.localPosition = Vector3.zero;
            arenaInstance.transform.localRotation = Quaternion.identity;

            var arenaRoot = arenaInstance.GetComponentInChildren<ArenaRoot>(true);
            if (arenaRoot == null)
            {
                Object.Destroy(arenaInstance);
                error = $"Arena '{context.Arena.Id}' instantiated without an ArenaRoot.";
                return false;
            }

            scene.MarkArenaConfigured();

            // Both slot sets are resolved before anything is spawned.
            if (!ArenaSpawnRules.TryResolveSlots(context.Arena.Id, arenaRoot, ArenaTeam.Party,
                                                 context.Party.Count, out var partySlots, out error))
            {
                Object.Destroy(arenaInstance);
                return false;
            }

            if (!ArenaSpawnRules.TryResolveSlots(context.Arena.Id, arenaRoot, ArenaTeam.Enemy,
                                                 context.EnemyRoster.Count, out var enemySlots, out error))
            {
                Object.Destroy(arenaInstance);
                return false;
            }

            var factory = new CombatantPresentationFactory(_data, scene.PlaceholderCombatant);
            var presentations = new List<CombatantPresentation>();

            for (int i = 0; i < context.Party.Count; i++)
            {
                var body = factory.CreateParty(context.Party[i], partySlots[i], i, scene.CombatantRoot);
                if (body != null) presentations.Add(body);
            }

            for (int i = 0; i < context.EnemyRoster.Count; i++)
            {
                var entry = context.EnemyRoster[i];
                _data.TryGet<EnemyData>(entry.enemyId, out var enemy);

                var body = factory.CreateEnemy(entry, enemy, enemySlots[i], i, scene.CombatantRoot);
                if (body != null) presentations.Add(body);
            }

            int expected = context.Party.Count + context.EnemyRoster.Count;
            if (presentations.Count != expected)
            {
                scene.ClearStaging();
                error = $"Encounter '{context.EncounterId}' staged {presentations.Count} of {expected} " +
                        "combatant bodies — see the errors above.";
                return false;
            }

            scene.AdoptStaging(arenaInstance, arenaRoot, presentations);
            scene.MarkCombatantsConfigured();

            error = null;
            return true;
        }
    }
}
