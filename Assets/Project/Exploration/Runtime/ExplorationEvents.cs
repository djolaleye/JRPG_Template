using UnityEngine;

namespace JRPG.Exploration
{
    public readonly struct InteractionTriggered
    {
        public readonly GameObject Target;
        public readonly Vector3 Position;
        public InteractionTriggered(GameObject target, Vector3 position) { Target = target; Position = position; }
    }

    public readonly struct MenuOpened
    {
        public readonly string MenuId;
        public MenuOpened(string menuId) { MenuId = menuId; }
    }

    public readonly struct CombatInitiationRequested
    {
        public readonly string Reason;
        public CombatInitiationRequested(string reason) { Reason = reason; }
    }
}
