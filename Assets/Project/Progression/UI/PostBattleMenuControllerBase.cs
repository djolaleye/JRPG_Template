using System.Collections.Generic;
using UnityEngine.InputSystem;
using JRPG.Data;
using JRPG.Menu;
using JRPG.Services;

namespace JRPG.Progression.UI
{
    /// Base for the post-battle screens: cancel is suppressed (the flow cannot be backed out of) and
    /// rows are rebuilt from ProgressionService state.
    public abstract class PostBattleMenuControllerBase : MenuController
    {
        protected static ProgressionService Progression
            => JRPG.Core.AppContext.Services != null
               && JRPG.Core.AppContext.Services.TryResolve<IProgressionService>(out var svc)
                ? svc as ProgressionService : null;

        protected static RowModel Info(string id, string label)
            => new() { id = id, label = label, enabled = false };

        /// <summary>
        /// Every post-battle screen advances the same way and none can be backed out of, so the prompt
        /// set is declared once here rather than repeated per screen. A screen with extra affordances
        /// (the allocation steppers) overrides this.
        /// </summary>
        public override IReadOnlyList<InputPrompt> Prompts { get; } = new[]
        {
            new InputPrompt("Submit", "Next"),
        };

        protected override void OnCancel(InputAction.CallbackContext ctx)
        {
            // Intentionally no-op: post-battle screens advance only via Continue.
        }

        // ---- Authored lookups -------------------------------------------------------------------
        //
        // Shared by the skill-unlock and skill-choice screens, which describe the same skills from
        // either side of the same event. Each falls back to the stable id rather than rendering blank,
        // so an unauthored asset shows up as a name to search for instead of an empty row.

        protected static DataRegistry Data => JRPG.Core.AppContext.Data as DataRegistry;

        protected static string CharacterName(DataRegistry data, string characterId)
        {
            if (data != null && data.TryGet<CharacterData>(characterId, out var cd) && cd != null
                && !string.IsNullOrEmpty(cd.displayName))
                return cd.displayName;

            return characterId;
        }

        protected static string SkillName(DataRegistry data, string skillId)
        {
            if (data != null && data.TryGet<CombatActionData>(skillId, out var a) && a != null
                && !string.IsNullOrEmpty(a.displayName))
                return a.displayName;

            return skillId;
        }

        protected static string SkillDescription(DataRegistry data, string skillId)
        {
            if (data != null && data.TryGet<CombatActionData>(skillId, out var a) && a != null
                && !string.IsNullOrEmpty(a.description))
                return a.description;

            return string.Empty;
        }

        /// The MP line, or empty for a skill that costs nothing.
        protected static string SkillCost(DataRegistry data, string skillId)
        {
            if (data == null || !data.TryGet<CombatActionData>(skillId, out var a) || a.costs == null) return "";

            for (int i = 0; i < a.costs.Count; i++)
            {
                if (a.costs[i].type == CombatCostType.MP) return $"MP {a.costs[i].costAmount}";
                if (a.costs[i].type == CombatCostType.SP) return $"SP {a.costs[i].costAmount}";
                if (a.costs[i].type == CombatCostType.HP) return $"HP {a.costs[i].costAmount}";
            }
            
            return "";
        }
    }
}
