using System;
using System.Collections.Generic;
using UnityEngine;
using JRPG.Characters;
using JRPG.Core;
using JRPG.Data;
using JRPG.Party;
using JRPG.Services;

namespace JRPG.Combat
{
    /// Owns the active battle and orchestrates initialization, the turn loop, action submission,
    /// win/loss evaluation, and end-of-battle commit/packaging.
    public sealed class CombatService : ICombatService
    {
        private readonly IEventBus _bus;
        private readonly GameStateController _state;
        private readonly DataRegistry _data;
        private readonly IPartyRuntimeQueries _party;
        private readonly IInventoryService _inventory;

        private readonly CombatantFactory _factory;
        private readonly TurnOrderService _turnOrder = new();
        private readonly TargetingSystem _targeting = new();
        private readonly CombatActionResolver _resolver;
        private readonly IEnemyActionSelector _enemyAI = new SimpleEnemyActionSelector();
        private readonly ICombatOutcomeEvaluator _outcome = new CombatOutcomeEvaluator();
        private readonly RuntimeCharacterFactory _characterFactory;

        private BattleContext _battle;
        private float _battleStartTime;

        public CombatService(IEventBus bus, GameStateController state, DataRegistry data,
            IPartyRuntimeQueries party, IInventoryService inventory)
        {
            _bus = bus;
            _state = state;
            _data = data;
            _party = party;
            _inventory = inventory;
            _factory = new CombatantFactory(data);
            _resolver = new CombatActionResolver(inventory, data,
                damage: DamagePipelineFactory.CreateStandard(data?.ElementMatrix));
            _characterFactory = new RuntimeCharacterFactory(data);
        }

        public bool IsInBattle => _battle != null && !_battle.isBattleOver;
        public BattleContext CurrentBattle => _battle;
        /// Exposed so later phases (statuses, elements, passives) can register executors and swap
        /// damage-pipeline stages without reaching through CombatService for every rule.
        public CombatActionResolver Resolver => _resolver;
        public BattleResultData LastResult { get; private set; }
        public CombatSequenceInterrupter Interrupter { get; set; }

        // ---- ICombatService 

        public void StartBattleFromActiveParty(string encounterId)
        {
            StartBattle(new BattleStartRequest
            {
                encounterId = encounterId,
                useActivePartyFromPartyService = true,
            });
        }

        public void StartBattle(string encounterId, IReadOnlyList<string> partyIds, IReadOnlyList<string> enemyIds)
        {
            StartBattle(new BattleStartRequest
            {
                encounterId = encounterId,
                partyCharacterIds = partyIds,
                enemyIds = enemyIds,
                useActivePartyFromPartyService = partyIds == null || partyIds.Count == 0,
            });
        }

        bool ICombatService.SubmitAction(string combatantId, string actionId, IReadOnlyList<string> targetIds)
            => SubmitAction(combatantId, actionId, targetIds).success;

        // ---- Battle initialization ----------------------------------------------------------

        public void StartBattle(BattleStartRequest request)
        {
            if (IsInBattle)
            {
                Debug.LogWarning("[JRPG.Combat] StartBattle ignored — already in battle.");
                return;
            }

            EncounterData encounter = null;
            if (!string.IsNullOrEmpty(request.encounterId))
                _data.TryGet(request.encounterId, out encounter);

            var ctx = new BattleContext
            {
                battleId = Guid.NewGuid().ToString("N"),
                encounterId = request.encounterId,
                escapable = encounter != null && encounter.escapable,
            };

            BuildPartyCombatants(ctx, request);
            BuildEnemyCombatants(ctx, request, encounter);

            if (ctx.partyCombatants.Count == 0)
            {
                Debug.LogError("[JRPG.Combat] StartBattle aborted — no party combatants resolved.");
                return;
            }
            if (ctx.enemyCombatants.Count == 0)
            {
                Debug.LogError("[JRPG.Combat] StartBattle aborted — no enemy combatants resolved.");
                return;
            }

            _battle = ctx;
            LastResult = null;
            _battleStartTime = Time.realtimeSinceStartup;

            ctx.phase = CombatPhase.CalculateTurnOrder;
            _turnOrder.BuildQueue(ctx);

            // Input stays Disabled until a menu claims it: CombatFlowController opens combat_command on
            // the first party TurnStarted, and MenuService applies that entry's input context from the
            // ContextualCanvasRegistry. Enemy turns never re-enable input.
            _state.SetState(new LayeredState(GameMode.Combat, OverlayState.None, InputContext.Disabled));
            _bus.Publish(new BattleStarted(ctx.battleId, ctx.encounterId ?? string.Empty));

            _resolver.Passives.DispatchAll(PassiveHook.BattleStart, ctx);

            AdvanceToNextActor();
        }

