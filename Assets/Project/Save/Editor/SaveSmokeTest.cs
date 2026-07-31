using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Party;
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

            // Save side: mutate the roster on a real PartyService contributor (the "characters"
            // proof-of-structure contributor was removed in save v2).
            const string ally = "char_ally";
            var party1 = new PartyService(registry, bus, "char_hero", null);
            party1.SetState(ally, CharacterRosterState.Met);
            party1.SetState(ally, CharacterRosterState.Recruitable);
            party1.Recruit(ally);                                  // -> Recruited
            party1.SetState(ally, CharacterRosterState.Reserve);   // Recruited -> Reserve

            var heroPre = party1.GetState("char_hero");            // Active (protagonist)
            var allyPre = party1.GetState(ally);                   // Reserve

            var contribs1 = new SaveRegistry();
            contribs1.Register(party1);
            var svc1 = new SaveSystemCore(contribs1, config, bus, state);
            if (!svc1.Save(0)) return "FAIL: Save returned false (CanSave gating?)";

            filePath = Path.Combine(Application.persistentDataPath, config.directoryName, string.Format(config.fileNameFormat, 0));
            if (!File.Exists(filePath)) return "FAIL: Expected save file at " + filePath;
            var json = File.ReadAllText(filePath);

            // Load side: a fresh PartyService (default roster) restores from the file.
            var party2 = new PartyService(registry, bus, "char_hero", null);
            var contribs2 = new SaveRegistry();
            contribs2.Register(party2);
            var svc2 = new SaveSystemCore(contribs2, config, bus, state);
            if (!svc2.Load(0)) return "FAIL: Load returned false";

            var heroPost = party2.GetState("char_hero");
            var allyPost = party2.GetState(ally);
            bool ok = heroPost == heroPre && allyPost == allyPre;

            string summary =
                $"Round-trip {(ok ? "PASSED" : "FAILED")}\n" +
                $"  pre-save:  hero={heroPre}  {ally}={allyPre}\n" +
                $"  restored:  hero={heroPost}  {ally}={allyPost}\n" +
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
            contribs.Register(new PartyService(registry, bus, "char_hero", null));
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
