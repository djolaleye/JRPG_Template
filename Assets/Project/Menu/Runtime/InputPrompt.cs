namespace JRPG.Menu
{
    /// <summary>
    /// A single declared input affordance for a menu screen: which InputSystem action it maps to and
    /// the player-facing verb for it. Screens declare these via <see cref="MenuController.Prompts"/>;
    /// a prompt bar widget consumes the declaration and resolves the glyph for the active device.
    /// </summary>
    public readonly struct InputPrompt
    {
        /// <summary>Name of the action in the "Menu" action map (e.g. "Submit", "Cancel", "Tab").</summary>
        public readonly string ActionName;

        /// <summary>Player-facing label for what the action does here (e.g. "Confirm", "Back").</summary>
        public readonly string Label;

        public InputPrompt(string actionName, string label)
        {
            ActionName = actionName;
            Label = label;
        }

        public override string ToString() => $"{ActionName}: {Label}";
    }
}
