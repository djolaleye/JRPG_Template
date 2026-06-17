using System.Collections.Generic;
using JRPG.Core;
using JRPG.Characters;
using JRPG.Data;

namespace JRPG.Save
{

    /// Phase 3 proof-of-structure contributor. Owns the runtime character list and
    /// captures/restores via the ISaveable contract.
    /// Later phases replace this with PartyService.

    public sealed class CharacterHolder : ISaveable
    {
        private readonly DataRegistry _registry;

        public string SaveKey => "characters";
        public List<CharacterRuntimeInstance> Instances { get; } = new();

        public CharacterHolder(DataRegistry registry)
        {
            _registry = registry;
        }

        public SaveDataBase CaptureState()
        {
            var payload = new CharactersPayload { version = SaveSystemCore.CurrentSaveVersion };
            for (int i = 0; i < Instances.Count; i++)
            {
                payload.entries.Add(Instances[i].CaptureState(SaveSystemCore.CurrentSaveVersion));
            }
            return payload;
        }

        public void RestoreState(SaveDataBase state)
        {
            if (state is not CharactersPayload payload) return;
            Instances.Clear();
            for (int i = 0; i < payload.entries.Count; i++)
            {
                var inst = new CharacterRuntimeInstance();
                inst.RestoreState(_registry, payload.entries[i]);
                Instances.Add(inst);
            }
        }
    }
}
