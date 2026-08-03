using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Services;

namespace JRPG.Combat
{
    /// Evaluates authored BattleTriggerData at safe combat transition points and drives mid-battle
    /// dialogue. Interactive interruptions pause combat and resume on dialogue completion; passive lines display
    /// without blocking. 
    public sealed class CombatSequenceInterrupter : ICombatInterruptionService
    {
        private static readonly BattleTriggerTiming[] TurnTransitionTimings =
        {
            BattleTriggerTiming.TurnEnd,
            BattleTriggerTiming.ActionResolved,
            BattleTriggerTiming.HpThresholdCrossed,
            BattleTriggerTiming.PartyMemberDefeated,
            BattleTriggerTiming.EnemyDefeated,
            BattleTriggerTiming.RoundReached,
            BattleTriggerTiming.SpecificActionUsed,
            BattleTriggerTiming.CombatantActingOrTargeted,
        };

        private readonly CombatService _combat;
        private readonly DataRegistry _data;
        private readonly IServiceRegistry _services;
        private readonly IEventBus _bus;

        /// Optional data-driven importance→presentation mapping.
        public DialoguePresentationProfile PresentationProfile { get; set; }

        public CombatSequenceInterrupter(CombatService combat, DataRegistry data, IServiceRegistry services, IEventBus bus)
        {
            _combat = combat;
            _data = data;
            _services = services;
            _bus = bus;

            if (_bus != null)
            {
                _bus.Subscribe<BattleStarted>(OnBattleStarted);
                _bus.Subscribe<TurnStarted>(OnTurnStarted);
            }
        }

        public bool IsInterruptionActive
        {
            get
            {
                var ts = _combat.CurrentBattle?.triggerState;
                return ts != null && ts.interruptionActive;
            }
        }

        // ---- Evaluation entry points --------------------------------------------------------

        /// Called from CombatService.OnTurnTransition (after an action fully resolves, before advance).
        /// Returns true if an interactive interruption is now holding the turn transition.
        public bool EvaluateAtTurnTransition(BattleContext ctx, ActionResult result)
        {
            if (ctx?.triggerState == null || ctx.triggerState.interruptionActive) return false;

            ctx.triggerState.currentEvaluationPoint = BattleTriggerTiming.TurnEnd;
            ProcessMatches(ctx, Collect(ctx, TurnTransitionTimings, result), result);

            return TryStartNextPending(ctx, isTurnTransition: true);
        }

        private void OnBattleStarted(BattleStarted e) => EvaluateAtEvent(BattleTriggerTiming.BattleStart);
        private void OnTurnStarted(TurnStarted e) => EvaluateAtEvent(BattleTriggerTiming.TurnStart);

        private void EvaluateAtEvent(BattleTriggerTiming timing)
        {
            var ctx = _combat.CurrentBattle;
            if (ctx == null || ctx.isBattleOver || ctx.triggerState == null) return;
            if (ctx.triggerState.interruptionActive) return;

            ctx.triggerState.currentEvaluationPoint = timing;

            ProcessMatches(ctx, Collect(ctx, new[] { timing }, null), null);

            TryStartNextPending(ctx, isTurnTransition: false);
        }

        // ---- Matching -----------------------------------------------------------------------

        private List<BattleTriggerData> Collect(BattleContext ctx, BattleTriggerTiming[] timings, ActionResult result)
        {
            var list = new List<BattleTriggerData>();
            if (string.IsNullOrEmpty(ctx.encounterId)) return list;
            if (!_data.TryGet<EncounterData>(ctx.encounterId, out var enc) || enc == null || enc.battleTriggerIds == null)
                return list;

            var ts = ctx.triggerState;
            for (int i = 0; i < enc.battleTriggerIds.Count; i++)
            {
                if (!_data.TryGet<BattleTriggerData>(enc.battleTriggerIds[i], out var trig) || trig == null) continue;
                if (!Contains(timings, trig.timing)) continue;
                if (trig.oneShot && ts.firedOneShotIds.Contains(trig.Id)) continue;
                if (!Matches(ctx, trig, result, out _)) continue;

                list.Add(trig);
            }

            list.Sort((a, b) => b.priority.CompareTo(a.priority));

            return list;
        }

        private void ProcessMatches(BattleContext ctx, List<BattleTriggerData> matches, ActionResult result)
        {
            var ts = ctx.triggerState;

            for (int i = 0; i < matches.Count; i++)
            {
                var trig = matches[i];

                Matches(ctx, trig, result, out var combatInstance);
                if (trig.oneShot) ts.firedOneShotIds.Add(trig.Id);

                var presentation = ResolvePresentation(trig.importance);
                if (presentation.usesPassiveOverlay)
                {
                    ShowPassive(trig, combatInstance);
                }
                else
                {
                    ts.pending.Enqueue(new PendingInterruption
                    {
                        triggerId = trig.Id,
                        dialogueGraphId = trig.dialogueGraphId,
                        importance = trig.importance,
                        blocksCombatInput = trig.blocksCombatInput,
                        interruptedCombatantId = combatInstance?.combatantId,
                        interruptedSourceDataId = combatInstance?.sourceDataId,
                        followUpStoryFlag = trig.followUpStoryFlag,
                        followUpCombatActionId = trig.followUpCombatActionId,
                        followUpEncounterId = trig.followUpEncounterId,
                    });
                }
            }
        }

