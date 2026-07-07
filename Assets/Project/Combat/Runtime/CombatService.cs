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

        private readonly CombatantFactory _factory = new();
        private readonly TurnOrderService _turnOrder = new();
        private readonly TargetingSystem _targeting = new();
        private readonly CombatActionResolver _resolver;
        private readonly IEnemyActionSelector _enemyAI = new SimpleEnemyActionSelector();
        private readonly ICombatOutcomeEvaluator _outcome = new CombatOutcomeEvaluator();
        private readonly RuntimeCharacterFactory _characterFactory;

        private BattleContext _battle;
        private float _battleStartTime;
        private int _actionsResolved;

        public CombatService(IEventBus bus, GameStateController state, DataRegistry data,
            IPartyRuntimeQueries party, IInventoryService inventory)
        {
            _bus = bus;
            _state = state;
            _data = data;
            _party = party;
            _inventory = inventory;
            _resolver = new CombatActionResolver(inventory);
            _characterFactory = new RuntimeCharacterFactory(data);
        }

        public bool IsInBattle => _battle != null && !_battle.isBattleOver;
        public BattleContext CurrentBattle => _battle;
        public BattleResultData LastResult { get; private set; }

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
            _actionsResolved = 0;

            ctx.phase = CombatPhase.CalculateTurnOrder;
            _turnOrder.BuildQueue(ctx);

            // Phase 6: no runtime command UI, so input is Disabled. The debug harness submits directly.
            _state.SetState(new LayeredState(GameMode.Combat, OverlayState.None, InputContext.Disabled));
            _bus.Publish(new BattleStarted(ctx.battleId, ctx.encounterId ?? string.Empty));

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
            IReadOnlyList<string> enemyIds = request.enemyIds != null && request.enemyIds.Count > 0
                ? request.enemyIds
                : encounter?.enemyIds;

            if (enemyIds == null) return;

            for (int i = 0; i < enemyIds.Count; i++)
            {
                if (!_data.TryGet<EnemyData>(enemyIds[i], out var enemy))
                {
                    Debug.LogError($"[JRPG.Combat] Unknown enemy id '{enemyIds[i]}' — skipped.");
                    continue;
                }
                ctx.enemyCombatants.Add(_factory.FromEnemy(enemy, i));
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

                    foreach (var c in _battle.AllCombatants()) c.hasActedThisRound = false;
                    
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
                if (usable) availableActions.Add(action);
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
            if (!TryValidateSubmission(combatantId, actionId, targetIds, out var actor, out var action, out var targets, out var reason))
                return ActionResult.Fail(combatantId, actionId, reason);

            _battle.phase = CombatPhase.ExecuteAction;
            _battle.pendingAction = action;
            _battle.pendingTargets = targets;

            var result = _resolver.Resolve(actor, action, targets);
            _battle.phase = CombatPhase.ResolveEffects;

            _bus.Publish(new BattleActionResolved(_battle.battleId, actor.combatantId, action.Id, result.success));
            _actionsResolved++;

            _battle.phase = CombatPhase.CheckWinLoss;
            var outcome = _outcome.Evaluate(_battle);
            if (outcome != BattleOutcome.None)
            {
                result.battleEnded = true;
                result.battleOutcome = outcome;
                EndBattle(outcome);
            }
            else
            {
                AdvanceToNextActor();
            }

            return result;
        }

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
            if (!usable) { reason = "Action not available to this combatant."; return false; }

            if (!_resolver.CanPayCosts(actor, action, out reason)) return false;

            // Resolve and validate targets against the action's TargetRule.
            var valid = _targeting.GetValidTargets(_battle, actor, action);
            targets = new List<CombatantInstance>();

            if (action.targetRule != null && action.targetRule.selectionMode == TargetSelectionMode.Self)
            {
                targets.Add(actor);
            }
            else
            {
                if (targetIds != null)
                {
                    for (int i = 0; i < targetIds.Count; i++)
                    {
                        var t = _battle.FindCombatant(targetIds[i]);
                        if (t != null && valid.Contains(t)) targets.Add(t);
                    }
                }
                if (targets.Count == 0) { reason = "No valid target selected."; return false; }
                if (targets.Count > 1) targets.RemoveRange(1, targets.Count - 1); // Single-target Phase 6.
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

            CommitPartyResources();

            _bus.Publish(new BattleEnded(_battle.battleId, outcome));

            if (outcome == BattleOutcome.Victory)
            {
                LastResult = PackageResult();
                _bus.Publish(new BattleResultPackaged(_battle.battleId, outcome));
            }

            // Phase 6 convenience: return to exploration. Phase 8 will intercept the victory chain.
            _state.SetState(new LayeredState(GameMode.Exploration, OverlayState.None, InputContext.Exploration));
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
