using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Menu
{
    public sealed class MenuFrame
    {
        public string menuId;
        public MenuContext context;
        public GameObject canvasInstance;
        public LayeredState priorState;
    }

    public sealed class MenuStateMachine
    {
        private readonly Stack<MenuFrame> _frames = new();
        public int Depth => _frames.Count;
        public bool IsEmpty => _frames.Count == 0;
        public MenuFrame Top => _frames.Count > 0 ? _frames.Peek() : null;
        public IEnumerable<MenuFrame> Frames => _frames;

        public void Push(MenuFrame frame) => _frames.Push(frame);
        public MenuFrame Pop() => _frames.Count > 0 ? _frames.Pop() : null;
        public void Clear() => _frames.Clear();
    }
}
