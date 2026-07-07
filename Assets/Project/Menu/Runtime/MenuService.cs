using System;
using UnityEngine;
using JRPG.Core;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// Single menu authority. Instantiates canvases from the registry, manages a screen stack, and
    /// drives layered game state on open/close. Emits <see cref="MenuOpened"/> / <see cref="MenuClosed"/>.
    /// </summary>
    public sealed class MenuService : IMenuService
    {
        private readonly ContextualCanvasRegistry _registry;
        private readonly Transform _parent;
        private readonly GameStateController _state;
        private readonly IEventBus _bus;
        private readonly MenuStateMachine _machine = new();

        public string ActiveMenuId => _machine.Top?.menuId;
        public int Depth => _machine.Depth;

        public MenuService(ContextualCanvasRegistry registry, Transform parent, GameStateController state, IEventBus bus)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        public void Open(string menuId, object context)
        {
            var entry = _registry.Find(menuId);
            if (entry == null) { Debug.LogError($"[JRPG.Menu] No registry entry for '{menuId}'."); return; }
            if (entry.canvasPrefab == null) { Debug.LogError($"[JRPG.Menu] Missing canvas prefab for '{menuId}'."); return; }

            var menuContext = context as MenuContext ?? new MenuContext { Services = JRPG.Core.AppContext.Services, Menus = this };
            // Hide currently-top canvas (stack stays alive for back-nav).
            if (_machine.Top != null && _machine.Top.canvasInstance != null)
                _machine.Top.canvasInstance.SetActive(false);

            var go = UnityEngine.Object.Instantiate(entry.canvasPrefab, _parent);
            go.name = "Menu_" + menuId;

            // Normalize the root RectTransform so the menu fills its parent at unit scale whether root or nested.
            if (go.transform is RectTransform rt)
            {
                rt.localScale = Vector3.one;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            var frame = new MenuFrame
            {
                menuId = menuId,
                context = menuContext,
                canvasInstance = go,
                priorState = _state.Current
            };

            _machine.Push(frame);
            _state.SetState(new LayeredState(entry.mode, entry.overlay, entry.input));
            _bus.Publish(new MenuOpened(menuId));
        }

        public void Close()
        {
            var top = _machine.Pop();
            if (top == null) return;
            if (top.canvasInstance != null) UnityEngine.Object.Destroy(top.canvasInstance);

            if (_machine.Top != null)
            {
                if (_machine.Top.canvasInstance != null) _machine.Top.canvasInstance.SetActive(true);
                // Restore the layered state captured when the now-top frame was opened.
                _state.SetState(_machine.Top.priorState);
            }
            else
            {
                // Empty stack — return to the layered state captured for the frame we just popped.
                _state.SetState(top.priorState);
            }

            _bus.Publish(new MenuClosed(top.menuId));
        }

        public void CloseAll()
        {
            // Capture the bottom-most frame's priorState (state before any menu opened).
            LayeredState? priorBottom = null;
            foreach (var frame in _machine.Frames)
                priorBottom = frame.priorState;

            while (_machine.Top != null)
            {
                var top = _machine.Pop();
                if (top.canvasInstance != null) UnityEngine.Object.Destroy(top.canvasInstance);
                _bus.Publish(new MenuClosed(top.menuId));
            }
            
            if (priorBottom.HasValue) _state.SetState(priorBottom.Value);
        }
    }
}
