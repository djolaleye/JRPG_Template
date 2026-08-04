using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using JRPG.Core;
using JRPG.Data;
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

        [Tooltip("Importance → presentation mapping.")]
        [SerializeField] private DialoguePresentationProfile presentationProfile;

        private InputActionMap _map;
        private InputAction _navigate, _submit, _cancel;
        private IEventBus _bus;
        private int _selectedIndex = -1;

        /// True while this presenter holds a reference on the shared "Menu" map.
        private bool _inputHooked;

        /// Input-system time at which this presenter started listening. See MenuInputMap.IsStalePress:
        /// gamepad buttonSouth is BOTH Exploration/Interact and Menu/Submit, so the very press that
        /// starts a conversation would otherwise immediately submit its choice list.
        private double _listeningSince;

        private Color _defaultSpeakerColor = Color.white;
        private bool _capturedDefaultColor;
        /// True while the current node is emphasized/blocking (Critical): cancel/skip is suppressed.
        private bool _blocksInput;

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

            ApplyImportance(snap.importance);

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

        /// Drives presentation from DialogueImportance via the DialoguePresentationProfile
        private void ApplyImportance(DialogueImportance importance)
        {
            var entry = presentationProfile != null ? presentationProfile.Resolve(importance) : Fallback(importance);
            _blocksInput = entry.blocksInput;

            if (speakerName != null)
            {
                if (!_capturedDefaultColor) { _defaultSpeakerColor = speakerName.color; _capturedDefaultColor = true; }
                speakerName.color = Emphasize(entry.emphasisKey, importance, _defaultSpeakerColor);
            }
        }

        private static DialoguePresentationProfile.PresentationEntry Fallback(DialogueImportance importance)
            => new()
            {
                importance = importance,
                usesPassiveOverlay = importance == DialogueImportance.Passive,
                blocksInput = importance == DialogueImportance.Critical,
            };

        private static Color Emphasize(string emphasisKey, DialogueImportance importance, Color fallback)
        {
            string key = string.IsNullOrEmpty(emphasisKey) ? importance.ToString().ToLowerInvariant() : emphasisKey.ToLowerInvariant();
            
            switch (key)
            {
                case "critical": return new Color(1f, 0.55f, 0.45f);
                case "system":   return new Color(0.72f, 0.74f, 0.8f);
                case "tutorial": return new Color(0.55f, 0.85f, 1f);
                default:         return fallback;
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
                if (_inputHooked) return;
                if (_navigate != null) _navigate.performed += OnNavigate;
                if (_submit != null) _submit.performed += OnSubmit;
                if (_cancel != null) _cancel.performed += OnCancel;
                _inputHooked = true;
                _listeningSince = MenuInputMap.Now;
                // Share MenuController's ref-count rather than enabling directly: this presenter used
                // to Enable() and never Disable(), leaving Menu/Navigate|Submit|Cancel live out in
                // exploration after a conversation ended.
                MenuInputMap.Acquire(_map);
            }
            else
            {
                if (!_inputHooked) return;
                if (_navigate != null) _navigate.performed -= OnNavigate;
                if (_submit != null) _submit.performed -= OnSubmit;
                if (_cancel != null) _cancel.performed -= OnCancel;
                _inputHooked = false;
                MenuInputMap.Release(_map);
            }
        }

        private void OnSubmit(InputAction.CallbackContext ctx)
        {
            // The press that opened this conversation is not an answer to it.
            if (MenuInputMap.IsStalePress(ctx, _listeningSince)) return;

            var d = Dialogue;
            if (d == null || !d.IsDialogueActive) return;
            var snap = d.GetCurrentRenderSnapshot();
            if (snap != null && snap.hasChoices) ConfirmSelectedChoice();
            else d.Advance();
        }

        private void OnCancel(InputAction.CallbackContext ctx)
        {
            // Dialogue can't be cancelled mid-flow; Critical/blocking nodes additionally suppress any
            // skip affordance. (No-op today, but honored as skip/advance-hold behavior is added.)
            if (_blocksInput) return;
        }

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
