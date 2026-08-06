using System;
using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Services;
using JRPG.Characters;
using JRPG.Save;

namespace JRPG.Party
{
    /// <summary>
    /// Persistent roster authority.
    /// </summary>
    public sealed class PartyService : IPartyService, IPartyRuntimeQueries, ISaveable, ISaveablePostRestore
    {
        private readonly DataRegistry _registry;
        private readonly RuntimeCharacterFactory _factory;
        private readonly IEventBus _bus;
        private readonly IRecruitmentConditionEvaluator _conditions;
        private readonly PartyRuntimeState _state = new();

        // Cache runtime instances keyed by character id. Created once and reused so HP/stat
        // state persists across roster transitions.
        private readonly Dictionary<string, CharacterRuntimeInstance> _instances = new();

        /// Resource pools read from a save, held between RestoreState and PostRestore.
        private readonly Dictionary<string, CharacterResourceEntry> _pendingResources = new();

        /// <summary>
        /// The id of the protagonist (set at construction). The protagonist is always present in the
        /// roster as Active or Reserve, can never be Unmet/Met/Recruitable/Recruited/Guest/Unavailable,
        /// and is never the cause of a zero-active party state.
        /// </summary>
        public string ProtagonistId { get; }

        public string SaveKey => "party";
        public PartyRuntimeState State => _state;

        public PartyService(DataRegistry registry,
                            IEventBus bus,
                            string protagonistId = null,
                            IRecruitmentConditionEvaluator conditions = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _factory = new RuntimeCharacterFactory(_registry);
            ProtagonistId = protagonistId;
            // No evaluator → treat every condition as unmet (so characters with conditions stay Met).
            // Characters with empty recruitmentFlagIds are unconditionally eligible regardless.
            _conditions = conditions;

            SeedInitialRoster();
        }

        /// <summary>
        /// Establishes the session-zero roster: every known character at <c>Unmet</c>, then the
        /// protagonist promoted straight to <c>Active</c> so the party always has ≥1 member.
        /// Shared by the constructor and <see cref="ResetForNewGame"/> so "initial state" has
        /// exactly one definition. Assumes the roster collections are already empty.
        /// </summary>
        private void SeedInitialRoster()
        {
            foreach (var kv in _registry.CharactersById)
                _state.stateByCharacterId[kv.Key] = CharacterRosterState.Unmet;

            if (string.IsNullOrEmpty(ProtagonistId)) return;

            if (!_state.stateByCharacterId.ContainsKey(ProtagonistId))
                throw new InvalidOperationException(
                    $"PartyService: protagonistId '{ProtagonistId}' is not a known character in the registry.");

            _state.stateByCharacterId[ProtagonistId] = CharacterRosterState.Active;
            _state.activeOrder.Add(ProtagonistId);
        }

        /// <summary>
        /// Returns the roster to the exact shape the constructor produces: roster/active/reserve
        /// wiped, every lock dropped, the scope stack emptied, the party caps back at their
        /// authored defaults, and the protagonist re-seeded to Active.
        ///
        /// The runtime-instance cache is cleared too (same reasoning as <see cref="RestoreState"/>):
        /// instances hold HP/MP/level/skills/stat-modifiers, so a surviving instance would carry the
        /// previous session's character straight into the new one. They are rebuilt lazily at level
        /// 1 on the next <see cref="ResolveInstanceById"/>.
        ///
        /// Idempotent and safe to call before anything has happened.
        /// </summary>
        public void ResetForNewGame()
        {
            _state.stateByCharacterId.Clear();
            _state.activeOrder.Clear();
            _state.reserveOrder.Clear();
            _state.lockedIds.Clear();
            _state.scopeStack.Clear();

            // Caps can be mutated by a pushed scope; restore the authored defaults by reading them
            // off a fresh state object.
            var defaults = new PartyRuntimeState();
            _state.maxActiveMembers = defaults.maxActiveMembers;
            _state.maxTotalParty = defaults.maxTotalParty;

            _instances.Clear();

            // A restore that was interrupted by New Game must not apply its pools to the fresh roster.
            _pendingResources.Clear();

            SeedInitialRoster();

            _bus.Publish(new PartyChanged());
        }

