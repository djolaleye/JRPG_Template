// Event-bus surface (JRPG.Core.EventBus, struct-only, synchronous).
//
// Many declared events are published but have no subscriber yet — this is a deliberate
// forward-compatibility surface, not dead code: UI refreshes by rebuilding rows on demand rather than
// reacting, and gameplay systems publish these so future features (analytics, reactive UI, quest
// hooks) can subscribe without touching the publishers. Live (subscribed) events today are the combat
// loop (BattleStarted/TurnStarted/BattleActionResolved/BattleEnded/BattleResultPackaged),
// PostBattleFlowStarted, DialogueNodeEntered, CombatInitiationRequested, GameStateChanged, and the new
// CharacterRosterStateChanged. The rest (menu/party/save/inventory/most dialogue+progression events)
// are publish-only by design.
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
