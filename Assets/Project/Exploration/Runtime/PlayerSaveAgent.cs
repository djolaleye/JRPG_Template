using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using JRPG.Core;
using JRPG.Save;

namespace JRPG.Exploration
{
    /// Contributes the player's world transform to the save manifest under SaveKey "player".
    /// Registers with GameBootstrap.SaveContributors at OnEnable; unregisters at OnDisable.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerSaveAgent : MonoBehaviour, ISaveable
    {
        public string SaveKey => "player";

        private CharacterController _charController;

        private void Awake() => _charController = GetComponent<CharacterController>();

        private void OnEnable()
        {
            var reg = AppContext.SaveContributors as SaveRegistry;
            if (reg != null)
            {
                try { reg.Register(this); }
                catch (System.Exception e) { Debug.LogWarning($"[JRPG.PlayerSave] Could not register: {e.Message}", this); }
            }
        }

        private void OnDisable()
        {
            var reg = AppContext.SaveContributors as SaveRegistry;
            reg?.Unregister(this);
        }

        public SaveDataBase CaptureState()
        {
            var dto = new PlayerSaveData
            {
                version = SaveSystemCore.CurrentSaveVersion,
                posX = transform.position.x,
                posY = transform.position.y,
                posZ = transform.position.z,
                yaw = transform.eulerAngles.y,
                sceneId = SceneManager.GetActiveScene().name
            };
            return new PlayerPayload { version = dto.version, data = dto };
        }

        public void RestoreState(SaveDataBase state)
        {
            if (state is not PlayerPayload p || p.data == null) return;
            // Disable CharacterController for one frame so transform writes stick.
            StartCoroutine(ApplyRestoreNextFrame(p.data));
        }

        private IEnumerator ApplyRestoreNextFrame(PlayerSaveData d)
        {
            bool wasEnabled = _charController != null && _charController.enabled;

            if (_charController != null) _charController.enabled = false;

            transform.position = new Vector3(d.posX, d.posY, d.posZ);
            transform.rotation = Quaternion.Euler(0f, d.yaw, 0f);
            
            yield return null;
            if (_charController != null) _charController.enabled = wasEnabled;
        }
    }
}
