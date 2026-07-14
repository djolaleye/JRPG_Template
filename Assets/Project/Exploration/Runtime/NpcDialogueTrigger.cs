using UnityEngine;
using JRPG.Core;
using JRPG.Services;

namespace JRPG.Exploration
{
    /// Starts a dialogue graph when interacted with.
    public sealed class NpcDialogueTrigger : MonoBehaviour, IInteractable
    {
        [SerializeField] private string dialogueGraphId;
        [Tooltip("Speaker name surfaced by the 'current_speaker' ref and [CurrentSpeaker] token.")]
        [SerializeField] private string speakerContextId = "NPC";

        public void Interact(GameObject initiator)
        {
            var services = AppContext.Services;
            if (services == null || !services.TryResolve<IDialogueService>(out var dialogue)) return;
            if (dialogue.IsDialogueActive) return;

            var context = new DialogueStartContext
            {
                initiatorId = "player",
                speakerContextId = speakerContextId,
                sceneId = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                sourceSystem = "exploration",
            };
            
            if (dialogue.CanStartDialogue(dialogueGraphId, context))
                dialogue.StartDialogue(dialogueGraphId, context);
            else
                Debug.LogWarning($"[JRPG.Exploration] Cannot start dialogue '{dialogueGraphId}'.", this);
        }
    }
}
