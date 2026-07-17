using System.Text;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Party;
using JRPG.Services;

namespace JRPG.Progression
{
    /// Debug harness for the progression loop. Drives the service directly through
    /// context-menu actions so XP/level/allocation/save behavior can be validated without combat.
    public sealed class ProgressionSandboxRunner : MonoBehaviour
    {
        [SerializeField] private string fakeEncounterId = "encounter_test_slimes";
        [SerializeField] private int fakeBaseXp = 100;
        [SerializeField] private string allocateCharacterId = "char_guardian";
        [SerializeField] private StatType allocateStat = StatType.Strength;
        [SerializeField] private int saveSlot = 7;

        private ProgressionService Progression
            => AppContext.Services != null && AppContext.Services.TryResolve<IProgressionService>(out var svc)
                ? svc as ProgressionService : null;

        private IPartyService Party
            => AppContext.Services != null && AppContext.Services.TryResolve<IPartyService>(out var svc) ? svc : null;

        [ContextMenu("Recruit + Activate Guardian")]
        public void RecruitGuardian()
        {
            var party = Party;
            if (party == null) { Debug.LogError("[ProgressionSandbox] No IPartyService."); return; }

            const string id = "char_guardian";
            if (!party.IsRecruited(id))
            {
                // Walk the legal roster transitions for a brand-new character.
                if (party.GetState(id) == CharacterRosterState.Unmet) party.SetState(id, CharacterRosterState.Met);
                if (party.GetState(id) == CharacterRosterState.Met) party.SetState(id, CharacterRosterState.Recruitable);
                party.Recruit(id);
            }
            bool activated = party.TrySetActive(id, 1);
            Debug.Log($"[ProgressionSandbox] Guardian state={party.GetState(id)} activated={activated}");
        }

        [ContextMenu("Begin Fake Post-Battle Flow")]
        public void BeginFakeFlow()
        {
            var progression = Progression;
            if (progression == null) { Debug.LogError("[ProgressionSandbox] No ProgressionService."); return; }

            var result = new BattleResultData
            {
                battleId = System.Guid.NewGuid().ToString("N"),
                encounterId = fakeEncounterId,
                outcome = BattleOutcome.Victory,
                baseXP = fakeBaseXp,
                turnCount = 3,
            };
            // Two slimes so the resolver rolls enemy drops (XP here is the fake override above).
            result.defeatedEnemyIds.Add("enemy_slime");
            result.defeatedEnemyIds.Add("enemy_slime");

            var party = Party;
            if (party != null)
                foreach (var id in party.GetActivePartyIds()) result.survivingPartyCharacterIds.Add(id);

            progression.BeginPostBattleFlow(result);
            PrintPreview();
        }

        [ContextMenu("Print Preview")]
        public void PrintPreview()
        {
            var p = Progression?.CurrentPreview;
            if (p == null) { Debug.Log("[ProgressionSandbox] No current preview."); return; }
            var sb = new StringBuilder($"[ProgressionSandbox] Preview battle={p.battleId} totalXp={p.totalXp}\n");
            foreach (var c in p.characters)
                sb.AppendLine($"  {c.displayName}: Lv{c.currentLevel} xp {c.currentXp} +{c.xpGained} -> Lv{c.projectedLevel}");
            Debug.Log(sb.ToString());
        }

        [ContextMenu("Apply Battle Result")]
        public void Apply()
        {
            Progression?.ApplyBattleResult();
            PrintProgress();
        }

        [ContextMenu("Allocate Point (serialized char/stat)")]
        public void Allocate()
        {
            bool ok = Progression?.TryAllocateAttributePoint(allocateCharacterId, allocateStat) ?? false;
            Debug.Log($"[ProgressionSandbox] Allocate {allocateStat} on {allocateCharacterId}: {(ok ? "OK" : "FAILED")}");
            PrintProgress();
        }

        [ContextMenu("Complete Flow")]
        public void Complete()
        {
            var progression = Progression;
            if (progression == null) return;
            Debug.Log($"[ProgressionSandbox] CanComplete={progression.CanCompletePostBattleFlow()}");
            progression.CompletePostBattleFlow();
        }

        [ContextMenu("Print Progress")]
        public void PrintProgress()
        {
            var progression = Progression;
            var party = Party;
            if (progression == null || party == null) return;

            var partyRuntime = party as IPartyRuntimeQueries;
            var sb = new StringBuilder("[ProgressionSandbox] Progress:\n");
            foreach (var id in party.GetActivePartyIds())
            {
                var inst = partyRuntime?.ResolveInstanceById(id);
                var prog = progression.GetProgressForCharacter(id);
                sb.AppendLine($"  {id}: Lv{inst?.level} xp={inst?.currentXp} unspent={prog.unspentAttributePoints} " +
                              $"STR={inst?.stats.GetFinal(StatType.Strength)} MaxHP={inst?.stats.GetFinal(StatType.MaxHP)}");
            }
            sb.AppendLine($"  flowActive={progression.IsPostBattleFlowActive} pendingAlloc={progression.HasPendingAttributeAllocations()}");
            Debug.Log(sb.ToString());
        }

        [ContextMenu("Save Slot")]
        public void SaveSlot()
        {
            if (AppContext.Services != null && AppContext.Services.TryResolve<ISaveService>(out var save))
                Debug.Log($"[ProgressionSandbox] Save slot {saveSlot}: {(save.CanSave() ? save.Save(saveSlot) : false)} (CanSave={save.CanSave()})");
        }

        [ContextMenu("Load Slot")]
        public void LoadSlot()
        {
            if (AppContext.Services != null && AppContext.Services.TryResolve<ISaveService>(out var save))
                Debug.Log($"[ProgressionSandbox] Load slot {saveSlot}: {save.Load(saveSlot)}");
            PrintProgress();
        }
    }
}
