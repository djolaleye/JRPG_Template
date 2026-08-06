using UnityEngine;
using JRPG.Core;

namespace JRPG.Data
{
    /// <summary>
    /// Presentation metadata for a passive effect: what to call it and how to describe it.
    ///
    /// <para><b>This carries no behaviour.</b> What a passive does is a code-defined
    /// <c>IPassive</c> in <c>PassiveCompendium</c>, resolved by id through <c>PassiveRegistry</c>. 
    /// This asset is keyed by that same id and supplies only the player-facing half,
    /// so UI can say "Fire Ward — halves incoming
    /// Fire damage" instead of printing <c>passive_fire_ward</c>.</para>
    ///
    /// <para><b>Optional by design.</b> A passive with no asset still works; screens fall back to the raw
    /// id. That keeps a code-registered passive usable the moment it exists, without forcing an authoring
    /// step before it can be tested.</para>
    ///
    /// <para>The id must match the one the passive registers under, e.g.
    /// <c>PassiveRegistry.CreateStandard()</c>'s <c>passive_fire_ward</c>.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "JRPG/Combat/Passive", fileName = "PassiveData")]
    public class PassiveData : GameDataBase
    {
        [Header("Presentation")]
        [Tooltip("Icon for status/passive strips. Optional; UI falls back to text.")]
        public Sprite icon;

        [Tooltip("Hex colour a HUD can tint the icon with.")]
        public string indicatorColorHex = "#FFFFFF";

        [Tooltip("Broad category, for grouping in a list. Presentation only.")]
        public PassiveKind kind = PassiveKind.Other;
    }

    /// <summary>How a passive reads to the player. Grouping only; the behaviour lives in code.</summary>
    public enum PassiveKind
    {
        Offensive,
        Defensive,
        Sustain,
        Other,
    }
}
