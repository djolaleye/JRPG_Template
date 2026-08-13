namespace JRPG.Data
{
    /// Which side of an arena a spawn formation belongs to. Mirrors the party/enemy split the
    /// combat engine already makes in <c>BattleContext</c>.
    public enum ArenaTeam
    {
        Party,
        Enemy,
    }

    /// <summary>
    /// The authored camera anchors an arena must provide.
    /// </summary>
    public enum ArenaAnchorKind
    {
        Intro,
        Battle,
        /// Intermediate placement the pull-back passes through so the party enters frame first.
        PartyReveal,
    }
}
