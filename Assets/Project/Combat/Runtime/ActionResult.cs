using System.Collections.Generic;
using JRPG.Core;

namespace JRPG.Combat
{
    /// Explicit outcome object so tests, logs, future UI, and animation sequencing can inspect what
    /// happened. Returned directly from CombatService.SubmitAction.
    public class ActionResult
    {
        public bool success;
        public string failureReason;

        public string actorCombatantId;
        public string actionId;

        public List<EffectResult> effects = new();
        public List<string> defeatedCombatantIds = new();

        public bool battleEnded;
        public BattleOutcome battleOutcome = BattleOutcome.None;

        public static ActionResult Fail(string actorCombatantId, string actionId, string reason) => new()
        {
            success = false,
            actorCombatantId = actorCombatantId,
            actionId = actionId,
            failureReason = reason,
        };
    }

    public struct EffectResult
    {
        public string targetCombatantId;
        public string effectType;
        public int amount;
        public int hpBefore;
        public int hpAfter;
        public bool wasDefeated;
    }

    /// Lightweight non-mutating prediction of an action's effects, used by debug tools and (later) UI.
    public class ActionPreview
    {
        public bool usable;
        public string blockedReason;
        public string actionId;
        public List<EffectResult> predictedEffects = new();
    }
}
