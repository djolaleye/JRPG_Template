using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Data
{
    /// Static description of a combat action. It does not know who is using it, who is targeted,
    /// whether costs are affordable, or whether a target is valid — those are runtime concerns
    /// handled by the combat service, targeting system, and action resolver.
    [CreateAssetMenu(menuName = "JRPG/Combat/Combat Action", fileName = "CombatAction")]
    public class CombatActionData : GameDataBase
    {
        public CombatActionCategory category;
        public TargetRule targetRule = new();

        public List<CombatCost> costs = new();
        public List<CombatEffect> effects = new();

        public bool usableByPlayers = true;
        public bool usableByEnemies = true;
    }
}
