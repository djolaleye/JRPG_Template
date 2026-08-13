using System;
using System.Collections.Generic;
using UnityEngine;
using JRPG.Data;

namespace JRPG.Combat.Arena
{
    /// <summary>
    /// One authored spawn layout: the ordered slots used when a team fields exactly
    /// <see cref="combatantCount"/> combatants.
    ///
    /// <para>Formations are per-count rather than one long list truncated from the front, because a
    /// three-member party is not a four-member party with one slot ignored — the shape of the line
    /// changes. Slot order is the mapping order: party member 0 goes to <c>slots[0]</c>.</para>
    /// </summary>
    [Serializable]
    public struct ArenaFormation
    {
        [Tooltip("Team size this formation stages. One formation per supported count.")]
        [Min(1)] public int combatantCount;

        [Tooltip("Spawn markers, in mapping order. Position and rotation are both used.")]
        public Transform[] slots;
    }

    /// <summary>
    /// The contract between an arena prefab and the combat scene. Everything the staging code needs
    /// is reachable from here, so the generic Combat scene never learns which visual theme it
    /// received.
    ///
    /// <para><b>Nothing here has gameplay behaviour.</b> Floor, backdrop and dressing are presentation
    /// roots; slots and anchors are empty markers. The component exposes lookups and a validation
    /// pass and does no work of its own.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArenaRoot : MonoBehaviour
    {
        [Header("Visual structure")]
        [Tooltip("The physical combat surface. Carries the collision and defines the walkable bounds.")]
        [SerializeField] private Transform floor;

        [Tooltip("Controlled background behind the combatants. Load-bearing for camera framing — a " +
                 "battle framed against the skybox reads as a bug.")]
        [SerializeField] private Transform backdrop;

        [Tooltip("Optional props, vegetation, debris, ambient VFX. Never gameplay logic.")]
        [SerializeField] private Transform dressing;

        [Header("Spawn formations")]
        [SerializeField] private ArenaFormation[] partyFormations = Array.Empty<ArenaFormation>();
        [SerializeField] private ArenaFormation[] enemyFormations = Array.Empty<ArenaFormation>();

        [Header("Camera anchors")]
        [Tooltip("Where the combat camera starts — matches the framing the exploration camera held.")]
        [SerializeField] private Transform introAnchor;

        [Tooltip("Resting battle framing: the whole party and the whole enemy formation in shot.")]
        [SerializeField] private Transform battleAnchor;

        [Tooltip("Intermediate placement the pull-back passes through, so the party enters frame first.")]
        [SerializeField] private Transform partyRevealAnchor;

        public Transform Floor => floor;
        public Transform Backdrop => backdrop;
        public Transform Dressing => dressing;

        /// <summary>
        /// Resolves the spawn slots for <paramref name="count"/> combatants on
        /// <paramref name="team"/>.
        ///
        /// <para>An exact formation wins. Failing that, the smallest formation that can seat
        /// everyone is used and its leading <paramref name="count"/> slots are taken.
        /// There is no fallback in the other direction.</para>
        /// </summary>
        public bool TryGetFormation(ArenaTeam team, int count, out IReadOnlyList<Transform> slots)
        {
            slots = null;
            if (count <= 0) return false;

            var formations = team == ArenaTeam.Party ? partyFormations : enemyFormations;
            if (formations == null) return false;

            ArenaFormation? best = null;

            for (int i = 0; i < formations.Length; i++)
            {
                var formation = formations[i];
                if (formation.slots == null || formation.slots.Length < formation.combatantCount) continue;

                if (formation.combatantCount == count)
                {
                    best = formation;
                    break;
                }

                if (formation.combatantCount > count &&
                    (best == null || formation.combatantCount < best.Value.combatantCount))
                    best = formation;
            }

            if (best == null) return false;

            var source = best.Value.slots;
            var resolved = new Transform[count];

            for (int i = 0; i < count; i++)
            {
                if (source[i] == null) return false;
                resolved[i] = source[i];
            }

            slots = resolved;
            return true;
        }

        /// <summary>Largest team size this arena can seat, per team. Used by validation and by the
        /// staging error message.</summary>
        public int GetMaxSupported(ArenaTeam team)
        {
            var formations = team == ArenaTeam.Party ? partyFormations : enemyFormations;
            if (formations == null) return 0;

            int max = 0;
            for (int i = 0; i < formations.Length; i++)
            {
                var formation = formations[i];
                if (formation.slots == null || formation.slots.Length < formation.combatantCount) continue;
                if (formation.combatantCount > max) max = formation.combatantCount;
            }

            return max;
        }

        public Transform GetAnchor(ArenaAnchorKind kind) => kind switch
        {
            ArenaAnchorKind.Intro => introAnchor,
            ArenaAnchorKind.Battle => battleAnchor,
            ArenaAnchorKind.PartyReveal => partyRevealAnchor,
            _ => null,
        };

        /// <summary>
        /// Structural check, run by the editor validator and again at runtime before staging.
        /// </summary>
        public bool Validate(out string error)
        {
            if (floor == null) { error = $"Arena prefab '{name}' has no floor assigned."; return false; }
            if (backdrop == null) { error = $"Arena prefab '{name}' has no backdrop assigned."; return false; }

            if (!ValidateFormations(ArenaTeam.Party, partyFormations, out error)) return false;
            if (!ValidateFormations(ArenaTeam.Enemy, enemyFormations, out error)) return false;

            foreach (ArenaAnchorKind kind in Enum.GetValues(typeof(ArenaAnchorKind)))
            {
                if (GetAnchor(kind) != null) continue;
                error = $"Arena prefab '{name}' is missing the {kind} camera anchor.";
                return false;
            }

            error = null;
            return true;
        }

        private bool ValidateFormations(ArenaTeam team, ArenaFormation[] formations, out string error)
        {
            if (formations == null || formations.Length == 0)
            {
                error = $"Arena prefab '{name}' has no {team} formations.";
                return false;
            }

            var seenCounts = new HashSet<int>();

            for (int i = 0; i < formations.Length; i++)
            {
                var formation = formations[i];
                string label = $"Arena prefab '{name}' {team} formation[{i}]";

                if (formation.combatantCount <= 0)
                {
                    error = $"{label} has a combatant count of {formation.combatantCount}.";
                    return false;
                }

                if (!seenCounts.Add(formation.combatantCount))
                {
                    error = $"{label} duplicates the formation for {formation.combatantCount} combatant(s).";
                    return false;
                }

                if (formation.slots == null || formation.slots.Length < formation.combatantCount)
                {
                    int have = formation.slots?.Length ?? 0;
                    error = $"{label} needs {formation.combatantCount} spawn point(s) but has {have}.";
                    return false;
                }

                for (int s = 0; s < formation.combatantCount; s++)
                {
                    if (formation.slots[s] != null) continue;
                    error = $"{label} slot {s} is empty.";
                    return false;
                }
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            DrawFormationGizmos(partyFormations, new Color(0.3f, 0.6f, 1f, 0.9f));
            DrawFormationGizmos(enemyFormations, new Color(1f, 0.35f, 0.25f, 0.9f));

            Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.9f);
            foreach (ArenaAnchorKind kind in Enum.GetValues(typeof(ArenaAnchorKind)))
            {
                var anchor = GetAnchor(kind);
                if (anchor == null) continue;
                Gizmos.DrawWireCube(anchor.position, Vector3.one * 0.4f);
                Gizmos.DrawRay(anchor.position, anchor.forward * 1.5f);
            }
        }

        private static void DrawFormationGizmos(ArenaFormation[] formations, Color color)
        {
            if (formations == null) return;
            Gizmos.color = color;

            for (int i = 0; i < formations.Length; i++)
            {
                var slots = formations[i].slots;
                if (slots == null) continue;

                for (int s = 0; s < slots.Length; s++)
                {
                    if (slots[s] == null) continue;
                    Gizmos.DrawWireSphere(slots[s].position, 0.35f);
                    Gizmos.DrawRay(slots[s].position, slots[s].forward);
                }
            }
        }
#endif
    }
}
