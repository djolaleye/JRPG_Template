namespace JRPG.Save
{
    public readonly struct SaveRequested
    {
        public readonly int Slot;
        public SaveRequested(int slot) { Slot = slot; }
    }

    public readonly struct GameSaved
    {
        public readonly int Slot;
        public GameSaved(int slot) { Slot = slot; }
    }

    public readonly struct GameLoaded
    {
        public readonly int Slot;
        public GameLoaded(int slot) { Slot = slot; }
    }
}
