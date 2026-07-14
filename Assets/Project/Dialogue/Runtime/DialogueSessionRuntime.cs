using System.Collections.Generic;
using JRPG.Core;
using JRPG.Data;
using JRPG.Services;

namespace JRPG.Dialogue
{
    /// Mutable state for one active dialogue session. Not saved directly (Phase 9) — only the
    /// persistent outcomes it produces (story flags, completed graphs) are saved via story state.
    public sealed class DialogueSessionRuntime
    {
        public string sessionId;
        public string graphId;
        public string currentNodeId;
        public DialogueStartContext startContext;

        public List<string> visitedNodeIds = new();
        public List<string> selectedChoiceIds = new();

        public LayeredState previousState;
        public bool isComplete;

        /// Set by a StartBattle command; overrides the terminal node's exit resolution on end.
        public DialogueExitResolution? pendingExit;
    }
}
