using UnityEngine;

namespace JRPG.Core
{
    public abstract class GameDataBase : ScriptableObject, IStableId
    {
        [SerializeField] private string id;
        public string Id => id;

        /// <summary>
        /// Assigns the stable id on a runtime created instance.
        /// </summary>
        public void SetRuntimeId(string runtimeId)
        {
            if (!string.IsNullOrEmpty(id))
            {
                Debug.LogWarning($"[JRPG] SetRuntimeId ignored on '{name}': id '{id}' is already assigned.");
                return;
            }
            
            id = runtimeId;
        }

        public string displayName;
        [TextArea] public string description;

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning($"[JRPG] {name} ({GetType().Name}) is missing a stable Id.", this);
            }
        }
#endif
    }
}
