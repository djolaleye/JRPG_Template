using UnityEngine;

namespace JRPG.Core
{
    public abstract class GameDataBase : ScriptableObject, IStableId
    {
        [SerializeField] private string id;
        public string Id => id;

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
