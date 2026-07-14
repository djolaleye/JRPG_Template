using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using JRPG.Core;
using JRPG.Menu;
using JRPG.Services;

namespace JRPG.Dialogue.UI
{
    /// Renders the active dialogue: resolved speaker + token-resolved body, an advance prompt for
    /// linear nodes, or a choice list (reusing the menu row framework) for choice nodes.
    public sealed class DialoguePresenterController : MonoBehaviour
    {
        [SerializeField] private TMP_Text speakerName;
        [SerializeField] private TMP_Text body;
        [SerializeField] private GameObject continuePrompt;
        [SerializeField] private VerticalListPopulator choicePopulator;
        [SerializeField] private InputActionAsset playerControls;
        [SerializeField] private string actionMapName = "Menu";

        private InputActionMap _map;
        private InputAction _navigate, _submit, _cancel;
        private IEventBus _bus;
        private int _selectedIndex = -1;

        private DialogueService Dialogue
            => AppContext.Services != null && AppContext.Services.TryResolve<IDialogueService>(out var d) ? d as DialogueService : null;

        private void OnEnable()
        {
            _bus = AppContext.Bus;
            _bus?.Subscribe<DialogueNodeEntered>(OnNodeEntered);
            HookInput(true);
            Refresh();
        }

        private void OnDisable()
        {
            _bus?.Unsubscribe<DialogueNodeEntered>(OnNodeEntered);
            HookInput(false);
        }

        private void OnNodeEntered(DialogueNodeEntered e) => Refresh();

        private void Refresh()
        {
            var d = Dialogue;
            if (d == null || !d.IsDialogueActive) return;
            var snap = d.GetCurrentRenderSnapshot();
            if (snap == null) return;

            if (speakerName != null) speakerName.text = snap.speaker.displayName ?? "";
            if (body != null) body.text = snap.body ?? "";

            if (snap.hasChoices)
            {
                var rows = new List<RowModel>();
                foreach (var c in d.GetCurrentChoices())
                    rows.Add(new RowModel { id = c.choiceId, label = c.text, enabled = c.available, action = new ChooseDialogueAction(c.choiceId) });
                choicePopulator?.Populate(rows);
                if (continuePrompt != null) continuePrompt.SetActive(false);
                FocusFirstEnabled();
            }
            else
            {
                choicePopulator?.Populate(null);
                _selectedIndex = -1;
                if (continuePrompt != null) continuePrompt.SetActive(true);
            }
        }

        // ---- Input ----------------------------------------------------------------------------

        private void HookInput(bool subscribe)
        {
            if (playerControls == null) return;
            if (_map == null)
            {
                _map = playerControls.FindActionMap(actionMapName, false);
                if (_map == null) { Debug.LogError($"[JRPG.Dialogue] input map '{actionMapName}' not found."); return; }
                _navigate = _map.FindAction("Navigate", false);
                _submit = _map.FindAction("Submit", false);
                _cancel = _map.FindAction("Cancel", false);
            }
            if (subscribe)
            {
                if (_navigate != null) _navigate.performed += OnNavigate;
                if (_submit != null) _submit.performed += OnSubmit;
                if (_cancel != null) _cancel.performed += OnCancel;
                _map.Enable();
            }
            else
            {
                if (_navigate != null) _navigate.performed -= OnNavigate;
                if (_submit != null) _submit.performed -= OnSubmit;
                if (_cancel != null) _cancel.performed -= OnCancel;
            }
        }

        private void OnSubmit(InputAction.CallbackContext ctx)
        {
            var d = Dialogue;
            if (d == null || !d.IsDialogueActive) return;
            var snap = d.GetCurrentRenderSnapshot();
            if (snap != null && snap.hasChoices) ConfirmSelectedChoice();
            else d.Advance();
        }

        private void OnCancel(InputAction.CallbackContext ctx) { /* dialogue can't be cancelled mid-flow in Phase 9 */ }

        private void OnNavigate(InputAction.CallbackContext ctx)
        {
            if (choicePopulator == null) return;
            var v = ctx.ReadValue<Vector2>();
            int dir = v.y > 0.5f ? -1 : v.y < -0.5f ? +1 : 0;
            if (dir == 0) return;
            int n = choicePopulator.ActiveRows.Count;
            if (n == 0) return;
            int idx = _selectedIndex;
            for (int step = 0; step < n; step++)
            {
                idx = (idx + dir + n) % n;
                if (choicePopulator.ActiveRows[idx].Model.enabled) { Select(idx); return; }
            }
        }

        private void ConfirmSelectedChoice()
        {
            if (choicePopulator == null || _selectedIndex < 0 || _selectedIndex >= choicePopulator.ActiveRows.Count) return;
            var model = choicePopulator.ActiveRows[_selectedIndex].Model;
            if (model.enabled) model.action?.Execute(model.context);
        }

        private void FocusFirstEnabled()
        {
            _selectedIndex = -1;
            for (int i = 0; i < choicePopulator.ActiveRows.Count; i++)
                if (choicePopulator.ActiveRows[i].Model.enabled) { Select(i); return; }
        }

        private void Select(int idx)
        {
            for (int i = 0; i < choicePopulator.ActiveRows.Count; i++)
            {
                var row = choicePopulator.ActiveRows[i];
                if (i == idx) row.SetVisualState(RowState.Selected);
                else row.SetVisualState(row.Model.enabled ? RowState.Normal : RowState.Disabled);
            }
            _selectedIndex = idx;
            if (EventSystem.current != null)
            {
                var sel = choicePopulator.ActiveRows[idx].Selectable;
                if (sel != null) EventSystem.current.SetSelectedGameObject(sel.gameObject);
            }
        }
    }
}