        private void BuildPartyCombatants(BattleContext ctx, BattleStartRequest request)
        {
            ctx.phase = CombatPhase.BuildRuntimeCombatants;

            List<CharacterRuntimeInstance> sources = new();
            if (request.useActivePartyFromPartyService)
            {
                var active = _party.GetActiveCombatParty();
                for (int i = 0; i < active.Count; i++) sources.Add(active[i]);
            }
            else if (request.partyCharacterIds != null)
            {
                // Resolve to live active instances where possible; otherwise build a fresh instance.
                var active = _party.GetActiveCombatParty();
                for (int i = 0; i < request.partyCharacterIds.Count; i++)
                {
                    var id = request.partyCharacterIds[i];
                    CharacterRuntimeInstance inst = null;
                    for (int a = 0; a < active.Count; a++)
                        if (active[a].SourceDataId == id) { inst = active[a]; break; }

                    if (inst == null) inst = _characterFactory.Create(id);
                    if (inst != null) sources.Add(inst);
                }
            }

            for (int i = 0; i < sources.Count; i++)
            {
                var combatant = _factory.FromCharacter(sources[i]);
                ctx.partyCombatants.Add(combatant);
                ctx.partySources[combatant.combatantId] = sources[i];
            }
        }

        private void BuildEnemyCombatants(BattleContext ctx, BattleStartRequest request, EncounterData encounter)
        {
            // An explicit request roster wins; otherwise use the encounter's authored roster, which
            // carries a per-instance slot id so duplicates of one EnemyData stay distinguishable.
            List<EncounterEnemyEntry> roster;

            if (request.enemyIds != null && request.enemyIds.Count > 0)
            {
                roster = new List<EncounterEnemyEntry>();
                for (int i = 0; i < request.enemyIds.Count; i++)
                    roster.Add(new EncounterEnemyEntry { enemyId = request.enemyIds[i], slotId = $"{request.enemyIds[i]}_{i}" });
            }
            else
            {
                roster = encounter?.ResolveRoster();
            }

            if (roster == null) return;

            for (int i = 0; i < roster.Count; i++)
            {
                var slot = roster[i];
                if (!_data.TryGet<EnemyData>(slot.enemyId, out var enemy))
                {
                    Debug.LogError($"[JRPG.Combat] Unknown enemy id '{slot.enemyId}' — skipped.");
                    continue;
                }
                ctx.enemyCombatants.Add(_factory.FromEnemySlot(enemy, slot, i));
            }
        }


        // ---- Turn loop ----------------------------------------------------------------------

        private void AdvanceToNextActor()
        {
            if (_battle == null || _battle.isBattleOver) return;

            while (true)
            {
                if (_battle.turnQueue.Count == 0)
                {
                    _battle.roundNumber++;
                    _turnOrder.BuildQueue(_battle);
                    if (_battle.turnQueue.Count == 0) return; // no living combatants
                }

                var id = _battle.turnQueue.Dequeue();
                var actor = _battle.FindCombatant(id);
                if (actor == null || actor.IsDefeated) continue;

                _battle.currentActor = actor;

                // Guard clears when the guarding combatant's own next turn begins.
                actor.isGuarding = false;
                actor.guardDamageMultiplier = 1f;

                // Status + passive timing: turn start.
                _resolver.Status.Process(StatusTiming.TurnStart, actor);
                _resolver.Passives.Dispatch(PassiveHook.TurnStart, actor, _battle);

                if (actor.IsDefeated) continue;

                _battle.phase = actor.team == CombatantTeam.Party ? CombatPhase.AwaitPlayerInput : CombatPhase.EnemyAI;
                _bus.Publish(new TurnStarted(actor.combatantId));
                return;
            }
        }

