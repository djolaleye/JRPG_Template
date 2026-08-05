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
    /// Pick whose gear to change.
    ///
    /// <para><b>Every roster member, not just the active party.</b> Resolution goes through
    /// <see cref="PartyService.ResolveInstanceById"/>, which works for any
    /// <see cref="CharacterRosterState"/> </para>
    ///
    /// <para>Roster states that are not really "in the party" (Unmet, Met, Recruitable) are excluded:
    /// they are people the player has heard of, not people whose inventory they manage.</para>
    /// </summary>
    public sealed class EquipCharacterMenuController : MenuController
    {
        [SerializeField] private DetailPanelController detailPanel;

        /// Instances backing the rows, index-aligned.
        private readonly List<CharacterRuntimeInstance> _rowInstances = new();

        protected override IReadOnlyList<RowModel> BuildRows()
        {
            _rowInstances.Clear();
            var rows = new List<RowModel>();

            if (Context?.Services == null) return rows;
            if (!Context.Services.TryResolve<IPartyService>(out var partySvc) || partySvc is not PartyService party)
                return rows;

            foreach (var id in RosterIds(party))
            {
                var inst = party.ResolveInstanceById(id);
                if (inst == null) continue;

                var rowContext = new MenuContext
                {
                    Services = Context.Services,
                    Menus = Context.Menus,
                    Subject = inst,
                };

                rows.Add(RowModel.Simple(id, RowLabel(inst, party.State.stateByCharacterId[id]),
                                         new OpenSubmenuAction("equip_slots", "No slot screen yet."),
                                         rowContext));

                _rowInstances.Add(inst);
            }

            if (rows.Count == 0)
            {
                _rowInstances.Add(null);
                rows.Add(RowModel.Simple("none", "No one to equip.", null, Context, enabled: false,
                                         disabledReason: string.Empty));
            }

            return rows;
        }

        /// Roster members whose equipment the player owns. Order: active, then reserve, then the rest
        private static IEnumerable<string> RosterIds(PartyService party)
        {
            var seen = new HashSet<string>();

            foreach (var id in party.State.activeOrder)
                if (seen.Add(id)) yield return id;

            foreach (var id in party.State.reserveOrder)
                if (seen.Add(id)) yield return id;

            foreach (var kv in party.State.stateByCharacterId)
            {
                if (seen.Contains(kv.Key)) continue;
                if (kv.Value is CharacterRosterState.Guest or CharacterRosterState.Recruited)
                {
                    seen.Add(kv.Key);
                    yield return kv.Key;
                }
            }
        }

        private static string RowLabel(CharacterRuntimeInstance inst, CharacterRosterState state)
            => $"{inst.DisplayName}    Lv {inst.level}    {state}";

        protected override void OnHighlightChanged(int index, RowModel model)
        {
            if (detailPanel == null) return;

            var inst = index >= 0 && index < _rowInstances.Count ? _rowInstances[index] : null;
            if (inst == null) { detailPanel.Clear(); return; }

            detailPanel.ShowDetail(inst.DisplayName, EquipmentSummary(inst), null,
                                   $"HP {inst.currentHP}/{inst.MaxHP}   MP {inst.currentMP}/{inst.MaxMP}   " +
                                   $"SP {inst.currentSP}/{inst.MaxSP}");
        }

        /// One line per slot, showing what is in it.
        private string EquipmentSummary(CharacterRuntimeInstance inst)
        {
            if (Context?.Services == null
                || !Context.Services.TryResolve<IEquipmentService>(out var svc)
                || svc is not EquipmentManager equipment)
                return string.Empty;

            var data = AppContext.Data as DataRegistry;
            var lines = new List<string>();

            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                var itemId = equipment.GetEquipped(inst.SourceDataId, slot);
                string name = "—";

                if (!string.IsNullOrEmpty(itemId))
                {
                    name = data != null && data.TryGet<ItemData>(itemId, out var item) && item != null
                        ? (string.IsNullOrEmpty(item.displayName) ? item.Id : item.displayName)
                        : itemId;
                }

                lines.Add($"{Prettify(slot)}: {name}");
            }

            return string.Join("\n", lines);
        }

        internal static string Prettify(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.MeleeWeapon => "Melee Weapon",
            EquipmentSlot.RangedWeapon => "Ranged Weapon",
            _ => slot.ToString(),
        };

        public override IReadOnlyList<InputPrompt> Prompts { get; } = new[]
        {
            new InputPrompt("Submit", "Select"),
            new InputPrompt("Cancel", "Back"),
        };
    }
}
