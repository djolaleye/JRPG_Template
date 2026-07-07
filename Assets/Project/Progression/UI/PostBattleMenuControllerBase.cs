using UnityEngine.InputSystem;
using JRPG.Menu;
using JRPG.Services;

namespace JRPG.Progression.UI
{
    /// Base for the post-battle placeholder screens: cancel is suppressed (the flow cannot be
    /// backed out of) and rows are rebuilt from ProgressionService state.
    public abstract class PostBattleMenuControllerBase : MenuController
    {
        protected static ProgressionService Progression
            => JRPG.Core.AppContext.Services != null
               && JRPG.Core.AppContext.Services.TryResolve<IProgressionService>(out var svc)
                ? svc as ProgressionService : null;

        protected static RowModel Info(string id, string label)
            => new() { id = id, label = label, enabled = false };

        protected override void OnCancel(InputAction.CallbackContext ctx)
        {
            // Intentionally no-op: post-battle screens advance only via Continue.
        }
    }
}
