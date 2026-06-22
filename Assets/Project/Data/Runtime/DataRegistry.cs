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
        private readonly Dictionary<string, ItemData> _itemsById = new();
        private readonly Dictionary<string, CombatActionData> _combatActionsById = new();
        private readonly Dictionary<string, EncounterData> _encountersById = new();

        public IReadOnlyDictionary<string, CharacterData> CharactersById => _charactersById;
        public IReadOnlyDictionary<string, EnemyData> EnemiesById => _enemiesById;
        public IReadOnlyDictionary<string, ItemData> ItemsById => _itemsById;
        public IReadOnlyDictionary<string, CombatActionData> CombatActionsById => _combatActionsById;
        public IReadOnlyDictionary<string, EncounterData> EncountersById => _encountersById;

        public void Build(GameDatabase db)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            _charactersById.Clear();
            _enemiesById.Clear();
            _itemsById.Clear();
            _combatActionsById.Clear();
            _encountersById.Clear();

            Index(db.characters, _charactersById, "characters");
            Index(db.enemies, _enemiesById, "enemies");
            Index(db.items, _itemsById, "items");
            Index(db.combatActions, _combatActionsById, "combatActions");
            Index(db.encounters, _encountersById, "encounters");
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
                // Items (and its subclasses like EquipmentData) — typeof(T) == ItemData OR T is a derived item type.
                if (typeof(ItemData).IsAssignableFrom(typeof(T)) || typeof(T) == typeof(ItemData))
                {
                    if (_itemsById.TryGetValue(id, out var i) && i is T ti) { value = ti; return true; }
                }
                if (typeof(T) == typeof(CombatActionData) || typeof(T).IsAssignableFrom(typeof(CombatActionData)))
                {
                    if (_combatActionsById.TryGetValue(id, out var a) && a is T ta) { value = ta; return true; }
                }
                if (typeof(T) == typeof(EncounterData) || typeof(T).IsAssignableFrom(typeof(EncounterData)))
                {
                    if (_encountersById.TryGetValue(id, out var en) && en is T ten) { value = ten; return true; }
                }
                if (typeof(T) == typeof(GameDataBase))
                {
                    if (_charactersById.TryGetValue(id, out var c) && c is T tc) { value = tc; return true; }
                    if (_enemiesById.TryGetValue(id, out var e) && e is T te) { value = te; return true; }
                    if (_itemsById.TryGetValue(id, out var i) && i is T ti) { value = ti; return true; }
                    if (_combatActionsById.TryGetValue(id, out var a) && a is T ta) { value = ta; return true; }
                    if (_encountersById.TryGetValue(id, out var en) && en is T ten) { value = ten; return true; }
                }
            }
            value = null;
            return false;
        }
    }
}
