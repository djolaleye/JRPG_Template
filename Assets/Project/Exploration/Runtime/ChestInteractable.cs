using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Services;

namespace JRPG.Exploration
{
    /// <summary>
    /// A placed treasure chest. Grants one authored item stack, optionally behind a consumable chest
    /// key, and records itself as opened through <see cref="IChestStateService"/>.
    ///
    /// <para><b>The open is a transaction, not a sequence of independent steps.</b> Every check —
    /// state, key possession and inventory room for the whole stack — runs before anything is
    /// removed, using <see cref="IInventoryService.RoomFor"/>. A refusal at any point leaves the key,
    /// the inventory and the chest exactly as they were.</para>
    /// </summary>
    public sealed class ChestInteractable : MonoBehaviour, IInteractable
    {
        [Header("Identity")]
        [Tooltip("Stable persistence id for this placed chest. Must be unique across the authored world.")]
        [SerializeField] private string chestId;
        [SerializeField] private ChestOpenedState authoredState = ChestOpenedState.Unopened;

        [Header("Lock")]
        [Tooltip("Item id of the chest key consumed on a successful open.")]
        [SerializeField] private string chestKeyId;

        [Header("Reward")]
        [SerializeField] private string heldItemId;
        [Min(1)][SerializeField] private int itemQuantity = 1;

        [Header("Presentation")]
        [SerializeField] private string alreadyOpenMessage = "The chest is empty.";
        [SerializeField] private string lockedMessage = "It's locked. A chest key would open it.";
        [SerializeField] private string inventoryFullMessage = "There's no room for that.";
        [SerializeField] private string speakerName = "system";

        [Tooltip("Swapped on when the chest is open, off when it is not.")]
        [SerializeField] private GameObject closedVisual;
        [SerializeField] private GameObject openedVisual;

        public string ChestId => chestId;
        public string ChestKeyId => chestKeyId;
        public string HeldItemId => heldItemId;
        public int ItemQuantity => itemQuantity;
        public ChestOpenedState AuthoredState => authoredState;
        public bool RequiresKey => authoredState == ChestOpenedState.Locked;

        /// <summary>Live state: the ledger's answer, falling back to how this chest was authored.</summary>
        public ChestOpenedState CurrentState
            => Chests != null ? Chests.GetState(chestId, authoredState) : authoredState;

        private IChestStateService Chests
            => AppContext.Services != null && AppContext.Services.TryResolve<IChestStateService>(out var svc)
                ? svc
                : null;

        private IInventoryService Inventory
            => AppContext.Services != null && AppContext.Services.TryResolve<IInventoryService>(out var svc)
                ? svc
                : null;

        private void OnEnable() => RefreshVisual();

        // ---- Queries ----------------------------------------------------------------------------

        public bool CanInteract(out string reason)
        {
            reason = null;

            if (string.IsNullOrEmpty(chestId))
            {
                reason = "This chest has no id.";
                return false;
            }

            if (CurrentState == ChestOpenedState.Opened)
            {
                reason = alreadyOpenMessage;
                return false;
            }

            var inventory = Inventory;
            if (inventory == null)
            {
                reason = "No inventory service.";
                return false;
            }

            if (RequiresKey && !inventory.Has(chestKeyId, 1))
            {
                reason = lockedMessage;
                return false;
            }

            if (string.IsNullOrEmpty(heldItemId) || itemQuantity <= 0)
            {
                reason = "This chest holds nothing.";
                return false;
            }

            if (inventory.RoomFor(heldItemId) < itemQuantity)
            {
                reason = inventoryFullMessage;
                return false;
            }

            return true;
        }

        public bool CanInteract() => CanInteract(out _);

        // ---- IInteractable ----------------------------------------------------------------------

        /// <summary>
        /// The only mutation path. Runs every check first, then removes the key, grants the item,
        /// records the open and publishes <see cref="ChestOpened"/> — in that order, so a failure
        /// cannot land between the key leaving and the reward arriving.
        /// </summary>
        public void Interact(GameObject initiator)
        {
            if (!CanInteract(out var reason))
            {
                Announce(reason);
                return;
            }

            var inventory = Inventory;
            var chests = Chests;

            if (chests == null)
            {
                Debug.LogWarning($"[JRPG.Exploration] Chest '{chestId}': no IChestStateService registered.", this);
                return;
            }

            if (RequiresKey && inventory.Remove(chestKeyId, 1) != 1)
            {
                Announce(lockedMessage);
                return;
            }

            int overflow = inventory.Add(heldItemId, itemQuantity);
            if (overflow > 0)
            {
                // RoomFor already guaranteed the space, so this is a real inconsistency.
                // Hand the key back and leave the chest closed.
                Debug.LogError($"[JRPG.Exploration] Chest '{chestId}': {overflow} of '{heldItemId}' did not fit " +
                               "despite a successful preflight.", this);

                inventory.Remove(heldItemId, itemQuantity - overflow);
                if (RequiresKey) inventory.Add(chestKeyId, 1);

                Announce(inventoryFullMessage);
                return;
            }

            chests.MarkOpened(chestId);
            RefreshVisual();

            AppContext.Bus?.Publish(new ChestOpened(chestId, heldItemId, itemQuantity));
            Announce(RewardLine());
        }

        // ---- Presentation -----------------------------------------------------------------------

        /// <summary>
        /// Applies the current state to the two optional visuals, so a scene re-entered after a save
        /// shows its looted chests looted without any component tracking that itself.
        /// </summary>
        public void RefreshVisual()
        {
            bool opened = CurrentState == ChestOpenedState.Opened;

            if (closedVisual != null) closedVisual.SetActive(!opened);
            if (openedVisual != null) openedVisual.SetActive(opened);
        }

        private string RewardLine()
        {
            var data = AppContext.Data as DataRegistry;
            string itemName = heldItemId;

            if (data != null && data.TryGet<ItemData>(heldItemId, out var item) && item != null)
                itemName = string.IsNullOrEmpty(item.displayName) ? item.Id : item.displayName;

            return itemQuantity > 1 ? $"Found {itemName} ×{itemQuantity}." : $"Found {itemName}.";
        }

        private void Announce(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            PassiveDialogueSink.Current?.ShowLine(speakerName, message, 0f);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (itemQuantity < 1) itemQuantity = 1;

            if (authoredState == ChestOpenedState.Locked && string.IsNullOrEmpty(chestKeyId))
                Debug.LogWarning($"[JRPG.Exploration] Chest '{name}' is Locked but has no chestKeyId.", this);

            if (authoredState != ChestOpenedState.Locked && !string.IsNullOrEmpty(chestKeyId))
                Debug.LogWarning($"[JRPG.Exploration] Chest '{name}' names a chest key but is not Locked; " +
                                 "the key will never be consumed.", this);
        }
#endif
    }
}
