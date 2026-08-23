using UnityEngine;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// Opens a shop screen for a vendor, carrying the shop (and optionally the section) with it.
    /// The destination id is decided by the caller.
    /// </summary>
    public sealed class OpenShopScreenAction : IMenuAction
    {
        private readonly string _menuId;
        private readonly string _shopId;
        private readonly string _sectionId;
        private readonly string _unavailableReason;

        public OpenShopScreenAction(string menuId, string shopId, string sectionId = null,
                                    string unavailableReason = null)
        {
            _menuId = menuId;
            _shopId = shopId;
            _sectionId = sectionId;
            _unavailableReason = unavailableReason;
        }

        public bool CanExecute(MenuContext c)
            => !string.IsNullOrEmpty(_menuId)
               && !string.IsNullOrEmpty(_shopId)
               && c.Menus != null
               && c.Menus.HasMenu(_menuId)
               && c.Services != null
               && c.Services.TryResolve<IShopService>(out var shops)
               && shops.IsShopAvailable(_shopId);

        public void Execute(MenuContext c)
        {
            c.Menus.Open(_menuId, new MenuContext
            {
                Services = c.Services,
                Menus = c.Menus,
                Subject = c.Subject,
                Payload = new ShopMenuPayload(_shopId, _sectionId),
            });
        }

        public string GetDisabledReason(MenuContext c)
        {
            if (string.IsNullOrEmpty(_shopId)) return "No shop.";
            if (c.Menus == null) return "No menu service.";
            if (!c.Menus.HasMenu(_menuId)) return _unavailableReason ?? "Not available yet.";

            return _unavailableReason ?? "This shop is closed.";
        }
    }

    /// <summary>
    /// Buys a quantity of one offering. The amount is captured when the row is built, so the row's
    /// enabled state and its refusal are both about the amount the player is actually looking at.
    /// </summary>
    public sealed class BuyOfferingAction : IMenuAction
    {
        private readonly string _shopId;
        private readonly string _offeringId;
        private readonly int _quantity;

        public BuyOfferingAction(string shopId, string offeringId, int quantity)
        {
            _shopId = shopId;
            _offeringId = offeringId;
            _quantity = quantity;
        }

        public bool CanExecute(MenuContext c)
            => c.Services != null
               && c.Services.TryResolve<IShopService>(out var shops)
               && shops.CanBuy(_shopId, _offeringId, _quantity, out _);

        public void Execute(MenuContext c)
        {
            if (c.Services == null || !c.Services.TryResolve<IShopService>(out var shops)) return;

            if (!shops.TryBuy(_shopId, _offeringId, _quantity))
                Debug.Log($"[JRPG.Menu] Purchase of '{_offeringId}' x{_quantity} was refused.");
        }

        public string GetDisabledReason(MenuContext c)
        {
            if (c.Services == null || !c.Services.TryResolve<IShopService>(out var shops)) return "No shop service.";

            shops.CanBuy(_shopId, _offeringId, _quantity, out var reason);

            return reason;
        }
    }

    /// <summary>
    /// Sells a quantity of one item to a vendor.
    /// </summary>
    public sealed class SellItemAction : IMenuAction
    {
        private readonly string _shopId;
        private readonly int _quantity;

        public SellItemAction(string shopId, int quantity)
        {
            _shopId = shopId;
            _quantity = quantity;
        }

        public bool CanExecute(MenuContext c)
            => c.Services != null
               && !string.IsNullOrEmpty(c.SelectedItemId)
               && c.Services.TryResolve<IShopService>(out var shops)
               && shops.CanSell(_shopId, c.SelectedItemId, _quantity, out _);

        public void Execute(MenuContext c)
        {
            if (c.Services == null || !c.Services.TryResolve<IShopService>(out var shops)) return;

            if (!shops.TrySell(_shopId, c.SelectedItemId, _quantity))
                Debug.Log($"[JRPG.Menu] Sale of '{c.SelectedItemId}' x{_quantity} was refused.");
        }

        public string GetDisabledReason(MenuContext c)
        {
            if (c.Services == null || !c.Services.TryResolve<IShopService>(out var shops)) return "No shop service.";

            shops.CanSell(_shopId, c.SelectedItemId, _quantity, out var reason);

            return reason;
        }
    }
}
