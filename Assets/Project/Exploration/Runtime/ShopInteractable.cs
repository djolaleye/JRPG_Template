using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Services;

namespace JRPG.Exploration
{
    /// <summary>
    /// A shopkeeper. Opens the vendor's screens when interacted with.
    ///
    /// <para><b>Which screen depends on the shop, not this component.</b> A basic shop opens straight
    /// into its catalog; a specialty shop opens its front desk. The type is authored on the
    /// <see cref="ShopData"/>, so re-pointing this at another vendor changes the flow with it.</para>
    ///
    /// <para>A shop whose story gate is unmet says so and opens nothing — the same passive line a locked
    /// chest uses.</para>
    /// </summary>
    public sealed class ShopInteractable : MonoBehaviour, IInteractable
    {
        private const string BasicMenuId = "shop_basic";
        private const string MainMenuId = "shop_main";

        [SerializeField] private string shopId;

        [Tooltip("Shown when the shop's story gate is not yet satisfied.")]
        [SerializeField] private string closedMessage = "They aren't open for business.";

        [SerializeField] private string speakerName = "system";

        public string ShopId => shopId;

        public void Interact(GameObject initiator)
        {
            var services = AppContext.Services;
            if (services == null || !services.TryResolve<IShopService>(out var shops))
            {
                Debug.LogWarning("[JRPG.Exploration] ShopInteractable: no IShopService registered.", this);
                return;
            }

            if (!shops.IsShopAvailable(shopId))
            {
                Announce(closedMessage);
                return;
            }

            if (!services.TryResolve<IMenuService>(out var menus))
            {
                Debug.LogWarning("[JRPG.Exploration] ShopInteractable: no IMenuService registered.", this);
                return;
            }

            var menuId = ResolveMenuId();
            if (!menus.HasMenu(menuId))
            {
                Debug.LogWarning($"[JRPG.Exploration] ShopInteractable: no '{menuId}' screen registered.", this);
                return;
            }

            // The payload type lives in JRPG.Menu, which Exploration does not reference. The menu
            // service takes an object, and the shop screens read the id back out of it. Passing the id
            // as a plain string keeps this component free of a UI dependency.
            menus.Open(menuId, shopId);
        }

        /// Basic -> catalog; specialty -> front desk.
        private string ResolveMenuId()
        {
            var data = AppContext.Data as DataRegistry;

            if (data != null && data.TryGet<ShopData>(shopId, out var shop) && shop != null)
                return shop.shopType == ShopType.Specialty ? MainMenuId : BasicMenuId;

            return BasicMenuId;
        }

        private void Announce(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            PassiveDialogueSink.Current?.ShowLine(speakerName, message, 0f);
        }
    }
}