        public void AdvanceEnemyTurn()
        {
            if (!IsInBattle) return;

            var actor = _battle.currentActor;
            if (actor == null || actor.team != CombatantTeam.Enemy) return;

            var choice = _enemyAI.ChooseAction(actor, _battle);
            SubmitAction(actor.combatantId, choice.actionId, choice.targetCombatantIds);
        }


        // ---- Queries ------------------------------------------------------------------------

        public IReadOnlyList<CombatActionData> GetAvailableActions(string combatantId)
        {
            var availableActions = new List<CombatActionData>();
            var actor = _battle?.FindCombatant(combatantId);
            if (actor == null) return availableActions;

            foreach (var kv in _data.CombatActionsById)
            {
                var action = kv.Value;
                bool usable = actor.team == CombatantTeam.Party ? action.usableByPlayers : action.usableByEnemies;

                // Equipment can unlock an action the combatant's team flags would otherwise exclude
                // (EquipmentData.actionUnlockIds, snapshotted onto the CombatProfile at battle start).
                bool unlocked = actor.profile != null && actor.profile.HasUnlockedAction(action.Id);
                if (!usable && unlocked) usable = true;
                if (!usable) continue;

                // Skills are per-combatant: only the ones this character/enemy actually knows (from
                // CharacterData.defaultSkillIds / EnemyData.skillIds) or that equipment unlocked.
                // Non-skill categories (attack, guard, item) stay universally available.
                if (action.category == CombatActionCategory.Skill
                    && actor.profile != null
                    && actor.profile.skillIds.Count > 0
                    && !actor.profile.HasSkill(action.Id)
                    && !unlocked)
                    continue;

                availableActions.Add(action);
            }
            return availableActions;
        }

        public IReadOnlyList<CombatantInstance> GetValidTargets(string combatantId, string actionId)
        {
            var actor = _battle?.FindCombatant(combatantId);
            if (actor == null || !_data.TryGet<CombatActionData>(actionId, out var action))
                return new List<CombatantInstance>();

            return _targeting.GetValidTargets(_battle, actor, action);
        }

        /// UI helper: can this combatant use the action right now ignoring target selection? Checks
        /// team availability + cost affordability only. Used to grey out command/skill/item rows
        /// (PreviewAction additionally requires a chosen target, so it's unsuitable for pre-target rows).
        public bool CanAfford(string combatantId, string actionId)
        {
            var actor = _battle?.FindCombatant(combatantId);
            if (actor == null || !_data.TryGet<CombatActionData>(actionId, out var action)) return false;

            bool usable = actor.team == CombatantTeam.Party ? action.usableByPlayers : action.usableByEnemies;
            if (!usable) return false;

            return _resolver.CanPayCosts(actor, action, out _);
        }

        public ActionPreview PreviewAction(string combatantId, string actionId, IReadOnlyList<string> targetIds)
        {
            var preview = new ActionPreview { actionId = actionId };
            if (!TryValidateSubmission(combatantId, actionId, targetIds, out var actor, out var action, out var targets, out var reason))
            {
                preview.usable = false;
                preview.blockedReason = reason;
                return preview;
            }
            preview.usable = true;
            // Phase 6 preview is structural (targets resolved); detailed damage prediction can be added later.
            for (int i = 0; i < targets.Count; i++)
                preview.predictedEffects.Add(new EffectResult { targetCombatantId = targets[i].combatantId });
            return preview;
        }


        // ---- Action submission --------------------------------------------------------------

