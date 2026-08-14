namespace JRPG.Core
{
    /// <summary>
    /// How hard the game is set to be. Chosen at New Game and changeable from the pause menu.
    ///
    /// <para>The values are persisted, so their order is part of the save format.</para>
    /// </summary>
    public enum Difficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
    }
}
