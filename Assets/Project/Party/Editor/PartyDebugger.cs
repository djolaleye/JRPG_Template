using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Save;
using JRPG.Party;

namespace JRPG.Party.EditorTools
{
    /// <summary>
    /// Standalone PartyService driver for designers and verification — builds edit-time
    /// stack, then exposes roster transitions, scope push/pop, and a
    /// save round-trip button.
    /// </summary>
    public class PartyDebugger : EditorWindow
    {
        private const string DbPath = "Assets/Settings/Database/GameDatabase.asset";
        private const string ConfigPath = "Assets/Settings/Database/SaveFileConfig.asset";

        private DataRegistry _registry;
        private EventBus _bus;
        private GameStateController _state;
        private SaveRegistry _contribs;
        private PartyService _party;
        private SaveSystemCore _save;
        private string _lastResult = "(not run)";

        private Vector2 _scroll;

        [MenuItem("JRPG/Party Debugger")]
        public static void ShowWindow() => GetWindow<PartyDebugger>("JRPG Party Debugger");

        private void OnEnable() => RebuildStack();

        private void RebuildStack()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DbPath);
            var cfg = AssetDatabase.LoadAssetAtPath<SaveFileConfig>(ConfigPath);
            if (db == null) { _lastResult = "GameDatabase missing at " + DbPath; return; }

            _registry = new DataRegistry();
            _registry.Build(db);

            _bus = new EventBus();
            _state = new GameStateController(_bus, new LayeredState(GameMode.Exploration, OverlayState.None, InputContext.Exploration));
            _contribs = new SaveRegistry();
            _party = new PartyService(_registry, _bus);
            _contribs.Register(_party);
            if (cfg != null) _save = new SaveSystemCore(_contribs, cfg, _bus, _state);

            _lastResult = $"Stack rebuilt. characters known: {_registry.CharactersById.Count}";
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("JRPG Party Debugger", EditorStyles.boldLabel);
            if (GUILayout.Button("Rebuild Stack")) RebuildStack();

