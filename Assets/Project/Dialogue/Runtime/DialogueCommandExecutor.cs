using System;
using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Party;
using JRPG.Services;

namespace JRPG.Dialogue
{
    /// Runs embedded DialogueCommand data via the live services. Never touches ScriptableObjects.
    public sealed class DialogueCommandExecutor
    {
        private readonly IPartyService _party;
        private readonly IInventoryService _inventory;
        private readonly IStoryStateService _story;
        private readonly IEventBus _bus;
        private readonly IServiceRegistry _services;

        public DialogueCommandExecutor(IPartyService party, IInventoryService inventory,
            IStoryStateService story, IEventBus bus, IServiceRegistry services)
        {
            _party = party;
            _inventory = inventory;
            _story = story;
            _bus = bus;
            _services = services;
        }

        public void ExecuteAll(List<DialogueCommand> commands, DialogueSessionRuntime session, string graphId)
        {
            if (commands == null) return;

            for (int i = 0; i < commands.Count; i++)
                Execute(commands[i], session, graphId);
        }

        public void Execute(DialogueCommand cmd, DialogueSessionRuntime session, string graphId)
        {
            switch (cmd.type)
            {
                case DialogueCommandType.SetStoryFlag:
                    _story?.SetBool(cmd.stringA, cmd.boolA);
                    break;

                case DialogueCommandType.GiveItem:
                    _inventory?.Add(cmd.stringA, Mathf.Max(1, cmd.intA));
                    break;

                case DialogueCommandType.RemoveItem:
                    _inventory?.Remove(cmd.stringA, Mathf.Max(1, cmd.intA));
                    break;

                case DialogueCommandType.MeetCharacter:
                    MeetCharacter(cmd.stringA);
                    break;

                case DialogueCommandType.EvaluateRecruitment:
                    EvaluateRecruitment(cmd.stringA);
                    break;

                case DialogueCommandType.SetCharacterGuest:
                    SetGuest(cmd.stringA);
                    break;

                case DialogueCommandType.SetCharacterUnavailable:
                    SetUnavailable(cmd.stringA);
                    break;

                case DialogueCommandType.RecruitCharacter:
                    Recruit(cmd.stringA);
                    break;

                case DialogueCommandType.SetCharacterActive:
                    SetActive(cmd.stringA);
                    break;

                case DialogueCommandType.StartBattle:
                    // Deferred: dialogue ends cleanly first, then DialogueService starts the battle.
                    if (session != null && !string.IsNullOrEmpty(cmd.stringA))
                        session.pendingExit = new DialogueExitResolution { exitType = DialogueExitType.StartBattle, targetId = cmd.stringA };

                    break;

                case DialogueCommandType.QueueCombatAction:
                    if (TryResolveInterruption(out var qi))
                        qi.QueueCombatAction(cmd.stringA, cmd.stringB, null);
                    break;

                case DialogueCommandType.SetEnemyActionProfile:
                    if (TryResolveInterruption(out var pi))
                        pi.SetEnemyActionProfile(cmd.stringA);
                    break;

                case DialogueCommandType.SetBattleTrigger:
                    if (TryResolveInterruption(out var bi))
                        bi.SetBattleTrigger(cmd.stringA, cmd.boolA);
                    break;

                case DialogueCommandType.ChangePartyScope:
                case DialogueCommandType.UnlockSkill:
                case DialogueCommandType.ModifyRelationshipValue:
                case DialogueCommandType.StartCutscene:
                    Debug.LogWarning($"[JRPG.Dialogue] Command {cmd.type} is a Phase 9 stub (no-op).");
                    break;
            }

            _bus?.Publish(new DialogueCommandExecuted(graphId, cmd.type.ToString()));
        }

        /// Promotes an already-recruited character into the active battle party.
        private void SetActive(string id)
        {
            if (!Guard(id, out var state)) return;

            if (state == CharacterRosterState.Active) return;
            if (state == CharacterRosterState.Unmet || state == CharacterRosterState.Met
                || state == CharacterRosterState.Recruitable)
            {
                Debug.LogWarning($"[JRPG.Dialogue] SetCharacterActive '{id}' ignored — recruit them first (is {state}).");
                return;
            }

            // new recruits join at the BACK of the order
            if (!_party.TrySetActive(id, int.MaxValue))
                Debug.LogWarning($"[JRPG.Dialogue] SetCharacterActive '{id}' failed — active party may be full or the character locked.");
        }

        private bool TryResolveInterruption(out ICombatInterruptionService interruption)
        {
            interruption = null;
            return _services != null && _services.TryResolve(out interruption);
        }

        private void MeetCharacter(string id)
        {
            if (!Guard(id, out var state)) return;
            if (state != CharacterRosterState.Unmet) return; // already met
            
            TrySetState(id, CharacterRosterState.Met);
        }

        /// Promotes Met → Recruitable, but only when recruitmentFlagIds are satisfied via the
        /// recruitment-condition evaluator.
        private void EvaluateRecruitment(string id)
        {
            if (!Guard(id, out _)) return;

            _party.TryPromoteToRecruitable(id);
        }

        private void SetGuest(string id)
        {
            if (!Guard(id, out var state)) return;
            if (state == CharacterRosterState.Guest) return;
           
            if (state != CharacterRosterState.Met && state != CharacterRosterState.Unavailable)
            {
                Debug.LogWarning($"[JRPG.Dialogue] SetCharacterGuest '{id}' ignored — Guest is only reachable from Met or Unavailable (is {state}).");
                return;
            }

            TrySetState(id, CharacterRosterState.Guest);
        }

        private void SetUnavailable(string id)
        {
            if (!Guard(id, out var state)) return;
            if (state == CharacterRosterState.Unavailable) return;

            if (state == CharacterRosterState.Unmet)
            {
                Debug.LogWarning($"[JRPG.Dialogue] SetCharacterUnavailable '{id}' ignored — meet the character first.");
                return;
            }

            TrySetState(id, CharacterRosterState.Unavailable);
        }

        private void Recruit(string id)
        {
            if (!Guard(id, out var state)) return;
            if (_party.IsRecruited(id)) return;

            if (state != CharacterRosterState.Recruitable)
            {
                Debug.LogWarning($"[JRPG.Dialogue] RecruitCharacter '{id}' ignored — character must be Recruitable (is {state}). Run EvaluateRecruitment after its recruitment flags are set.");
                return;
            }

            TryRun(() => _party.Recruit(id), id, "recruit");
        }


        private bool Guard(string id, out CharacterRosterState state)
        {
            state = CharacterRosterState.Unmet;

            if (_party == null || string.IsNullOrEmpty(id)) return false;

            if (!_party.HasCharacter(id))
            {
                Debug.LogWarning($"[JRPG.Dialogue] Roster command targets unknown character '{id}'.");
                return false;
            }
            
            if (_party.IsLocked(id))
            {
                Debug.LogWarning($"[JRPG.Dialogue] Roster command on '{id}' ignored — character is locked.");
                return false;
            }

            state = _party.GetState(id);

            return true;
        }

        private void TrySetState(string id, CharacterRosterState next)
            => TryRun(() => _party.SetState(id, next), id, $"set {next}");

        /// Final safety net for the numeric edge throws (active cap, last-active demotion)
        private void TryRun(Action action, string id, string what)
        {
            try { action(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[JRPG.Dialogue] Could not {what} '{id}': {e.Message}");
            }
        }
    }
}
