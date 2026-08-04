using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace JRPG.Menu
{
    /// <summary>
    /// Shared ownership and press-gating for the single "Menu" <see cref="InputActionMap"/>.
    ///
    /// <para>Every menu screen and the dialogue presenter subscribe to the SAME map asset, so neither
    /// enabling nor disabling it can be an individual screen's decision. Both concerns handled here
    /// are cross-screen by nature, which is why this is a static helper rather than base-class
    /// behaviour — <see cref="JRPG.Dialogue.UI.DialoguePresenterController"/> is not a
    /// <see cref="MenuController"/> but has exactly the same two problems.</para>
    /// </summary>
    public static class MenuInputMap
    {
        // ---- Enable/disable ref-count ---------------------------------------------------------

        private sealed class MapRef
        {
            public int Count;
            public bool OwnsEnable; // false when something outside our control had already enabled it
        }

        private static readonly Dictionary<InputActionMap, MapRef> s_refs = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_refs.Clear();

        /// Take a reference on <paramref name="map"/>, enabling it on the 0→1 transition.
        public static void Acquire(InputActionMap map)
        {
            if (map == null) return;
            if (!s_refs.TryGetValue(map, out var entry))
            {
                entry = new MapRef { Count = 0, OwnsEnable = !map.enabled };
                s_refs[map] = entry;
            }

            entry.Count++;
            if (entry.Count == 1 && !map.enabled) map.Enable();
        }

        /// Drop a reference, disabling on the 1→0 transition — but only if we were the one who enabled it.
        public static void Release(InputActionMap map)
        {
            if (map == null) return;
            if (!s_refs.TryGetValue(map, out var entry)) return;

            entry.Count--;
            if (entry.Count > 0) return;

            s_refs.Remove(map);
            if (entry.OwnsEnable && map.enabled) map.Disable();
        }

        // ---- Stale-press gating ---------------------------------------------------------------

        /// Input-system timestamp to record when a screen starts listening.
        public static double Now => InputState.currentTime;

        /// <summary>
        /// True when <paramref name="ctx"/> is the tail of a button press that began BEFORE this
        /// screen started listening — in which case the press was meant for whatever was on screen
        /// at the time, not for us.
        ///
        /// <para><b>Why this is required, not defensive.</b> Several controls are deliberately bound
        /// twice across action maps that are never enabled simultaneously:</para>
        /// <list type="bullet">
        ///   <item>Gamepad <c>buttonSouth</c> = <c>Exploration/Interact</c> AND <c>Menu/Submit</c></item>
        ///   <item>Keyboard <c>e</c> = <c>Exploration/Interact</c> AND <c>Menu/PageR</c></item>
        /// </list>
        /// <para>Talking to an NPC with the gamepad therefore opened the dialogue AND immediately
        /// submitted its choice list, silently picking the first option — the player never saw the
        /// question. The same hazard exists for any menu opened from a button that is also bound in
        /// the Menu map. Comparing against <see cref="InputAction.CallbackContext.startTime"/> (when
        /// the press began) rather than a frame counter also covers a button held across several
        /// frames.</para>
        /// </summary>
        public static bool IsStalePress(InputAction.CallbackContext ctx, double listeningSince)
            => ctx.startTime < listeningSince;
    }
}