        private bool IsProtagonist(string id)
        {
            return !string.IsNullOrEmpty(ProtagonistId) && id == ProtagonistId;
        }

        // ----- membership -----

        public bool HasCharacter(string id)
        {
            return !string.IsNullOrEmpty(id) && _state.stateByCharacterId.ContainsKey(id);
        }

        public bool IsRecruited(string id)
        {
            if (!HasCharacter(id)) return false;

            var currentState = _state.stateByCharacterId[id];
            return currentState == CharacterRosterState.Recruited
                || currentState == CharacterRosterState.Active
                || currentState == CharacterRosterState.Reserve
                || currentState == CharacterRosterState.Guest;
        }

        public CharacterRosterState GetState(string id) 
        {
            return HasCharacter(id) ? _state.stateByCharacterId[id] : CharacterRosterState.Unmet;
        }

        public void SetState(string id, CharacterRosterState next)
        {
            if (!HasCharacter(id))
                throw new ArgumentException($"PartyService: unknown character id '{id}'.", nameof(id));

            // Lock guard: a locked character cannot be moved.
            if (_state.lockedIds.Contains(id))
                throw new InvalidOperationException(
                    $"PartyService: '{id}' is locked — Unlock() before changing state.");

            // Protagonist: only Active <-> Reserve transitions are legal.
            if (IsProtagonist(id) && next != CharacterRosterState.Active && next != CharacterRosterState.Reserve)
                throw new InvalidOperationException(
                    $"PartyService: protagonist '{id}' can only be Active or Reserve (requested {next}).");

            var prev = _state.stateByCharacterId[id];
            if (prev == next) return;

            if (!IsLegalTransition(prev, next))
                throw new InvalidOperationException($"PartyService: illegal transition {prev} → {next} for '{id}'.");

            // Active membership enforces the cap.
            if (next == CharacterRosterState.Active && CountActiveOrder() >= EffectiveMaxActiveMembers())
                throw new InvalidOperationException($"PartyService: cannot set '{id}' Active — active party at cap ({EffectiveMaxActiveMembers()}).");

            // ≥1 active guarantee: refuse to demote the last active member.
            if (prev == CharacterRosterState.Active && next != CharacterRosterState.Active && CountActiveOrder() <= 1)
                throw new InvalidOperationException(
                    $"PartyService: cannot demote '{id}' from Active — party would have zero active members.");

            // Maintain order lists.
            _state.activeOrder.Remove(id);
            _state.reserveOrder.Remove(id);
            if (next == CharacterRosterState.Active) _state.activeOrder.Add(id);
            else if (next == CharacterRosterState.Reserve) _state.reserveOrder.Add(id);

            _state.stateByCharacterId[id] = next;
            _bus.Publish(new CharacterRosterStateChanged(id, prev, next));
            _bus.Publish(new PartyChanged());
        }

        public void Recruit(string id)
        {
            if (!HasCharacter(id))
                throw new ArgumentException($"PartyService: unknown character id '{id}'.", nameof(id));

            // Protagonist is permanently in the pool
            if (IsProtagonist(id)) return;

            if (_state.lockedIds.Contains(id))
                throw new InvalidOperationException(
                    $"PartyService: '{id}' is locked — Unlock() before recruiting.");

            var prev = _state.stateByCharacterId[id];
            if (prev == CharacterRosterState.Recruited
             || prev == CharacterRosterState.Active
             || prev == CharacterRosterState.Reserve)
                return; // already in the pool

            if (prev != CharacterRosterState.Recruitable)
                throw new InvalidOperationException($"PartyService: cannot recruit '{id}' from state {prev}.");

            _state.stateByCharacterId[id] = CharacterRosterState.Recruited;
            _bus.Publish(new CharacterRosterStateChanged(id, prev, CharacterRosterState.Recruited));
            _bus.Publish(new CharacterRecruited(id));
            _bus.Publish(new PartyChanged());
        }

        // ----- recruitment eligibility -----

