using UnityEngine;

namespace JRPG.Data
{
    /// <summary>
    /// Project-wide bond tuning, referenced directly from <see cref="GameDatabase"/>.
    /// It is a singleton with no stable id, so it is a field rather than an indexed list entry.
    ///
    /// <para>Only the numbers live here. Which characters have bonds, what each level costs and what
    /// it pays are authored per character on <c>BondData</c>.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "JRPG/Bond Settings", fileName = "BondSettings")]
    public class BondSettings : ScriptableObject
    {
        [Tooltip("Bond progress each eligible party member earns per battle won. 0 disables the " +
                 "battle drip entirely, leaving dialogue and quest rewards as the only sources.")]
        [Min(0)] public int battleParticipationProgress = 1;

        [Tooltip("Whether reserve members earn the battle drip. Off means only the active party does.")]
        public bool reserveMembersEarnFromBattle;

        [Tooltip("Fallback ceiling for a BondData that authors no levels above it.")]
        [Min(1)] public int defaultMaxLevel = 10;
    }
}
