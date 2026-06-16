using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Services;

namespace JRPG.Bootstrap
{
    /// Single composition root. The only place concrete service implementations are wired up.
    /// Lives in the first-loaded scene.

    [DefaultExecutionOrder(-10000)]
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameDatabase database;
        [SerializeField] private bool logProbeOutput = true;

        public static ServiceRegistry Services { get; private set; }
        public static DataRegistry Data { get; private set; }
        public static GameStateController State { get; private set; }

        private void Awake()
        {
            var bus = new EventBus();
            Services = new ServiceRegistry();
            Services.Register<IEventBus>(bus);
            Services.Register<IServiceRegistry>(Services);

            // Game database -> data registry (with runtime guard)
            if (database == null)
            {
                Debug.LogError("[JRPG.Bootstrap] GameDatabase reference is missing.", this);
                return;
            }

            Data = new DataRegistry();
            try
            {
                Data.Build(database);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[JRPG.Bootstrap] Database build failed: {e.Message}", this);
                return;
            }

            // State controller -> initial state
            State = new GameStateController(bus, new LayeredState(GameMode.MainMenu, OverlayState.None, InputContext.Menu));
            // GameStateController is internal; expose via interface registration too:
            // (No interface for it yet — systems read it through the singleton-style static, or future IService.)

            // Smoke probe (proves bus + state + subscriber wiring)
            if (logProbeOutput)
            {
                var probe = new BootstrapSmokeProbe(bus, Services, Data);
                probe.Run();

                // Drive a state transition so the subscriber observes it.
                State.SetState(new LayeredState(GameMode.Exploration, OverlayState.None, InputContext.Exploration));
            }

            // ISaveService registration: filled in Phase 3.
        }
    }
}
