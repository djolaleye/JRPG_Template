using UnityEngine;

namespace JRPG.Exploration
{
    /// Marks a world object (e.g. an enemy) as a combat encounter source. The player's
    /// <see cref="EncounterAttackProbe"/> starts this encounter when the player attacks within range.
    public sealed class EncounterTrigger : MonoBehaviour
    {
        [Tooltip("Stable id of the EncounterData to start.")]
        public string encounterId = "encounter_test_slimes";

        [Tooltip("Player must be within this distance (world units) to trigger on attack.")]
        public float range = 2.5f;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, range);
        }
#endif
    }
}
