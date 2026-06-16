namespace JRPG.Core
{
    public enum GameMode
    {
        MainMenu,
        Exploration,
        Combat,
        Cutscene
    }

    public enum OverlayState
    {
        None,
        PauseMenu,
        CombatMenu,
        DialoguePassive,
        DialogueInteractive,
        RewardScreen,
        LevelUpScreen,
        Inventory,
        SaveMenu
    }

    public enum InputContext
    {
        Exploration,
        Menu,
        CombatCommand,
        CombatTargeting,
        Dialogue,
        Disabled
    }
}
