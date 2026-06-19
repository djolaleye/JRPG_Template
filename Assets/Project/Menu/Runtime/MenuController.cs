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

        protected InputActionMap _map;
        protected InputAction _navigate;
        protected InputAction _submit;
        protected InputAction _cancel;

        protected int _selectedIndex = -1;

        protected MenuContext Context;

        protected virtual void OnEnable()
        {
            Context = new MenuContext
            {
                Services = AppContext.Services,
                Menus = AppContext.Services?.TryResolve<IMenuService>(out var ms) == true ? ms : null
            };
            RebuildAndFocus();
            HookInput(true);
        }

        protected virtual void OnDisable()
        {
            HookInput(false);
        }

        protected void RebuildAndFocus()
        {
            var rows = BuildRows();
            if (populator != null) populator.Populate(rows);
            // Initial focus: first executable row.
            _selectedIndex = -1;
            for (int i = 0; i < populator.ActiveRows.Count; i++)
            {
                if (RowEnabled(i)) { Select(i); break; }
            }
        }

        protected abstract IReadOnlyList<RowModel> BuildRows();

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

        private void OnNavigate(InputAction.CallbackContext ctx)
        {
            var v = ctx.ReadValue<Vector2>();
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

        private void OnSubmit(InputAction.CallbackContext ctx)
        {
            if (_selectedIndex < 0 || _selectedIndex >= populator.ActiveRows.Count) return;
            var row = populator.ActiveRows[_selectedIndex];
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
            // After executing, rebuild — quantities or selection state may have changed.
            RebuildAndFocus();
        }

        private void OnCancel(InputAction.CallbackContext ctx)
        {
            Context.Menus?.Close();
        }

        private void Select(int idx)
        {
            for (int i = 0; i < populator.ActiveRows.Count; i++)
            {
                var row = populator.ActiveRows[i];
                if (i == idx) row.SetVisualState(RowState.Selected);
                else row.SetVisualState(row.Model.enabled ? RowState.Normal : RowState.Disabled);
            }
            _selectedIndex = idx;
            if (EventSystem.current != null)
            {
                var sel = populator.ActiveRows[idx].Selectable;
                if (sel != null) EventSystem.current.SetSelectedGameObject(sel.gameObject);
            }
        }

        private bool RowEnabled(int idx)
        {
            if (idx < 0 || idx >= populator.ActiveRows.Count) return false;
            return populator.ActiveRows[idx].Model.enabled;
        }
    }
}
