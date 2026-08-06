using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using JRPG.Characters;
using JRPG.Core;
using JRPG.Party;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// The roster, as one column — the P5 shape. Active members carry a <c>(PARTY)</c> badge; reserve
    /// members are the same row without it, rather than a separate list.
    ///
    /// <para><b>Why one column.</b> Two columns make "who is deployed" a matter of which side of the
    /// screen a name sits on, which stops working the moment the list scrolls. A badge travels with the
    /// row, and the ordering (active first) still reads at a glance.</para>
    ///
    /// <para><b>Two verbs, both on the row under the cursor.</b> Submit opens that character's detail;
    /// Tab toggles them between active and reserve. The toggle asks
    /// <see cref="PartyService.CanSetActive"/> / <see cref="PartyService.CanMoveToReserve"/> and shows
    /// their reason on refusal, so party caps, story locks and scope requirements are explained rather
    /// than silently ignored. Rows are never disabled for being un-toggleable — the character can still
    /// be inspected.</para>
    ///
    /// <para><b>No developer-only transitions.</b> Only Active↔Reserve is offered. Recruiting, guest
    /// status and availability are story outcomes, shown here but never edited.</para>
    /// </summary>
    public class PartyMenuController : MenuController
    {
        [Header("12.8")]
        [Tooltip("Optional. Bound to the focused member.")]
        [SerializeField] private DetailPanelController detailPanel;

        [Tooltip("Shown on rows whose character is in the active party. Falls back to a text badge in " +
                 "the row's cost slot when unassigned.")]
        [SerializeField] private Sprite activeBadgeSprite;

        [Tooltip("Text badge used while activeBadgeSprite is unassigned.")]
        [SerializeField] private string activeBadgeText = "(PARTY)";

        /// Roster ids backing the rows, index-aligned.
        private readonly List<string> _rowIds = new();

        private PartyService Party =>
            Context?.Services != null
            && Context.Services.TryResolve<IPartyService>(out var svc)
            && svc is PartyService party
                ? party
                : null;

        protected override IReadOnlyList<RowModel> BuildRows()
        {
            _rowIds.Clear();
            var rows = new List<RowModel>();

            var party = Party;
            if (party == null) return rows;

            foreach (var id in RosterOrder(party))
            {
                var inst = party.ResolveInstanceById(id);
                if (inst == null) continue;

                var state = party.State.stateByCharacterId[id];
                bool isActive = state == CharacterRosterState.Active;

                rows.Add(new RowModel
                {
                    id = id,
                    label = inst.DisplayName,
                    // The reference's per-row readout. In the flexible aux slot, not the 64px quantity
                    // column, which it would overflow straight across the badge.
                    auxText = $"Lv {inst.level}    HP {inst.currentHP}/{inst.MaxHP}    SP {inst.currentSP}/{inst.MaxSP}",
                    // The badge rides the row's cost slot; the row prefab has no dedicated badge element.
                    costText = isActive ? activeBadgeText : string.Empty,
                    icon = isActive ? activeBadgeSprite : null,
                    enabled = true,
                    action = new OpenSubmenuAction("character_detail", "No character screen yet."),
                    context = new MenuContext
                    {
                        Services = Context.Services,
                        Menus = Context.Menus,
                        Subject = inst,
                    },
                });

                _rowIds.Add(id);
            }

            if (rows.Count == 0)
            {
                _rowIds.Add(null);
                rows.Add(RowModel.Simple("none", "No party members.", null, Context, enabled: false,
                                         disabledReason: string.Empty));
            }

            return rows;
        }

        /// Active first (in slot order), then reserve, then anyone else travelling with the party.
        private static IEnumerable<string> RosterOrder(PartyService party)
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

        // ---- Toggle -------------------------------------------------------------------------------

        /// <summary>
        /// Tab moves the focused character between the active party and the reserve. Which direction is
        /// implied by where they are now, so it is one key rather than two.
        /// </summary>
        protected override void OnTab()
        {
            var party = Party;
            int index = HighlightedIndex;
            if (party == null || index < 0 || index >= _rowIds.Count) return;

            var id = _rowIds[index];
            if (string.IsNullOrEmpty(id)) return;

            bool isActive = party.State.stateByCharacterId.TryGetValue(id, out var state)
                            && state == CharacterRosterState.Active;

            bool ok = isActive
                ? Bench(party, id)
                : Deploy(party, id);

            if (!ok) return;

            RebuildAndFocus();
            Select(Mathf.Min(index, _rowIds.Count - 1));
        }

        private bool Deploy(PartyService party, string id)
        {
            if (!party.CanSetActive(id, out var reason)) { Refuse(reason); return false; }

            // Append to the end of the active order; explicit slot ordering is not exposed here.
            return party.TrySetActive(id, party.State.activeOrder.Count);
        }

        private bool Bench(PartyService party, string id)
        {
            if (!party.CanMoveToReserve(id, out var reason)) { Refuse(reason); return false; }
            return party.MoveToReserve(id);
        }

        /// Surfaces the domain's refusal on the row the player is looking at.
        private void Refuse(string reason)
        {
            int index = HighlightedIndex;
            if (populator != null && index >= 0 && index < populator.ActiveRows.Count)
                populator.ActiveRows[index].SetVisualState(RowState.Invalid);

            if (detailPanel != null && !string.IsNullOrEmpty(reason))
                detailPanel.ShowDetail("Can't move", reason);
        }

        // ---- Detail panel -------------------------------------------------------------------------

        protected override void OnHighlightChanged(int index, RowModel model)
        {
            if (detailPanel == null) return;

            var party = Party;
            var id = index >= 0 && index < _rowIds.Count ? _rowIds[index] : null;
            if (party == null || string.IsNullOrEmpty(id)) { detailPanel.Clear(); return; }

            var inst = party.ResolveInstanceById(id);
            if (inst == null) { detailPanel.Clear(); return; }

            var state = party.State.stateByCharacterId[id];
            var notes = new List<string>(3) { state.ToString() };
            if (party.IsLocked(id)) notes.Add("Locked");

            // The cap belongs on this screen: it is the reason a deploy will be refused.
            notes.Add($"Party {party.State.activeOrder.Count}/{party.EffectiveMaxActiveMembers()}");

            detailPanel.ShowDetail(
                inst.DisplayName,
                $"HP {inst.currentHP}/{inst.MaxHP}\nMP {inst.currentMP}/{inst.MaxMP}\nSP {inst.currentSP}/{inst.MaxSP}",
                null,
                string.Join("   ·   ", notes));
        }

        public override IReadOnlyList<InputPrompt> Prompts { get; } = new[]
        {
            new InputPrompt("Submit", "Details"),
            new InputPrompt("Tab", "Join / Leave"),
            new InputPrompt("Cancel", "Back"),
        };
    }
}
