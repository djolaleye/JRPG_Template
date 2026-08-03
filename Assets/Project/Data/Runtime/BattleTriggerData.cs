using UnityEngine;
using JRPG.Core;

namespace JRPG.Data
{
    /// Authored, reusable definition of a mid-battle narrative interruption. Evaluated by the combat
    /// interrupter (JRPG.Combat) at the timing below; when it matches, it starts a dialogue graph.
    /// Runtime firing state (which one-shots already fired, pending queue, resume context) lives in
    /// BattleTriggerRuntimeState — never written back to this asset.
    [CreateAssetMenu(menuName = "JRPG/Combat/Battle Trigger", fileName = "BattleTrigger")]
    public class BattleTriggerData : GameDataBase
    {
        [Header("Timing & ordering")]
        public BattleTriggerTiming timing = BattleTriggerTiming.TurnStart;

        [Tooltip("Higher priority triggers are evaluated/queued first when several match the same point.")]
        public int priority;

        [Tooltip("If true, fires at most once per battle. If false, may fire every time it matches.")]
        public bool oneShot = true;

        [Header("Conditions")]
        [Tooltip("Hp Threshold Crossed: fires when a matching combatant crosses at/below this fraction of MaxHP (0..1).")]
        [Range(0f, 1f)] public float hpThresholdPercent = 0.5f;

        [Tooltip("Round Reached: the round number at which this fires.")]
        public int roundNumber = 1;

        [Tooltip("Specific Action Used: the CombatActionData id that must have been used.")]
        public string actionId;

        [Header("Combatant filter")]
        public CombatantTeamFilter teamFilter = CombatantTeamFilter.Any;

        [Tooltip("Optional: restrict to a combatant whose source data id (CharacterData/EnemyData id) matches. Empty = any.")]
        public string sourceDataIdFilter;

        [Tooltip("Optional: restrict to ONE encounter slot (EncounterEnemyEntry.slotId), so a trigger " +
                 "can single out one of several copies of the same enemy. Empty = any.")]
        public string slotIdFilter;

        [Header("Dialogue")]
        public string dialogueGraphId;
        public DialogueImportance importance = DialogueImportance.Interactive;
        
        [Tooltip("If true, combat pauses and command/target menus are suspended until the dialogue completes. " +
                 "Passive presentation ignores this (it never blocks).")]
        public bool blocksCombatInput = true;

        [Header("Optional follow-up (applied when the interruption resolves)")]
        public string followUpStoryFlag;
        public string followUpCombatActionId;
        public string followUpEncounterId;
    }
}
