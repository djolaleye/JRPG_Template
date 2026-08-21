using System.Collections.Generic;
using UnityEngine;
using JRPG.Characters;
using JRPG.Core;
using JRPG.Data;
using JRPG.Inventory;
using JRPG.Party;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// The exploration inventory: a horizontal category tab strip,
    /// the party down the left, item rows in the middle, and a description panel bound to the
    /// focused row.
    ///
    /// <para><b>Tabs are a filter argument, not a second filter.</b> Each tab is an
    /// <see cref="ItemCategory"/> passed to <see cref="ContextualFilterRequest.ExplorationTab"/>, which
    /// already supported a category narrowing. The screen never partitions the item list itself, so
    /// context rules apply identically on every tab.</para>
    ///
    /// <para><b>The party column answers "use on whom" in place.</b> It is bound from
    /// <see cref="IPartyRuntimeQueries"/> and is presentation only — using an item still goes through
    /// <see cref="UseItemAction"/>.</para>
    ///
    /// <para><b>Empty tabs are stated, not blank.</b> A category with nothing in it renders a single
    /// disabled row. The player can tell the difference between "nothing here" and "screen
    /// broken".</para>
    /// </summary>
    public class InventoryMenuController : MenuController
    {
        private static readonly (string id, string label, ItemCategory? category)[] TabDefs =
        {
            ("all",         "All",               null),
            ("consumable",  "Consumables",       ItemCategory.Consumable),
            ("tool",        "Exploration Tools", ItemCategory.ExplorationTool),
            ("key",         "Key Items",         ItemCategory.KeyItem),
            ("quest",       "Quest Items",       ItemCategory.QuestItem),
            ("material",    "Materials",         ItemCategory.Material),
            ("equipment",   "Equipment",         ItemCategory.Equipment),
        };

        [Header("12.6 presentation (all optional)")]
        [SerializeField] private TabStripController tabStrip;
        [SerializeField] private DetailPanelController detailPanel;
        [SerializeField] private PartyStatusPanel partyColumn;

        private const string DiscardMenuId = "item_discard";

        /// Items backing the current rows, index-aligned with them.
        private readonly List<ItemData> _rowItems = new();

        private bool _tabsBuilt;

        protected override void OnEnable()
        {
            BuildTabs();
            base.OnEnable();
        }

        // ---- Tabs ---------------------------------------------------------------------------------

        private void BuildTabs()
        {
            if (tabStrip == null || _tabsBuilt) return;

            var defs = new List<TabDef>(TabDefs.Length);
            for (int i = 0; i < TabDefs.Length; i++)
                defs.Add(new TabDef(TabDefs[i].id, TabDefs[i].label));

            tabStrip.SetTabs(defs);
            tabStrip.TabChanged += OnTabChanged;
            _tabsBuilt = true;
        }

        private void OnDestroy()
        {
            if (tabStrip != null) tabStrip.TabChanged -= OnTabChanged;
        }

        private void OnTabChanged(int index) => RebuildAndFocus();

        protected override void OnPageLeft() => tabStrip?.Previous();
        protected override void OnPageRight() => tabStrip?.Next();

        /// <summary>
        /// Opens the discard screen for the focused item.
        /// </summary>
        protected override void OnTab()
        {
            var item = HighlightedItem();
            if (item == null || Context?.Menus == null) return;

            if (!Context.Menus.HasMenu(DiscardMenuId))
            {
                Debug.LogWarning($"[JRPG.Menu] No '{DiscardMenuId}' screen registered.");
                return;
            }

            Context.Menus.Open(DiscardMenuId, new MenuContext
            {
                Services = Context.Services,
                Menus = Context.Menus,
                SelectedItemId = item.Id,
                Subject = Context.Subject,
            });
        }

        private ItemData HighlightedItem()
        {
            int index = HighlightedIndex;

            return index >= 0 && index < _rowItems.Count ? _rowItems[index] : null;
        }
        protected override void OnNavigateHorizontal(int dir)
        {
            if (dir < 0) tabStrip?.Previous();
            else tabStrip?.Next();
        }

        private ItemCategory? ActiveCategory()
        {
            if (tabStrip == null) return null;

            int index = tabStrip.ActiveIndex;
            return index >= 0 && index < TabDefs.Length ? TabDefs[index].category : null;
        }

        // ---- Rows ---------------------------------------------------------------------------------

        protected override IReadOnlyList<RowModel> BuildRows()
        {
            // Every rebuild is also the moment party state may have changed — using a potion is the
            // obvious case, since ExecuteRow rebuilds straight afterwards. Refreshing here keeps the
            // column truthful without a second event subscription.
            RefreshPartyColumn();

            _rowItems.Clear();

            var rows = new List<RowModel>();
            if (Context?.Services == null) return rows;
            if (!Context.Services.TryResolve<IInventoryService>(out var invSvc) || invSvc is not InventoryService inv)
                return rows;

            var data = AppContext.Data as DataRegistry;
            var state = AppContext.State?.Current ?? default;
            var filtered = inv.Filter(state, ContextualFilterRequest.ExplorationTab(ActiveCategory()));

            for (int i = 0; i < filtered.Count; i++)
            {
                var stack = filtered[i];
                if (data == null || !data.TryGet<ItemData>(stack.itemId, out var item) || item == null) continue;

                var rowContext = new MenuContext
                {
                    Services = Context.Services,
                    Menus = Context.Menus,
                    SelectedItemId = stack.itemId,
                };

                // The rule lives in ItemUseResolver, which is also what refuses the use at execution
                // time — so the row is enabled exactly when the action would succeed, and the greyed-out
                // explanation is the domain's own wording.
                bool usable = inv.CanUse(stack.itemId, UseSubject(), state, out var reason);

                rows.Add(new RowModel
                {
                    id = stack.itemId,
                    label = string.IsNullOrEmpty(item.displayName) ? item.Id : item.displayName,
                    icon = item.icon,
                    quantityText = $"× {stack.quantity}",
                    enabled = usable,
                    action = new UseItemAction(),
                    context = rowContext,
                    disabledReason = usable ? null : reason,
                });

                _rowItems.Add(item);
            }

            if (rows.Count == 0)
            {
                _rowItems.Add(null);
                rows.Add(RowModel.Simple("empty", EmptyLabel(), null, Context, enabled: false,
                                         disabledReason: string.Empty));
            }

            return rows;
        }

        /// <summary>
        /// Who "use" would target — the same default <see cref="UseItemAction"/> applies, so the row's
        /// enabled state is computed against the character the action will actually act on.
        /// </summary>
        private CharacterRuntimeInstance UseSubject()
        {
            if (Context?.Subject != null) return Context.Subject;

            if (Context?.Services != null
                && Context.Services.TryResolve<IPartyRuntimeQueries>(out var party)
                && party != null)
            {
                var active = party.GetActiveCombatParty();
                if (active != null && active.Count > 0) return active[0];
            }

            return null;
        }

        private string EmptyLabel()
        {
            var tab = tabStrip != null ? tabStrip.ActiveTab.displayName : null;
            return string.IsNullOrEmpty(tab) ? "Nothing here." : $"No {tab.ToLowerInvariant()}.";
        }

        // ---- Detail panel -------------------------------------------------------------------------

        protected override void OnHighlightChanged(int index, RowModel model)
        {
            if (detailPanel == null) return;

            var item = index >= 0 && index < _rowItems.Count ? _rowItems[index] : null;
            if (item == null) { detailPanel.Clear(); return; }

            detailPanel.ShowDetail(
                string.IsNullOrEmpty(item.displayName) ? item.Id : item.displayName,
                string.IsNullOrEmpty(item.description) ? "No description." : item.description,
                item.icon,
                BuildFooter(item));
        }

        /// The usage/sell/discard flags the panel needs to surface. Authored data, read straight
        /// off ItemData.
        private static string BuildFooter(ItemData item)
        {
            var parts = new List<string>(4) { Prettify(item.category) };

            if (item.usageRule != null)
            {
                if (item.usageRule.usableInExploration) parts.Add("Field");
                if (item.usageRule.usableInCombat) parts.Add("Battle");
                if (item.usageRule.consumedOnUse && item.usageRule.usableInExploration) parts.Add("Consumed");
            }

            if (!item.canSell) parts.Add("Can't sell");
            if (!item.canDiscard) parts.Add("Can't discard");

            return string.Join("  ·  ", parts);
        }

        private static string Prettify(ItemCategory category) => category switch
        {
            ItemCategory.KeyItem => "Key Item",
            ItemCategory.ExplorationTool => "Exploration Tool",
            ItemCategory.QuestItem => "Quest Item",
            _ => category.ToString(),
        };

        // ---- Party column -------------------------------------------------------------------------

        private void RefreshPartyColumn()
        {
            if (partyColumn == null) return;

            if (Context?.Services == null
                || !Context.Services.TryResolve<IPartyRuntimeQueries>(out var party)
                || party == null)
            {
                partyColumn.Clear();
                return;
            }

            partyColumn.Bind(party.GetActiveCombatParty());
        }

        public override IReadOnlyList<InputPrompt> Prompts { get; } = new[]
        {
            new InputPrompt("Submit", "Use"),
            new InputPrompt("Tab", "Discard"),
            new InputPrompt("PageL", "Prev Tab"),
            new InputPrompt("PageR", "Next Tab"),
            new InputPrompt("Cancel", "Back"),
        };
    }
}
