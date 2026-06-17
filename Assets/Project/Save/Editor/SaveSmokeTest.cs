using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Characters;
using JRPG.Save;

namespace JRPG.Save.Editor
{

    /// Manual smoke test for the Phase 3 save/load skeleton. Constructs a minimal stack in-editor,
    /// mutates a runtime character, saves to slot 0, clears, loads, and asserts equivalence.
    public class SaveSmokeTest : EditorWindow
    {
        private const string DbPath = "Assets/Settings/Database/GameDatabase.asset";
        private const string ConfigPath = "Assets/Settings/Database/SaveFileConfig.asset";

        private string _lastResult = "(not run)";
        private string _lastFilePath = "";

        [MenuItem("JRPG/Save Smoke Test")]
        public static void ShowWindow() => GetWindow<SaveSmokeTest>("JRPG Save Smoke Test");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("JRPG Save Smoke Test", EditorStyles.boldLabel);

            if (GUILayout.Button("Run Round-Trip"))
            {
                _lastResult = RunRoundTrip(out _lastFilePath);
            }
            if (GUILayout.Button("Run 'Save Outside Allowed State' (expect rejection)"))
            {
                _lastResult = RunRejectionTest();
            }
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_lastFilePath) || !File.Exists(_lastFilePath)))
            {
                if (GUILayout.Button("Reveal Save File"))
                {
                    EditorUtility.RevealInFinder(_lastFilePath);
                }
                if (GUILayout.Button("Print Save JSON to Console"))
                {
                    UnityEngine.Debug.Log(File.ReadAllText(_lastFilePath));
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last result:", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(_lastResult, GUILayout.MinHeight(120));
            if (!string.IsNullOrEmpty(_lastFilePath))
                EditorGUILayout.SelectableLabel("File: " + _lastFilePath);
        }

        private static string RunRoundTrip(out string filePath)
        {
            filePath = "";
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DbPath);
            var config = AssetDatabase.LoadAssetAtPath<SaveFileConfig>(ConfigPath);
            if (db == null) return "FAIL: GameDatabase missing at " + DbPath;
            if (config == null) return "FAIL: SaveFileConfig missing at " + ConfigPath;

            var registry = new DataRegistry();
            registry.Build(db);

            var bus = new EventBus();
            var state = new GameStateController(bus, new LayeredState(GameMode.Exploration, OverlayState.None, InputContext.Exploration));
            var contribs = new SaveRegistry();
            var holder = new CharacterHolder(registry);
            contribs.Register(holder);
            var svc = new SaveSystemCore(contribs, config, bus, state);

            // Build a hero and mutate it.
            var factory = new RuntimeCharacterFactory(registry);
            var hero = factory.Create("char_hero");
            hero.currentHP = 33;
            hero.level = 7;
            hero.currentXp = 999;
            hero.stats.AddModifier(new StatModifier(StatType.Strength, ModifierType.Flat, 4f, "level_up_x", true));
            hero.stats.AddModifier(new StatModifier(StatType.Strength, ModifierType.PercentMult, 2f, "temp_rage", false));
            int finalStrPreSave = hero.stats.GetFinal(StatType.Strength);
            holder.Instances.Add(hero);

            int snapshotLevel = hero.level;
            int snapshotHp = hero.currentHP;
            int snapshotXp = hero.currentXp;
            string snapshotInstanceId = hero.InstanceId;

            if (!svc.Save(0)) return "FAIL: Save returned false (CanSave gating?)";

            filePath = Path.Combine(Application.persistentDataPath, config.directoryName, string.Format(config.fileNameFormat, 0));
            if (!File.Exists(filePath)) return "FAIL: Expected save file at " + filePath;
            var json = File.ReadAllText(filePath);

            // Tear down: clear holder, then load.
            holder.Instances.Clear();
            if (!svc.Load(0)) return "FAIL: Load returned false";
            if (holder.Instances.Count != 1) return "FAIL: Expected 1 restored instance, got " + holder.Instances.Count;
            var restored = holder.Instances[0];

            int restoredFinalStr = restored.stats.GetFinal(StatType.Strength);
            int expectedRestoredStr = hero.stats.GetBase(StatType.Strength) + 4; // base + permanent flat only

            bool ok =
                restored.level == snapshotLevel &&
                restored.currentHP == snapshotHp &&
                restored.currentXp == snapshotXp &&
                restored.InstanceId == snapshotInstanceId &&
                restoredFinalStr == expectedRestoredStr;

            string summary =
                $"Round-trip {(ok ? "PASSED" : "FAILED")}\n" +
                $"  pre-save: lvl={snapshotLevel} hp={snapshotHp} xp={snapshotXp} finalStr={finalStrPreSave} (with temp rage)\n" +
                $"  restored: lvl={restored.level} hp={restored.currentHP} xp={restored.currentXp} finalStr={restoredFinalStr} (expected {expectedRestoredStr}; temp rage stripped)\n" +
                $"  instanceId match={restored.InstanceId == snapshotInstanceId}\n" +
                $"  json size={json.Length} chars; version={ExtractVersion(json)}";
            return summary;
        }

        private static string RunRejectionTest()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DbPath);
            var config = AssetDatabase.LoadAssetAtPath<SaveFileConfig>(ConfigPath);
            if (db == null) return "FAIL: GameDatabase missing.";
            if (config == null) return "FAIL: SaveFileConfig missing.";

            var registry = new DataRegistry();
            registry.Build(db);
            var bus = new EventBus();
            // Not an allowed state: MainMenu.
            var state = new GameStateController(bus, new LayeredState(GameMode.MainMenu, OverlayState.None, InputContext.Menu));
            var contribs = new SaveRegistry();
            contribs.Register(new CharacterHolder(registry));
            var svc = new SaveSystemCore(contribs, config, bus, state);

            bool result = svc.Save(0);
            return result
                ? "FAIL: Save should have been rejected in MainMenu state."
                : "PASSED: Save correctly rejected in MainMenu state.";
        }

        private static string ExtractVersion(string json)
        {
            int i = json.IndexOf("\"version\"");
            if (i < 0) return "(no version field!)";
            int colon = json.IndexOf(':', i);
            int comma = json.IndexOf(',', colon);
            if (colon < 0 || comma < 0) return "(parse error)";
            return json.Substring(colon + 1, comma - colon - 1).Trim();
        }
    }
}
