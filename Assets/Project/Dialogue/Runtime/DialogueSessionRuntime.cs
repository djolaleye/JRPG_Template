using System.Collections.Generic;
using JRPG.Core;
using JRPG.Data;
using JRPG.Services;

namespace JRPG.Dialogue
{
    /// Mutable state for one active dialogue session.
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

        /// The ContextualCanvasRegistry menu id the presenter was opened under (interactive vs combat),
        /// so EndInternal closes exactly what it opened.
        public string presenterMenuId;

        /// Set by a StartBattle command; overrides the terminal node's exit resolution on end.
        public DialogueExitResolution? pendingExit;
    }
}
