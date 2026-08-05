using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using JRPG.Core;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// Base controller for one menu screen. Owns selection and dispatches Submit/Cancel through
    /// <see cref="IMenuAction"/> + <see cref="IMenuService"/>.
    /// </summary>
    public abstract class MenuController : MonoBehaviour
    {
        [SerializeField] protected VerticalListPopulator populator;
        [SerializeField] protected InputActionAsset playerControls;
        [SerializeField] protected string actionMapName = "Menu";
        [SerializeField] protected string menuId;

        [Tooltip("Optional. Left unassigned, the first InputPromptBar under this canvas is used. The bar " +
                 "is fed from Prompts on enable.")]
        [SerializeField] protected InputPromptBar promptBar;

        protected InputActionMap _map;
        protected InputAction _navigate;
        protected InputAction _submit;
        protected InputAction _cancel;
        protected InputAction _tab;
        protected InputAction _pageLeft;
        protected InputAction _pageRight;

        protected int _selectedIndex = -1;

        protected MenuContext Context;

        /// True while this instance holds a reference on the shared action map.
        private bool _inputHooked;

        /// Input-system time at which this screen started listening. Presses that BEGAN before this
        /// were meant for whatever was on screen previously — see <see cref="MenuInputMap.IsStalePress"/>.
        private double _listeningSince;

        // ---- Lifecycle ----------------------------------------------------------------------------

        protected virtual void OnEnable()
        {
            Context = new MenuContext
            {
                Services = AppContext.Services,
                Menus = AppContext.Services?.TryResolve<IMenuService>(out var ms) == true ? ms : null
            };
            RebuildAndFocus();
            HookInput(true);
            RefreshPrompts();
        }

        protected virtual void OnDisable()
        {
            HookInput(false);
        }

        protected void RebuildAndFocus()
        {
            var rows = BuildRows();
            // Initial focus: first executable row.
            _selectedIndex = -1;

            if (populator == null) return;
            
            populator.Populate(rows);
            for (int i = 0; i < populator.ActiveRows.Count; i++)
            {
                if (RowEnabled(i)) { Select(i); break; }
            }
        }

        protected abstract IReadOnlyList<RowModel> BuildRows();

        // ---- Input --------------------------------------------------------------------------------

        private void HookInput(bool subscribe)
        {
            if (playerControls == null) return;
            if (_map == null)
            {
                _map = playerControls.FindActionMap(actionMapName, false);
                if (_map == null) { Debug.LogError($"[JRPG.Menu] map '{actionMapName}' not found."); return; }
                _navigate = _map.FindAction("Navigate", false);
                _submit = _map.FindAction("Submit", false);
                _cancel = _map.FindAction("Cancel", false);
                // Optional — authored but not necessarily bound; screens opt in by overriding the hooks.
                _tab = _map.FindAction("Tab", false);
                _pageLeft = _map.FindAction("PageL", false);
                _pageRight = _map.FindAction("PageR", false);
            }
            if (subscribe)
            {
                if (_inputHooked) return;
                if (_navigate != null) _navigate.performed += OnNavigate;
                if (_submit != null) _submit.performed += OnSubmit;
                if (_cancel != null) _cancel.performed += OnCancel;
                if (_tab != null) _tab.performed += OnTabPerformed;
                if (_pageLeft != null) _pageLeft.performed += OnPageLeftPerformed;
                if (_pageRight != null) _pageRight.performed += OnPageRightPerformed;
                _inputHooked = true;
                _listeningSince = MenuInputMap.Now;
                MenuInputMap.Acquire(_map);
            }
            else
            {
                if (!_inputHooked) return;
                if (_navigate != null) _navigate.performed -= OnNavigate;
                if (_submit != null) _submit.performed -= OnSubmit;
                if (_cancel != null) _cancel.performed -= OnCancel;
                if (_tab != null) _tab.performed -= OnTabPerformed;
                if (_pageLeft != null) _pageLeft.performed -= OnPageLeftPerformed;
                if (_pageRight != null) _pageRight.performed -= OnPageRightPerformed;
                _inputHooked = false;
                MenuInputMap.Release(_map);
            }
        }

        /// <summary>
        /// True while a modal this screen raised (a <see cref="ConfirmPromptController"/>) owns input.
        /// Navigate/Submit/Cancel on the row list are suppressed for as long as it is.
        ///
        /// <para>Needed because the modal and this screen subscribe to the <i>same</i> Menu action map:
        /// without the gate, one Submit both answers the prompt and re-fires the row underneath it.
        /// The modal itself decides nothing here — it just has input priority while open.</para>
        /// </summary>
        protected virtual bool ModalActive => false;

        private void OnNavigate(InputAction.CallbackContext ctx)
        {
            if (ModalActive) return;
            if (populator == null) return;
            var v = ctx.ReadValue<Vector2>();

            // Horizontal flick (no meaningful vertical component) is a separate channel.
            if (Mathf.Abs(v.x) > 0.5f && Mathf.Abs(v.y) <= 0.5f)
            {
                OnNavigateHorizontal(v.x > 0f ? +1 : -1);
                return;
            }

            int dir = v.y > 0.5f ? -1 : v.y < -0.5f ? +1 : 0;
            if (dir == 0) return;
            int n = populator.ActiveRows.Count;
            if (n == 0) return;
            int idx = _selectedIndex;
            for (int step = 0; step < n; step++)
            {
                idx = (idx + dir + n) % n;
                if (RowEnabled(idx)) { Select(idx); return; }
            }
        }

        /// Left/right on the Navigate stick. Default is a no-op; tabbed or paired-panel screens override.
        protected virtual void OnNavigateHorizontal(int dir) { }

        private void OnTabPerformed(InputAction.CallbackContext ctx) => OnTab();
        private void OnPageLeftPerformed(InputAction.CallbackContext ctx) => OnPageLeft();
        private void OnPageRightPerformed(InputAction.CallbackContext ctx) => OnPageRight();

        /// Cycle to the next tab/category. Default no-op.
        protected virtual void OnTab() { }

        /// Previous page/tab. Default no-op.
        protected virtual void OnPageLeft() { }

        /// Next page/tab. Default no-op.
        protected virtual void OnPageRight() { }

        /// <summary>
        /// Virtual so a screen can interpose before a row runs — the save screen asks for overwrite
        /// confirmation here, then calls <see cref="ExecuteRow"/> once the player says yes.
        /// </summary>
        protected virtual void OnSubmit(InputAction.CallbackContext ctx)
        {
            if (ModalActive) return;

            // A press that began before this screen opened belongs to the screen that opened it —
            // e.g. gamepad buttonSouth is both Exploration/Interact and Menu/Submit.
            if (MenuInputMap.IsStalePress(ctx, _listeningSince)) return;

            ExecuteRow(_selectedIndex);
        }

        /// <summary>
        /// Runs a row's action, honouring its own <see cref="IMenuAction.CanExecute"/> gate, then
        /// rebuilds — quantities, slot contents or selection state may have changed. Shared by the
        /// normal Submit path and by any screen that defers execution behind a confirmation.
        /// </summary>
        protected void ExecuteRow(int index)
        {
            if (populator == null) return;
            if (index < 0 || index >= populator.ActiveRows.Count) return;

            var row = populator.ActiveRows[index];
            var model = row.Model;
            if (model.action == null) return;

            var ctxObj = model.context ?? Context;
            if (!model.action.CanExecute(ctxObj))
            {
                row.SetVisualState(RowState.Invalid);
                Debug.Log($"[JRPG.Menu] disabled: {model.action.GetDisabledReason(ctxObj)}");
                return;
            }

            row.SetVisualState(RowState.Confirmed);
            model.action.Execute(ctxObj);
            RebuildAndFocus();
        }

        protected virtual void OnCancel(InputAction.CallbackContext ctx)
        {
            if (ModalActive) return;
            if (MenuInputMap.IsStalePress(ctx, _listeningSince)) return;
            Context.Menus?.Close();
        }

        // ---- Selection ----------------------------------------------------------------------------

        /// Index of the currently highlighted row, or -1 when nothing is focused.
        protected int HighlightedIndex => _selectedIndex;

        /// Raised whenever focus lands on a row. Paired detail panels subscribe instead of polling.
        public event System.Action<int, RowModel> HighlightChanged;

        /// Move focus to <paramref name="idx"/>. Protected so tabbed screens can drive focus directly.
        protected void Select(int idx)
        {
            if (populator == null) return;
            var rows = populator.ActiveRows;

            if (rows == null || rows.Count == 0) return;
            if (idx < 0 || idx >= rows.Count) return;

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (i == idx) row.SetVisualState(RowState.Selected);
                else row.SetVisualState(row.Model.enabled ? RowState.Normal : RowState.Disabled);
            }
            
            _selectedIndex = idx;
            if (EventSystem.current != null)
            {
                var sel = rows[idx].Selectable;
                if (sel != null) EventSystem.current.SetSelectedGameObject(sel.gameObject);
            }

            var model = rows[idx].Model;
            OnHighlightChanged(idx, model);
            HighlightChanged?.Invoke(idx, model);
        }

        /// Called after focus changes. Default no-op; detail panels override to mirror the highlight.
        protected virtual void OnHighlightChanged(int index, RowModel model) { }

        private bool RowEnabled(int idx)
        {
            if (populator == null) return false;
            if (idx < 0 || idx >= populator.ActiveRows.Count) return false;
            return populator.ActiveRows[idx].Model.enabled;
        }

        // ---- Prompts ------------------------------------------------------------------------------

        private static readonly InputPrompt[] s_defaultPrompts =
        {
            new InputPrompt("Submit", "Confirm"),
            new InputPrompt("Cancel", "Back"),
        };

        /// Input affordances this screen wants advertised. Override to add or replace.
        public virtual IReadOnlyList<InputPrompt> Prompts => s_defaultPrompts;

        /// <summary>
        /// Pushes <see cref="Prompts"/> onto this screen's prompt bar. Called automatically on enable;
        /// call it again from a screen whose prompt set changes with its own mode (a tabbed screen
        /// gaining page prompts, a targeting screen swapping Confirm for Select).
        /// </summary>
        protected void RefreshPrompts()
        {
            if (promptBar == null) promptBar = GetComponentInChildren<InputPromptBar>(true);
            if (promptBar == null) return;

            promptBar.Show(Prompts);
        }
    }
}