            if (_party == null)
            {
                EditorGUILayout.HelpBox("No party service. Click 'Rebuild Stack' after ensuring GameDatabase and SaveFileConfig assets exist.", MessageType.Warning);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Roster", EditorStyles.boldLabel);
            var ids = new List<string>(_party.State.stateByCharacterId.Keys);
            ids.Sort();
            foreach (var id in ids)
            {
                EditorGUILayout.BeginHorizontal();
                var s = _party.State.stateByCharacterId[id];
                string lockBadge = _party.IsLocked(id) ? " 🔒" : "";
                
                EditorGUILayout.LabelField($"{id}  [{s}{lockBadge}]", GUILayout.Width(280));
                if (GUILayout.Button("Met", GUILayout.Width(60)))
                    TryTransition(id, CharacterRosterState.Met);
                if (GUILayout.Button("Recruitable", GUILayout.Width(90)))
                    TryTransition(id, CharacterRosterState.Recruitable);
                if (GUILayout.Button("Recruit", GUILayout.Width(70)))
                    SafeRecruit(id);
                if (GUILayout.Button("→Active", GUILayout.Width(70)))
                    _party.TrySetActive(id, _party.State.activeOrder.Count);
                if (GUILayout.Button("→Reserve", GUILayout.Width(80)))
                    _party.MoveToReserve(id);
                if (GUILayout.Button("Guest", GUILayout.Width(60)))
                    TryTransition(id, CharacterRosterState.Guest);
                if (_party.IsLocked(id))
                {
                    if (GUILayout.Button("Unlock", GUILayout.Width(70))) _party.Unlock(id);
                }
                else
                {
                    if (GUILayout.Button("Lock", GUILayout.Width(60))) _party.Lock(id);
                }
                if (GUILayout.Button("Unavail", GUILayout.Width(70)))
                    TryTransition(id, CharacterRosterState.Unavailable);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Active party (cap = " + EffectiveCap() + ")", EditorStyles.boldLabel);
            foreach (var id in _party.State.activeOrder) EditorGUILayout.LabelField("  • " + id);

            EditorGUILayout.LabelField("Reserve party", EditorStyles.boldLabel);
            foreach (var id in _party.State.reserveOrder) EditorGUILayout.LabelField("  • " + id);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scope stack (top = active)", EditorStyles.boldLabel);
            var arr = _party.State.scopeStack.ToArray();
            for (int i = 0; i < arr.Length; i++)
                EditorGUILayout.LabelField($"  [{i}] {arr[i].scope.scopeId} (max={arr[i].scope.maxActiveMembers})");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Push Sample Scope: hero-only, cap=1"))
            {
                _party.PushScope("sample_hero_only", new[] { "char_hero" }, System.Array.Empty<string>(), System.Array.Empty<string>(), 1);
            }
            if (GUILayout.Button("Pop Scope")) _party.PopScope();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Save / Load", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(_save == null))
            {
                if (GUILayout.Button("Run Save Round-Trip (slot 0)"))
                    _lastResult = RunSaveRoundTrip();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last result", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(_lastResult, GUILayout.MinHeight(80));

            EditorGUILayout.EndScrollView();
        }

        private void TryTransition(string id, CharacterRosterState next)
        {
            try { _party.SetState(id, next); _lastResult = $"OK: {id} → {next}"; }
            catch (System.Exception e) { _lastResult = $"REJECTED: {id} → {next}: {e.Message}"; }
        }

        private void SafeRecruit(string id)
        {
            try { _party.Recruit(id); _lastResult = $"Recruited {id}"; }
            catch (System.Exception e) { _lastResult = $"Recruit failed for {id}: {e.Message}"; }
        }

        private int EffectiveCap()
        {
            if (_party.State.scopeStack.Count > 0)
                return _party.State.scopeStack.Peek().scope.maxActiveMembers;
            return _party.State.maxActiveMembers;
        }

        private string RunSaveRoundTrip()
        {
            if (_save == null) return "No SaveSystemCore (SaveFileConfig missing).";

            // Snapshot
            var preActive = new List<string>(_party.State.activeOrder);
            var preReserve = new List<string>(_party.State.reserveOrder);
            var preRoster = new Dictionary<string, CharacterRosterState>(_party.State.stateByCharacterId);
            int preCap = _party.State.maxActiveMembers;
            int preStackDepth = _party.State.scopeStack.Count;

            if (!_save.Save(0)) return "Save returned false.";

            // Mutate before load
            _party.State.activeOrder.Clear();
            _party.State.reserveOrder.Clear();
            _party.State.stateByCharacterId.Clear();
            _party.State.scopeStack.Clear();

            if (!_save.Load(0)) return "Load returned false.";

            bool match = ListsEqual(preActive, _party.State.activeOrder)
                      && ListsEqual(preReserve, _party.State.reserveOrder)
                      && preCap == _party.State.maxActiveMembers
                      && preStackDepth == _party.State.scopeStack.Count
                      && DictEqual(preRoster, _party.State.stateByCharacterId);

            return match
                ? $"Round-trip PASSED. active={preActive.Count}, reserve={preReserve.Count}, scopes={preStackDepth}"
                : $"Round-trip FAILED. active={string.Join(",", _party.State.activeOrder)}; reserve={string.Join(",", _party.State.reserveOrder)}; scopes={_party.State.scopeStack.Count}";
        }

        private static bool ListsEqual<T>(List<T> a, List<T> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (!Equals(a[i], b[i])) return false;
            return true;
        }
        private static bool DictEqual(Dictionary<string, CharacterRosterState> a, Dictionary<string, CharacterRosterState> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var kv in a) if (!b.TryGetValue(kv.Key, out var v) || v != kv.Value) return false;
            return true;
        }
    }
}