        public ActionResult SubmitAction(string combatantId, string actionId, IReadOnlyList<string> targetIds)
        {
            if (!IsInBattle) return ActionResult.Fail(combatantId, actionId, "No active battle.");

            // Only a turn that is actually awaiting a decision may be submitted into, preventing duplicate submit
            if (_battle.phase != CombatPhase.AwaitPlayerInput && _battle.phase != CombatPhase.EnemyAI)
                return ActionResult.Fail(combatantId, actionId, $"Not accepting a submission in phase {_battle.phase}.");

            if (!TryValidateSubmission(combatantId, actionId, targetIds, out var actor, out var action, out var targets, out var reason))
                return ActionResult.Fail(combatantId, actionId, reason);

            _battle.phase = CombatPhase.ExecuteAction;

            // Status timing: before/after the action resolves.
            _resolver.Status.Process(StatusTiming.BeforeAction, actor);

            var result = _resolver.Resolve(actor, action, targets, _battle);
            _battle.phase = CombatPhase.ResolveEffects;

            for (int i = 0; i < targets.Count; i++)
                if (targets[i] != null && targets[i] != actor) _resolver.Status.OnDamaged(targets[i], result);

            _resolver.Status.Process(StatusTiming.AfterAction, actor, result);
            _resolver.Passives.Dispatch(PassiveHook.AfterAction, actor, _battle, result);

            _bus.Publish(new BattleActionResolved(_battle.battleId, actor.combatantId, action.Id, result.success));

            EnterTurnTransition(result);
            return result;
        }

        /// The point between one action resolving and the next combatant turn beginning. 
        /// holds 'between turns' responsibilities: finalize the action, check the
        /// outcome, advance or rebuild the turn order, reset round state, prepare the next combatant,
        /// and publish "TurnStarted".
        private void EnterTurnTransition(ActionResult result)
        {
            _battle.phase = CombatPhase.TurnTransition;

            // Phase 11 status timing (turn-end / round-end ticks) hooks in here too.
            _battle.triggerState.heldResult = result;
            if (OnTurnTransition(result))
            {
                // An interactive interruption is holding this transition. The win/loss check and turn
                // advance are deferred to CompleteHeldTurnTransition(), invoked when the dialogue resolves.
                return;
            }
            _battle.triggerState.heldResult = null;

            CompleteTurnTransition(result);
        }

        /// The deferred tail of a turn transition: status ticks, win/loss evaluation, then advance-or-end.
        private void CompleteTurnTransition(ActionResult result)
        {
            var actedCombatant = _battle.currentActor;
            if (actedCombatant != null)
            {
                _resolver.Status.Process(StatusTiming.TurnEnd, actedCombatant, result);
                _resolver.Passives.Dispatch(PassiveHook.TurnEnd, actedCombatant, _battle, result);
            }

            if (_battle.turnQueue.Count == 0)
            {
                _resolver.Status.ProcessAll(StatusTiming.RoundEnd, _battle, result);
                _resolver.Passives.DispatchAll(PassiveHook.RoundEnd, _battle, result);
            }

            _battle.phase = CombatPhase.CheckWinLoss;

            var outcome = _outcome.Evaluate(_battle);
            if (outcome != BattleOutcome.None)
            {
                if (result != null)
                {
                    result.battleEnded = true;
                    result.battleOutcome = outcome;
                }

                EndBattle(outcome);
                return;
            }

            AdvanceToNextActor();
        }

        /// Called by the interrupter once an interactive interruption
        /// dialogue that held a turn transition has fully resolved.
        internal void CompleteHeldTurnTransition()
        {
            if (_battle == null || _battle.isBattleOver) return;

            var result = _battle.triggerState.heldResult;

            _battle.triggerState.heldResult = null;
            
            CompleteTurnTransition(result);
        }

        /// Extension seam for Phase 10 (CombatSequenceInterrupter) and Phase 11 (status timing).
        /// Returns true if an interactive interruption is now holding the turn transition.
        private bool OnTurnTransition(ActionResult result)
            => Interrupter != null && Interrupter.EvaluateAtTurnTransition(_battle, result);

