using System.Collections.Generic;
using UnityEngine;

namespace JRPG.Menu
{
    /// <summary>
    /// Data-agnostic list populator. Owns a RowPool and refreshes a content root from a RowModel list.
    /// </summary>
    public class VerticalListPopulator : MonoBehaviour
    {
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private RowUIController rowPrefab;

        private RowPool _pool;
        private readonly List<RowUIController> _active = new();

        public IReadOnlyList<RowUIController> ActiveRows => _active;

        private void EnsurePool()
        {
            if (_pool == null && rowPrefab != null && contentRoot != null)
                _pool = new RowPool(rowPrefab, contentRoot);
        }

        public void Populate(IReadOnlyList<RowModel> rows)
        {
            EnsurePool();
            if (_pool == null) return;
            ReleaseAll();
            if (rows == null) return;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = _pool.Get();
                row.transform.SetParent(contentRoot, false);
                row.transform.SetSiblingIndex(i);
                row.Bind(rows[i]);
                _active.Add(row);
            }
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i] != null) _pool.Release(_active[i]);
            }
            _active.Clear();
        }

        private void OnDisable() => ReleaseAll();
    }
}
