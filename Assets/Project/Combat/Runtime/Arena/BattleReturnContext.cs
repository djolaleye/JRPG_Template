using UnityEngine;

namespace JRPG.Combat.Arena
{
    /// <summary>
    /// Where the player was standing when the encounter started, captured before the world commits to
    /// the battle.
    /// </summary>
    public sealed class BattleReturnContext
    {
        public string ExplorationSceneId;
        public string EncounterId;
        public Vector3 PlayerPosition;
        public float PlayerRotationY;

        public bool IsValid => !string.IsNullOrEmpty(ExplorationSceneId) && !string.IsNullOrEmpty(EncounterId);

        public override string ToString()
            => $"return(scene='{ExplorationSceneId}', encounter='{EncounterId}', pos={PlayerPosition}, yaw={PlayerRotationY:0.#})";
    }
}
