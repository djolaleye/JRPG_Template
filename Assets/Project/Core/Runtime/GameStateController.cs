namespace JRPG.Core
{
    public sealed class GameStateController
    {
        private readonly IEventBus _bus;

        public LayeredState Current { get; private set; }

        public GameStateController(IEventBus bus, LayeredState initial)
        {
            _bus = bus;
            Current = initial;
        }

        public void SetState(LayeredState next)
        {
            var previous = Current;
            Current = next;
            
            _bus.Publish(new GameStateChanged(previous, next));
        }
    }
}
