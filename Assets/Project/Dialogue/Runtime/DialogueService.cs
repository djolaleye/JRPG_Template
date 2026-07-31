using System;
using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Party;
using JRPG.Services;

namespace JRPG.Dialogue
{
    /// Owns the active dialogue session: validates graphs, walks nodes, evaluates choices, runs
    /// commands, resolves speaker/tokens, drives the presenter through IMenuService, emits events,
    /// and performs the deferred StartBattle handoff on completion. UI reads snapshots
    public sealed class DialogueService : IDialogueService
    {
        private const string PresenterMenuId = "dialogue_interactive";
        private const string CombatPresenterMenuId = "dialogue_combat";

        private readonly IServiceRegistry _services;
        private readonly IEventBus _bus;
        private readonly GameStateController _state;
        private readonly DataRegistry _data;

        private readonly DialogueConditionEvaluator _conditions;
        private readonly DialogueCommandExecutor _commands;
        private readonly SpeakerResolver _speakers;
        private readonly DynamicTokenResolver _tokens;

        private DialogueSessionRuntime _session;

        public DialogueService(IServiceRegistry services, IEventBus bus, GameStateController state,
            DataRegistry data, IPartyService party, IInventoryService inventory, IStoryStateService story)
        {
            _services = services;
            _bus = bus;
            _state = state;
            _data = data;

            var partyRuntime = party as IPartyRuntimeQueries;
            _conditions = new DialogueConditionEvaluator(party, partyRuntime, inventory, story);
            _commands = new DialogueCommandExecutor(party, inventory, story, bus, services);
            _speakers = new SpeakerResolver(data, partyRuntime);
            _tokens = new DynamicTokenResolver(data, partyRuntime, story);
        }

        // ---- IDialogueService -----------------------------------------------------------------

        public bool IsDialogueActive => _session != null && !_session.isComplete;

        public DialogueSessionRuntime CurrentSession => _session;

        public bool CanStartDialogue(string graphId, DialogueStartContext context)
        {
            if (IsDialogueActive) return false;
            return _data.TryGet<DialogueGraphData>(graphId, out var graph) && graph.FindNode(graph.entryNodeId) != null;
        }

        public void StartDialogue(string graphId, DialogueStartContext context)
        {
            if (IsDialogueActive) { Debug.LogWarning("[JRPG.Dialogue] StartDialogue ignored — already active."); return; }
            if (!_data.TryGet<DialogueGraphData>(graphId, out var graph))
            {
                Debug.LogError($"[JRPG.Dialogue] Unknown dialogue graph '{graphId}'.");
                return;
            }
            var entry = graph.FindNode(graph.entryNodeId);
            if (entry == null)
            {
                Debug.LogError($"[JRPG.Dialogue] Graph '{graphId}' has no entry node '{graph.entryNodeId}'.");
                return;
            }

            // Combat interruption - route to the combat presenter entry (GameMode.Combat) so closing
            // restores the battle's layered state rather than Exploration. 
            bool inCombat = _services.TryResolve<ICombatService>(out var activeCombat) && activeCombat.IsInBattle;
           
            string presenterMenuId = inCombat ? CombatPresenterMenuId : PresenterMenuId;

            _session = new DialogueSessionRuntime
            {
                sessionId = Guid.NewGuid().ToString("N"),
                graphId = graphId,
                startContext = context,
                previousState = _state.Current,
                presenterMenuId = presenterMenuId,
            };

            _bus.Publish(new DialogueStarted(graphId, _session.sessionId));
            EnterNode(entry.nodeId);

            // Presenter open captures the pre-dialogue state as priorState and switches to the entry's
            // layered state; Close restores it automatically on end.
            if (_services.TryResolve<IMenuService>(out var menus)) menus.Open(presenterMenuId, null);
            else Debug.LogWarning("[JRPG.Dialogue] No IMenuService — presenter not opened.");
        }

