using System.Collections.Generic;

namespace JRPG.Combat
{
    public struct BattleStartRequest
    {
        public string encounterId;
        public IReadOnlyList<string> partyCharacterIds;
        public IReadOnlyList<string> enemyIds;
        public bool useActivePartyFromPartyService;
    }
}
