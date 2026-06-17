using UnityEngine;
using JRPG.Core;

namespace JRPG.Exploration
{
    public class Interactor : MonoBehaviour
    {
        [SerializeField] private Transform interactOrigin;
        [SerializeField] private float radius = 0.5f;
        [SerializeField] private float distance = 1.5f;
        [SerializeField] private LayerMask interactableMask = ~0;

        public void TryInteract()
        {
            if (interactOrigin == null) return;

            var ray = new Ray(interactOrigin.position, interactOrigin.forward);
            if (!Physics.SphereCast(ray, radius, out var hit, distance, interactableMask, QueryTriggerInteraction.Collide))
                return;

            var target = hit.collider.gameObject;
            var interactable = target.GetComponentInParent<IInteractable>();
            if (interactable == null) return;

            interactable.Interact(gameObject);

            var services = AppContext.Services;
            if (services != null && services.TryResolve<IEventBus>(out var bus))
                bus.Publish(new InteractionTriggered(target, hit.point));
        }
    }
}
