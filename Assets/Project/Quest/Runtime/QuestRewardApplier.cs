using System;
using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Party;
using JRPG.Progression;
using JRPG.Services;

namespace JRPG.Quest
{
    /// <summary>
    /// Turns authored <see cref="QuestRewardData"/> into calls on the services that already own each
    /// resource. The quest system never writes inventory, currency, XP or story flags itself. This is
    /// the one place quest payouts touch anything, and every branch is a delegation.
    ///
    /// <para><b>Inventory room is preflighted.</b> Item rewards check <c>RoomFor</c> before anything is
    /// granted, matching the economy transaction rule: a payout that cannot be received in full is
    /// refused rather than silently truncated, and the quest stays completable.</para>
    /// </summary>
    public sealed class QuestRewardApplier
    {
        private readonly IInventoryService _inventory;
        private readonly ICurrencyService _currency;
        private readonly IStoryStateService _story;
        private readonly IPartyRuntimeQueries _partyRuntime;
        private readonly ProgressionService _progression;
        private readonly Func<string, int, BondProgressSource, bool> _grantBond;

        /// Handlers for QuestRewardType.Custom, keyed by the reward's id. The extension seam: a project
        /// registers what its own rewards mean without the quest system growing a case per.
        private readonly Dictionary<string, Action<QuestRewardData>> _customHandlers = new();

        public QuestRewardApplier(IInventoryService inventory, ICurrencyService currency,
                                  IStoryStateService story, IPartyRuntimeQueries partyRuntime,
                                  ProgressionService progression,
                                  Func<string, int, BondProgressSource, bool> grantBond)
        {
            _inventory = inventory;
            _currency = currency;
            _story = story;
            _partyRuntime = partyRuntime;
            _progression = progression;
            _grantBond = grantBond;
        }

        public void RegisterCustomHandler(string id, Action<QuestRewardData> handler)
        {
            if (string.IsNullOrEmpty(id) || handler == null) return;
            _customHandlers[id] = handler;
        }

        /// <summary>
        /// True when every reward can be received right now. Checked before the completion transaction
        /// mutates anything, so a full bag blocks the turn-in instead of eating the reward.
        /// </summary>
        public bool CanReceiveAll(IReadOnlyList<QuestRewardData> rewards, out string blockedReason)
        {
            blockedReason = null;
            if (rewards == null) return true;

            // Several lines may pay the same item, so room is accumulated rather than checked per line.
            Dictionary<string, int> wanted = null;

            for (int i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                if (reward == null || reward.type != QuestRewardType.Item) continue;
                if (string.IsNullOrEmpty(reward.id) || reward.amount <= 0) continue;

                wanted ??= new Dictionary<string, int>();
                wanted.TryGetValue(reward.id, out int running);
                wanted[reward.id] = running + reward.amount;
            }

            if (wanted == null || _inventory == null) return true;

            foreach (var kv in wanted)
            {
                if (_inventory.RoomFor(kv.Key) >= kv.Value) continue;

                blockedReason = "Not enough room to receive the reward.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Applies every reward. Call only after <see cref="CanReceiveAll"/> passes and the caller has
        /// committed to the transaction.
        /// </summary>
        /// <param name="ownerCharacterId">
        /// Who a character-scoped reward (Skill, BondProgress) belongs to — the quest's characterId for
        /// a Party quest. Empty falls back to the party leader for skills, and makes bond progress a no-op.
        /// </param>
        public void ApplyAll(IReadOnlyList<QuestRewardData> rewards, string ownerCharacterId,
                             BondProgressSource bondSource = BondProgressSource.QuestReward)
        {
            if (rewards == null) return;

            for (int i = 0; i < rewards.Count; i++)
                Apply(rewards[i], ownerCharacterId, bondSource);
        }

        private void Apply(QuestRewardData reward, string ownerCharacterId, BondProgressSource bondSource)
        {
            if (reward == null) return;

            switch (reward.type)
            {
                case QuestRewardType.Experience:
                    if (_progression == null) { Warn(reward, "no progression service"); return; }
                    _progression.GrantXp(reward.amount, "quest reward");
                    return;

                case QuestRewardType.Currency:
                    if (_currency == null) { Warn(reward, "no currency service"); return; }
                    _currency.Add(reward.amount, CurrencyChangeReason.Unspecified);
                    return;

                case QuestRewardType.Item:
                    if (_inventory == null) { Warn(reward, "no inventory service"); return; }
                    int overflow = _inventory.Add(reward.id, reward.amount);
                    if (overflow > 0)
                        Debug.LogWarning($"[JRPG.Quest] {overflow} × '{reward.id}' did not fit and was lost.");
                    return;

                case QuestRewardType.Skill:
                    GrantSkill(reward.id, ownerCharacterId);
                    return;

                case QuestRewardType.StoryFlag:
                    if (_story == null) { Warn(reward, "no story service"); return; }
                    _story.SetBool(reward.id, reward.flagValue);
                    return;

                case QuestRewardType.BondProgress:
                    // The reward's own id wins when authored, so a quest can deepen a bond other than
                    // its owner's; otherwise it feeds the quest's character.
                    var target = string.IsNullOrEmpty(reward.id) ? ownerCharacterId : reward.id;
                    if (string.IsNullOrEmpty(target) || _grantBond == null) { Warn(reward, "no bond target"); return; }
                    _grantBond(target, reward.amount, bondSource);
                    return;

                case QuestRewardType.Custom:
                    if (!string.IsNullOrEmpty(reward.id) && _customHandlers.TryGetValue(reward.id, out var handler))
                    {
                        handler(reward);
                        return;
                    }
                    Warn(reward, "no handler registered for this custom id");
                    return;
            }
        }

        /// <summary>
        /// Teaches a skill through the runtime instance, which owns the 8-skill cap and refuses a
        /// duplicate. A full list is reported rather than silently discarding an existing skill: the
        /// replacement decision belongs to the player, and quest turn-in is not a screen that can ask.
        /// </summary>
        private void GrantSkill(string skillId, string ownerCharacterId)
        {
            if (string.IsNullOrEmpty(skillId) || _partyRuntime == null) return;

            var instance = string.IsNullOrEmpty(ownerCharacterId)
                ? _partyRuntime.GetPartyMemberInSlot(0)
                : _partyRuntime.ResolveInstanceById(ownerCharacterId);

            if (instance == null)
            {
                Debug.LogWarning($"[JRPG.Quest] Skill reward '{skillId}' had no recipient.");
                return;
            }

            if (instance.KnowsSkill(skillId)) return;

            if (!instance.TryLearnSkill(skillId))
                Debug.LogWarning($"[JRPG.Quest] '{instance.SourceDataId}' could not learn '{skillId}' — skill list full.");
        }

        private static void Warn(QuestRewardData reward, string why)
            => Debug.LogWarning($"[JRPG.Quest] {reward.type} reward '{reward.id}' skipped — {why}.");
    }
}
