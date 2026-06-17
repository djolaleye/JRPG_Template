using UnityEngine;

namespace JRPG.Exploration
{
    /// Drop on a cube on the Interactable layer to verify the interaction pipeline.
    public class TestInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string label = "Test Object";

        public void Interact(GameObject initiator)
        {
            Debug.Log($"[JRPG.Exploration] Interacted with '{label}' by {initiator.name}", this);
        }
    }
}
