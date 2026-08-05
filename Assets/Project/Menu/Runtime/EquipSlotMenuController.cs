using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using JRPG.Core;
using JRPG.Data;
using JRPG.Inventory;
using JRPG.Services;

// `using System` pulls in System.AppContext.
using AppContext = JRPG.Core.AppContext;

namespace JRPG.Menu
{
    /// <summary>
    /// The subject's slots, each showing what currently occupies it.
    ///
    /// <para>Submit opens the item picker for that slot. Tab unequips the focused slot.</para>
    ///
    /// <para><b>Slots the character cannot use are shown, disabled.</b> Hiding them would make the list
    /// change shape per character.
    /// <c>CharacterData.allowedSlots</c> is the authority; empty means no restriction.</para>
    /// </summary>
    public sealed class EquipSlotMenuController : MenuController
    {
        [SerializeField] private DetailPanelController detailPanel;

        [Tooltip("Confirmation for unequip. Optional: without it, unequip happens immediately.")]
        [SerializeField] private ConfirmPromptController confirmPrompt;

        private readonly List<EquipmentSlot> _rowSlots = new();

        protected override bool ModalActive => confirmPrompt != null && confirmPrompt.IsOpen;

        private EquipmentManager Equipment =>
            Context?.Services != null
            && Context.Services.TryResolve<IEquipmentService>(out var svc)
            && svc is EquipmentManager manager
                ? manager
                : null;

        protected override IReadOnlyList<RowModel> BuildRows()
        {
            _rowSlots.Clear();
            var rows = new List<RowModel>();

            var subject = Context?.Subject;
            var equipment = Equipment;
            if (subject == null || equipment == null)
            {
                rows.Add(RowModel.Simple("none", "No character selected.", null, Context, enabled: false,
                                         disabledReason: string.Empty));
                return rows;
            }

            var data = AppContext.Data as DataRegistry;
            data.TryGet<CharacterData>(subject.SourceDataId, out var charData);

            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                bool allowed = charData?.allowedSlots == null
                               || charData.allowedSlots.Count == 0
                               || charData.allowedSlots.Contains(slot);

                var itemId = equipment.GetEquipped(subject.SourceDataId, slot);

                var rowContext = new MenuContext
                {
                    Services = Context.Services,
                    Menus = Context.Menus,
                    Subject = subject,
                    Payload = slot,
                };

                rows.Add(new RowModel
                {
                    id = slot.ToString(),
                    label = EquipCharacterMenuController.Prettify(slot),
                    quantityText = ItemName(data, itemId),
                    enabled = allowed,
                    action = new OpenSubmenuAction("equip_items", "No item picker yet."),
                    context = rowContext,
                    disabledReason = allowed ? null : $"{subject.DisplayName} has no {EquipCharacterMenuController.Prettify(slot)} slot.",
                });

                _rowSlots.Add(slot);
            }

            return rows;
        }

        private static string ItemName(DataRegistry data, string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return "— empty —";
            if (data != null && data.TryGet<ItemData>(itemId, out var item) && item != null)
                return string.IsNullOrEmpty(item.displayName) ? item.Id : item.displayName;

            return itemId;
        }

        // ---- Detail ---------------------------------------------------------------------------------

        protected override void OnHighlightChanged(int index, RowModel model)
        {
            if (detailPanel == null) return;

            var subject = Context?.Subject;
            var equipment = Equipment;
            if (subject == null || equipment == null || index < 0 || index >= _rowSlots.Count)
            {
                detailPanel.Clear();
                return;
            }

            var slot = _rowSlots[index];
            var itemId = equipment.GetEquipped(subject.SourceDataId, slot);
            var data = AppContext.Data as DataRegistry;

            if (string.IsNullOrEmpty(itemId) || data == null || !data.TryGet<EquipmentData>(itemId, out var equip))
            {
                detailPanel.ShowDetail(EquipCharacterMenuController.Prettify(slot), "Nothing equipped.");
                return;
            }

            detailPanel.ShowDetail(
                string.IsNullOrEmpty(equip.displayName) ? equip.Id : equip.displayName,
                EquipItemMenuController.DescribeEquipment(equip),
                equip.icon,
                EquipItemMenuController.DescribeGrants(equip));
        }

        // ---- Unequip --------------------------------------------------------------------------------

        protected override void OnTab()
        {
            if (ModalActive) return;

            var subject = Context?.Subject;
            var equipment = Equipment;
            if (subject == null || equipment == null) return;

            int index = HighlightedIndex;
            if (index < 0 || index >= _rowSlots.Count) return;

            var slot = _rowSlots[index];
            if (string.IsNullOrEmpty(equipment.GetEquipped(subject.SourceDataId, slot))) return;

            void DoUnequip()
            {
                if (!equipment.Unequip(subject, slot, out var reason))
                    Debug.LogWarning($"[JRPG.Menu] Unequip refused: {reason}");

                RebuildAndFocus();
            }

            if (confirmPrompt == null) { DoUnequip(); return; }

            confirmPrompt.Ask($"Remove {subject.DisplayName}'s {EquipCharacterMenuController.Prettify(slot)}?",
                              "Remove", "Cancel", DoUnequip);
        }

        public override IReadOnlyList<InputPrompt> Prompts { get; } = new[]
        {
            new InputPrompt("Submit", "Change"),
            new InputPrompt("Tab", "Remove"),
            new InputPrompt("Cancel", "Back"),
        };
    }
}
