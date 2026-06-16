using System;
using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Data
{
    /// <summary>
    /// Runtime ID lookup over a GameDatabase. Built once at bootstrap; never mutated.
    /// </summary>
    public sealed class DataRegistry
    {
        private readonly Dictionary<string, CharacterData> _charactersById = new();
        private readonly Dictionary<string, EnemyData> _enemiesById = new();

        public IReadOnlyDictionary<string, CharacterData> CharactersById => _charactersById;
        public IReadOnlyDictionary<string, EnemyData> EnemiesById => _enemiesById;

        public void Build(GameDatabase db)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            _charactersById.Clear();
            _enemiesById.Clear();

            Index(db.characters, _charactersById, "characters");
            Index(db.enemies, _enemiesById, "enemies");
        }

        private static void Index<T>(List<T> list, Dictionary<string, T> map, string label) where T : GameDataBase
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var asset = list[i];
                if (asset == null)
                    throw new InvalidOperationException($"GameDatabase.{label}[{i}] is null.");
                var id = asset.Id;
                if (string.IsNullOrWhiteSpace(id))
                    throw new InvalidOperationException(
                        $"GameDatabase.{label}[{i}] ('{asset.name}') has an empty stable Id.");
                if (map.ContainsKey(id))
                    throw new InvalidOperationException(
                        $"GameDatabase.{label} contains duplicate stable Id '{id}'.");
                map[id] = asset;
            }
        }

        public T Get<T>(string id) where T : GameDataBase
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Id is null or empty.", nameof(id));
            if (TryGet<T>(id, out var v)) return v;
            throw new KeyNotFoundException($"DataRegistry: unknown id '{id}' for type '{typeof(T).Name}'.");
        }

        public bool TryGet<T>(string id, out T value) where T : GameDataBase
        {
            if (!string.IsNullOrEmpty(id))
            {
                if (typeof(T) == typeof(CharacterData) || typeof(T).IsAssignableFrom(typeof(CharacterData)))
                {
                    if (_charactersById.TryGetValue(id, out var c) && c is T tc) { value = tc; return true; }
                }
                if (typeof(T) == typeof(EnemyData) || typeof(T).IsAssignableFrom(typeof(EnemyData)))
                {
                    if (_enemiesById.TryGetValue(id, out var e) && e is T te) { value = te; return true; }
                }
                if (typeof(T) == typeof(GameDataBase))
                {
                    if (_charactersById.TryGetValue(id, out var c) && c is T tc) { value = tc; return true; }
                    if (_enemiesById.TryGetValue(id, out var e) && e is T te) { value = te; return true; }
                }
            }
            value = null;
            return false;
        }
    }
}
