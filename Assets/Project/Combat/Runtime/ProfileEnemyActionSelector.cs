using System.Collections.Generic;
using UnityEngine;
using JRPG.Data;

namespace JRPG.Combat
{
    /// Data-driven enemy AI. For each turn it:
    ///   1. plays the profile's scripted opening, if any
    ///   2. resolves the active PHASE from the owner's HP fraction
    ///   3. filters that phase's entries by condition, affordability, and cooldown
    ///   4. keeps only the highest-priority survivors, then picks among them by weight
    ///   5. falls back to the profile's fallback action (then plain melee) if nothing qualifies.
    /// 
    public sealed class ProfileEnemyActionSelector : IEnemyActionSelector
    {
        private readonly DataRegistry _data;
        private readonly CombatActionResolver _resolver;
        private readonly System.Random _sharedRng;
        private readonly IEnemyActionSelector _fallback = new SimpleEnemyActionSelector();

        /// Per-combatant scripted-sequence cursor and per-profile RNG, battle-local.
        private readonly Dictionary<string, int> _scriptCursor = new();
        private readonly Dictionary<string, System.Random> _profileRngs = new();

        /// Runtime profile overrides (dialogue's SetEnemyActionProfile), keyed by combatant id.
        private readonly Dictionary<string, string> _profileOverrides = new();

        public ProfileEnemyActionSelector(DataRegistry data, CombatActionResolver resolver, System.Random rng)
        {
            _data = data;
            _resolver = resolver;
            _sharedRng = rng ?? new System.Random(CombatActionResolver.DefaultCombatSeed);
        }

        /// Swaps an enemy's profile mid-battle. combatantId empty = apply to every enemy.
        public void SetProfileOverride(string combatantId, string profileId)
        {
            if (string.IsNullOrEmpty(profileId)) return;

            _profileOverrides[combatantId ?? string.Empty] = profileId;
        }

        public void ClearOverrides() => _profileOverrides.Clear();

        public EnemyActionChoice ChooseAction(CombatantInstance enemy, BattleContext context)
        {
            var profile = ResolveProfile(enemy);
            if (profile == null) return _fallback.ChooseAction(enemy, context);

            // 1. Scripted opening.
            string scripted = NextScripted(enemy, profile);
            if (!string.IsNullOrEmpty(scripted) && CanUse(enemy, scripted))
                return Build(enemy, context, scripted, AiTargetRule.FirstLiving);

            // 2/3. Active phase → eligible entries.
            float hpFraction = enemy.MaxHP > 0 ? (float)enemy.currentHP / enemy.MaxHP : 0f;
            var phase = profile.ResolvePhase(hpFraction);

            if (phase != null)
            {
                var eligible = new List<AiActionEntry>();
                for (int i = 0; i < phase.actions.Count; i++)
                {
                    var entry = phase.actions[i];
                    if (entry == null || string.IsNullOrEmpty(entry.actionId)) continue;
                    if (entry.weight <= 0) continue;
                    if (!CanUse(enemy, entry.actionId)) continue;
                    if (!ConditionsPass(entry, enemy, context)) continue;
                    
                    eligible.Add(entry);
                }

                // 4. Priority gate, then weighted pick.
                var chosen = PickWeighted(eligible, ProfileRng(profile));
                if (chosen != null)
                {
                    // The entry's own cooldown rides the shared cooldown system.
                    if (chosen.cooldownTurns > 0) enemy.StartCooldown(chosen.actionId, chosen.cooldownTurns);
                    return Build(enemy, context, chosen.actionId, chosen.targetRule, chosen);
                }
            }

            // 5. Fallback.
            if (!string.IsNullOrEmpty(profile.fallbackActionId) && CanUse(enemy, profile.fallbackActionId))
                return Build(enemy, context, profile.fallbackActionId, AiTargetRule.FirstLiving);

            return _fallback.ChooseAction(enemy, context);
        }

        // ---- Selection internals ------------------------------------------------------------

        private static AiActionEntry PickWeighted(List<AiActionEntry> eligible, System.Random rng)
        {
            if (eligible.Count == 0) return null;

            int topPriority = int.MinValue;
            for (int i = 0; i < eligible.Count; i++)
                if (eligible[i].priority > topPriority) topPriority = eligible[i].priority;

            int total = 0;
            for (int i = 0; i < eligible.Count; i++)
                if (eligible[i].priority == topPriority) total += eligible[i].weight;

            if (total <= 0) return null;

            int roll = rng.Next(total);
            for (int i = 0; i < eligible.Count; i++)
            {
                if (eligible[i].priority != topPriority) continue;

                roll -= eligible[i].weight;
                if (roll < 0) return eligible[i];
            }

            return null;
        }

        private bool ConditionsPass(AiActionEntry entry, CombatantInstance self, BattleContext ctx)
        {
            if (entry.conditions == null) return true;

            for (int i = 0; i < entry.conditions.Count; i++)
                if (!Evaluate(entry.conditions[i], self, ctx)) return false;

            return true;
        }

