using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using JRPG.Data;

namespace JRPG.Dialogue.Editor
{
    /// One-shot setup for the Phase 9 sample dialogue graphs, mapped onto existing content so every
    /// condition/command resolves against real data. Registers them in the GameDatabase. Idempotent.
    public static class DialogueTestDataBuilder
    {
        private const string DatabaseFolder = "Assets/Settings/Database";
        private const string DialogueFolder = DatabaseFolder + "/Dialogue";

        [MenuItem("JRPG/Setup/Build Phase 9 Dialogue Data")]
        public static void Build()
        {
            EnsureFolder(DialogueFolder);

            var graphs = new List<DialogueGraphData>
            {
                BuildBlacksmith(),
                BuildGuardGate(),
                BuildRivalIntro(),
            };

            RegisterInDatabase(graphs);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[JRPG.Dialogue] Built {graphs.Count} sample dialogue graphs and registered in GameDatabase.");
        }

        // ---- Sample graphs --------------------------------------------------------------------

        private static DialogueGraphData BuildBlacksmith()
        {
            var g = LoadOrCreate("dialogue_blacksmith_greeting", "Blacksmith Greeting");
            g.entryNodeId = "n1";
            g.nodes = new List<DialogueNodeData>
            {
                new() { nodeId = "n1", speakerRef = "current_speaker", text = "Welcome, [ProtagonistName].", nextNodeId = "n2" },
                new() { nodeId = "n2", speakerRef = "current_speaker", text = "Bring me ore if you find any.",
                        endGraphAfterThisNode = true, exitResolution = Exit(DialogueExitType.ReturnToExploration) },
            };
            Finalize(g, "dialogue_blacksmith_greeting");
            return g;
        }

        private static DialogueGraphData BuildGuardGate()
        {
            var g = LoadOrCreate("dialogue_guard_gate", "Guard Gate");
            g.entryNodeId = "n1";
            g.nodes = new List<DialogueNodeData>
            {
                new()
                {
                    nodeId = "n1", speakerRef = "current_speaker", text = "State your business.",
                    choices = new List<DialogueChoiceData>
                    {
                        new()
                        {
                            choiceId = "c_pass", displayText = "I have the pass.", nextNodeId = "n_open",
                            conditions = new() { Cond(DialogueConditionType.HasItem, sA: "item_key_dorm", iA: 1) },
                            commands = new() { Cmd(DialogueCommandType.SetStoryFlag, sA: "gate_open", bA: true) },
                        },
                        new() { choiceId = "c_never", displayText = "Never mind.", nextNodeId = "" },
                    },
                },
                new() { nodeId = "n_open", speakerRef = "current_speaker", text = "The gate is open. Go ahead.",
                        endGraphAfterThisNode = true, exitResolution = Exit(DialogueExitType.ReturnToExploration) },
            };
            Finalize(g, "dialogue_guard_gate");
            return g;
        }

        private static DialogueGraphData BuildRivalIntro()
        {
            var g = LoadOrCreate("dialogue_rival_intro", "Rival Intro");
            g.entryNodeId = "n1";
            g.nodes = new List<DialogueNodeData>
            {
                new()
                {
                    nodeId = "n1", speakerRef = "current_speaker", text = "You made it this far, [ProtagonistName].",
                    choices = new List<DialogueChoiceData>
                    {
                        new()
                        {
                            choiceId = "c_join", displayText = "Join us.", nextNodeId = "n_joined",
                            conditions = new() { Cond(DialogueConditionType.StoryFlagEquals, sA: "rival_respected", bA: true) },
                            commands = new()
                            {
                                Cmd(DialogueCommandType.RecruitCharacter, sA: "char_ally"),
                                Cmd(DialogueCommandType.GiveItem, sA: "item_potion_small", iA: 1),
                            },
                        },
                        new()
                        {
                            choiceId = "c_fight", displayText = "Fight me.", nextNodeId = "",
                            commands = new() { Cmd(DialogueCommandType.StartBattle, sA: "encounter_test_slimes") },
                        },
                    },
                },
                new() { nodeId = "n_joined", speakerRef = "character:char_ally", text = "Welcome to the party.",
                        endGraphAfterThisNode = true, exitResolution = Exit(DialogueExitType.ReturnToExploration) },
            };
            Finalize(g, "dialogue_rival_intro");
            return g;
        }

        // ---- Builders -------------------------------------------------------------------------

        private static DialogueCondition Cond(DialogueConditionType type, string sA = null, string sB = null, int iA = 0, bool bA = false)
            => new() { type = type, stringA = sA, stringB = sB, intA = iA, boolA = bA };

        private static DialogueCommand Cmd(DialogueCommandType type, string sA = null, int iA = 0, bool bA = false)
            => new() { type = type, stringA = sA, intA = iA, boolA = bA };

        private static DialogueExitResolution Exit(DialogueExitType type, string targetId = null)
            => new() { exitType = type, targetId = targetId };

        // ---- Asset plumbing (mirrors ProgressionTestDataBuilder) -------------------------------

        private static DialogueGraphData LoadOrCreate(string id, string displayName)
        {
            string path = $"{DialogueFolder}/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<DialogueGraphData>(path);
            if (asset == null) asset = ScriptableObject.CreateInstance<DialogueGraphData>();
            asset.displayName = displayName;
            asset.defaultImportance = DialogueImportance.Interactive;
            return asset;
        }

        private static void Finalize(DialogueGraphData asset, string id)
        {
            string path = $"{DialogueFolder}/{id}.asset";
            SetStableId(asset, id);
            if (AssetDatabase.LoadAssetAtPath<DialogueGraphData>(path) == null) AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
        }

        private static void SetStableId(Object asset, string id)
        {
            var so = new SerializedObject(asset);
            var prop = so.FindProperty("id");
            if (prop != null) { prop.stringValue = id; so.ApplyModifiedPropertiesWithoutUndo(); }
            asset.name = id;
        }

        private static void RegisterInDatabase(List<DialogueGraphData> graphs)
        {
            var db = FindGameDatabase();
            if (db == null) { Debug.LogError("[JRPG.Dialogue] No GameDatabase found — graphs not registered."); return; }
            db.dialogueGraphs ??= new List<DialogueGraphData>();
            foreach (var g in graphs)
                if (!db.dialogueGraphs.Contains(g)) db.dialogueGraphs.Add(g);
            EditorUtility.SetDirty(db);
        }

        private static GameDatabase FindGameDatabase()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:GameDatabase"))
            {
                var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(AssetDatabase.GUIDToAssetPath(guid));
                if (db != null) return db;
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
