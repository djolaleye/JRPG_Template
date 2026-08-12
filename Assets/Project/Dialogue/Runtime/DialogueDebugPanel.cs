using System.Text;
using UnityEngine;
using JRPG.Core;
using JRPG.Services;

namespace JRPG.Dialogue
{
    /// Headless dialogue harness: start/advance/choose graphs and inspect story state via context-menu actions
    public sealed class DialogueDebugPanel : MonoBehaviour
    {
        [SerializeField] private string graphId = "dialogue_blacksmith_greeting";
        [SerializeField] private string speakerContextId = "Blacksmith";
        [SerializeField] private int choiceIndex;
        [SerializeField] private string flagId = "rival_respected";
        [SerializeField] private string itemId = "item_key_dorm";
        [SerializeField] private int saveSlot = 8;

        private DialogueService Dialogue
            => AppContext.Services != null && AppContext.Services.TryResolve<IDialogueService>(out var svc)
                ? svc as DialogueService : null;

        private T Resolve<T>() where T : class
            => AppContext.Services != null && AppContext.Services.TryResolve<T>(out var svc) ? svc : null;

        [ContextMenu("Start (serialized graphId)")]
        public void StartGraph()
        {
            var d = Dialogue;
            if (d == null) { Debug.LogError("[DialogueDebug] No IDialogueService."); return; }
            d.StartDialogue(graphId, new DialogueStartContext
            {
                initiatorId = "player",
                speakerContextId = speakerContextId,
                sceneId = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                sourceSystem = "exploration",
            });
            DumpSnapshot();
        }

        [ContextMenu("Advance")]
        public void Advance() { Dialogue?.Advance(); DumpSnapshot(); }

        [ContextMenu("Choose (serialized index)")]
        public void ChooseIndex()
        {
            var d = Dialogue;
            var choices = d?.GetCurrentChoices();
            if (choices == null || choiceIndex < 0 || choiceIndex >= choices.Count) { Debug.Log("[DialogueDebug] No such choice."); return; }
            Debug.Log($"[DialogueDebug] Choosing '{choices[choiceIndex].choiceId}' (available={choices[choiceIndex].available})");
            d.Choose(choices[choiceIndex].choiceId);
            DumpSnapshot();
        }

        [ContextMenu("Dump Snapshot")]
        public void DumpSnapshot()
        {
            var d = Dialogue;
            if (d == null || !d.IsDialogueActive) { Debug.Log($"[DialogueDebug] (no active dialogue) state={StateStr()}"); return; }
            var snap = d.GetCurrentRenderSnapshot();
            var sb = new StringBuilder();
            sb.AppendLine($"[DialogueDebug] node={snap.nodeId} state={StateStr()}");
            sb.AppendLine($"  {snap.speaker.displayName}: {snap.body}");
            foreach (var c in d.GetCurrentChoices())
                sb.AppendLine($"   - [{(c.available ? "x" : " ")}] {c.choiceId}: {c.text}");
            Debug.Log(sb.ToString());
        }

        [ContextMenu("Set Flag = true")]
        public void SetFlag() { Resolve<IStoryStateService>()?.SetBool(flagId, true); Debug.Log($"[DialogueDebug] {flagId} = true"); }

        [ContextMenu("Give Item")]
        public void GiveItem() { Resolve<IInventoryService>()?.Add(itemId, 1); Debug.Log($"[DialogueDebug] +1 {itemId}"); }

        [ContextMenu("Save")]
        public void Save()
        {
            var s = Resolve<ISaveService>();
            if (s != null) Debug.Log($"[DialogueDebug] Save {saveSlot}: {(s.CanSave() ? s.Save(saveSlot) : false)} (CanSave={s.CanSave()})");
        }

        [ContextMenu("Load")]
        public void Load()
        {
            var s = Resolve<ISaveService>();
            if (s != null) Debug.Log($"[DialogueDebug] Load {saveSlot}: {s.Load(saveSlot)}");
        }

        private static string StateStr() => AppContext.State?.Current.ToString() ?? "null";
    }
}
