using System.Text.RegularExpressions;
using JRPG.Characters;
using JRPG.Core;
using JRPG.Data;
using JRPG.Party;
using JRPG.Services;

namespace JRPG.Dialogue
{
    /// Replaces [Token] and [Token:arg] patterns in body text at render time. Unknown tokens are currently left
    /// visible as [UnknownToken:Name] to surface authoring typos.
    public sealed class DynamicTokenResolver
    {
        private static readonly Regex TokenPattern = new(@"\[([A-Za-z0-9_]+)(?::([^\]]+))?\]", RegexOptions.Compiled);

        private readonly DataRegistry _data;
        private readonly IPartyRuntimeQueries _partyRuntime;
        private readonly IStoryStateService _story;

        public DynamicTokenResolver(DataRegistry data, IPartyRuntimeQueries partyRuntime, IStoryStateService story)
        {
            _data = data;
            _partyRuntime = partyRuntime;
            _story = story;
        }

        public string ResolveTokens(string rawText, DialogueStartContext context)
        {
            if (string.IsNullOrEmpty(rawText)) return rawText;

            return TokenPattern.Replace(rawText, m => Resolve(m.Groups[1].Value, m.Groups[2].Success ? m.Groups[2].Value : null, context));
        }

        private string Resolve(string name, string arg, DialogueStartContext context)
        {
            switch (name)
            {
                case "ProtagonistName": return SlotName(0);
                case "ActiveHealer": return HealerName();
                case "CurrentSpeaker": return context.speakerContextId ?? "";
                case "ItemName": return _data != null && _data.TryGet<ItemData>(arg, out var item) ? item.displayName : Unknown($"ItemName:{arg}");
                case "CharacterName": return _data != null && _data.TryGet<CharacterData>(arg, out var cd) ? cd.displayName : Unknown($"CharacterName:{arg}");
                case "Flag": return _story != null ? _story.GetInt(arg).ToString() : "0";
                default:
                    if (TryResolvePartySlot(name, out var slotName)) return slotName;
                    return Unknown(arg == null ? name : $"{name}:{arg}");
            }
        }

        /// <summary>
        /// <c>[PartyMember_N]</c>, 1-based, for every slot the party can actually field.
        ///
        /// <para>Derived from <see cref="PartyRules.MaxActiveMembers"/> rather than written out as one
        /// case per slot.</para>
        /// </summary>
        private bool TryResolvePartySlot(string name, out string result)
        {
            result = null;

            const string prefix = "PartyMember_";
            if (!name.StartsWith(prefix, System.StringComparison.Ordinal)) return false;

            if (!int.TryParse(name.Substring(prefix.Length), out int oneBased)) return false;
            if (oneBased < 1 || oneBased > PartyRules.MaxActiveMembers) return false;

            result = SlotName(oneBased - 1);
            return true;
        }

        private string SlotName(int slot)
        {
            var inst = _partyRuntime?.GetPartyMemberInSlot(slot);
            return InstanceName(inst);
        }

        private string HealerName()
        {
            if (_partyRuntime == null) return "";
            
            var active = _partyRuntime.GetActiveCombatParty();

            CharacterRuntimeInstance best = null;
            int bestMagic = -1;
            for (int i = 0; i < active.Count; i++)
            {
                int magic = active[i].stats.GetFinal(StatType.Magic);
                if (magic > bestMagic) { bestMagic = magic; best = active[i]; }
            }
            return InstanceName(best);
        }

        private string InstanceName(CharacterRuntimeInstance inst)
        {
            if (inst == null) return "";
            
            return _data != null && _data.TryGet<CharacterData>(inst.SourceDataId, out var cd) ? cd.displayName : inst.SourceDataId;
        }

        private static string Unknown(string token) => $"[UnknownToken:{token}]";
    }
}
