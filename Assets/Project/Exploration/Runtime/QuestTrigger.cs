using UnityEngine;
using JRPG.Core;
using JRPG.Services;

namespace JRPG.Exploration
{
    /// <summary>
    /// A placed quest hook: finds a quest, or reports a location/interaction objective.
    ///
    /// <para>This exists because location and interaction objectives are the two kinds with no
    /// existing event to listen to — a shrine being reached publishes nothing, unlike an enemy dying
    /// or an item being used. Everything else the quest service picks up on its own, and no other
    /// gameplay system needs a component like this.</para>
    ///
    /// <para><b>It reports only.</b> Whether the quest is eligible, whether the
    /// objective belongs to an active quest, and whether anything is granted are all the service's
    /// call.</para>
    ///
    /// <para>Set <see cref="triggerOnEnter"/> for a walk-into volume (needs a trigger collider), or
    /// leave it off for a talk-to/examine interactable.</para>
    /// </summary>
    public sealed class QuestTrigger : MonoBehaviour, IInteractable
    {
        [Header("What this reports")]
        [Tooltip("Discovered when this fires. Leave empty to only report objective progress.")]
        [SerializeField] private string discoverQuestId;

        [Tooltip("Objective kind reported when this fires. ReachLocation and Interact are the two " +
                 "the quest service cannot observe for itself.")]
        [SerializeField] private QuestObjectiveType objectiveType = QuestObjectiveType.Interact;

        [Tooltip("Objective target id — matched against the authored objective's targetId. Empty " +
                 "reports nothing.")]
        [SerializeField] private string objectiveTargetId;

        [Min(1)][SerializeField] private int amount = 1;

        [Header("Firing")]
        [Tooltip("Fire when the player walks into the collider instead of on interact.")]
        [SerializeField] private bool triggerOnEnter;

        [Tooltip("Tag the entering collider must carry. Empty accepts anything.")]
        [SerializeField] private string playerTag = "Player";

        [Tooltip("Fires once per session. A quest already discovered is refused by the service " +
                 "anyway; this also stops repeated objective counts from a volume walked in and out of.")]
        [SerializeField] private bool oneShot = true;

        [Header("Presentation")]
        [Tooltip("Passive line shown when this fires. Empty says nothing.")]
        [SerializeField] private string noticeMessage;
        [SerializeField] private string speakerName = "system";

        /// How often a still-occupied volume re-checks whether it now has something to report.
        private const float RetrySeconds = 0.5f;

        private bool _fired;
        private float _nextRetryTime;

        private IQuestService Quests
            => AppContext.Services != null && AppContext.Services.TryResolve<IQuestService>(out var svc)
                ? svc
                : null;

        public void Interact(GameObject initiator)
        {
            if (triggerOnEnter) return;
            Fire();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!triggerOnEnter) return;
            if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;

            Fire();
        }

        /// <summary>
        /// Re-checks while the player stands inside, throttled to <see cref="RetrySeconds"/>.
        ///
        /// <para>Entry alone is not enough. Once the trigger has fired this stops
        /// costing anything.</para>
        /// </summary>
        private void OnTriggerStay(Collider other)
        {
            if (!triggerOnEnter || (oneShot && _fired)) return;
            if (Time.unscaledTime < _nextRetryTime) return;
            if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;

            _nextRetryTime = Time.unscaledTime + RetrySeconds;
            Fire();
        }

        private void Fire()
        {
            if (oneShot && _fired) return;

            var quests = Quests;
            if (quests == null)
            {
                Debug.LogWarning($"[JRPG.Exploration] '{name}': no IQuestService registered.", this);
                return;
            }

            if (string.IsNullOrEmpty(discoverQuestId) && string.IsNullOrEmpty(objectiveTargetId))
            {
                Debug.LogWarning($"[JRPG.Exploration] '{name}' fired but reports nothing — set a quest to " +
                                 "discover or an objective target.", this);
                return;
            }

            bool discovered = !string.IsNullOrEmpty(discoverQuestId) && quests.DiscoverQuest(discoverQuestId);

            bool advanced = !string.IsNullOrEmpty(objectiveTargetId)
                            && quests.RegisterObjectiveProgress(objectiveType, objectiveTargetId, Mathf.Max(1, amount));

            // A one-shot is spent by taking effect
            if (!discovered && !advanced) return;

            _fired = true;

            if (!string.IsNullOrEmpty(noticeMessage))
                PassiveDialogueSink.Current?.ShowLine(speakerName, noticeMessage, 0f);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (amount < 1) amount = 1;
        }
#endif
    }
}