        private bool Evaluate(AiCondition c, CombatantInstance self, BattleContext ctx)
        {
            float selfFraction = self.MaxHP > 0 ? (float)self.currentHP / self.MaxHP : 0f;
            var opponents = self.team == CombatantTeam.Enemy ? ctx.partyCombatants : ctx.enemyCombatants;
            var allies = self.team == CombatantTeam.Enemy ? ctx.enemyCombatants : ctx.partyCombatants;

            switch (c.type)
            {
                case AiConditionType.Always: return true;
                case AiConditionType.SelfHpBelowPercent: return selfFraction <= c.value;
                case AiConditionType.SelfHpAbovePercent: return selfFraction > c.value;
                case AiConditionType.RoundAtLeast: return ctx.roundNumber >= c.intValue;

                case AiConditionType.AnyEnemyHpBelowPercent:
                    for (int i = 0; i < opponents.Count; i++)
                    {
                        var o = opponents[i];
                        if (o.IsDefeated || o.MaxHP <= 0) continue;
                        if ((float)o.currentHP / o.MaxHP <= c.value) return true;
                    }

                    return false;

                case AiConditionType.AllyCountAtLeast:
                {
                    int living = 0;
                    for (int i = 0; i < allies.Count; i++) if (!allies[i].IsDefeated) living++;

                    return living >= c.intValue;
                }

                case AiConditionType.SelfHasStatus: return HasStatus(self, c.stringValue);

                case AiConditionType.TargetHasStatus:
                    for (int i = 0; i < opponents.Count; i++)
                        if (!opponents[i].IsDefeated && HasStatus(opponents[i], c.stringValue)) return true;

                    return false;

                case AiConditionType.TargetMissingStatus:
                    for (int i = 0; i < opponents.Count; i++)
                        if (!opponents[i].IsDefeated && !HasStatus(opponents[i], c.stringValue)) return true;

                    return false;

                default: 
                    return true;
            }
        }

        private static bool HasStatus(CombatantInstance c, string statusId)
        {
            if (string.IsNullOrEmpty(statusId)) return false;
            for (int i = 0; i < c.activeStatuses.Count; i++)
                if (c.activeStatuses[i].statusId == statusId) return true;

            return false;
        }

        /// Affordable, off cooldown, and known to this combatant.
        private bool CanUse(CombatantInstance enemy, string actionId)
        {
            if (!_data.TryGet<CombatActionData>(actionId, out var action) || action == null) return false;
            if (action.category == CombatActionCategory.Skill
                && enemy.profile != null
                && enemy.profile.skillIds.Count > 0
                && !enemy.profile.HasSkill(actionId))
                return false;

            return _resolver.CanPayCosts(enemy, action, out _);
        }

        // ---- Target selection ---------------------------------------------------------------

        private EnemyActionChoice Build(CombatantInstance enemy, BattleContext ctx, string actionId,
            AiTargetRule rule, AiActionEntry entry = null)
        {
            var targets = new List<string>();
            _data.TryGet<CombatActionData>(actionId, out var action);

            // Auto-resolved actions (All / Random / Self) let the targeting system decide.
            bool autoResolved = action?.targetRule != null && action.targetRule.IsAutoResolved;
            if (!autoResolved)
            {
                var pick = PickTarget(enemy, ctx, action, rule, entry);
                if (pick != null) targets.Add(pick.combatantId);
            }

            return new EnemyActionChoice { actionId = actionId, targetCombatantIds = targets };
        }

