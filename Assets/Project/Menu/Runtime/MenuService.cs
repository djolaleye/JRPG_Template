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

        public bool HasMenu(string menuId)
        {
            var entry = _registry.Find(menuId);
            return entry != null && entry.canvasPrefab != null;
        }

        /// Set only for the duration of a screen's instantiation.
        private MenuContext _openingContext;

        /// <summary>
        /// The context belonging to the screen currently coming up, or the one on top of the stack.
        /// A <see cref="MenuController"/> adopts this on enable so a selection made on one screen is
        /// visible to the next, rather than every screen starting from a blank context.
        /// </summary>
        public MenuContext ActiveContext => _openingContext ?? _machine.Top?.context;

        public void Open(string menuId, object context)
        {
            var entry = _registry.Find(menuId);
            if (entry == null) { Debug.LogError($"[JRPG.Menu] No registry entry for '{menuId}'."); return; }
            if (entry.canvasPrefab == null) { Debug.LogError($"[JRPG.Menu] Missing canvas prefab for '{menuId}'."); return; }

            // A caller outside JRPG.Menu can only reach Open through the lean interface, whose context is
            // an object — so a raw argument (i.e a shop id from an exploration component) is carried
            // into the new context as its Payload rather than dropped.
            var menuContext = context as MenuContext
                              ?? new MenuContext
                              {
                                  Services = JRPG.Core.AppContext.Services,
                                  Menus = this,
                                  Payload = context,
                              };
                              
            // Hide currently-top canvas (stack stays alive for back-nav).
            if (_machine.Top != null && _machine.Top.canvasInstance != null)
                _machine.Top.canvasInstance.SetActive(false);

            // Published before Instantiate: Instantiate runs Awake/OnEnable synchronously, so
            // the new screen's controller reads its context here, before the frame below exists. This is
            // how a selection (which character, which slot) reaches the screen it was made for.
            _openingContext = menuContext;

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
            _openingContext = null;

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

                var entry = _registry.Find(_machine.Top.menuId);
                if (entry != null) _state.SetState(new LayeredState(entry.mode, entry.overlay, entry.input));
                else _state.SetState(_machine.Top.priorState);
            }
            else
            {
                // Empty stack — the popped frame's priorState is the pre-menu gameplay state.
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
