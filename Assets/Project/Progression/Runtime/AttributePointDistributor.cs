using UnityEngine;
using JRPG.Characters;
using JRPG.Core;
using JRPG.Data;

namespace JRPG.Progression
{
    /// Spends manual attribute points on a runtime character. One point = +1 Flat to the chosen
    /// stat, maintained as a single accumulated permanent modifier per (character, stat).
    public sealed class AttributePointDistributor
    {
        /// SourceId format: manual_attr:{characterId}:{stat}
        public static string ManualSourceId(string characterId, StatType stat) => $"manual_attr:{characterId}:{stat}";

        public static readonly StatType[] AllowedStats =
        {
            StatType.Strength, StatType.Magic, StatType.Defense,
            StatType.Resistance, StatType.Speed, StatType.Luck,
        };

        private readonly IEventBus _bus;

        public AttributePointDistributor(IEventBus bus)
        {
            _bus = bus;
        }

        public static bool IsAllowed(StatType stat)
        {
            for (int i = 0; i < AllowedStats.Length; i++)
                if (AllowedStats[i] == stat) return true;
            return false;
        }

        public bool TryAllocate(CharacterRuntimeInstance character, CharacterProgressRuntime progress, StatType stat)
        {
            if (character == null || progress == null) return false;
            if (progress.unspentAttributePoints <= 0) return false;
            if (!IsAllowed(stat))
            {
                Debug.LogWarning($"[JRPG.Progression] Manual allocation to {stat} is not allowed.");
                return false;
            }

            progress.unspentAttributePoints--;
            progress.manuallyAllocatedPoints.TryGetValue(stat, out int points);
            progress.manuallyAllocatedPoints[stat] = points + 1;

            ReapplyManualModifier(character, progress.characterId, stat, points + 1);
            character.Recalculate();

            _bus?.Publish(new AttributePointsAssigned(progress.characterId, stat.ToString(), 1));
            return true;
        }

        /// Rebuilds the accumulated permanent modifier for one stat (also used on save restore).
        public static void ReapplyManualModifier(CharacterRuntimeInstance character, string characterId, StatType stat, int totalPoints)
        {
            string sourceId = ManualSourceId(characterId, stat);
            character.stats.RemoveModifiersFrom(sourceId);
            if (totalPoints > 0)
                character.stats.AddModifier(new StatModifier(stat, ModifierType.Flat, totalPoints, sourceId, isPermanent: true));
        }
    }
}
