using UnityEngine;
using JRPG.Characters;
using JRPG.Data;
using JRPG.Party;
using JRPG.Services;

namespace JRPG.Dialogue
{
    /// Turns an authored speaker reference string into a display name + portrait id at render time.
    public sealed class SpeakerResolver
    {
        private readonly DataRegistry _data;
        private readonly IPartyRuntimeQueries _partyRuntime;

        public SpeakerResolver(DataRegistry data, IPartyRuntimeQueries partyRuntime)
        {
            _data = data;
            _partyRuntime = partyRuntime;
        }

        public SpeakerViewData Resolve(string speakerRef, DialogueStartContext context)
        {
            if (string.IsNullOrEmpty(speakerRef))
                return new SpeakerViewData { displayName = "" };

            if (speakerRef == "system")
                return new SpeakerViewData { speakerId = "system", displayName = "System" };

            if (speakerRef == "current_speaker")
                return new SpeakerViewData { speakerId = context.speakerContextId, displayName = context.speakerContextId ?? "" };

            if (speakerRef == "protagonist")
                return FromInstance(_partyRuntime?.GetPartyMemberInSlot(0));

            if (speakerRef == "active_healer")
                return FromInstance(FindHealer());

            if (speakerRef.StartsWith("party_slot_") && int.TryParse(speakerRef.Substring("party_slot_".Length), out int oneBased))
                return FromInstance(_partyRuntime?.GetPartyMemberInSlot(oneBased - 1));

            if (speakerRef.StartsWith("character:"))
                return FromCharacterId(speakerRef.Substring("character:".Length));

            if (speakerRef.StartsWith("npc:"))
            {
                string npcId = speakerRef.Substring("npc:".Length);
                // No NpcData type yet — resolve to a character if one matches, else show a readable id.
                if (_data != null && _data.TryGet<CharacterData>(npcId, out var cd))
                    return new SpeakerViewData { speakerId = npcId, displayName = cd.displayName, portraitId = npcId };
                
                return new SpeakerViewData { speakerId = npcId, displayName = Readable(npcId), portraitId = npcId };
            }

            Debug.LogWarning($"[JRPG.Dialogue] Unresolved speaker ref '{speakerRef}'.");

            return new SpeakerViewData { displayName = $"[Missing Speaker: {speakerRef}]" };
        }

        private SpeakerViewData FromCharacterId(string characterId)
        {
            if (_data != null && _data.TryGet<CharacterData>(characterId, out var cd))
                return new SpeakerViewData { speakerId = characterId, displayName = cd.displayName, portraitId = characterId };
            
            return new SpeakerViewData { speakerId = characterId, displayName = Readable(characterId), portraitId = characterId };
        }

        private SpeakerViewData FromInstance(CharacterRuntimeInstance inst)
        {
            if (inst == null) return new SpeakerViewData { displayName = "" };
            
            return FromCharacterId(inst.SourceDataId);
        }

        private CharacterRuntimeInstance FindHealer()
        {
            if (_partyRuntime == null) return null;
            var active = _partyRuntime.GetActiveCombatParty();

            CharacterRuntimeInstance best = null;
            int bestMagic = -1;

            for (int i = 0; i < active.Count; i++)
            {
                int magic = active[i].stats.GetFinal(StatType.Magic);
                if (magic > bestMagic) { bestMagic = magic; best = active[i]; }
            }

            return best;
        }

        private static string Readable(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            // "npc_blacksmith" -> "Blacksmith" (use the last underscore-separated segment, capitalized).
            int us = id.LastIndexOf('_');
            string tail = us >= 0 && us < id.Length - 1 ? id.Substring(us + 1) : id;
            
            return char.ToUpperInvariant(tail[0]) + tail.Substring(1);
        }
    }
}
