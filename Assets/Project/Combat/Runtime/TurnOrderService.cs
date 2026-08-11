using System.Collections.Generic;

namespace JRPG.Combat
{
    /// Deterministic turn order: higher Speed first, Party before Enemy on ties, then stable
    /// combatantId for fully reproducible test results.
    public sealed class TurnOrderService
    {
        public void BuildQueue(BattleContext ctx)
        {
            var living = new List<CombatantInstance>();

            foreach (var combatant in ctx.AllCombatants())
                if (!combatant.IsDefeated) living.Add(combatant);

            living.Sort(Compare);

            ctx.turnQueue.Clear();
            
            for (int i = 0; i < living.Count; i++)
                ctx.turnQueue.Enqueue(living[i].combatantId);
        }

        /// <summary>
        /// The order next round's queue would be built in, without touching the live queue.
        ///
        /// <para>The turn-order strip needs this because <c>ctx.turnQueue</c> is consumed as the
        /// round advances: late in a round it holds only the stragglers, so a strip rendered from it
        /// alone shows the coming order shrinking to nothing. Showing "then next round…" requires
        /// projecting a fresh sort, which is what <see cref="BuildQueue"/> will do when the round rolls
        /// over.</para>
        ///
        /// <para>Non-mutating: sorts a copy. Defeated combatants are excluded exactly as
        /// <see cref="BuildQueue"/> excludes them.</para>
        /// </summary>
        public IReadOnlyList<string> ProjectNextRound(BattleContext ctx)
        {
            var living = new List<CombatantInstance>();
            if (ctx == null) return System.Array.Empty<string>();

            foreach (var combatant in ctx.AllCombatants())
                if (!combatant.IsDefeated) living.Add(combatant);

            living.Sort(Compare);

            var ids = new List<string>(living.Count);
            for (int i = 0; i < living.Count; i++) ids.Add(living[i].combatantId);

            return ids;
        }

        /// <summary>
        /// The ordering rule itself, for callers that need to sort combatants the way the queue does
        /// without building a queue.
        /// </summary>
        public static int CompareTurnOrder(CombatantInstance a, CombatantInstance b) => Compare(a, b);

        private static int Compare(CombatantInstance a, CombatantInstance b)
        {
            int bySpeed = b.Speed.CompareTo(a.Speed); // descending
            if (bySpeed != 0) return bySpeed;

            int byTeam = TeamPriority(a.team).CompareTo(TeamPriority(b.team));
            if (byTeam != 0) return byTeam;

            return string.CompareOrdinal(a.combatantId, b.combatantId);
        }

        private static int TeamPriority(CombatantTeam team) => team == CombatantTeam.Party ? 0 : 1;
    }
}
