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
        private readonly EffectContext _ctx = new();

        /// Effect execution and damage calculation are injectable so battles stay deterministic and
        /// individual rules can be swapped without touching this class.
        public CombatActionResolver(IInventoryService inventory, DataRegistry data,
            EffectExecutorRegistry executors = null, DamagePipeline damage = null, System.Random rng = null,
            StatusProcessor status = null, PassiveRegistry passives = null)
        {
            _inventory = inventory;
            _data = data;
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

        public bool CanPayCosts(CombatantInstance user, CombatActionData action, out string reason)
        {
            reason = null;
            if (action.costs == null) return true;

            for (int i = 0; i < action.costs.Count; i++)
            {
                var cost = action.costs[i];
                switch (cost.type)
                {
                    case CombatCostType.MP:
                        if (user.currentMP < cost.costAmount)
                        {
                            reason = $"Not enough MP ({user.currentMP}/{cost.costAmount}).";
                            return false;
                        }
                        break;
                    case CombatCostType.SP:
                        if (user.currentSP < cost.costAmount)
                        {
                            reason = $"Not enough SP ({user.currentSP}/{cost.costAmount}).";
                            return false;
                        }
                        break;
                    case CombatCostType.HP:
                        if (user.currentHP < cost.costAmount)
                        {
                            reason = $"Not enough HP ({user.currentHP}/{cost.costAmount}).";
                            return false;
                        }
                        break;
                    case CombatCostType.Item:
                        int need = Mathf.Max(1, cost.quantity);

                        // Item menu already filters on usableInCombat. Extra defense -
                        // refuse an ineligible item however the submission arrived
                        if (!ItemCombatRules.IsUsableInCombat(_data, cost.itemId))
                        {
                            reason = $"Item '{cost.itemId}' is not usable in combat.";
                            return false;
                        }

                        if (_inventory == null || !_inventory.Has(cost.itemId, need))
                        {
                            reason = $"Missing item '{cost.itemId}' x{need}.";
                            return false;
                        }
                        break;
                }
            }
            return true;
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

            PayCosts(actor, action);

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

                    _executors.Execute(_ctx);
                }
            }

            return result;
        }

        private void PayCosts(CombatantInstance user, CombatActionData action)
        {
            if (action.costs == null) return;

            for (int i = 0; i < action.costs.Count; i++)
            {
                var cost = action.costs[i];
                switch (cost.type)
                {
                    case CombatCostType.MP:
                        user.currentMP = Mathf.Max(0, user.currentMP - cost.costAmount);
                        break;
                    case CombatCostType.SP:
                        user.currentSP = Mathf.Max(0, user.currentSP - cost.costAmount);
                        break;
                    case CombatCostType.HP:
                        user.currentHP = Mathf.Max(0, user.currentHP - cost.costAmount);
                        break;
                    case CombatCostType.Item:
                        _inventory?.Remove(cost.itemId, Mathf.Max(1, cost.quantity));
                        break;
                }
            }
        }

    }
}
