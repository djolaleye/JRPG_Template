using UnityEngine;
using JRPG.Core;
using JRPG.Data;

namespace JRPG.Bootstrap
{

    /// sanity check: exercises Register/Resolve, ID lookup, and GameStateChanged.
    /// Removed when meaningful systems exist that exercise the same paths.

    internal sealed class BootstrapSmokeProbe
    {
        private readonly IEventBus _bus;
        private readonly ServiceRegistry _services;
        private readonly DataRegistry _data;

        public BootstrapSmokeProbe(IEventBus bus, ServiceRegistry services, DataRegistry data)
        {
            _bus = bus;
            _services = services;
            _data = data;
        }

        public void Run()
        {
            // 1. Service register/resolve.
            var resolvedBus = _services.Resolve<IEventBus>();
            Debug.Log($"[JRPG.Probe] IEventBus resolved: {resolvedBus != null} (sameRef={ReferenceEquals(resolvedBus, _bus)})");

            // 2. Data registry counts.
            Debug.Log($"[JRPG.Probe] DataRegistry: {_data.CharactersById.Count} characters, {_data.EnemiesById.Count} enemies.");

            // 3. Subscribe to GameStateChanged.
            _bus.Subscribe<GameStateChanged>(OnStateChanged);
        }

        private void OnStateChanged(GameStateChanged evt)
        {
            Debug.Log($"[JRPG.Probe] GameStateChanged: {evt.Previous} -> {evt.Current}");
        }
    }
}
