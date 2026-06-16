namespace JRPG.Core
{
    public readonly struct LayeredState
    {
        public readonly GameMode Mode;
        public readonly OverlayState Overlay;
        public readonly InputContext Input;

        public LayeredState(GameMode mode, OverlayState overlay, InputContext input)
        {
            Mode = mode;
            Overlay = overlay;
            Input = input;
        }

        public override string ToString() => $"{Mode} + {Overlay} + {Input}";
    }
}