        private bool TryStartNextPending(BattleContext ctx, bool isTurnTransition)
        {
            var ts = ctx.triggerState;
            if (ts.interruptionActive || ts.pending.Count == 0) return false;

            var p = ts.pending.Dequeue();

            ts.interruptionActive = true;
            ts.activeInterruption = p;
            ts.interruptedCombatantId = p.interruptedCombatantId;
            if (isTurnTransition) ts.holdingTurnTransition = true;

            // Hold now, but defer the actual dialogue open to the next combat-UI pump
            ts.dialogueOpenPending = true;
            return true;
        }

        /// Called by combat UI pump (CombatFlowController.Update) while an interruption is active.
        public void PumpPendingInterruptionDialogue()
        {
            var ts = _combat.CurrentBattle?.triggerState;
            if (ts == null || !ts.interruptionActive || !ts.dialogueOpenPending) return;

            ts.dialogueOpenPending = false;
            
            StartInterruptionDialogue(ts.activeInterruption);
        }

        private void StartInterruptionDialogue(PendingInterruption pendingInt)
        {
            if (string.IsNullOrEmpty(pendingInt.dialogueGraphId))
            {
                Debug.LogWarning($"[JRPG.Combat] Interruption trigger '{pendingInt.triggerId}' has no dialogueGraphId; resuming.");
                NotifyInterruptionDialogueEnded();
                return;
            }
            if (!_services.TryResolve<IDialogueService>(out var dialogue))
            {
                Debug.LogWarning("[JRPG.Combat] No IDialogueService — cannot start interruption dialogue; resuming.");
                NotifyInterruptionDialogueEnded();
                return;
            }

            dialogue.StartDialogue(pendingInt.dialogueGraphId, new DialogueStartContext
            {
                initiatorId = "combat",
                speakerContextId = pendingInt.interruptedSourceDataId ?? string.Empty,
                sceneId = string.Empty,
                sourceSystem = "combat",
            });
        }

        // Passive lines never hold combat: they render through the read-only overlay and auto-dismiss.
        private void ShowPassive(BattleTriggerData trig, CombatantInstance combatInstance)
        {
            if (string.IsNullOrEmpty(trig.dialogueGraphId)) return;
            if (!_services.TryResolve<IDialogueService>(out var dialogue)) return;

            float seconds = ResolvePresentation(trig.importance).autoDismissSeconds;
            dialogue.ShowPassiveLine(trig.dialogueGraphId, new DialogueStartContext
            {
                initiatorId = "combat",
                speakerContextId = combatInstance?.sourceDataId ?? string.Empty,
                sceneId = string.Empty,
                sourceSystem = "combat",
            }, seconds);
        }

        // ---- Resume + resolution commands ---------------------------------------------------

        public void NotifyInterruptionDialogueEnded()
        {
            var ctx = _combat.CurrentBattle;
            var ts = ctx?.triggerState;
            if (ts == null || !ts.interruptionActive) return;

            ApplyFollowUps(ts.activeInterruption);
            ts.activeInterruption = default;
            ts.interruptionActive = false;

            // Another queued interactive interruption at this same point?
            if (TryStartNextPending(ctx, isTurnTransition: ts.holdingTurnTransition)) return;

            // Batch drained. If we deferred a turn transition, complete it now.
            if (ts.holdingTurnTransition)
            {
                ts.holdingTurnTransition = false;
                _combat.CompleteHeldTurnTransition();
            }
        }

        private void ApplyFollowUps(PendingInterruption p)
        {
            if (!string.IsNullOrEmpty(p.followUpStoryFlag) && _services.TryResolve<IStoryStateService>(out var story))
                story.SetBool(p.followUpStoryFlag, true);

            if (!string.IsNullOrEmpty(p.followUpCombatActionId))
            {
                var actorId = _combat.CurrentBattle?.currentActor?.combatantId;
                if (!string.IsNullOrEmpty(actorId))
                    _combat.SubmitAction(actorId, p.followUpCombatActionId, null);
            }

            if (!string.IsNullOrEmpty(p.followUpEncounterId))
                Debug.Log($"[JRPG.Combat] Trigger follow-up encounter '{p.followUpEncounterId}' noted " +
                          "(chained-battle handoff is authored via a StartFollowUpBattle command).");
        }

        public void ResumeBattle() => NotifyInterruptionDialogueEnded();

        public void EndBattleWithOutcome(BattleOutcome outcome)
        {
            var ts = _combat.CurrentBattle?.triggerState;
            if (ts != null) { ts.interruptionActive = false; ts.holdingTurnTransition = false; ts.heldResult = null; }
            
            _combat.EndBattle(outcome);
        }

