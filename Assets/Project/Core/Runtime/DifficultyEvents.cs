namespace JRPG.Core
{
    /// The difficulty setting changed. Carries both ends so a subscriber can tell a raise from a drop
    /// without re-querying.
    public readonly struct DifficultyChanged
    {
        public readonly Difficulty Previous;
        public readonly Difficulty Current;

        public DifficultyChanged(Difficulty previous, Difficulty current)
        {
            Previous = previous;
            Current = current;
        }
    }
}
