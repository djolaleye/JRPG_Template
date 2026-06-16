using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;

namespace JRPG.Core.Editor
{
    public class DatabaseValidator : EditorWindow
    {
        private const string SettingsDatabaseFolder = "Assets/Settings/Database";

        private GameDatabase _db;
        private Vector2 _scroll;
        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();
        private readonly List<string> _infos = new();

        [MenuItem("JRPG/Validate Database")]
        public static void ShowWindow()
        {
            var w = GetWindow<DatabaseValidator>("JRPG Database Validator");
            w.AutoLocateDatabase();
        }

        private void AutoLocateDatabase()
        {
            if (_db != null) return;

            var guids = AssetDatabase.FindAssets("t:GameDatabase");
            if (guids.Length > 0)
            {
                _db = AssetDatabase.LoadAssetAtPath<GameDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("JRPG Database Validator", EditorStyles.boldLabel);
            _db = (GameDatabase)EditorGUILayout.ObjectField("Database", _db, typeof(GameDatabase), false);

            using (new EditorGUI.DisabledScope(_db == null))
            {
                if (GUILayout.Button("Run Validation"))
                {
                    Validate();
                }
            }

            EditorGUILayout.Space();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawSection("Errors", _errors, MessageType.Error);
            DrawSection("Warnings", _warnings, MessageType.Warning);
            DrawSection("Info", _infos, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private void DrawSection(string label, List<string> lines, MessageType mt)
        {
            if (lines.Count == 0) return;
            EditorGUILayout.LabelField($"{label} ({lines.Count})", EditorStyles.boldLabel);
            foreach (var l in lines)
                EditorGUILayout.HelpBox(l, mt);
            EditorGUILayout.Space();
        }

        private void Validate()
        {
            _errors.Clear();
            _warnings.Clear();
            _infos.Clear();

            if (_db == null)
            {
                _errors.Add("No GameDatabase assigned.");
                return;
            }

            var seenIds = new Dictionary<string, string>(); // id -> list label
            CheckList("characters", _db.characters, seenIds);
            CheckList("enemies", _db.enemies, seenIds);

            // Look for orphan assets in the database folder.
            CheckOrphans(_db);

            if (_errors.Count == 0 && _warnings.Count == 0)
                _infos.Add("Validation passed.");
        }

        private void CheckList<T>(string label, List<T> list, Dictionary<string, string> seenIds) where T : GameDataBase
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                GameDataBase a = list[i];
                if (a == null) { _errors.Add($"{label}[{i}] is null."); continue; }
                var id = a.Id;
                if (string.IsNullOrWhiteSpace(id))
                {
                    _errors.Add($"{label}[{i}] ('{a.name}') has an empty stable Id.");
                    continue;
                }
                if (seenIds.TryGetValue(id, out var prior))
                {
                    _errors.Add($"Duplicate stable Id '{id}' in '{label}' (previously in '{prior}').");
                }
                else
                {
                    seenIds.Add(id, label);
                }
            }
        }

        private void CheckOrphans(GameDatabase db)
        {
            if (!AssetDatabase.IsValidFolder(SettingsDatabaseFolder)) return;

            var listed = new HashSet<Object>();
            foreach (var c in db.characters) if (c != null) listed.Add(c);
            foreach (var e in db.enemies) if (e != null) listed.Add(e);

            var guids = AssetDatabase.FindAssets("t:GameDataBase", new[] { SettingsDatabaseFolder });
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var asset = AssetDatabase.LoadAssetAtPath<GameDataBase>(path);
                if (asset == null) continue;
                if (!listed.Contains(asset))
                {
                    _warnings.Add($"Asset '{path}' is in the database folder but not listed in GameDatabase.");
                }
            }
        }
    }
}