        public void Advance()
        {
            if (!IsDialogueActive) return;
            var node = CurrentNode();
            if (node == null) { EndInternal(default); return; }
            if (node.choices != null && node.choices.Count > 0) return; // choice node — wait for Choose

            RunExit(node);
            if (node.endGraphAfterThisNode || string.IsNullOrEmpty(node.nextNodeId)) EndInternal(ResolveExit(node));
            else EnterNode(node.nextNodeId);
        }

        public void Choose(string choiceId)
        {
            if (!IsDialogueActive) return;
            var node = CurrentNode();
            if (node == null) return;

            DialogueChoiceData choice = null;
            for (int i = 0; i < node.choices.Count; i++)
                if (node.choices[i].choiceId == choiceId) { choice = node.choices[i]; break; }
            if (choice == null) { Debug.LogWarning($"[JRPG.Dialogue] Choice '{choiceId}' not on node '{node.nodeId}'."); return; }
            if (!_conditions.EvaluateAll(choice.conditions, _session)) { Debug.LogWarning($"[JRPG.Dialogue] Choice '{choiceId}' is unavailable."); return; }

            _session.selectedChoiceIds.Add(choiceId);
            _commands.ExecuteAll(choice.commands, _session, _session.graphId);
            _bus.Publish(new DialogueChoiceSelected(_session.graphId, node.nodeId, choiceId));

            RunExit(node);
            if (string.IsNullOrEmpty(choice.nextNodeId)) EndInternal(ResolveExit(node));
            else EnterNode(choice.nextNodeId);
        }

        public void EndDialogue(DialogueEndReason reason)
        {
            if (_session == null || _session.isComplete) return;
            var node = CurrentNode();
            if (node != null) RunExit(node);
            EndInternal(ResolveExit(node));
        }

        public void ShowPassiveLine(string graphId, DialogueStartContext context, float autoDismissSeconds)
        {
            var sink = PassiveDialogueSink.Current;

            if (sink == null) { Debug.LogWarning("[JRPG.Dialogue] No passive overlay in scene — passive line dropped."); return; }
            
            if (!_data.TryGet<DialogueGraphData>(graphId, out var graph))
            {
                Debug.LogError($"[JRPG.Dialogue] Unknown passive dialogue graph '{graphId}'.");
                return;
            }

            // Passive is read-only: walk linear nodes from the entry, pushing each resolved line to the overlay.
            var node = graph.FindNode(graph.entryNodeId);
            int guard = 0;

            while (node != null && guard++ < 64)
            {
                var speaker = _speakers.Resolve(node.speakerRef, context);
                var body = _tokens.ResolveTokens(node.text, context);

                sink.ShowLine(speaker.displayName, body, autoDismissSeconds);

                bool stop = node.endGraphAfterThisNode
                            || string.IsNullOrEmpty(node.nextNodeId)
                            || (node.choices != null && node.choices.Count > 0);

                if (stop) break;
                
                node = graph.FindNode(node.nextNodeId);
            }
        }

        // ---- Rich read API  -----------------------------

        public DialogueRenderSnapshot GetCurrentRenderSnapshot()
        {
            var node = CurrentNode();
            if (node == null) return null;
            return new DialogueRenderSnapshot
            {
                graphId = _session.graphId,
                nodeId = node.nodeId,
                speaker = _speakers.Resolve(node.speakerRef, _session.startContext),
                body = _tokens.ResolveTokens(node.text, _session.startContext),
                hasChoices = node.choices != null && node.choices.Count > 0,
                importance = node.importance,
            };
        }

        public IReadOnlyList<DialogueChoiceViewData> GetCurrentChoices()
        {
            var list = new List<DialogueChoiceViewData>();
            var node = CurrentNode();
            if (node?.choices == null) return list;
            for (int i = 0; i < node.choices.Count; i++)
            {
                var choice = node.choices[i];
                list.Add(new DialogueChoiceViewData
                {
                    choiceId = choice.choiceId,
                    text = _tokens.ResolveTokens(choice.displayText, _session.startContext),
                    available = _conditions.EvaluateAll(choice.conditions, _session),
                });
            }
            return list;
        }

        // ---- Internals ------------------------------------------------------------------------

