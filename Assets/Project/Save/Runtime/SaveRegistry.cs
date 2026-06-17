using System;
using System.Collections.Generic;

namespace JRPG.Save
{
    public sealed class SaveRegistry
    {
        private readonly Dictionary<string, ISaveable> _byKey = new();

        public IReadOnlyDictionary<string, ISaveable> Contributors => _byKey;

        public void Register(ISaveable contributor)
        {
            if (contributor == null) throw new ArgumentNullException(nameof(contributor));
            
            var key = contributor.SaveKey;
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Contributor SaveKey is empty.", nameof(contributor));
            
            if (_byKey.ContainsKey(key))
                throw new InvalidOperationException($"SaveRegistry: duplicate SaveKey '{key}'.");
            
            _byKey[key] = contributor;
        }

        public void Unregister(ISaveable contributor)
        {
            if (contributor == null) return;
            
            _byKey.Remove(contributor.SaveKey);
        }

        public bool TryGet(string key, out ISaveable contributor) => _byKey.TryGetValue(key, out contributor);
    }
}
