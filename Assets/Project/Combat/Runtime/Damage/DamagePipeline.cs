using System.Collections.Generic;
using System.Text;

namespace JRPG.Combat
{
    /// The ordered damage calculation. Composed once (not per action) and run for every damage hit, so
    /// adding/removing/reordering a rule is a change to this list rather than to action execution.
    ///
    /// Stage order: base power → attacker stat → equipment → accuracy/evasion → defense →
    /// element → critical → status → passive → difficulty → variance → guard → clamp/round.
    public sealed class DamagePipeline
    {
        private readonly List<IDamageStage> _stages = new();
        private readonly DamageContext _ctx = new();

        /// Optional audit trail: when enabled, records each stage's running value so a damage number can
        /// be explained.
        public bool AuditEnabled { get; set; }
        public string LastAudit { get; private set; } = string.Empty;

        public IReadOnlyList<IDamageStage> Stages => _stages;

        public DamagePipeline Add(IDamageStage stage)
        {
            if (stage != null) _stages.Add(stage);

            return this;
        }

        public void Insert(int index, IDamageStage stage)
        {
            if (stage != null) _stages.Insert(index, stage);
        }

        public bool Remove<T>() where T : IDamageStage
        {
            int i = _stages.FindIndex(s => s is T);

            if (i < 0) return false;

            _stages.RemoveAt(i);

            return true;
        }

        /// Replaces the first stage of type T (used to swap a no-op placeholder for a live stage).
        public bool Replace<T>(IDamageStage stage) where T : IDamageStage
        {
            int i = _stages.FindIndex(s => s is T);

            if (i < 0) return false;

            _stages[i] = stage;

            return true;
        }

        /// <summary>
        /// Non-mutating projection of a hit, for specific screens/tooltips.
        /// </summary>
        public DamageContext RunPreview(CombatantInstance actor, CombatantInstance target,
            JRPG.Data.CombatEffect effect, JRPG.Data.CombatActionData action)
            => Run(actor, target, effect, action, rng: null, isPreview: true);

        /// Runs every stage in order and returns the populated context
        public DamageContext Run(CombatantInstance actor, CombatantInstance target,
            JRPG.Data.CombatEffect effect, JRPG.Data.CombatActionData action, System.Random rng,
            bool isPreview = false)
        {
            _ctx.Reset(actor, target, effect, action, rng, isPreview);

            StringBuilder sb = AuditEnabled ? new StringBuilder() : null;
            sb?.Append($"[dmg] {actor?.displayName} → {target?.displayName} ({_ctx.element})");

            for (int i = 0; i < _stages.Count; i++)
            {
                _stages[i].Apply(_ctx);
                sb?.Append($" | {_stages[i].Name}={_ctx.runningDamage:0.##}");

                // A miss or immunity short-circuits the rest of the calculation.
                if (_ctx.missed || _ctx.immune) break;
            }

            if (sb != null)
            {
                sb.Append($" => {_ctx.finalAmount}");
                if (_ctx.missed) sb.Append(" (MISS)");
                if (_ctx.critical) sb.Append(" (CRIT)");
                if (_ctx.absorbed) sb.Append(" (ABSORB)");
                if (_ctx.immune) sb.Append(" (IMMUNE)");
                LastAudit = sb.ToString();
            }

            return _ctx;
        }
    }
}