        private DialogueGraphData CurrentGraph()
            => _session != null && _data.TryGet<DialogueGraphData>(_session.graphId, out var g) ? g : null;

        private DialogueNodeData CurrentNode() => CurrentGraph()?.FindNode(_session?.currentNodeId);

        private void EnterNode(string nodeId)
        {
            var graph = CurrentGraph();
            var node = graph?.FindNode(nodeId);
            if (node == null) { Debug.LogError($"[JRPG.Dialogue] Missing node '{nodeId}' in '{_session.graphId}'."); EndInternal(default); return; }

            _session.currentNodeId = nodeId;
            if (!_session.visitedNodeIds.Contains(nodeId)) _session.visitedNodeIds.Add(nodeId);
            _commands.ExecuteAll(node.onEnterCommands, _session, _session.graphId);
            _bus.Publish(new DialogueNodeEntered(_session.graphId, nodeId));
        }

        private void RunExit(DialogueNodeData node) => _commands.ExecuteAll(node?.onExitCommands, _session, _session.graphId);

        private DialogueExitResolution ResolveExit(DialogueNodeData node)
            => _session.pendingExit ?? (node != null ? node.exitResolution : default);

        private void EndInternal(DialogueExitResolution exit)
        {
            if (_session == null) return;
            var s = _session;
            s.isComplete = true;

            if (_services.TryResolve<IStoryStateService>(out var story)) story.MarkDialogueCompleted(s.graphId);
            for (int i = 0; i < s.selectedChoiceIds.Count; i++)
                if (_services.TryResolve<IStoryStateService>(out var st)) st.MarkChoiceSelected(s.selectedChoiceIds[i]);

            _bus.Publish(new DialogueCompleted(s.graphId, s.sessionId, exit.exitType.ToString(), exit.targetId ?? string.Empty));

            // Close presenter (restores the pre-dialogue layered state), then hand off.
            var presenterId = string.IsNullOrEmpty(s.presenterMenuId) ? PresenterMenuId : s.presenterMenuId;
            if (_services.TryResolve<IMenuService>(out var menus) && menus.ActiveMenuId == presenterId) menus.Close();

            _session = null;

            bool wasCombatInterruption = s.startContext.sourceSystem == "combat";

            // Exploration handoff: an authored StartBattle exit starts a fresh battle (never mid-combat).
            if (exit.exitType == DialogueExitType.StartBattle && !string.IsNullOrEmpty(exit.targetId)
                && _services.TryResolve<ICombatService>(out var combat) && !combat.IsInBattle)
            {
                combat.StartBattleFromActiveParty(exit.targetId);
                return;
            }

            // Combat-integration exits + the default combat-interruption resume, routed through the
            // narrow interruption interface.
            if (wasCombatInterruption || IsCombatExit(exit.exitType))
                ResolveCombatExit(exit, wasCombatInterruption);
        }

        private static bool IsCombatExit(DialogueExitType t)
            => t == DialogueExitType.ResumeCombat || t == DialogueExitType.EndCombat
               || t == DialogueExitType.StartFollowUpBattle;

        private void ResolveCombatExit(DialogueExitResolution exit, bool wasCombatInterruption)
        {
            if (!_services.TryResolve<ICombatInterruptionService>(out var interruption)) return;

            switch (exit.exitType)
            {
                case DialogueExitType.EndCombat:
                    var outcome = ParseOutcome(exit.targetId);
                    interruption.EndBattleWithOutcome(outcome);
                    return;
                case DialogueExitType.StartFollowUpBattle:
                    interruption.StartFollowUpBattle(exit.targetId);
                    return;
                default:
                    // ResumeCombat, or any exit reached from a combat interruption: resume the held battle.
                    if (wasCombatInterruption || exit.exitType == DialogueExitType.ResumeCombat)
                        interruption.NotifyInterruptionDialogueEnded();
                    return;
            }
        }

        private static BattleOutcome ParseOutcome(string targetId)
            => System.Enum.TryParse<BattleOutcome>(targetId, true, out var o) ? o : BattleOutcome.Victory;
    }
}
