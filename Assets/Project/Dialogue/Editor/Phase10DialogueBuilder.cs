using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Menu;
using JRPG.Bootstrap;
using JRPG.Dialogue.UI;

namespace JRPG.Dialogue.Editor
{
    /// One-shot, idempotent setup for the Phase 10 mid-battle dialogue sample. Authors two dialogue
    /// graphs (a passive taunt + an interactive parley) and two battle triggers (HP-threshold passive @
    /// 50%, round-3 interactive), wires them onto encounter_test_slimes, ensures the dialogue_combat
    /// registry entry + a DialoguePresentationProfile, and drops a PassiveDialoguePresenter into
    /// TestExplore. Re-running regenerates in place.
    public static class Phase10DialogueBuilder
    {
        private const string DatabaseFolder = "Assets/Settings/Database";
        private const string DialogueFolder = DatabaseFolder + "/Dialogue";
        private const string TriggerFolder = DatabaseFolder + "/Combat/Triggers";
        private const string UiFolder = "Assets/UI/Dialogue";
        private const string ProfilePath = UiFolder + "/DialoguePresentationProfile.asset";
        private const string PresenterPrefabPath = UiFolder + "/DialoguePresenter.prefab";
        private const string EncounterId = "encounter_test_slimes";
        private const string SlimeEnemyId = "enemy_slime";

        private const string PassiveGraphId = "dialogue_slime_taunt_passive";
        private const string InteractiveGraphId = "dialogue_slime_parley_interactive";
        private const string PassiveTriggerId = "trigger_slime_hp_passive";
        private const string InteractiveTriggerId = "trigger_slime_turn_interactive";

        [MenuItem("JRPG/Setup/Build Phase 10 Combat Dialogue")]
        public static void Build()
        {
            EnsureFolder(DialogueFolder);
            EnsureFolder(TriggerFolder);
            EnsureFolder(UiFolder);

            var db = FindGameDatabase();
            if (db == null) { Debug.LogError("[JRPG.Phase10] No GameDatabase found — aborting."); return; }

            // --- Dialogue graphs ---
            var passiveGraph = BuildPassiveGraph();
            var interactiveGraph = BuildInteractiveGraph();
            db.dialogueGraphs ??= new List<DialogueGraphData>();
            AddUnique(db.dialogueGraphs, passiveGraph);
            AddUnique(db.dialogueGraphs, interactiveGraph);

            // --- Battle triggers ---
            var passiveTrigger = BuildPassiveTrigger();
            var interactiveTrigger = BuildInteractiveTrigger();
            db.battleTriggers ??= new List<BattleTriggerData>();
            AddUnique(db.battleTriggers, passiveTrigger);
            AddUnique(db.battleTriggers, interactiveTrigger);

            // --- Wire triggers onto the encounter ---
            var encounter = FindEncounter(db, EncounterId);
            if (encounter != null)
            {
                encounter.battleTriggerIds ??= new List<string>();
                AddUnique(encounter.battleTriggerIds, PassiveTriggerId);
                AddUnique(encounter.battleTriggerIds, InteractiveTriggerId);
                EditorUtility.SetDirty(encounter);
            }
            else Debug.LogWarning($"[JRPG.Phase10] Encounter '{EncounterId}' not found — triggers authored but not attached.");

            // --- Presentation profile + registry entry ---
            var profile = EnsurePresentationProfile();
            EnsureCombatDialogueEntry();
            WirePresenterPrefabProfile(profile);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // --- Scene wiring (Bootstrap profile field, then the TestExplore passive overlay) ---
            WireBootstrapProfile(profile);
            WirePassiveOverlay();

            Debug.Log("[JRPG.Phase10] Built sample: 2 graphs, 2 triggers on encounter_test_slimes, " +
                      "dialogue_combat entry, presentation profile, and a PassiveDialoguePresenter in TestExplore.");
        }

        // ---- Graphs -------------------------------------------------------------------------

        private static DialogueGraphData BuildPassiveGraph()
        {
            var g = LoadOrCreateGraph(PassiveGraphId, "Slime Taunt (passive)", DialogueImportance.Passive);
            g.entryNodeId = "n1";
            g.nodes = new List<DialogueNodeData>
            {
                new() { nodeId = "n1", speakerRef = "current_speaker", importance = DialogueImportance.Passive,
                        text = "The slime quivers and hisses, half-broken but still standing!",
                        endGraphAfterThisNode = true, exitResolution = Exit(DialogueExitType.None) },
            };
            FinalizeGraph(g, PassiveGraphId);
            return g;
        }

