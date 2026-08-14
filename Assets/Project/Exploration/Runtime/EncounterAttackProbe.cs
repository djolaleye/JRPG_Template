using UnityEngine;
using JRPG.Core;
using JRPG.Services;

namespace JRPG.Exploration
{
    /// <summary>
    /// Lives on the player. Listens for the Attack-driven <see cref="CombatInitiationRequested"/>
    /// (published by <see cref="ExplorationInputBridge"/>) and, if an <see cref="EncounterTrigger"/> is
    /// within its range, hands that encounter to <see cref="IEncounterTransitionService"/>.
    ///
    /// <para><b>It asks; it does not decide.</b> Whether the encounter may start (Ready vs. InProgress
    /// vs. Complete), whether an arena resolves, and everything about freezing and staging belongs to
    /// the transition service. Duplicating any of it here is how a trigger and the world end up
    /// disagreeing.</para>
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
            if (services == null) return;

            // Only initiate from exploration — never while a menu, battle, or dialogue owns input.
            if (AppContext.State == null || AppContext.State.Current.Input != InputContext.Exploration) return;

            var trigger = FindNearestTriggerInRange();
            if (trigger == null) return;

            if (!services.TryResolve<IEncounterTransitionService>(out var transition))
            {
                Debug.LogWarning("[JRPG.Exploration] No IEncounterTransitionService registered — is the " +
                                 "CombatArenaDirector missing from the Startup scene?", this);
                return;
            }

            // The trigger's own transform is what the camera frames: the trigger sits on the world
            // object the player just attacked.
            transition.RequestEncounter(trigger.encounterId, trigger.transform);
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
