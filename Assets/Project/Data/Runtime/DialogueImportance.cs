namespace JRPG.Data
{
    /// Classifies how a node/graph should be presented. Phase 9 uses Normal/Interactive/Critical;
    /// Passive/Tutorial/System are declared now for Phase 10+ but routed through the same presenter.
    public enum DialogueImportance
    {
        Passive,
        Normal,
        Interactive,
        Critical,
        Tutorial,
        System
    }
}