        private static DialogueGraphData BuildInteractiveGraph()
        {
            var g = LoadOrCreateGraph(InteractiveGraphId, "Slime Parley (interactive)", DialogueImportance.Interactive);
            g.entryNodeId = "n1";
            g.nodes = new List<DialogueNodeData>
            {
                new() { nodeId = "n1", speakerRef = "protagonist", importance = DialogueImportance.Interactive,
                        text = "Round three already? These slimes are more stubborn than they look.",
                        nextNodeId = "n2" },
                new()
                {
                    nodeId = "n2", speakerRef = "current_speaker", importance = DialogueImportance.Interactive,
                    text = "The slimes gurgle a challenge. Press on?",
                    exitResolution = Exit(DialogueExitType.ResumeCombat),
                    choices = new List<DialogueChoiceData>
                    {
                        new() { choiceId = "c_fight", displayText = "Press the attack!", nextNodeId = "" },
                        new() { choiceId = "c_steady", displayText = "Steady... hold formation.", nextNodeId = "" },
                    },
                },
            };
            FinalizeGraph(g, InteractiveGraphId);
            return g;
        }

        // ---- Triggers -----------------------------------------------------------------------

        private static BattleTriggerData BuildPassiveTrigger()
        {
            var t = LoadOrCreateTrigger(PassiveTriggerId, "Slime HP Passive Line");
            t.timing = BattleTriggerTiming.HpThresholdCrossed;
            t.hpThresholdPercent = 0.5f;
            t.teamFilter = CombatantTeamFilter.Enemy;
            t.sourceDataIdFilter = SlimeEnemyId;
            t.importance = DialogueImportance.Passive;
            t.blocksCombatInput = false;
            t.oneShot = true;
            t.priority = 0;
            t.dialogueGraphId = PassiveGraphId;
            FinalizeTrigger(t, PassiveTriggerId);
            return t;
        }

        private static BattleTriggerData BuildInteractiveTrigger()
        {
            var t = LoadOrCreateTrigger(InteractiveTriggerId, "Slime Round-3 Interactive");
            t.timing = BattleTriggerTiming.RoundReached;
            t.roundNumber = 3;
            t.teamFilter = CombatantTeamFilter.Any;
            t.importance = DialogueImportance.Interactive;
            t.blocksCombatInput = true;
            t.oneShot = true;
            t.priority = 10;
            t.dialogueGraphId = InteractiveGraphId;
            FinalizeTrigger(t, InteractiveTriggerId);
            return t;
        }

        // ---- Presentation profile + registry + prefab ---------------------------------------

        private static DialoguePresentationProfile EnsurePresentationProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<DialoguePresentationProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<DialoguePresentationProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            // Entries are optional — the profile's built-in fallbacks already route Passive→passive and
            // Critical→blocks — so an empty asset is a valid, self-documenting default here.
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void EnsureCombatDialogueEntry()
        {
            var registry = FindRegistry();
            if (registry == null) { Debug.LogWarning("[JRPG.Phase10] No ContextualCanvasRegistry — dialogue_combat not ensured."); return; }
            var interactive = registry.Find("dialogue_interactive");
            if (interactive == null) { Debug.LogWarning("[JRPG.Phase10] dialogue_interactive missing — run Build Phase 9 Dialogue UI first."); return; }

            var combat = registry.Find("dialogue_combat");
            if (combat == null) { combat = new ContextualCanvasRegistry.Entry { menuId = "dialogue_combat" }; registry.entries.Add(combat); }
            combat.canvasPrefab = interactive.canvasPrefab;
            combat.mode = GameMode.Combat;
            combat.overlay = OverlayState.DialogueInteractive;
            combat.input = InputContext.Dialogue;
            EditorUtility.SetDirty(registry);
        }

