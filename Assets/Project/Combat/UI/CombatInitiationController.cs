using UnityEngine;
using JRPG.Core;
using JRPG.Services;

namespace JRPG.Combat.UI
{
    /// Listens for CombatInitiationRequested (published today by the exploration Attack input) and
    /// starts a battle from the active party. Ignored while already in battle. Closes the
    /// exploration→combat→back loop without coupling combat to the exploration assembly.
    public sealed class CombatInitiationController : MonoBehaviour
    {
        [Tooltip("Encounter started when a combat-initiation request is received.")]
        [SerializeField] private string encounterId = "encounter_test_slimes";

        private IEventBus _bus;

        private void OnEnable()
        {
            _bus = AppContext.Bus;
            _bus?.Subscribe<CombatInitiationRequested>(OnRequested);
        }

        private void OnDisable() => _bus?.Unsubscribe<CombatInitiationRequested>(OnRequested);

        private void OnRequested(CombatInitiationRequested e)
        {
            if (AppContext.Services == null) return;
            if (!AppContext.Services.TryResolve<ICombatService>(out var combat)) return;
            if (combat.IsInBattle) return;
            combat.StartBattleFromActiveParty(encounterId);
        }
    }
}