        private bool TryValidateSubmission(string combatantId, string actionId, IReadOnlyList<string> targetIds,
            out CombatantInstance actor, out CombatActionData action, out List<CombatantInstance> targets, out string reason)
        {
            actor = null; action = null; targets = null; reason = null;

            if (!IsInBattle) { reason = "No active battle."; return false; }

            actor = _battle.FindCombatant(combatantId);
            if (actor == null) { reason = $"Unknown combatant '{combatantId}'."; return false; }
            if (actor != _battle.currentActor) { reason = "Not this combatant's turn."; return false; }
            if (actor.IsDefeated) { reason = "Combatant is defeated."; return false; }

            if (!_data.TryGet(actionId, out action)) { reason = $"Unknown action '{actionId}'."; return false; }

            bool usable = actor.team == CombatantTeam.Party ? action.usableByPlayers : action.usableByEnemies;
            // Mirror GetAvailableActions: equipment-unlocked actions are submittable too.
            bool unlockedByGear = actor.profile != null && actor.profile.HasUnlockedAction(action.Id);
            if (!usable && unlockedByGear) usable = true;
            if (!usable) { reason = "Action not available to this combatant."; return false; }

            // A combatant may only use skills it actually knows (see GetAvailableActions).
            if (action.category == CombatActionCategory.Skill
                && actor.profile != null
                && actor.profile.skillIds.Count > 0
                && !actor.profile.HasSkill(action.Id)
                && !unlockedByGear)
            {
                reason = $"{actor.displayName} does not know '{action.Id}'.";
                return false;
            }

            // Statuses may forbid this action entirely.
            if (_resolver.Status.IsActionBlocked(actor, action, out reason)) return false;

            if (!_resolver.CanPayCosts(actor, action, out reason)) return false;

            // Resolve targets against the action's TargetRule.
            targets = _targeting.ResolveTargets(_battle, actor, action, targetIds, _resolver.Rng);

            if (targets.Count == 0)
            {
                reason = action.targetRule != null && action.targetRule.IsAutoResolved
                    ? "No valid targets for this action."
                    : "No valid target selected.";
                return false;
            }

            return true;
        }


        // ---- End of battle ------------------------------------------------------------------

        void ICombatService.EndBattle() => EndBattle(BattleOutcome.None);

        public void EndBattle(BattleOutcome outcome)
        {
            if (_battle == null || _battle.isBattleOver) return;

            _battle.phase = CombatPhase.EndBattle;
            _battle.isBattleOver = true;
            _battle.outcome = outcome;

            // Status timing: last chance for on-battle-end statuses, then every status is cleared.
            _resolver.Passives.DispatchAll(PassiveHook.BattleEnd, _battle);
            _resolver.Status.ProcessAll(StatusTiming.OnBattleEnd, _battle);
            _resolver.Status.ClearAll(_battle);

            CommitPartyResources();

            _bus.Publish(new BattleEnded(_battle.battleId, outcome));

            if (outcome == BattleOutcome.Victory)
            {
                LastResult = PackageResult();
                _bus.Publish(new BattleResultPackaged(_battle.battleId, outcome, LastResult));
            }
            else
            {
                // TODO: Defeat/escape post-battle flow; for now, return to exploration immediately.
                _state.SetState(new LayeredState(GameMode.Exploration, OverlayState.None, InputContext.Exploration));
            }
        }

        private void CommitPartyResources()
        {
            foreach (var combatant in _battle.partyCombatants)
            {
                if (!_battle.partySources.TryGetValue(combatant.combatantId, out var source) || source == null)
                    continue;
                source.currentHP = combatant.currentHP;
                source.currentMP = combatant.currentMP;
                source.currentSP = combatant.currentSP;
                source.Recalculate();
            }
        }

        private BattleResultData PackageResult()
        {
            var result = new BattleResultData
            {
                battleId = _battle.battleId,
                encounterId = _battle.encounterId,
                outcome = _battle.outcome,
                turnCount = _battle.roundNumber,
                elapsedSeconds = Time.realtimeSinceStartup - _battleStartTime,
            };

            foreach (var enemy in _battle.enemyCombatants)
            {
                result.defeatedEnemyIds.Add(enemy.sourceDataId);
                if (_data.TryGet<EnemyData>(enemy.sourceDataId, out var data))
                {
                    result.baseXP += data.baseXpReward;
                    result.currency += data.baseCurrencyReward;
                }
            }

            foreach (var combatant in _battle.partyCombatants)
                if (!combatant.IsDefeated) result.survivingPartyCharacterIds.Add(combatant.sourceDataId);

            return result;
        }
    }
}