        /// Chooses one combatant for a single-target action.
        ///
        /// Every rule degrades gracefully
        private CombatantInstance PickTarget(CombatantInstance enemy, BattleContext ctx,
            CombatActionData action, AiTargetRule rule, AiActionEntry entry = null)
        {
            // Support actions aim at the AI's own side.
            bool targetsAllies = action?.targetRule != null
                                 && (action.targetRule.team == TargetTeam.Allies || action.targetRule.team == TargetTeam.Self);

            var pool = new List<CombatantInstance>();
            var source = targetsAllies
                ? (enemy.team == CombatantTeam.Enemy ? ctx.enemyCombatants : ctx.partyCombatants)
                : (enemy.team == CombatantTeam.Enemy ? ctx.partyCombatants : ctx.enemyCombatants);

            for (int i = 0; i < source.Count; i++)
                if (!source[i].IsDefeated) pool.Add(source[i]);

            if (pool.Count == 0) return null;

            var randomOpp = pool[_sharedRng.Next(pool.Count)];

            switch (rule)
            {
                case AiTargetRule.Self: return enemy;

                case AiTargetRule.LowestHp:
                case AiTargetRule.LowestHpAlly:
                {
                    var best = pool[0];
                    for (int i = 1; i < pool.Count; i++) if (pool[i].currentHP < best.currentHP) best = pool[i];
                    return best;
                }

                case AiTargetRule.HighestHp:
                {
                    var best = pool[0];
                    for (int i = 1; i < pool.Count; i++) if (pool[i].currentHP > best.currentHP) best = pool[i];
                    return best;
                }

                case AiTargetRule.Random:
                    return randomOpp;

                case AiTargetRule.LastAttacker:
                {
                    if (!string.IsNullOrEmpty(enemy.lastAttackerCombatantId))
                        for (int i = 0; i < pool.Count; i++)
                            if (pool[i].combatantId == enemy.lastAttackerCombatantId) return pool[i];
                    
                    return randomOpp;
                }

                case AiTargetRule.StrongestAlly:
                {
                    var allies = targetsAllies ? pool : LivingAllies(enemy, ctx);
                    if (allies.Count == 0) return randomOpp;

                    var best = allies[0];
                    for (int i = 1; i < allies.Count; i++)
                    {
                        if (allies[i].level > best.level) best = allies[i];
                        else if (allies[i].level == best.level && allies[i].currentHP > best.currentHP) best = allies[i];
                    }

                    return best;
                }

                case AiTargetRule.SpecificEnemy:
                {
                    string wanted = entry?.specificTargetId;
                    if (!string.IsNullOrEmpty(wanted))
                        for (int i = 0; i < pool.Count; i++)
                        {
                            var c = pool[i];
                            if (c.combatantId == wanted || c.encounterSlotId == wanted || c.sourceDataId == wanted)
                                return c;
                        }
                    return pool[0];
                }

                case AiTargetRule.AvoidResistance:
                {
                    var element = ResolveActionElement(action);
                    var viable = new List<CombatantInstance>();

                    for (int i = 0; i < pool.Count; i++)
                    {
                        var affinity = pool[i].profile?.GetAffinity(element) ?? ElementAffinity.Normal;
                        if (affinity != ElementAffinity.Resist
                            && affinity != ElementAffinity.Immune
                            && affinity != ElementAffinity.Absorb)
                            viable.Add(pool[i]);
                    }

                    return viable.Count > 0 ? viable[0] : randomOpp;
                }

                case AiTargetRule.TargetWeakness:
                {
                    var element = ResolveActionElement(action);
                    for (int i = 0; i < pool.Count; i++)
                        if ((pool[i].profile?.GetAffinity(element) ?? ElementAffinity.Normal) == ElementAffinity.Weak)
                            return pool[i];

                    // Nobody is weak — at least avoid hitting something that resists it.
                    return PickTarget(enemy, ctx, action, AiTargetRule.AvoidResistance, entry);
                }

                default:
                    return pool[0];
            }
        }

        private static List<CombatantInstance> LivingAllies(CombatantInstance self, BattleContext ctx)
        {
            var source = self.team == CombatantTeam.Enemy ? ctx.enemyCombatants : ctx.partyCombatants;
            var allies = new List<CombatantInstance>();
            for (int i = 0; i < source.Count; i++)
                if (!source[i].IsDefeated) allies.Add(source[i]);
            return allies;
        }

        /// The element an action deals — taken from its first damaging effect
        private static Element ResolveActionElement(CombatActionData action)
        {
            if (action?.effects == null) return Element.Neutral;

            for (int i = 0; i < action.effects.Count; i++)
                if (action.effects[i].type == CombatEffectType.Damage) return action.effects[i].element;
            
            return Element.Neutral;
        }

        // ---- Profile plumbing ---------------------------------------------------------------

        private EnemyActionProfileData ResolveProfile(CombatantInstance enemy)
        {
            // Overrides: this combatant first, then a battle-wide override.
            if (_profileOverrides.TryGetValue(enemy.combatantId, out var overrideId)
                || _profileOverrides.TryGetValue(string.Empty, out overrideId))
            {
                if (_data.TryGet<EnemyActionProfileData>(overrideId, out var overridden)) return overridden;
            }

            if (!_data.TryGet<EnemyData>(enemy.sourceDataId, out var enemyData) || enemyData == null) return null;
            if (string.IsNullOrEmpty(enemyData.actionProfileId)) return null;

            _data.TryGet<EnemyActionProfileData>(enemyData.actionProfileId, out var profile);
            return profile;
        }

        private string NextScripted(CombatantInstance enemy, EnemyActionProfileData profile)
        {
            if (profile.scriptedSequence == null || profile.scriptedSequence.Count == 0) return null;

            _scriptCursor.TryGetValue(enemy.combatantId, out int cursor);
            if (cursor >= profile.scriptedSequence.Count)
            {
                if (!profile.loopScriptedSequence) return null;
                cursor = 0;
            }

            _scriptCursor[enemy.combatantId] = cursor + 1;
            return profile.scriptedSequence[cursor];
        }

        private System.Random ProfileRng(EnemyActionProfileData profile)
        {
            if (profile.randomSeed == 0) return _sharedRng;
            if (!_profileRngs.TryGetValue(profile.Id, out var rng))
            {
                rng = new System.Random(profile.randomSeed);
                _profileRngs[profile.Id] = rng;
            }
            return rng;
        }
    }
}
