using System.Collections.Generic;
using UnityEngine;

namespace JRPG.Data
{
    /// The elemental interaction table: how much damage each affinity takes. One asset for the
    /// whole game, referenced from GameDatabase. Per-element overrides let a single element bend the rules.
    [CreateAssetMenu(menuName = "JRPG/Combat/Element Interaction Matrix", fileName = "ElementInteractionMatrix")]
    public class ElementInteractionMatrix : ScriptableObject
    {
        [Header("Default multipliers by affinity")]
        public float normalMultiplier = 1f;
        public float weakMultiplier = 1.5f;
        public float resistMultiplier = 0.5f;
        public float absorbMultiplier = 1f;

        [System.Serializable]
        public struct Override
        {
            public Element element;
            public ElementAffinity affinity;
            public float multiplier;
        }

        [Header("Optional per-(element, affinity) overrides")]
        public List<Override> overrides = new();

        /// Damage multiplier for an attacking element against a defender's affinity toward it.
        /// Immune returns 0; Absorb returns a positive value the caller converts into healing.
        public float GetMultiplier(Element element, ElementAffinity affinity)
        {
            for (int i = 0; i < overrides.Count; i++)
                if (overrides[i].element == element && overrides[i].affinity == affinity)
                    return overrides[i].multiplier;

            return affinity switch
            {
                ElementAffinity.Weak => weakMultiplier,
                ElementAffinity.Resist => resistMultiplier,
                ElementAffinity.Immune => 0f,
                ElementAffinity.Absorb => absorbMultiplier,
                _ => normalMultiplier,
            };
        }
    }
}
