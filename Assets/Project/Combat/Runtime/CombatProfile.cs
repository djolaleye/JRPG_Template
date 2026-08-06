using System.Collections.Generic;
using JRPG.Data;

namespace JRPG.Combat
{
    /// Per-combatant snapshot of everything combat rules need to consult repeatedly (elemental
    /// affinities now; status immunities, passives, and action unlocks in 11.4).
    ///
    /// Built once by CombatantFactory at battle start from innate data + equipment. Battle-local.
    public sealed class CombatProfile
    {
        private readonly Dictionary<Element, ElementAffinity> _affinities = new();

        public readonly HashSet<string> statusImmunities = new(); /// Status ids this combatant cannot be afflicted with
        public readonly List<string> passiveEffectIds = new(); /// Passive effect ids granted by traits/equipment
        public readonly HashSet<string> unlockedActionIds = new(); /// Extra action ids unlocked by equipment
        private readonly Dictionary<string, string> _unlockSources = new();

        public readonly List<string> skillIds = new(); /// This combatant's own Skill-category actions, in authored order.

        /// <summary>
        /// The cap on <see cref="skillIds"/>.
        ///
        /// <para>Equipment unlocks are deliberately held in <see cref="unlockedActionIds"/> instead, and
        /// are <b>not</b> capped. Gear can grant extra skills on top of a full skill list rather than
        /// competing with it, so a character at 8/8 loses nothing by wearing an unlocking item.</para>
        /// </summary>
        public const int MaxSkills = 8;

        public void AddSkills(List<string> ids)
        {
            if (ids == null) return;
            
            for (int i = 0; i < ids.Count && skillIds.Count < MaxSkills; i++)
                if (!string.IsNullOrEmpty(ids[i]) && !skillIds.Contains(ids[i])) skillIds.Add(ids[i]);
        }

        public bool HasSkill(string actionId)
            => !string.IsNullOrEmpty(actionId) && skillIds.Contains(actionId);

        /// <summary>
        /// Records an action granted by a piece of equipment, remembering the item responsible.
        ///
        /// <para>First writer wins on the source: two items granting the same action still yield one
        /// entry, and the player is shown whichever is listed first.</para>
        /// </summary>
        public void AddUnlockedAction(string actionId, string sourceItemId)
        {
            if (string.IsNullOrEmpty(actionId)) return;

            unlockedActionIds.Add(actionId);
            if (!string.IsNullOrEmpty(sourceItemId) && !_unlockSources.ContainsKey(actionId))
                _unlockSources[actionId] = sourceItemId;
        }

        /// <summary>Item id that unlocked <paramref name="actionId"/>, or null when it was not gear-granted.</summary>
        public string GetUnlockSource(string actionId)
            => !string.IsNullOrEmpty(actionId) && _unlockSources.TryGetValue(actionId, out var itemId)
                ? itemId
                : null;

        public string DescribeSkills() => string.Join(",", skillIds);

        public IReadOnlyDictionary<Element, ElementAffinity> Affinities => _affinities;

        /// Merge an affinity. Stronger responses win, so a resistance can be upgraded to an
        /// immunity/absorb by equipment.
        public void AddAffinity(Element element, ElementAffinity affinity)
        {
            if (!_affinities.TryGetValue(element, out var existing))
            {
                _affinities[element] = affinity;
                return;
            }

            if (Rank(affinity) > Rank(existing)) _affinities[element] = affinity;
        }

        public void AddAffinities(List<ElementAffinityEntry> entries)
        {
            if (entries == null) return;
            
            for (int i = 0; i < entries.Count; i++) AddAffinity(entries[i].element, entries[i].affinity);
        }

        public ElementAffinity GetAffinity(Element element)
            => _affinities.TryGetValue(element, out var a) ? a : ElementAffinity.Normal;

        // ---- Read-only queries -------------------------------------------------------------
        // Callers use these rather than touching the backing collections, so consumers never need to
        // reference the concrete set types.

        public bool IsImmuneToStatus(string statusId)
            => !string.IsNullOrEmpty(statusId) && statusImmunities.Contains(statusId);

        public bool HasPassive(string passiveId)
            => !string.IsNullOrEmpty(passiveId) && passiveEffectIds.Contains(passiveId);

        public bool HasUnlockedAction(string actionId)
            => !string.IsNullOrEmpty(actionId) && unlockedActionIds.Contains(actionId);

        public int StatusImmunityCount => statusImmunities.Count;
        public int UnlockedActionCount => unlockedActionIds.Count;

        /// Flattened views for logging/debug tooling.
        public string DescribeImmunities() => string.Join(",", statusImmunities);
        public string DescribeUnlockedActions() => string.Join(",", unlockedActionIds);
        public string DescribePassives() => string.Join(",", passiveEffectIds);

        
        private static int Rank(ElementAffinity a) => a switch
        {
            ElementAffinity.Weak => 0,
            ElementAffinity.Normal => 1,
            ElementAffinity.Resist => 2,
            ElementAffinity.Immune => 3,
            ElementAffinity.Absorb => 4,
            _ => 1,
        };
    }
}
