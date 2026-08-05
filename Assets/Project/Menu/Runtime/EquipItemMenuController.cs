using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using JRPG.Core;
using JRPG.Data;
using JRPG.Inventory;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// Choose what goes in the slot.
    ///
    /// <para><b>Every row's availability is <see cref="EquipmentManager.CanEquip"/>.</b> Level gates,
    /// character restrictions and slot restrictions are all its answer, and the greyed-out text is its
    /// reason, so a row cannot promise something the domain will then refuse.</para>
    ///
    /// <para>The comparison panel comes from <see cref="EquipmentManager.PreviewEquip"/>, which projects
    /// onto a cloned stat block. It accounts for the item being displaced, so the numbers answer "versus
    /// what I have on".</para>
    /// </summary>
    public sealed class EquipItemMenuController : MenuController
    {
        [SerializeField] private DetailPanelController detailPanel;

        [Tooltip("Confirmation before swapping gear. Optional.")]
        [SerializeField] private ConfirmPromptController confirmPrompt;

        private readonly List<EquipmentData> _rowItems = new();

        protected override bool ModalActive => confirmPrompt != null && confirmPrompt.IsOpen;

        private EquipmentSlot? TargetSlot => Context?.Payload is EquipmentSlot slot ? slot : null;

        private EquipmentManager Equipment =>
            Context?.Services != null
            && Context.Services.TryResolve<IEquipmentService>(out var svc)
            && svc is EquipmentManager manager
                ? manager
                : null;

        protected override IReadOnlyList<RowModel> BuildRows()
        {
            _rowItems.Clear();
            var rows = new List<RowModel>();

            var subject = Context?.Subject;
            var equipment = Equipment;
            var slot = TargetSlot;

            if (subject == null || equipment == null || slot == null)
            {
                rows.Add(RowModel.Simple("none", "Nothing to show.", null, Context, enabled: false,
                                         disabledReason: string.Empty));
                return rows;
            }

            if (!Context.Services.TryResolve<IInventoryService>(out var invSvc) || invSvc is not InventoryService inv)
                return rows;

            var data = AppContext.Data as DataRegistry;
            var state = AppContext.State?.Current ?? default;

            // The filter narrows to equipment this character is allowed to hold at all; CanEquip below
            // then answers the per-item question, with its reason.
            var filtered = inv.Filter(state, ContextualFilterRequest.EquipScreen(subject.SourceDataId));

            for (int i = 0; i < filtered.Count; i++)
            {
                if (data == null || !data.TryGet<EquipmentData>(filtered[i].itemId, out var equip) || equip == null)
                    continue;

                if (equip.slot != slot.Value) continue;

                bool can = equipment.CanEquip(subject, equip.Id, out var reason);

                var rowContext = new MenuContext
                {
                    Services = Context.Services,
                    Menus = Context.Menus,
                    Subject = subject,
                    SelectedItemId = equip.Id,
                    Payload = slot.Value,
                };

                rows.Add(new RowModel
                {
                    id = equip.Id,
                    label = string.IsNullOrEmpty(equip.displayName) ? equip.Id : equip.displayName,
                    icon = equip.icon,
                    quantityText = $"× {filtered[i].quantity}",
                    enabled = can,
                    action = new EquipItemAction(),
                    context = rowContext,
                    disabledReason = can ? null : reason,
                });

                _rowItems.Add(equip);
            }

            if (rows.Count == 0)
            {
                _rowItems.Add(null);
                rows.Add(RowModel.Simple("empty",
                                         $"No {EquipCharacterMenuController.Prettify(slot.Value)} in the bag.",
                                         null, Context, enabled: false, disabledReason: string.Empty));
            }

            return rows;
        }

        // ---- Comparison panel -----------------------------------------------------------------------

        protected override void OnHighlightChanged(int index, RowModel model)
        {
            if (detailPanel == null) return;

            var equip = index >= 0 && index < _rowItems.Count ? _rowItems[index] : null;
            var subject = Context?.Subject;
            var equipment = Equipment;

            if (equip == null || subject == null || equipment == null) { detailPanel.Clear(); return; }

            var body = new StringBuilder();
            body.AppendLine(DescribeEquipment(equip));

            var deltas = equipment.PreviewEquip(subject, equip.Id);
            if (deltas.Count == 0)
            {
                body.AppendLine("\nNo stat change.");
            }
            else
            {
                body.AppendLine();
                for (int i = 0; i < deltas.Count; i++)
                {
                    var d = deltas[i];
                    // before → after.
                    body.AppendLine($"{d.stat}   {d.before} → {d.after}   ({(d.Change > 0 ? "+" : "")}{d.Change})");
                }
            }

            detailPanel.ShowDetail(
                string.IsNullOrEmpty(equip.displayName) ? equip.Id : equip.displayName,
                body.ToString().TrimEnd(),
                equip.icon,
                DescribeGrants(equip));
        }

        /// Authored description plus the equip requirements the player needs before committing.
        internal static string DescribeEquipment(EquipmentData equip)
        {
            var parts = new List<string>(3);
            if (!string.IsNullOrEmpty(equip.description)) parts.Add(equip.description);
            if (equip.requiredLevel > 1) parts.Add($"Requires level {equip.requiredLevel}.");

            return parts.Count > 0 ? string.Join(" ", parts) : "No description.";
        }

        /// <summary>
        /// The resistance and passive preview, everything the item grants beyond raw stats.
        /// </summary>
        internal static string DescribeGrants(EquipmentData equip)
        {
            var parts = new List<string>(4);

            if (equip.elementAffinities != null && equip.elementAffinities.Count > 0)
            {
                var affinities = new List<string>(equip.elementAffinities.Count);
                for (int i = 0; i < equip.elementAffinities.Count; i++)
                    affinities.Add($"{equip.elementAffinities[i].element} {equip.elementAffinities[i].affinity}");

                parts.Add(string.Join(", ", affinities));
            }

            if (equip.statusImmunityIds != null && equip.statusImmunityIds.Count > 0)
                parts.Add("Immune: " + string.Join(", ", equip.statusImmunityIds));

            if (equip.passiveEffectIds != null && equip.passiveEffectIds.Count > 0)
                parts.Add("Passive: " + string.Join(", ", equip.passiveEffectIds));

            if (equip.actionUnlockIds != null && equip.actionUnlockIds.Count > 0)
                parts.Add("Unlocks: " + string.Join(", ", equip.actionUnlockIds));

            return parts.Count > 0 ? string.Join("   ·   ", parts) : null;
        }

        // ---- Confirmation ---------------------------------------------------------------------------

        protected override void OnSubmit(InputAction.CallbackContext ctx)
        {
            if (ModalActive) return;

            int index = HighlightedIndex;
            var subject = Context?.Subject;
            var equipment = Equipment;
            var slot = TargetSlot;

            if (confirmPrompt == null || subject == null || equipment == null || slot == null
                || index < 0 || index >= _rowItems.Count || _rowItems[index] == null)
            {
                base.OnSubmit(ctx);
                return;
            }

            // Only a swap warrants a prompt.
            var occupying = equipment.GetEquipped(subject.SourceDataId, slot.Value);
            if (string.IsNullOrEmpty(occupying)) { base.OnSubmit(ctx); return; }

            var data = AppContext.Data as DataRegistry;
            string occupyingName = occupying;
            if (data != null && data.TryGet<ItemData>(occupying, out var current) && current != null)
                occupyingName = string.IsNullOrEmpty(current.displayName) ? current.Id : current.displayName;

            int target = index;
            confirmPrompt.Ask($"Replace {occupyingName}? It goes back to the bag.",
                              "Equip", "Cancel", () => ExecuteRow(target), defaultToCancel: false);
        }

        public override IReadOnlyList<InputPrompt> Prompts { get; } = new[]
        {
            new InputPrompt("Submit", "Equip"),
            new InputPrompt("Cancel", "Back"),
        };
    }
}
