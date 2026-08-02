using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Data
{
    /// Static description of a combat action.
    [CreateAssetMenu(menuName = "JRPG/Combat/Combat Action", fileName = "CombatAction")]
    public class CombatActionData : GameDataBase
    {
        public CombatActionCategory category;
        public TargetRule targetRule = new();

        public List<CombatCost> costs = new();
        public List<CombatEffect> effects = new();

        public bool usableByPlayers = true;
        public bool usableByEnemies = true;

        [Tooltip("Turns this action is unavailable to the same combatant after use.")]
        [Min(0)] public int cooldownTurns;
    }
}
