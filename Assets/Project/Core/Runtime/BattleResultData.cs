using System.Collections.Generic;

namespace JRPG.Core
{
    /// <summary>
    /// Read-only view of a completed battle, handed to subscribers through
    /// <see cref="BattleResultPackaged"/>. Lets any assembly consume the result without referencing
    /// JRPG.Combat or reaching into a mutable service property — and without being able to mutate it.
    /// </summary>
    public interface IBattleResult
    {
        string BattleId { get; }
        string EncounterId { get; }
        BattleOutcome Outcome { get; }
        IReadOnlyList<string> DefeatedEnemyIds { get; }
        IReadOnlyList<string> SurvivingPartyCharacterIds { get; }
        int BaseXP { get; }
        int Currency { get; }
        IReadOnlyList<ItemDropResult> ItemDrops { get; }
        int TurnCount { get; }
        float ElapsedSeconds { get; }
    }

    /// <summary>
    /// Victory payload built by CombatService and consumed by the Progression phase (XP, rewards,
    /// level-ups). A Core DTO — no Unity or Combat types — so it can travel on the event bus. Producers
    /// use the fields; external consumers should take the read-only <see cref="IBattleResult"/>.
    /// </summary>
    public class BattleResultData : IBattleResult
    {
        public string battleId;
        public string encounterId;
        public BattleOutcome outcome = BattleOutcome.None;

        public List<string> defeatedEnemyIds = new();
        public List<string> survivingPartyCharacterIds = new();

        public int baseXP;
        public int currency;

        public List<ItemDropResult> itemDrops = new();

        public int turnCount;
        public float elapsedSeconds;

        // Read-only projection (explicit so the mutable fields above stay the producer's ergonomic API).
        string IBattleResult.BattleId => battleId;
        string IBattleResult.EncounterId => encounterId;
        BattleOutcome IBattleResult.Outcome => outcome;
        IReadOnlyList<string> IBattleResult.DefeatedEnemyIds => defeatedEnemyIds;
        IReadOnlyList<string> IBattleResult.SurvivingPartyCharacterIds => survivingPartyCharacterIds;
        int IBattleResult.BaseXP => baseXP;
        int IBattleResult.Currency => currency;
        IReadOnlyList<ItemDropResult> IBattleResult.ItemDrops => itemDrops;
        int IBattleResult.TurnCount => turnCount;
        float IBattleResult.ElapsedSeconds => elapsedSeconds;
    }

    public struct ItemDropResult
    {
        public string itemId;
        public int quantity;
    }
}
