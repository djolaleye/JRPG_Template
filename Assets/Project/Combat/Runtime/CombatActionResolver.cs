using System.Collections.Generic;
using UnityEngine;
using JRPG.Data;
using JRPG.Services;

namespace JRPG.Combat
{
    /// <summary>
    /// The only place that executes actions. Validates, pays costs, and applies effects against
    /// runtime combatants. 
    /// </summary>
    public sealed class CombatActionResolver
    {
        private readonly IInventoryService _inventory;
        private readonly DataRegistry _data;
        private readonly EffectExecutorRegistry _executors;
        private readonly StatusProcessor _status;
        private readonly PassiveRegistry _passives;
        private readonly DamagePipeline _damage;
        private readonly System.Random _rng;
        private readonly CostRegistry _costs;
        private readonly EffectContext _ctx = new();
        private readonly CostContext _costCtx = new();

        /// Effect execution and damage calculation are injectable so battles stay deterministic and
        /// individual rules can be swapped without touching this class.
        public CombatActionResolver(IInventoryService inventory, DataRegistry data,
            EffectExecutorRegistry executors = null, DamagePipeline damage = null, System.Random rng = null,
            StatusProcessor status = null, PassiveRegistry passives = null, CostRegistry costs = null)
        {
            _inventory = inventory;
            _data = data;
            _costs = costs ?? CostRegistry.CreateStandard();
            _executors = executors ?? EffectExecutorRegistry.CreateStandard();
            _status = status ?? new StatusProcessor(data);
            _passives = passives ?? PassiveRegistry.CreateStandard();
            _damage = damage ?? DamagePipelineFactory.CreateStandard(
                data?.ElementMatrix, _status, null, _passives);
            _rng = rng ?? new System.Random(DefaultCombatSeed);
        }


        /// Fixed default seed for reproducible combat - unless a
        /// caller supplies its own Random.
        public const int DefaultCombatSeed = 20;

        public EffectExecutorRegistry Executors => _executors;
        public DamagePipeline Damage => _damage;
        public StatusProcessor Status => _status;
        public PassiveRegistry Passives => _passives;
        public System.Random Rng => _rng;
        public CostRegistry Costs => _costs;

        /// True when every cost is payable AND the action is off cooldown.
        public bool CanPayCosts(CombatantInstance user, CombatActionData action, out string reason)
        {
            reason = null;

            if (user.IsOnCooldown(action.Id))
            {
                reason = $"'{action.displayName ?? action.Id}' is on cooldown ({user.GetCooldown(action.Id)} turn(s) left).";
                return false;
            }

            return _costs.CanPayAll(MakeCostContext(user, action), action, out reason);
        }

        private CostContext MakeCostContext(CombatantInstance user, CombatActionData action)
        {
            _costCtx.user = user;
            _costCtx.action = action;
            _costCtx.data = _data;
            _costCtx.inventory = _inventory;
            return _costCtx;
        }

        /// Validate → pay costs → dispatch effects to their executors. Targets must already be
        /// resolved/validated by the caller (CombatService) against the action's TargetRule.
        public ActionResult Resolve(CombatantInstance actor, CombatActionData action,
            List<CombatantInstance> targets, BattleContext battle = null)
        {
            var result = new ActionResult
            {
                success = true,
                actorCombatantId = actor.combatantId,
                actionId = action.Id,
            };

            if (!CanPayCosts(actor, action, out var reason))
                return ActionResult.Fail(actor.combatantId, action.Id, reason);

            var costCtx = MakeCostContext(actor, action);
            var paid = _costs.PayAll(costCtx, action);

            int effectsBefore = result.effects.Count;

            if (action.effects != null)
            {
                for (int i = 0; i < action.effects.Count; i++)
                {
                    var effect = action.effects[i];

                    _ctx.actor = actor;
                    _ctx.effect = effect;
                    _ctx.action = action;
                    _ctx.targets = targets;
                    _ctx.result = result;
                    _ctx.battle = battle;
                    _ctx.data = _data;
                    _ctx.inventory = _inventory;
                    _ctx.damage = _damage;
                    _ctx.rng = _rng;
                    _ctx.status = _status;

                    // Chance-gated effects (0 means "always").
                    if (!_ctx.Roll(effect.executionChance)) continue;

                    try
                    {
                        _executors.Execute(_ctx);
                    }
                    catch (System.Exception e)
                    {
                        // Failed executor won't silently spend the player's MP/items.
                        Debug.LogError($"[JRPG.Combat] Effect '{effect.type}' of '{action.Id}' threw — " +
                                       $"rolling back costs. {e}");
                        _costs.Rollback(costCtx, paid);
                        return ActionResult.Fail(actor.combatantId, action.Id, "Action failed during execution.");
                    }
                }
            }

            // Nothing landed at all == refund
            bool authoredEffects = action.effects != null && action.effects.Count > 0;
            if (authoredEffects && result.effects.Count == effectsBefore)
            {
                _costs.Rollback(costCtx, paid);
                return ActionResult.Fail(actor.combatantId, action.Id, "Action had no effect.");
            }

            // Action landed, so start its cooldown.
            actor.StartCooldown(action.Id, action.cooldownTurns);

            return result;
        }


    }
}