        /// <summary>
        /// True iff <paramref name="id"/> is currently <c>Met</c> AND every condition listed in
        /// <see cref="CharacterData.recruitmentFlagIds"/> reports satisfied via the injected evaluator.
        /// Characters with no listed flags are unconditionally eligible (still must be Met).
        /// If no evaluator was provided at construction, conditional characters are treated as ineligible.
        /// </summary>
        public bool EvaluateRecruitable(string id)
        {
            if (!HasCharacter(id)) return false;
            if (_state.stateByCharacterId[id] != CharacterRosterState.Met) return false;
            if (!_registry.TryGet<CharacterData>(id, out var data)) return false;

            var flags = data.recruitmentFlagIds;
            if (flags == null || flags.Count == 0) return true;

            if (_conditions == null) return false; // can't evaluate → stay Met
            for (int i = 0; i < flags.Count; i++)
            {
                if (!_conditions.IsConditionMet(flags[i])) return false;
            }
            return true;
        }

        /// <summary>
        /// If <see cref="EvaluateRecruitable"/> is true, performs the Met → Recruitable transition.
        /// Returns true on a successful promotion, false otherwise (already past Met, ineligible, locked, etc.).
        /// </summary>
        public bool TryPromoteToRecruitable(string id)
        {
            if (!EvaluateRecruitable(id)) return false;
            if (_state.lockedIds.Contains(id)) return false;

            // Use the standard SetState path so legality and bus notification stay consistent.
            SetState(id, CharacterRosterState.Recruitable);
            return true;
        }

        // ----- locking  -----

        public bool IsLocked(string id) 
        { 
            return HasCharacter(id) && _state.lockedIds.Contains(id);
        }

        public bool Lock(string id)
        {
            if (!HasCharacter(id)) return false;

            var currentState = _state.stateByCharacterId[id];

            // Only meaningful for characters that exist in some "deployed" slot — Active/Reserve/Unavailable.
            if (currentState != CharacterRosterState.Active
             && currentState != CharacterRosterState.Reserve
             && currentState != CharacterRosterState.Unavailable) return false;

            if (!_state.lockedIds.Add(id)) return false; // already locked

            _bus.Publish(new PartyChanged());
            return true;
        }

        public bool Unlock(string id)
        {
            if (!HasCharacter(id)) return false;
            if (!_state.lockedIds.Remove(id)) return false;

            _bus.Publish(new PartyChanged());
            return true;
        }

        // ----- composition -----

        public bool IsActiveCombatMember(string id) => _state.activeOrder.Contains(id);

        public IReadOnlyList<string> GetActivePartyIds() => _state.activeOrder;
        public IReadOnlyList<string> GetReservePartyIds() => _state.reserveOrder;

