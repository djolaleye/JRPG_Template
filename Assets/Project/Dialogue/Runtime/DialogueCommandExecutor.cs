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

        public DialogueCommandExecutor(IPartyService party, IInventoryService inventory,
            IStoryStateService story, IEventBus bus)
        {
            _party = party;
            _inventory = inventory;
            _story = story;
            _bus = bus;
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

                case DialogueCommandType.RecruitCharacter:
                    Recruit(cmd.stringA);
                    break;

                case DialogueCommandType.StartBattle:
                    // Deferred: dialogue ends cleanly first, then DialogueService starts the battle.
                    if (session != null && !string.IsNullOrEmpty(cmd.stringA))
                        session.pendingExit = new DialogueExitResolution { exitType = DialogueExitType.StartBattle, targetId = cmd.stringA };
                    
                    break;

                case DialogueCommandType.ChangePartyScope:
                case DialogueCommandType.UnlockSkill:
                case DialogueCommandType.ModifyRelationshipValue:
                    Debug.LogWarning($"[JRPG.Dialogue] Command {cmd.type} is a Phase 9 stub (no-op).");
                    break;
            }

            _bus?.Publish(new DialogueCommandExecuted(graphId, cmd.type.ToString()));
        }

        /// Walk a brand-new character through the legal roster transitions to Recruited.
        private void Recruit(string characterId)
        {
            if (_party == null || string.IsNullOrEmpty(characterId)) return;
            if (_party.IsRecruited(characterId)) return;

            if (_party.GetState(characterId) == CharacterRosterState.Unmet) _party.SetState(characterId, CharacterRosterState.Met);
            if (_party.GetState(characterId) == CharacterRosterState.Met) _party.SetState(characterId, CharacterRosterState.Recruitable);
            _party.Recruit(characterId);
        }
    }
}
