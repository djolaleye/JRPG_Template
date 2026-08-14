using UnityEngine;
using JRPG.Core;

namespace JRPG.Data
{
    /// <summary>
    /// A reusable, authored battle staging environment: floor, backdrop, dressing, spawn formations
    /// and camera anchors, all carried by <see cref="arenaPrefab"/>.
    ///
    /// <para><b>Layout lives on the prefab.</b> Spawn slots and camera anchors are marker
    /// Transforms under the prefab's <c>ArenaRoot</c> component so they can be dragged in the scene
    /// view against the actual geometry. This asset holds the stable id, the prefab reference, and
    /// the coverage requirements the validator enforces.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "JRPG/Combat/Arena Definition", fileName = "Arena")]
    public class CombatArenaDefinition : GameDataBase
    {
        [Tooltip("Prefab instantiated under the Combat scene's ArenaMount. Must carry an ArenaRoot " +
                 "component supplying the floor, backdrop, spawn formations and camera anchors.")]
        public GameObject arenaPrefab;
        public bool isBossArena;

        [Header("Required coverage")]
        [Tooltip("Largest party size this arena must support. The prefab needs a party formation for " +
                 "every count from 1 to this number.")]
        [Min(1)] public int requiredPartyFormations = PartyRules.MaxActiveMembers;

        [Tooltip("Largest enemy roster this arena must support. The prefab needs an enemy formation " +
                 "for every count from 1 to this number.")]
        [Min(1)] public int requiredEnemyFormations = 6;
    }
}
