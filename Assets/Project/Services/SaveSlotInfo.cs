using System;

namespace JRPG.Services
{
    /// <summary>
    /// Lightweight, UI-facing description of one save slot. Produced by
    /// <see cref="ISaveService.GetSlotInfo"/> by reading only the save file's metadata header.
    ///
    /// Missing or corrupt slots are reported as a fully-populated struct with
    /// <see cref="exists"/> = false rather than by throwing or returning null.
    /// </summary>
    [Serializable]
    public struct SaveSlotInfo
    {
        /// Zero-based slot index. Always populated, even when <see cref="exists"/> is false.
        public int slot;

        /// True when a readable save file exists for this slot. When false every other field is
        /// at its default and the UI should render an "Empty Slot" row.
        public bool exists;

        /// Scene name to load when restoring this slot. May be empty for legacy saves written
        /// before scene metadata was recorded — callers should fall back to their own default.
        public string sceneId;

        /// Total accumulated play time in seconds at the moment the slot was written.
        public float playTime;

        /// <see cref="DateTime.UtcNow"/>.Ticks at save time. <b>0 means "unknown"</b> — legacy
        /// (pre-v3) saves carry no timestamp and the UI should render a placeholder such as "—"
        /// instead of the Unix/DateTime epoch.
        public long savedAtUtcTicks;

        /// Identifier of the lead/protagonist character. This is the character's stable id (the
        /// save format carries no display names), so UI that wants a localized name should look
        /// the id up in the data registry and fall back to the raw id. May be empty.
        public string protagonistName;

        /// Level of the protagonist at save time, or the highest level in the roster if the
        /// protagonist could not be identified. 0 means "unknown" — UI should hide the level.
        public int partyLevel;

        /// <summary>
        /// Convenience one-line label for simple slot lists, e.g.
        /// <c>"Slot 1 — char_hero Lv.12 · Village · 1:23:45 · 2026-08-03 14:02"</c>, or
        /// <c>"Slot 1 — Empty"</c>. Rich/localized layouts should ignore this and format the raw
        /// fields themselves. Exists so a debug or placeholder screen needs no formatting code.
        /// </summary>
        public string DisplayLabel
        {
            get
            {
                if (!exists) return $"Slot {slot + 1} — Empty";

                var parts = $"Slot {slot + 1} —";
                if (!string.IsNullOrEmpty(protagonistName)) parts += $" {protagonistName}";
                if (partyLevel > 0) parts += $" Lv.{partyLevel}";
                if (!string.IsNullOrEmpty(sceneId)) parts += $" · {sceneId}";

                var t = TimeSpan.FromSeconds(playTime < 0f ? 0f : playTime);
                parts += $" · {(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}";

                parts += savedAtUtcTicks > 0
                    ? $" · {new DateTime(savedAtUtcTicks, DateTimeKind.Utc).ToLocalTime():yyyy-MM-dd HH:mm}"
                    : " · —";

                return parts;
            }
        }

        /// Canonical "no save here" value for a given slot index.
        public static SaveSlotInfo Empty(int slot) => new SaveSlotInfo
        {
            slot = slot,
            exists = false,
            sceneId = string.Empty,
            playTime = 0f,
            savedAtUtcTicks = 0L,
            protagonistName = string.Empty,
            partyLevel = 0
        };
    }
}
