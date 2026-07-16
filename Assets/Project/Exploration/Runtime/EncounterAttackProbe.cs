using UnityEngine;
using JRPG.Core;
using JRPG.Services;

namespace JRPG.Exploration
{
    /// <summary>
    /// Lives on the player. Listens for the Attack-driven <see cref="CombatInitiationRequested"/>
    /// (published by <see cref="ExplorationInputBridge"/>) and, if an <see cref="EncounterTrigger"/> is
    /// within its range, starts that encounter's battle through <see cref="ICombatService"/>. Does
    /// nothing when no trigger is in range or a battle is already running.
    /// </summary>
    public sealed class EncounterAttackProbe : MonoBehaviour
    {
        private IEventBus _bus;

        private void OnEnable()
        {
            _bus = AppContext.Bus;
            _bus?.Subscribe<CombatInitiationRequested>(OnRequested);
        }

        private void OnDisable() => _bus?.Unsubscribe<CombatInitiationRequested>(OnRequested);

        private void OnRequested(CombatInitiationRequested e)
        {
            var services = AppContext.Services;
            if (services == null || !services.TryResolve<ICombatService>(out var combat)) return;
            if (combat.IsInBattle) return;

            // Only initiate from exploration — never while a menu, battle, or dialogue owns input.
            if (AppContext.State == null || AppContext.State.Current.Input != InputContext.Exploration) return;

            var trigger = FindNearestTriggerInRange();
            if (trigger == null) return;

            combat.StartBattleFromActiveParty(trigger.encounterId);
        }

        private EncounterTrigger FindNearestTriggerInRange()
        {
            var triggers = Object.FindObjectsByType<EncounterTrigger>(FindObjectsSortMode.None);
            EncounterTrigger best = null;
            float bestSq = float.PositiveInfinity;
            Vector3 p = transform.position;

            for (int i = 0; i < triggers.Length; i++)
            {
                var t = triggers[i];
                if (t == null || string.IsNullOrEmpty(t.encounterId)) continue;
                float d2 = (t.transform.position - p).sqrMagnitude;
                float r = t.range;
                if (d2 <= r * r && d2 < bestSq) { bestSq = d2; best = t; }
            }
            return best;
        }
    }
}