        public void StartFollowUpBattle(string encounterId)
        {
            if (string.IsNullOrEmpty(encounterId)) return;
            // The current battle must end before a new one starts.
            if (_combat.IsInBattle) _combat.EndBattle(BattleOutcome.Victory);

            _combat.StartBattleFromActiveParty(encounterId);
        }

        public void QueueCombatAction(string combatantId, string actionId, IReadOnlyList<string> targetIds)
        {
            if (string.IsNullOrEmpty(combatantId)) combatantId = _combat.CurrentBattle?.currentActor?.combatantId;
            if (!string.IsNullOrEmpty(combatantId) && !string.IsNullOrEmpty(actionId))
                _combat.SubmitAction(combatantId, actionId, targetIds);
        }

        public void SetEnemyActionProfile(string profileId)
            => Debug.LogWarning($"[JRPG.Combat] SetEnemyActionProfile('{profileId}') is a Phase 11 stub (EnemyActionProfiles not built).");

        public void SetBattleTrigger(string triggerId, bool active)
        {
            var ts = _combat.CurrentBattle?.triggerState;

            if (ts == null || string.IsNullOrEmpty(triggerId)) return;
            if (active) ts.firedOneShotIds.Remove(triggerId);   // re-enable
            else ts.firedOneShotIds.Add(triggerId);             // suppress
        }

        // ---- Condition matching -------------------------------------------------------------

        private static bool Matches(BattleContext ctx, BattleTriggerData trig, ActionResult result, out CombatantInstance subjectActor)
        {
            subjectActor = null;

            switch (trig.timing)
            {
                case BattleTriggerTiming.BattleStart:
                    return true;

                case BattleTriggerTiming.TurnStart:
                case BattleTriggerTiming.CombatantActingOrTargeted:
                    subjectActor = ctx.currentActor;
                    return subjectActor != null && PassesFilter(trig, subjectActor);

                case BattleTriggerTiming.TurnEnd:
                case BattleTriggerTiming.ActionResolved:
                    subjectActor = ctx.FindCombatant(result?.actorCombatantId);
                    return subjectActor != null && PassesFilter(trig, subjectActor);

                case BattleTriggerTiming.SpecificActionUsed:
                    if (result == null || result.actionId != trig.actionId) return false;
                    subjectActor = ctx.FindCombatant(result.actorCombatantId);
                    return subjectActor != null && PassesFilter(trig, subjectActor);

                case BattleTriggerTiming.HpThresholdCrossed:
                    foreach (var c in ctx.AllCombatants())
                        if (PassesFilter(trig, c) && c.currentHP > 0 && c.MaxHP > 0 &&
                            c.currentHP <= Mathf.CeilToInt(trig.hpThresholdPercent * c.MaxHP))
                        { subjectActor = c; return true; }
                    return false;

                case BattleTriggerTiming.PartyMemberDefeated:
                    foreach (var c in ctx.partyCombatants)
                        if (c.IsDefeated && PassesFilter(trig, c)) { subjectActor = c; return true; }
                    return false;

                case BattleTriggerTiming.EnemyDefeated:
                    foreach (var c in ctx.enemyCombatants)
                        if (c.IsDefeated && PassesFilter(trig, c)) { subjectActor = c; return true; }
                    return false;

                case BattleTriggerTiming.RoundReached:
                    return ctx.roundNumber >= trig.roundNumber;
            }
            return false;
        }

        private static bool PassesFilter(BattleTriggerData trig, CombatantInstance combatInstance)
        {
            if (combatInstance == null) return false;
            if (trig.teamFilter == CombatantTeamFilter.Party && combatInstance.team != CombatantTeam.Party) return false;
            if (trig.teamFilter == CombatantTeamFilter.Enemy && combatInstance.team != CombatantTeam.Enemy) return false;
            if (!string.IsNullOrEmpty(trig.sourceDataIdFilter) && combatInstance.sourceDataId != trig.sourceDataIdFilter) return false;
            // Slot filter singles out one instance when several copies of the same enemy are present.
            if (!string.IsNullOrEmpty(trig.slotIdFilter) && combatInstance.encounterSlotId != trig.slotIdFilter) return false;
            
            return true;
        }

        private DialoguePresentationProfile.PresentationEntry ResolvePresentation(DialogueImportance importance)
        {
            if (PresentationProfile != null) return PresentationProfile.Resolve(importance);

            return new DialoguePresentationProfile.PresentationEntry
            {
                importance = importance,
                usesPassiveOverlay = importance == DialogueImportance.Passive,
                blocksInput = importance == DialogueImportance.Critical,
            };
        }

        private static bool Contains(BattleTriggerTiming[] timings, BattleTriggerTiming t)
        {
            for (int i = 0; i < timings.Length; i++) if (timings[i] == t) return true;

            return false;
        }
    }
}
