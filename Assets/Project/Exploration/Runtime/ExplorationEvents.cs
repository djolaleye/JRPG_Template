using UnityEngine;

namespace JRPG.Exploration
{
    public readonly struct InteractionTriggered
    {
        public readonly GameObject Target;
        public readonly Vector3 Position;
        public InteractionTriggered(GameObject target, Vector3 position) { Target = target; Position = position; }
    }

    // CombatInitiationRequested moved to JRPG.Core (CombatEvents.cs) so the combat UI can subscribe
    // without referencing JRPG.Exploration. ExplorationInputBridge still publishes it via `using JRPG.Core`.
}
