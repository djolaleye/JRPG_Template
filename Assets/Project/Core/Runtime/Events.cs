namespace JRPG.Core
{
    public readonly struct GameStateChanged
    {
        public readonly LayeredState Previous;
        public readonly LayeredState Current;

        public GameStateChanged(LayeredState previous, LayeredState current)
        {
            Previous = previous;
            Current = current;
        }
    }
}
