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