        private static void WirePresenterPrefabProfile(DialoguePresentationProfile profile)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PresenterPrefabPath);
            if (prefab == null) return;
            var controller = prefab.GetComponent<DialoguePresenterController>();
            if (controller == null) return;
            var so = new SerializedObject(controller);
            var prop = so.FindProperty("presentationProfile");
            if (prop != null) { prop.objectReferenceValue = profile; so.ApplyModifiedPropertiesWithoutUndo(); }
            EditorUtility.SetDirty(prefab);
        }

        // ---- Scene wiring -------------------------------------------------------------------

        private static void WireBootstrapProfile(DialoguePresentationProfile profile)
        {
            // Bootstrap.unity became the persistent root Startup.unity in Phase 12.2.
            const string bootstrapScene = "Assets/Scenes/Startup.unity";
            var scene = EditorSceneManager.OpenScene(bootstrapScene, OpenSceneMode.Single);
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            if (boot != null)
            {
                var so = new SerializedObject(boot);
                var prop = so.FindProperty("dialoguePresentationProfile");
                if (prop != null) { prop.objectReferenceValue = profile; so.ApplyModifiedPropertiesWithoutUndo(); }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            else Debug.LogWarning("[JRPG.Phase10] No GameBootstrap in Startup.unity — presentation profile not wired.");
        }

        /// The passive overlay belongs on the persistent root, not in a content scene. It used to live in
        /// TestExplore, which meant combat started anywhere else had nowhere to render passive lines. It
        /// registers itself into the static PassiveDialogueSink, so exactly one instance must exist.
        private static void WirePassiveOverlay()
        {
            const string startupScene = "Assets/Scenes/Startup.unity";
            var scene = EditorSceneManager.OpenScene(startupScene, OpenSceneMode.Single);
            if (Object.FindFirstObjectByType<PassiveDialoguePresenter>() == null)
                new GameObject("PassiveDialoguePresenter").AddComponent<PassiveDialoguePresenter>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ---- Asset plumbing (mirrors DialogueTestDataBuilder) -------------------------------

        private static DialogueGraphData LoadOrCreateGraph(string id, string displayName, DialogueImportance defImportance)
        {
            string path = $"{DialogueFolder}/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<DialogueGraphData>(path);
            if (asset == null) asset = ScriptableObject.CreateInstance<DialogueGraphData>();
            asset.displayName = displayName;
            asset.defaultImportance = defImportance;
            return asset;
        }

        private static void FinalizeGraph(DialogueGraphData asset, string id)
        {
            string path = $"{DialogueFolder}/{id}.asset";
            SetStableId(asset, id);
            if (AssetDatabase.LoadAssetAtPath<DialogueGraphData>(path) == null) AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
        }

        private static BattleTriggerData LoadOrCreateTrigger(string id, string displayName)
        {
            string path = $"{TriggerFolder}/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<BattleTriggerData>(path);
            if (asset == null) asset = ScriptableObject.CreateInstance<BattleTriggerData>();
            asset.displayName = displayName;
            return asset;
        }

        private static void FinalizeTrigger(BattleTriggerData asset, string id)
        {
            string path = $"{TriggerFolder}/{id}.asset";
            SetStableId(asset, id);
            if (AssetDatabase.LoadAssetAtPath<BattleTriggerData>(path) == null) AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
        }

        private static DialogueExitResolution Exit(DialogueExitType type, string targetId = null)
            => new() { exitType = type, targetId = targetId };

        private static void SetStableId(Object asset, string id)
        {
            var so = new SerializedObject(asset);
            var prop = so.FindProperty("id");
            if (prop != null) { prop.stringValue = id; so.ApplyModifiedPropertiesWithoutUndo(); }
            asset.name = id;
        }

        private static void AddUnique<T>(List<T> list, T item) { if (item != null && !list.Contains(item)) list.Add(item); }

        private static EncounterData FindEncounter(GameDatabase db, string id)
        {
            if (db.encounters != null)
                for (int i = 0; i < db.encounters.Count; i++)
                    if (db.encounters[i] != null && db.encounters[i].Id == id) return db.encounters[i];
            return null;
        }

        /// Resolve the live database. FindAssets("t:GameDatabase") can return a stray/duplicate first
        /// (and is case-insensitive vs the GameDataBase base class), so prefer the one GameBootstrap
        /// actually references; fall back to whichever holds the target encounter, then the richest.
        private static GameDatabase FindGameDatabase()
        {
            var all = new List<GameDatabase>();
            foreach (var guid in AssetDatabase.FindAssets("t:GameDatabase"))
            {
                var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(AssetDatabase.GUIDToAssetPath(guid));
                if (db != null) all.Add(db);
            }
            if (all.Count == 0) return null;

            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            if (boot != null)
            {
                var dbRef = new SerializedObject(boot).FindProperty("database").objectReferenceValue as GameDatabase;
                if (dbRef != null) return dbRef;
            }

            foreach (var db in all)
                if (FindEncounter(db, EncounterId) != null) return db;

            GameDatabase best = all[0];
            foreach (var db in all)
                if ((db.encounters?.Count ?? 0) + (db.characters?.Count ?? 0) >
                    (best.encounters?.Count ?? 0) + (best.characters?.Count ?? 0)) best = db;
            return best;
        }

        private static ContextualCanvasRegistry FindRegistry()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ContextualCanvasRegistry"))
            {
                var r = AssetDatabase.LoadAssetAtPath<ContextualCanvasRegistry>(AssetDatabase.GUIDToAssetPath(guid));
                if (r != null) return r;
            }
            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
