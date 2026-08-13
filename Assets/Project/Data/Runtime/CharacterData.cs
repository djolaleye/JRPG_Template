using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Data
{
    [CreateAssetMenu(menuName = "JRPG/Character Data", fileName = "CharacterData")]
    public class CharacterData : GameDataBase
    {
        public List<StatEntry> baseStats = new();
        public string growthDataId;
        public List<string> defaultSkillIds = new();
        public List<EquipmentSlot> allowedSlots = new();

        /// <summary>
        /// Stable IDs of recruitment conditions (story flags / event IDs) that must all be
        /// satisfied for this character to move from <c>Met</c> to <c>Recruitable</c>.
        /// Resolved at runtime by an <see cref="JRPG.Services.IRecruitmentConditionEvaluator"/>.
        /// Empty list = unconditionally eligible once Met.
        /// </summary>
        [Tooltip("Story-flag / event IDs that must all be satisfied before this character can become Recruitable.")]
        public List<string> recruitmentFlagIds = new();

        public List<ElementAffinityEntry> elementAffinities = new();
        public List<string> statusImmunityIds = new();
        public List<string> passiveEffectIds = new();


        [Tooltip("Optional. Speaker bust / portrait. Consumers fall back to the name plate alone when null.")]
        public Sprite portraitSprite;

        [Tooltip("Optional. Battle body staged on an arena spawn point. Null uses the placeholder capsule.")]
        public GameObject battlePrefab;
    }
}
