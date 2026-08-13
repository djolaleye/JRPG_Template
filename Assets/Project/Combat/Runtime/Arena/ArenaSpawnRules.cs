using System.Collections.Generic;
using JRPG.Data;

namespace JRPG.Combat.Arena
{
    /// <summary>
    /// The one place a team is mapped onto arena spawn slots, so the pre-flight validation and the
    /// staging pass cannot disagree about what "enough spawn points" means.
    ///
    /// <para>Failure is always an error naming both stable ids and both counts. Silently overlapping
    /// two combatants on one slot is never an option.</para>
    /// </summary>
    public static class ArenaSpawnRules
    {
        public static bool TryResolveSlots(string arenaId, ArenaRoot root, ArenaTeam team, int count,
                                           out IReadOnlyList<UnityEngine.Transform> slots, out string error)
        {
            slots = null;

            if (root == null)
            {
                error = $"Arena '{arenaId}' has no ArenaRoot.";
                return false;
            }

            if (count <= 0)
            {
                error = $"Arena '{arenaId}' was asked to stage {count} {Noun(team)}.";
                return false;
            }

            if (root.TryGetFormation(team, count, out slots))
            {
                error = null;
                return true;
            }

            error = $"Arena '{arenaId}' requires {count} {Noun(team)} spawn points, " +
                    $"but only {root.GetMaxSupported(team)} are configured.";
            return false;
        }

        private static string Noun(ArenaTeam team) => team == ArenaTeam.Party ? "player" : "enemy";
    }
}