        /// <summary>
        /// Non-mutating form of the <see cref="TrySetActive"/> ladder, with the player-facing reason it
        /// would be refused. Extracted so the party screen can grey a row and say why rather than
        /// re-deriving cap/lock/scope rules in the view; <see cref="TrySetActive"/> runs this same check,
        /// so the two can never disagree.
        /// </summary>
        public bool CanSetActive(string id, out string reason)
        {
            reason = null;

            if (!HasCharacter(id)) { reason = "Not in the roster."; return false; }
            if (_state.lockedIds.Contains(id)) { reason = "Locked by the story."; return false; }

            var currentState = _state.stateByCharacterId[id];
            if (currentState != CharacterRosterState.Active
             && currentState != CharacterRosterState.Reserve
             && currentState != CharacterRosterState.Recruited
             && currentState != CharacterRosterState.Guest
             && currentState != CharacterRosterState.Unavailable)
            {
                reason = "Not available to deploy.";
                return false;
            }

            int cap = EffectiveMaxActiveMembers();
            if (!_state.activeOrder.Contains(id) && _state.activeOrder.Count >= cap)
            {
                reason = $"Party is full ({cap}).";
                return false;
            }

            if (_state.scopeStack.Count > 0)
            {
                var top = _state.scopeStack.Peek().scope;
                if (top.allowedCharacterIds.Count > 0 && !top.allowedCharacterIds.Contains(id))
                {
                    reason = "Can't join here.";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Non-mutating form of the <see cref="MoveToReserve"/> ladder. Same rationale as
        /// <see cref="CanSetActive"/>.
        /// </summary>
        public bool CanMoveToReserve(string id, out string reason)
        {
            reason = null;

            if (!HasCharacter(id)) { reason = "Not in the roster."; return false; }
            if (_state.lockedIds.Contains(id)) { reason = "Locked by the story."; return false; }

            if (!_state.activeOrder.Contains(id) && !_state.reserveOrder.Contains(id))
            {
                reason = "Not travelling with the party.";
                return false;
            }

            if (_state.activeOrder.Contains(id) && _state.activeOrder.Count <= 1)
            {
                reason = "Someone must stay active.";
                return false;
            }

            // A scope that requires this character forces them active; benching would be undone
            // immediately by ApplyTopScope, so refuse it here where the reason can be explained.
            if (_state.scopeStack.Count > 0)
            {
                var top = _state.scopeStack.Peek().scope;
                if (top.requiredCharacterIds.Contains(id)) { reason = "Required here."; return false; }
            }

            return true;
        }

        public bool TrySetActive(string id, int slot)
        {
            if (!CanSetActive(id, out _)) return false;

            _state.activeOrder.Remove(id);
            _state.reserveOrder.Remove(id);
            slot = Mathf.Clamp(slot, 0, _state.activeOrder.Count);
            _state.activeOrder.Insert(slot, id);
            _state.stateByCharacterId[id] = CharacterRosterState.Active;
            _bus.Publish(new PartyChanged());
            return true;
        }

        public bool MoveToReserve(string id)
        {
            if (!CanMoveToReserve(id, out _)) return false;

            _state.activeOrder.Remove(id);

            if (!_state.reserveOrder.Contains(id)) _state.reserveOrder.Add(id);

            _state.stateByCharacterId[id] = CharacterRosterState.Reserve;
            _bus.Publish(new PartyChanged());
            return true;
        }

        // ----- Dialogue-facing -----

        /// <summary>
        /// A character is "present" for dialogue if they're traveling with the party — i.e. in the
        /// Active or Reserve slot, or a Guest. Unavailable / pre-recruit states are absent.
        /// </summary>
        public bool IsCharacterPresentForDialogue(string id)
        {
            if (!HasCharacter(id)) return false;

            var currentState = _state.stateByCharacterId[id];
            return currentState == CharacterRosterState.Active
                || currentState == CharacterRosterState.Reserve
                || currentState == CharacterRosterState.Guest;
        }

        public bool CanCharacterComment(string id)
        {
            if (!IsCharacterPresentForDialogue(id)) return false;

            var inst = ResolveInstanceById(id);
            return inst != null && inst.currentHP > 0;
        }

        // ----- Runtime Queries -----

        public IReadOnlyList<CharacterRuntimeInstance> GetActiveCombatParty()
        {
            var list = new List<CharacterRuntimeInstance>(_state.activeOrder.Count);
            for (int i = 0; i < _state.activeOrder.Count; i++)
            {
                var inst = ResolveInstanceById(_state.activeOrder[i]);
                if (inst != null) list.Add(inst);
            }
            return list;
        }

        public CharacterRuntimeInstance GetPartyMemberInSlot(int index)
        {
            if (index < 0 || index >= _state.activeOrder.Count) return null;
            return ResolveInstanceById(_state.activeOrder[index]);
        }

        public IReadOnlyList<CharacterRuntimeInstance> GetActiveSpeakerCandidates() => GetActiveCombatParty();

        public CharacterRuntimeInstance ResolveInstanceById(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return null;
            if (_instances.TryGetValue(characterId, out var inst)) return inst;
            if (!_registry.TryGet<CharacterData>(characterId, out _)) return null;

            inst = _factory.Create(characterId);
            _instances[characterId] = inst;

            return inst;
        } 

        // ----- Scope stack -----

        public void PushScope(string scopeId,
                              IReadOnlyList<string> allowedIds,
                              IReadOnlyList<string> requiredIds,
                              IReadOnlyList<string> lockedIds,
                              int maxActive)
        {
            var scope = new PartyScope(scopeId, allowedIds, requiredIds, lockedIds, maxActive);
            var snap = new ScopeSnapshot
            {
                scope = scope,
                savedActiveOrder = new List<string>(_state.activeOrder),
                savedReserveOrder = new List<string>(_state.reserveOrder),
                savedStates = new Dictionary<string, CharacterRosterState>(_state.stateByCharacterId),
                savedLockedIds = new HashSet<string>(_state.lockedIds),
                savedMaxActive = _state.maxActiveMembers
            };
            _state.scopeStack.Push(snap);

            // Apply scope: bench anyone not allowed, force required to active, lock those flagged.
            ApplyTopScope();
            _bus.Publish(new PartyScopeChanged(scope.scopeId));
        }

        public void PopScope()
        {
            if (_state.scopeStack.Count == 0) return;
            var snap = _state.scopeStack.Pop();
            _state.activeOrder.Clear();   _state.activeOrder.AddRange(snap.savedActiveOrder);
            _state.reserveOrder.Clear();  _state.reserveOrder.AddRange(snap.savedReserveOrder);
            _state.stateByCharacterId.Clear();
            foreach (var kv in snap.savedStates) _state.stateByCharacterId[kv.Key] = kv.Value;
            _state.lockedIds.Clear();
            foreach (var id in snap.savedLockedIds) _state.lockedIds.Add(id);
            _state.maxActiveMembers = snap.savedMaxActive;

            var nextTop = _state.scopeStack.Count > 0 ? _state.scopeStack.Peek().scope.scopeId : null;
            _bus.Publish(new PartyScopeChanged(nextTop));
            _bus.Publish(new PartyChanged());
        }

        private void ApplyTopScope()
        {
            var scope = _state.scopeStack.Peek().scope;
            _state.maxActiveMembers = scope.maxActiveMembers;

            // Bench characters not allowed (if allowed list is non-empty).
            if (scope.allowedCharacterIds.Count > 0)
            {
                for (int i = _state.activeOrder.Count - 1; i >= 0; i--)
                {
                    var id = _state.activeOrder[i];
                    if (!scope.allowedCharacterIds.Contains(id))
                    {
                        _state.activeOrder.RemoveAt(i);
                        if (!_state.reserveOrder.Contains(id)) _state.reserveOrder.Add(id);
                        _state.stateByCharacterId[id] = CharacterRosterState.Reserve;
                    }
                }
            }

            // Force required to active (within cap).
            for (int i = 0; i < scope.requiredCharacterIds.Count; i++)
            {
                var id = scope.requiredCharacterIds[i];
                if (!HasCharacter(id)) continue;
                if (!_state.activeOrder.Contains(id))
                {
                    if (_state.activeOrder.Count >= scope.maxActiveMembers) break;
                    _state.reserveOrder.Remove(id);
                    _state.activeOrder.Add(id);
                }
                _state.stateByCharacterId[id] = CharacterRosterState.Active;
            }

            // Lock those flagged by the scope.
            for (int i = 0; i < scope.lockedCharacterIds.Count; i++)
            {
                var id = scope.lockedCharacterIds[i];
                if (!HasCharacter(id)) continue;
                _state.lockedIds.Add(id);
            }

            // Trim active list to current cap.
            while (_state.activeOrder.Count > scope.maxActiveMembers)
            {
                var demoted = _state.activeOrder[_state.activeOrder.Count - 1];
                _state.activeOrder.RemoveAt(_state.activeOrder.Count - 1);
                if (!_state.reserveOrder.Contains(demoted)) _state.reserveOrder.Add(demoted);
                _state.stateByCharacterId[demoted] = CharacterRosterState.Reserve;
            }

            // ≥1 active guarantee under a scope: if filtering emptied the active list, force the
            // protagonist back in. If there's no protagonist, fall back to the first reserve member.
            if (_state.activeOrder.Count == 0)
            {
                string fallback = !string.IsNullOrEmpty(ProtagonistId) && HasCharacter(ProtagonistId)
                    ? ProtagonistId
                    : (_state.reserveOrder.Count > 0 ? _state.reserveOrder[0] : null);

                if (fallback != null)
                {
                    _state.reserveOrder.Remove(fallback);
                    _state.activeOrder.Add(fallback);
                    _state.stateByCharacterId[fallback] = CharacterRosterState.Active;
                }
            }
        }

        // ----- ISaveable -----

        public SaveDataBase CaptureState()
        {
            var dto = new PartySaveData
            {
                version = SaveSystemCore.CurrentSaveVersion,
                maxActiveMembers = _state.maxActiveMembers,
                maxTotalParty = _state.maxTotalParty,
                activeOrder = new List<string>(_state.activeOrder),
                reserveOrder = new List<string>(_state.reserveOrder),
                lockedIds = new List<string>(_state.lockedIds)
            };

            foreach (var kv in _state.stateByCharacterId)
                dto.roster.Add(new RosterEntry { id = kv.Key, state = kv.Value });

            // Live resource pools. Only instances that actually exist are written.
            foreach (var kv in _instances)
            {
                var inst = kv.Value;
                if (inst == null) continue;

                dto.resources.Add(new CharacterResourceEntry
                {
                    id = kv.Key,
                    currentHP = inst.currentHP,
                    currentMP = inst.currentMP,
                    currentSP = inst.currentSP,
                });
            }

            // Stack is bottom-first in the DTO so order is preserved on load.
            var stackArr = _state.scopeStack.ToArray(); // ToArray returns top-first
            for (int i = stackArr.Length - 1; i >= 0; i--)
            {
                var snap = stackArr[i];
                var snapDto = new ScopeSnapshotDto
                {
                    scopeId = snap.scope.scopeId,
                    allowedCharacterIds = new List<string>(snap.scope.allowedCharacterIds),
                    requiredCharacterIds = new List<string>(snap.scope.requiredCharacterIds),
                    lockedCharacterIds = new List<string>(snap.scope.lockedCharacterIds),
                    scopeMaxActive = snap.scope.maxActiveMembers,
                    savedActiveOrder = new List<string>(snap.savedActiveOrder),
                    savedReserveOrder = new List<string>(snap.savedReserveOrder),
                    savedLockedIds = new List<string>(snap.savedLockedIds),
                    savedMaxActive = snap.savedMaxActive
                };

                foreach (var kv in snap.savedStates)
                    snapDto.savedRoster.Add(new RosterEntry { id = kv.Key, state = kv.Value });
                dto.scopeStack.Add(snapDto);
            }

            return new PartyPayload { version = dto.version, data = dto };
        }

        public void RestoreState(SaveDataBase state)
        {
            if (state is not PartyPayload p || p.data == null) return;
            var dto = p.data;

            _state.maxActiveMembers = dto.maxActiveMembers;
            _state.maxTotalParty = dto.maxTotalParty;

            _state.stateByCharacterId.Clear();
            for (int i = 0; i < dto.roster.Count; i++)
                _state.stateByCharacterId[dto.roster[i].id] = dto.roster[i].state;

            _state.activeOrder.Clear();
            _state.activeOrder.AddRange(dto.activeOrder ?? new List<string>());
            _state.reserveOrder.Clear();
            _state.reserveOrder.AddRange(dto.reserveOrder ?? new List<string>());

            _state.lockedIds.Clear();
            if (dto.lockedIds != null)
                foreach (var lid in dto.lockedIds) _state.lockedIds.Add(lid);

            _state.scopeStack.Clear();
            // DTO is bottom-first; push bottom first so top stays on top.
            for (int i = 0; i < dto.scopeStack.Count; i++)
            {
                var sd = dto.scopeStack[i];
                var scope = new PartyScope(sd.scopeId, sd.allowedCharacterIds, sd.requiredCharacterIds, sd.lockedCharacterIds, sd.scopeMaxActive);
                var snap = new ScopeSnapshot
                {
                    scope = scope,
                    savedActiveOrder = new List<string>(sd.savedActiveOrder),
                    savedReserveOrder = new List<string>(sd.savedReserveOrder),
                    savedMaxActive = sd.savedMaxActive
                };
                for (int j = 0; j < sd.savedRoster.Count; j++)
                    snap.savedStates[sd.savedRoster[j].id] = sd.savedRoster[j].state;
                if (sd.savedLockedIds != null)
                    foreach (var lid in sd.savedLockedIds) snap.savedLockedIds.Add(lid);
                _state.scopeStack.Push(snap);
            }

            // Don't re-derive instances here; they're created lazily on first query so
            // any cached state from before restore is rebuilt.
            _instances.Clear();

            // Resource pools are stashed. Instances do not exist yet, and once they do
            // ProgressionService re-stamps level/growth and finishes by filling every pool to its new
            // maximum — so anything written now is guaranteed to be overwritten. PostRestore() applies
            // these after the whole contributor graph has settled.
            _pendingResources.Clear();
            if (dto.resources != null)
            {
                for (int i = 0; i < dto.resources.Count; i++)
                {
                    var entry = dto.resources[i];
                    if (string.IsNullOrEmpty(entry.id)) continue;
                    _pendingResources[entry.id] = entry;
                }
            }

            _bus.Publish(new PartyChanged());
        }

        /// <summary>
        /// Applies the saved resource pools, clamped to each character's restored maximums.
        ///
        /// <para>Clamping matters in both directions: a save made at level 9 restored into a build whose
        /// growth table has since changed could carry an HP value above the new maximum, and a pool
        /// recorded before an equipment change could exceed what the character can now hold.</para>
        /// </summary>
        public void PostRestore()
        {
            if (_pendingResources.Count == 0) return;

            foreach (var kv in _pendingResources)
            {
                var inst = ResolveInstanceById(kv.Key);
                if (inst == null) continue;

                var saved = kv.Value;
                inst.currentHP = Mathf.Clamp(saved.currentHP, 0, inst.stats.GetFinal(StatType.MaxHP));
                inst.currentMP = Mathf.Clamp(saved.currentMP, 0, inst.stats.GetFinal(StatType.MaxMP));
                inst.currentSP = Mathf.Clamp(saved.currentSP, 0, inst.stats.GetFinal(StatType.MaxSP));
            }

            _pendingResources.Clear();
            _bus.Publish(new PartyChanged());
        }

        // ----- Helpers -----

        /// <summary>
        /// The active-party slot cap that is actually in force. When a <see cref="PartyScope"/>
        /// is pushed, the top scope's <c>maxActiveMembers</c> wins (it is the temporary, story-imposed
        /// cap); otherwise the roster's own <c>State.maxActiveMembers</c> applies. Public so party
        /// screens can render "n/cap" without re-deriving the scope rule in the view.
        /// </summary>
        public int EffectiveMaxActiveMembers()
        {
            if (_state.scopeStack.Count > 0)
                return _state.scopeStack.Peek().scope.maxActiveMembers;

            return _state.maxActiveMembers;
        }

        private int CountActiveOrder() => _state.activeOrder.Count;

        /// <summary>
        /// Legality table
        /// </summary>
        private static bool IsLegalTransition(CharacterRosterState from, CharacterRosterState to)
        {
            switch (from)
            {
                case CharacterRosterState.Unmet:        return to == CharacterRosterState.Met;

                case CharacterRosterState.Met:          return to == CharacterRosterState.Recruitable
                                                            || to == CharacterRosterState.Guest
                                                            || to == CharacterRosterState.Unavailable;

                case CharacterRosterState.Recruitable:  return to == CharacterRosterState.Recruited
                                                            || to == CharacterRosterState.Unavailable;

                case CharacterRosterState.Recruited:    return to == CharacterRosterState.Active
                                                            || to == CharacterRosterState.Reserve
                                                            || to == CharacterRosterState.Unavailable;

                case CharacterRosterState.Active:       return to == CharacterRosterState.Reserve
                                                            || to == CharacterRosterState.Unavailable;

                case CharacterRosterState.Reserve:      return to == CharacterRosterState.Active
                                                            || to == CharacterRosterState.Unavailable;

                case CharacterRosterState.Guest:        return to == CharacterRosterState.Active
                                                            || to == CharacterRosterState.Reserve
                                                            || to == CharacterRosterState.Unavailable;

                case CharacterRosterState.Unavailable:  return to == CharacterRosterState.Reserve
                                                            || to == CharacterRosterState.Active
                                                            || to == CharacterRosterState.Guest;
            }

            return false;
        }
    }
}
