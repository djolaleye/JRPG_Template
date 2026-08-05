using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// Shared substrate for the two slot screens. One row per slot, in slot order, so the row index
    /// <i>is</i> the slot index — which is what makes "act on the focused slot" (delete) trivial.
    ///
    /// <para><b>The screen decides presentation; the save service decides everything else.</b> Whether a
    /// slot exists, what it contains, whether saving is currently legal and whether a delete succeeded
    /// are all <see cref="ISaveService"/> answers. This class formats them and sequences the
    /// confirmation modal.</para>
    ///
    /// <para><b>Destructive actions are confirmed.</b> Overwriting an occupied slot and deleting one both
    /// route through <see cref="ConfirmPromptController"/>, which defaults its focus to Cancel. While
    /// that modal is open <see cref="MenuController.ModalActive"/> suppresses this screen's own input,
    /// so a single press cannot both answer the prompt and re-fire the row beneath it.</para>
    /// </summary>
    public abstract class SaveLoadMenuControllerBase : MenuController
    {
        [Tooltip("Modal used for overwrite / delete confirmation. Optional: without it, destructive " +
                 "actions still work but happen immediately.")]
        [SerializeField] protected ConfirmPromptController confirmPrompt;

        [Tooltip("Allow deleting the focused slot with the Tab action.")]
        [SerializeField] private bool allowDelete = true;

        /// Slots as of the last rebuild, index-aligned with the rows.
        private readonly List<SaveSlotInfo> _slots = new();

        protected override bool ModalActive => confirmPrompt != null && confirmPrompt.IsOpen;

        protected bool TryGetSaveService(out ISaveService save)
        {
            save = null;
            return Context?.Services != null && Context.Services.TryResolve(out save) && save != null;
        }

        // ---- Rows ---------------------------------------------------------------------------------

        protected override IReadOnlyList<RowModel> BuildRows()
        {
            _slots.Clear();

            if (!TryGetSaveService(out var save))
                return new[] { RowModel.Simple("no_service", "Save service unavailable", null, Context, enabled: false) };

            var slots = save.ListSlots();
            if (slots == null || slots.Count == 0)
                return new[] { RowModel.Simple("no_slots", "No save slots configured", null, Context, enabled: false) };

            var rows = new List<RowModel>(slots.Count);

            for (int i = 0; i < slots.Count; i++)
            {
                var info = slots[i];
                _slots.Add(info);

                rows.Add(RowModel.Simple($"slot_{info.slot}", FormatSlot(info),
                                         CreateSlotAction(info), Context,
                                         enabled: IsSlotEnabled(info, save)));
            }

            return rows;
        }

        /// identity, place, level, clock, wall-date. Empty slots read NONE.
        protected virtual string FormatSlot(SaveSlotInfo info)
        {
            if (!info.exists) return $"Slot {info.slot + 1}    NONE";

            var parts = new List<string>(4);
            if (!string.IsNullOrEmpty(info.sceneId)) parts.Add(info.sceneId);
            if (info.partyLevel > 0) parts.Add($"Lv {info.partyLevel}");
            parts.Add(FormatPlayTime(info.playTime));
            parts.Add(FormatSavedAt(info.savedAtUtcTicks));

            return $"Slot {info.slot + 1}    {string.Join("  |  ", parts)}";
        }

        protected static string FormatPlayTime(float seconds)
        {
            var t = TimeSpan.FromSeconds(seconds < 0f ? 0f : seconds);
            return $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}";
        }

        /// 0 ticks means "unknown", not the epoch — pre-v3 saves carry no timestamp.
        protected static string FormatSavedAt(long utcTicks)
            => utcTicks > 0
                ? new DateTime(utcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : "--";

        protected abstract IMenuAction CreateSlotAction(SaveSlotInfo info);
        protected abstract bool IsSlotEnabled(SaveSlotInfo info, ISaveService save);

        // ---- Confirmation flow --------------------------------------------------------------------

        /// Screens override to demand confirmation before a row runs. Return false to submit directly.
        protected virtual bool NeedsConfirmation(SaveSlotInfo info, out string question, out string confirmLabel)
        {
            question = null;
            confirmLabel = null;
            return false;
        }

        protected override void OnSubmit(InputAction.CallbackContext ctx)
        {
            if (ModalActive) return;

            int index = HighlightedIndex;
            if (index < 0 || index >= _slots.Count) { base.OnSubmit(ctx); return; }

            if (confirmPrompt == null || !NeedsConfirmation(_slots[index], out var question, out var confirmLabel))
            {
                base.OnSubmit(ctx);
                return;
            }

            // Capture the index: the prompt answers on a later frame, by which point focus may differ.
            int target = index;
            confirmPrompt.Ask(question, confirmLabel, "Cancel", () => ExecuteRow(target));
        }

        // ---- Delete -------------------------------------------------------------------------------

        protected override void OnTab()
        {
            if (!allowDelete || ModalActive) return;

            int index = HighlightedIndex;
            if (index < 0 || index >= _slots.Count) return;

            var info = _slots[index];
            if (!info.exists) return;

            void DoDelete()
            {
                if (!TryGetSaveService(out var save)) return;

                if (!save.DeleteSlot(info.slot))
                    Debug.LogWarning($"[JRPG.Menu] Delete of slot {info.slot} was rejected.");

                RebuildAndFocus();
            }

            if (confirmPrompt == null) { DoDelete(); return; }

            confirmPrompt.Ask($"Delete the save in slot {info.slot + 1}? This cannot be undone.",
                              "Delete", "Cancel", DoDelete);
        }
    }
}
