using UnityEngine;
using UnityEngine.Pool;

namespace JRPG.Menu
{
    /// <summary>
    /// Thin wrapper around UnityEngine.Pool.ObjectPool that knows how to instantiate row prefabs
    /// under a target parent and release them back when a screen tears down.
    /// </summary>
    public sealed class RowPool
    {
        private readonly RowUIController _prefab;
        private readonly Transform _parent;
        private readonly ObjectPool<RowUIController> _pool;

        public RowPool(RowUIController prefab, Transform parent, int defaultCapacity = 16, int maxSize = 128)
        {
            _prefab = prefab;
            _parent = parent;
            _pool = new ObjectPool<RowUIController>(
                createFunc: CreateRow,
                actionOnGet: r => r.gameObject.SetActive(true),
                actionOnRelease: r => r.gameObject.SetActive(false),
                actionOnDestroy: r => { if (r != null) Object.Destroy(r.gameObject); },
                collectionCheck: false,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize);
        }

        public RowUIController Get() => _pool.Get();
        public void Release(RowUIController row) => _pool.Release(row);
        public void Clear() => _pool.Clear();

        private RowUIController CreateRow()
        {
            var row = Object.Instantiate(_prefab, _parent);
            return row;
        }
    }
}
