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
        private readonly IEnemyActionSelector _enemyAI;
        private readonly ProfileEnemyActionSelector _profileAI;
        private readonly ICombatOutcomeEvaluator _outcome = new CombatOutcomeEvaluator();
        private readonly RuntimeCharacterFactory _characterFactory;

        private BattleContext _battle;
        private float _battleStartTime;
        
        /// How the current/last battle was launched, so a defeat can be retried verbatim.
        private BattleStartRequest? _lastRequest;

        /// Party HP/MP/SP as they were when the battle began, keyed by character id. A retry restores
        /// these rather than healing to full, so losing does not hand the player free resources.
        private readonly Dictionary<string, (int hp, int mp, int sp)> _battleStartResources = new();

        public CombatService(IEventBus bus, GameStateController state, DataRegistry data,
            IPartyRuntimeQueries party, IInventoryService inventory)
        {
            _bus = bus;
            _state = state;
            _data = data;
            _party = party;
            _inventory = inventory;
            _factory = new CombatantFactory(data);
            _resolver = new CombatActionResolver(inventory, data);
            _profileAI = new ProfileEnemyActionSelector(data, _resolver, _resolver.Rng);
            _enemyAI = _profileAI;

            _characterFactory = new RuntimeCharacterFactory(data);
        }

        /// <summary>
        /// Drops all per-session battle state so a New Game cannot inherit the previous session's
        /// battle. Without this, <see cref="_lastRequest"/> (which exists so a defeat can be retried
        /// verbatim) still points at the old session's encounter, and the AI selector resumes the
        /// previous script cursor / RNG stream. Combat is not a save contributor, so this is the only
        /// place that state gets cleared outside a normal battle end.
        /// </summary>
        public void ResetForNewGame()
        {
            _battle = null;
            _battleStartTime = 0f;
            _lastRequest = null;
            _battleStartResources.Clear();
            LastResult = null;
            _profileAI?.ResetForNewGame();
        }

        public bool IsInBattle => _battle != null && !_battle.isBattleOver;
        public BattleContext CurrentBattle => _battle;
        public CombatActionResolver Resolver => _resolver;
        public ProfileEnemyActionSelector EnemyAI => _profileAI;
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
            // Remembered so the defeat flow can offer a retry of this exact encounter.
            _lastRequest = request;
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

            // Snapshot the party's resources BEFORE combat mutates them, so a retry after defeat can
            // restore the state the battle actually started from.
            _battleStartResources.Clear();

            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                var combatant = _factory.FromCharacter(source);
                ctx.partyCombatants.Add(combatant);
                ctx.partySources[combatant.combatantId] = source;

                _battleStartResources[source.SourceDataId] =
                    (source.currentHP, source.currentMP, source.currentSP);
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

                // Cooldowns tick down on the owner's own turn, so "2 turns" means two of THEIR turns.
                actor.TickCooldowns();

                // Status + passive timing: turn start.
                _resolver.Status.Process(StatusTiming.TurnStart, actor);
                _resolver.Passives.Dispatch(PassiveHook.TurnStart, actor, _battle);

                if (actor.IsDefeated) continue;

                _battle.phase = actor.team == CombatantTeam.Party ? CombatPhase.AwaitPlayerInput : CombatPhase.EnemyAI;
                _bus.Publish(new TurnStarted(actor.combatantId));
                return;
            }
        }

        /// <summary>
        /// True when <paramref name="actor"/> is under a status that forbids every action, so this turn
        /// cannot produce a submission from either the player or the AI.
        ///
        /// <para>Queried by the combat UI pump at turn start, before any menu is opened or any enemy
        /// choice is made. Without it a stunned party member is handed a command menu with no legal
        /// row, and a stunned enemy sends the selector hunting for an action it can never find —
        /// bottoming out in the melee fallback, which submission then rejects, stalling the turn loop
        /// on that combatant.</para>
        /// </summary>
        public bool IsTurnBlocked(CombatantInstance actor, out string message)
        {
            message = null;
            if (!IsInBattle || actor == null || actor.IsDefeated) return false;

            return _resolver.Status.IsTurnBlocked(actor, out message);
        }

        /// <summary>
        /// Consumes the current actor's turn without an action, for a combatant that
        /// <see cref="IsTurnBlocked"/> reports cannot act.
        ///
        /// <para>Runs the ordinary turn transition with a null result, so a skipped turn is a real turn:
        /// TurnEnd status ticks fire (this is what counts the stun down and expires it), TurnEnd passives
        /// fire, round-end processing runs if the queue emptied, battle triggers get their evaluation
        /// point, and the win/loss check happens before the next actor is raised. Skipping straight to
        /// AdvanceToNextActor instead would leave the status's duration untouched and stun the combatant
        /// forever.</para>
        ///
        /// <para>Pacing is the UI's business, not this method's — the caller displays whatever notice it
        /// wants and calls this when it is done. Returns false if the battle moved on in the meantime.</para>
        /// </summary>
        public bool SkipBlockedTurn(string combatantId)
        {
            if (!IsInBattle) return false;
            if (_battle.phase != CombatPhase.AwaitPlayerInput && _battle.phase != CombatPhase.EnemyAI) return false;

            var actor = _battle.FindCombatant(combatantId);
            if (actor == null || actor != _battle.currentActor) return false;

            EnterTurnTransition(null);
            return true;
        }

        public void AdvanceEnemyTurn()
        {
            if (!IsInBattle) return;

            var actor = _battle.currentActor;
            if (actor == null || actor.team != CombatantTeam.Enemy) return;

            // Defence in depth: the UI pump skips blocked turns before reaching here, but a caller that
            // does not (the sandbox runner's AutoPlayToEnd, a future headless driver) must not fall into
            // the selector, which has no action to offer a combatant that cannot act.
            if (_resolver.Status.IsTurnBlocked(actor, out _))
            {
                SkipBlockedTurn(actor.combatantId);
                return;
            }

            var choice = _enemyAI.ChooseAction(actor, _battle);
            var result = SubmitAction(actor.combatantId, choice.actionId, choice.targetCombatantIds);

            if (!result.success)
            {
                // The turn loop only moves on a successful submission: a rejected enemy choice leaves
                // this combatant as currentActor with no TurnStarted published, so the UI pump never
                // re-arms and the battle hangs. Never silence this — an AI that picks an unsubmittable
                // action is a data/selector defect and must be visible.
                Debug.LogError($"[JRPG.Combat] Enemy '{actor.combatantId}' could not use '{choice.actionId}': " +
                               $"{result.failureReason} Turn loop is stalled on this combatant.");
                return;
            }

            // AI-pacing cooldown from the chosen profile entry, started only now that the action has
            // landed.
            if (choice.cooldownTurns > actor.GetCooldown(choice.actionId))
                actor.StartCooldown(choice.actionId, choice.cooldownTurns);
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

            // A successful flee ends the battle here
            if (_battle.escapeSucceeded)
            {
                if (result != null)
                {
                    result.battleEnded = true;
                    result.battleOutcome = BattleOutcome.Escaped;
                }

                EndBattle(BattleOutcome.Escaped);

                return;
            }

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
            else if (outcome == BattleOutcome.Defeat)
            {
                // Hand the screen to the defeat flow, mirroring how victory hands off to the
                // post-battle flow. The controller owns the exit (retry or return to exploration),
                // so combat does not force a state change here.
                _bus.Publish(new DefeatFlowStarted(_battle.battleId, _battle.encounterId ?? string.Empty,
                    _lastRequest.HasValue));
            }
            else
            {
                // Escaped / None: no rewards, no ceremony — straight back to exploration.
                _state.SetState(new LayeredState(GameMode.Exploration, OverlayState.None, InputContext.Exploration));
            }
        }

        /// Re-runs the encounter that just ended, from the top. Used by the defeat flow's Retry option.
        /// Party resources were committed at defeat, so callers should restore them first for a
        /// a fair rematch
        public bool RestartLastBattle(bool restoreResources = true)
        {
            if (!_lastRequest.HasValue) return false;
            if (IsInBattle) return false;

            // Rewind the party to how it ENTERED the battle — not to full. Losing must not become a
            // way to refill resources; the retry replays the same fight from the same footing.
            if (restoreResources)
            {
                var active = _party.GetActiveCombatParty();

                for (int i = 0; i < active.Count; i++)
                {
                    var c = active[i];
                    if (!_battleStartResources.TryGetValue(c.SourceDataId, out var snapshot)) continue;

                    c.currentHP = snapshot.hp;
                    c.currentMP = snapshot.mp;
                    c.currentSP = snapshot.sp;
                    c.Recalculate();
                }
            }

            var request = _lastRequest.Value;
            _battle = null;              // clear the finished battle so StartBattle accepts a new one
            StartBattle(request);
            
            return IsInBattle;
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
