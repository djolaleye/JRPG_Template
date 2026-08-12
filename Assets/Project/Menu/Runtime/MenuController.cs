using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
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

        /// <summary>
        /// When this screen started listening, for <see cref="MenuInputMap.IsStalePress"/>. Exposed so a
        /// subclass that overrides <see cref="OnSubmit"/> outright can still reject the press that
        /// opened it.
        /// </summary>
        protected double ListeningSince => _listeningSince;

        // ---- Lifecycle ----------------------------------------------------------------------------

        protected virtual void OnEnable()
        {
            var menus = AppContext.Services?.TryResolve<IMenuService>(out var ms) == true ? ms : null;

            // Adopt the context this screen was opened with, so a selection made on the previous screen
            // (which character, which slot) is visible here.
            Context = (menus as MenuService)?.ActiveContext ?? new MenuContext();

            Context.Services ??= AppContext.Services;
            Context.Menus ??= menus;
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

        /// <summary>
        /// A row that navigates to another screen, with its enabled state and its disabled reason both
        /// taken from the action's own <see cref="IMenuAction.CanExecute"/>/<see cref="IMenuAction.GetDisabledReason"/>.
        /// </summary>
        protected RowModel NavigationRow(string id, string label, string menuId,
                                         string unavailableReason = null,
                                         System.Func<MenuContext, string> gate = null)
        {
            var action = new OpenSubmenuAction(menuId, unavailableReason, gate);
            bool allowed = action.CanExecute(Context);

            return RowModel.Simple(id, label, action, Context,
                                   enabled: allowed,
                                   disabledReason: allowed ? null : action.GetDisabledReason(Context));
        }

        // ---- Input --------------------------------------------------------------------------------

        private void HookInput(bool subscribe)
        {
            // Editor-only activations — PrefabUtility.LoadPrefabContents opening a screen in a preview
            // scene, prefab-stage editing, a scene being authored — run OnEnable without a live input
            // state behind the asset, so enabling the map throws "Map must be contained in state". A
            // menu has nothing to listen to outside play mode anyway.
            if (!Application.isPlaying) return;

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

            ScrollIntoView(idx);

            var model = rows[idx].Model;
            OnHighlightChanged(idx, model);
            HighlightChanged?.Invoke(idx, model);
        }

        /// Called after focus changes. Default no-op; detail panels override to mirror the highlight.
        protected virtual void OnHighlightChanged(int index, RowModel model) { }

        private ScrollRect _scroll;
        private bool _scrollResolved;
        private Coroutine _deferredScroll;

        /// <summary>
        /// Keeps the focused row inside its scroll viewport.
        ///
        /// <para><b>uGUI does not do this for us.</b> EventSystem selection and <see cref="ScrollRect"/>
        /// position are unrelated systems, so any list taller than its viewport can focus a row the
        /// player cannot see: the cursor disappears off the bottom edge and the screen reads as frozen.
        /// A skill list at the 8-known cap plus an equipment grant is exactly that case — the granted
        /// row lands past the fold — but so is any long inventory or save-slot list, which is why this
        /// lives on the base controller instead of in one screen.</para>
        ///
        /// <para><b>Applied twice, deliberately.</b> The first focus of a screen happens inside
        /// <c>OnEnable</c> → <see cref="RebuildAndFocus"/>, before the <c>ContentSizeFitter</c> and
        /// layout group have run — so the content is still its authored size, the row's measured
        /// position is meaningless, and the ScrollRect clamps the correction straight back to zero. The
        /// immediate pass keeps steady-state navigation instant; the one-frame-deferred pass is what
        /// makes the very first frame land correctly.</para>
        ///
        /// <para>Scrolls by the minimum needed, so moving down a list nudges rather than re-centres.
        /// Instant rather than animated, so reduced motion does not affect it.</para>
        /// </summary>
        private void ScrollIntoView(int index)
        {
            ApplyScroll(index);

            // Pooled rows are re-bound on rebuild, so the retry re-resolves by index rather than
            // holding a RectTransform that may have been recycled underneath it.
            if (!isActiveAndEnabled) return;
            if (_deferredScroll != null) StopCoroutine(_deferredScroll);
            _deferredScroll = StartCoroutine(ScrollAfterLayout(index));
        }

        private IEnumerator ScrollAfterLayout(int index)
        {
            yield return null;
            ApplyScroll(index);
            _deferredScroll = null;
        }

        private void ApplyScroll(int index)
        {
            if (populator == null) return;
            var rows = populator.ActiveRows;
            if (rows == null || index < 0 || index >= rows.Count) return;

            var row = rows[index].transform as RectTransform;
            if (row == null) return;

            // Rows are pooled under the scroll content, so the ScrollRect is found from a row, not from
            // the populator — which may sit on the canvas root, outside the scroll hierarchy entirely.
            if (!_scrollResolved)
            {
                _scroll = row.GetComponentInParent<ScrollRect>();
                _scrollResolved = true;
            }

            if (_scroll == null || _scroll.content == null || _scroll.viewport == null) return;

            Canvas.ForceUpdateCanvases();

            var rowCorners = new Vector3[4];
            var viewCorners = new Vector3[4];
            row.GetWorldCorners(rowCorners);
            _scroll.viewport.GetWorldCorners(viewCorners);

            float rowBottom = rowCorners[0].y, rowTop = rowCorners[1].y;
            float viewBottom = viewCorners[0].y, viewTop = viewCorners[1].y;

            // A row taller than the viewport can never fit; align its top and leave it there rather
            // than oscillating between two unsatisfiable corrections.
            float delta;
            if (rowTop - rowBottom > viewTop - viewBottom) delta = rowTop - viewTop;
            else if (rowTop > viewTop) delta = rowTop - viewTop;
            else if (rowBottom < viewBottom) delta = rowBottom - viewBottom;
            else return;                                   // already fully visible

            float scale = _scroll.content.lossyScale.y;
            if (Mathf.Approximately(scale, 0f)) return;

            // Any in-flight inertia would otherwise fight the correction on the next LateUpdate.
            _scroll.StopMovement();

            var pos = _scroll.content.anchoredPosition;
            pos.y -= delta / scale;
            _scroll.content.anchoredPosition = pos;
        }

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
