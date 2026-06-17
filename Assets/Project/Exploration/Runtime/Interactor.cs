using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Exploration
{
    public class Interactor : MonoBehaviour
    {
        [SerializeField] private Transform interactOrigin;
        [SerializeField] private float radius = 1.0f;
        [SerializeField] private LayerMask interactableMask = ~0;
        [SerializeField] private bool drawDebug = false;

        public void TryInteract()
        {
            if (interactOrigin == null)
            {
                Debug.LogWarning("[JRPG.Exploration] Interactor: interactOrigin not assigned.", this);
                return;
            }

            Vector3 probeCenter = interactOrigin.position;

            if (drawDebug)
                Debug.DrawRay(probeCenter, Vector3.up * 0.5f, Color.cyan, 1f);

            var hits = Physics.OverlapSphere(probeCenter, radius, interactableMask, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0) return;

            // Pick the nearest IInteractable.
            IInteractable bestInteractable = null;
            GameObject bestTarget = null;
            Vector3 bestPoint = probeCenter;
            float bestDistSq = float.PositiveInfinity;

            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i];
                if (col == null) continue;
                if (col.transform.IsChildOf(transform)) continue; // skip self
                var interactable = col.GetComponentInParent<IInteractable>();
                if (interactable == null) continue;
                float d2 = (col.bounds.center - probeCenter).sqrMagnitude;
                if (d2 < bestDistSq)
                {
                    bestDistSq = d2;
                    bestInteractable = interactable;
                    bestTarget = col.gameObject;
                    bestPoint = col.bounds.ClosestPoint(probeCenter);
                }
            }

            if (bestInteractable == null) return;

            bestInteractable.Interact(gameObject);

            var services = AppContext.Services;
            if (services != null && services.TryResolve<IEventBus>(out var bus))
                bus.Publish(new InteractionTriggered(bestTarget, bestPoint));
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (interactOrigin == null) return;
            Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
            Gizmos.DrawWireSphere(interactOrigin.position, radius);
        }
#endif
    }
}
