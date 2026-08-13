using TMPro;
using UnityEngine;
using JRPG.Data;

namespace JRPG.Combat.Arena
{
    /// <summary>
    /// One staged body. Carries the combatant id the engine uses, so later presentation work — damage
    /// floaters, target highlighting, action animation — can find the body for a combatant without a
    /// parallel roster of its own.
    ///
    /// <para>The id is computed by <c>CombatantFactory.PartyCombatantId</c> /
    /// <c>EnemyCombatantId</c> at staging time, ahead of the battle, and matches what
    /// <c>CombatService</c> mints when it builds the real combatants.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatantPresentation : MonoBehaviour
    {
        [Tooltip("Optional world-space name plate. Placeholder bodies carry one; real art need not.")]
        [SerializeField] private TMP_Text nameLabel;

        [Tooltip("Renderer tinted by team on placeholder bodies. Left null on authored art.")]
        [SerializeField] private Renderer tintTarget;

        public string CombatantId { get; private set; }
        public ArenaTeam Team { get; private set; }
        public int SlotIndex { get; private set; }
        public string DisplayName { get; private set; }

        public void Initialize(string combatantId, ArenaTeam team, int slotIndex, string displayName)
        {
            CombatantId = combatantId;
            Team = team;
            SlotIndex = slotIndex;
            DisplayName = displayName;

            // Named for the hierarchy, so a staged arena is readable at a glance during debugging.
            gameObject.name = $"{team}_{slotIndex}_{combatantId}";

            if (nameLabel != null) nameLabel.text = displayName;
        }

        /// <summary>
        /// Tints the placeholder body. Written through a <see cref="MaterialPropertyBlock"/> rather
        /// than <c>renderer.material</c>, which would instantiate a material per body and leak one per
        /// battle.
        /// </summary>
        public void ApplyTint(Color color)
        {
            if (tintTarget == null) tintTarget = GetComponentInChildren<Renderer>();
            if (tintTarget == null) return;

            var block = new MaterialPropertyBlock();
            tintTarget.GetPropertyBlock(block);

            // URP Lit uses _BaseColor; _Color is set too so the built-in fallback shader also tints.
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);

            tintTarget.SetPropertyBlock(block);
        }

        private Camera _billboardCamera;

        /// <summary>
        /// Keeps the name plate readable from wherever the combat camera happens to be. Without this
        /// an enemy plate renders mirrored, because enemy spawn slots face the party — 180° from the
        /// camera.
        /// </summary>
        private void LateUpdate()
        {
            if (nameLabel == null) return;

            if (_billboardCamera == null) _billboardCamera = ResolveBillboardCamera();
            if (_billboardCamera == null) return;

            nameLabel.transform.rotation = _billboardCamera.transform.rotation;
        }

        /// <summary>
        /// The combat scene's own camera, not <see cref="Camera.main"/>. During a battle the
        /// exploration scene is still resident and still holds the MainCamera tag, so
        /// <c>Camera.main</c> would billboard every plate towards a camera that is not even rendering.
        /// </summary>
        private static Camera ResolveBillboardCamera()
        {
            var scene = CombatSceneController.Current;
            if (scene != null && scene.CombatCamera != null) return scene.CombatCamera;

            return Camera.main;
        }
    }
}
