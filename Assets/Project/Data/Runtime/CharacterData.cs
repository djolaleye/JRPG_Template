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
    }
}
