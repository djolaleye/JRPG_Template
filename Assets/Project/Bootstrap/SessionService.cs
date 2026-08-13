using System;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Services;
using JRPG.Party;
using JRPG.Inventory;
using JRPG.Progression;
using JRPG.Dialogue;
using JRPG.Combat;

namespace JRPG.Bootstrap
{
    /// <summary>
    /// Concrete <see cref="ISessionService"/>.
    ///
    /// This class owns no game state of its own beyond
    /// <see cref="IsSessionActive"/>. Each service knows how to return itself to session zero via
    /// its own <c>ResetForNewGame()</c>; all this type contributes is the order those calls
    /// happen in, and the scene/state choreography around them.
    /// </summary>
    public sealed class SessionService : ISessionService
    {
        private readonly IServiceRegistry _services;
        private readonly GameStateController _state;
        private readonly DataRegistry _data;

        private readonly StoryStateService _story;
        private readonly InventoryService _inventory;
        private readonly EquipmentManager _equipment;
        private readonly PartyService _party;
        private readonly ProgressionService _progression;

        private readonly StartingInventoryConfig _startingInventory;
        private readonly string _newGameSceneName;
        private readonly string _titleSceneName;

        public bool IsSessionActive { get; private set; }

        /// <param name="services">
        /// Resolved from, not just read at construction: <see cref="ISceneFlowService"/>,
        /// <see cref="ISaveService"/> and <see cref="IMenuService"/> are all looked up lazily on
        /// each call.
        /// </param>
        /// <param name="newGameSceneName">Content scene a fresh game starts in.</param>
        /// <param name="titleSceneName">Content scene <see cref="ReturnToTitle"/> returns to.</param>
        public SessionService(IServiceRegistry services,
                              GameStateController state,
                              DataRegistry data,
                              StoryStateService story,
                              InventoryService inventory,
                              EquipmentManager equipment,
                              PartyService party,
                              ProgressionService progression,
                              StartingInventoryConfig startingInventory,
                              string newGameSceneName,
                              string titleSceneName)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _story = story ?? throw new ArgumentNullException(nameof(story));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            _party = party ?? throw new ArgumentNullException(nameof(party));
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));

            // Optional: a project with no authored starting kit is legal, the baker no-ops on null.
            _startingInventory = startingInventory;
            _newGameSceneName = newGameSceneName;
            _titleSceneName = titleSceneName;
        }

        // ---- New game -------------------------------------------------------------------------

        public void NewGame()
        {
            if (string.IsNullOrEmpty(_newGameSceneName))
                Debug.LogWarning("[JRPG.Session] NewGame: no new-game scene name configured on GameBootstrap.");

            ResetAllServices();

            // Re-bake after the inventory reset. Passing the equipment manager re-applies the authored
            // starting gear too — ResetAllServices unequipped everything, so a second New Game must put
            // it back rather than leaving the protagonist stripped of kit a first New Game gives them.
            StartingInventoryBaker.Bake(_startingInventory, _inventory.Container, _data, _equipment);

            // Session is committed here, before the (asynchronous) scene load finishes — a
            // caller that queries IsSessionActive from the scene-loaded callback must see true.
            IsSessionActive = true;

            EnterContentScene(_newGameSceneName, "NewGame", onSceneReady: null);
        }

        /// <summary>
        /// Returns every stateful service to its session-zero condition.
        ///
        /// Mirrors GameBootstrap's construction order.
        ///
        /// 1. <b>Story</b> — first: it doubles as the
        ///    <c>IRecruitmentConditionEvaluator</c> PartyService consults. Clearing flags before the
        ///    roster is re-seeded means no recruitment condition can evaluate against last
        ///    session's flags.
        ///
        /// 2. <b>Inventory</b> — before equipment (EquipmentManager is handed
        ///    <c>inventory.Container</c>). Unequip returns gear to the inventory; ordering the
        ///    inventory wipe first guarantees nothing can be handed back into the fresh container.
        ///
        /// 3. <b>Equipment</b> — after inventory, before party. Equipment's
        ///    <c>equip:*</c> stat modifiers live on the character runtime instances, not in the
        ///    equipment table, so the removal must run while PartyService still holds those
        ///    instances.
        ///
        /// 4. <b>Party</b> — after equipment, before progression. This is the step that drops the
        ///    runtime-instance cache and re-seeds the protagonist to Active.
        ///
        /// 5. <b>Progression</b> — last. Progression is constructed with the
        ///    party service and registered as a save contributor after it, so it can re-stamp
        ///    level/XP onto instances party rebuilds). <c>GetProgressForCharacter</c> seeds a record
        ///    from the live instance, so resetting progression before party would let a stale
        ///    level/XP be copied into the "cleared" bookkeeping.
        /// </summary>
        private void ResetAllServices()
        {
            _story.ResetForNewGame();
            _inventory.ResetForNewGame();
            _equipment.ResetForNewGame();
            _party.ResetForNewGame();
            _progression.ResetForNewGame();

            // World state holds only encounter ids and enum values, so it reads nothing above and
            // nothing above reads it — resolved lazily rather than added to the constructor.
            if (_services.TryResolve<IWorldStateService>(out var world)) world.ResetForNewGame();

            ResetTransientSessionServices();
        }

        /// <summary>
        /// Clears session state held by services that are neither save contributors nor constructor
        /// dependencies here, and which therefore have nothing else to reset them at a session
        /// boundary.
        ///
        /// Order-independent — none of these read each other.
        /// </summary>
        private void ResetTransientSessionServices()
        {
            // Combat keeps _lastRequest (so a defeat can be retried verbatim) and the enemy AI's
            // scripted-sequence cursors / RNG streams. Left alone, a fresh session's first boss
            // resumes the previous session's script cursor.
            if (_services.TryResolve<ICombatService>(out var combat) && combat is CombatService concreteCombat)
                concreteCombat.ResetForNewGame();

            // An in-flight conversation would otherwise survive into the new session.
            if (_services.TryResolve<IDialogueService>(out var dialogue) && dialogue is DialogueService concreteDialogue)
                concreteDialogue.ResetForNewGame();

            // Play time is adopted from a loaded save and never otherwise lowered, so without this a
            // New Game started after loading a long save inherits that save's clock.
            if (_services.TryResolve<ISaveService>(out var save) && save is JRPG.Save.SaveSystemCore concreteSave)
                concreteSave.ResetPlayTime();
        }

        // ---- Load game ------------------------------------------------------------------------

        public bool LoadGame(int slot)
        {
            if (!_services.TryResolve<ISaveService>(out var save) || save == null)
            {
                Debug.LogWarning($"[JRPG.Session] LoadGame(slot {slot}) rejected — no ISaveService registered " +
                                 "(SaveFileConfig is probably unassigned on GameBootstrap).");
                return false;
            }

            var info = save.GetSlotInfo(slot);
            if (!info.exists)
            {
                Debug.LogWarning($"[JRPG.Session] LoadGame(slot {slot}) rejected — slot is empty or unreadable.");
                return false;
            }

            var sceneName = info.sceneId;
            if (string.IsNullOrEmpty(sceneName))
            {
                // Legacy saves predate scene metadata; SaveSlotInfo documents "" as "unknown".
                sceneName = _newGameSceneName;
                Debug.LogWarning($"[JRPG.Session] LoadGame(slot {slot}): save carries no sceneId — " +
                                 $"falling back to '{sceneName}'.");
            }
            else if (TryGetSceneFlow(out var flowCheck) && !flowCheck.IsSceneAvailable(sceneName))
            {
                // The recorded scene no longer exists — renamed, removed, or dropped from Build
                // Settings since the save was written. The rest of the payload (party, inventory,
                // progression, story flags) is still perfectly good, so recover into the default world
                // rather than refusing the load outright. Only the player's position is lost.
                Debug.LogWarning($"[JRPG.Session] LoadGame(slot {slot}): recorded scene '{sceneName}' is no " +
                                 $"longer in Build Settings — loading '{_newGameSceneName}' instead. The " +
                                 "player's saved position will not be restored.");
                sceneName = _newGameSceneName;
            }

            // ORDER IS LOAD-BEARING: scene first, restore second.
            //
            // PlayerSaveAgent registers itself as the "player" save contributor in OnEnable, i.e.
            // only once its scene is loaded. SaveSystemCore.Load iterates the contributors that are
            // registered at that instant, so calling Load before the world scene exists silently
            // skips the player payload — no error, the character simply spawns at the scene's
            // authored default position. Loading the scene first guarantees the agent has
            // registered by the time the restore loop runs.
            if (TryGetSceneFlow(out var sceneFlow))
            {
                IsSessionActive = true;
                sceneFlow.SwapTo(sceneName, () =>
                {
                    if (!save.Load(slot))
                    {
                        // The scene is up but the payload failed — do not leave a half-session live.
                        Debug.LogError($"[JRPG.Session] LoadGame(slot {slot}): scene '{sceneName}' loaded but the " +
                                       "save payload failed to restore.");
                        IsSessionActive = false;
                        return;
                    }
                    SetExplorationState();
                });

                // "Accepted and in flight" — the restore's own success is reported via GameLoaded.
                return true;
            }

            if (!save.Load(slot)) return false;

            IsSessionActive = true;
            SetExplorationState();
            return true;
        }

        // ---- Return to title ------------------------------------------------------------------

        public void ReturnToTitle()
        {
            // Menus first: MenuService.CloseAll restores the bottom-most frame's priorState, so it
            // would stomp any state set beforehand.
            if (_services.TryResolve<IMenuService>(out var menus) && menus != null)
                menus.CloseAll();

            _state.SetState(new LayeredState(GameMode.MainMenu, OverlayState.None, InputContext.Menu));

            if (TryGetSceneFlow(out var sceneFlow))
            {
                // Swap to the title scene rather than merely unloading the world. Unloading alone would
                // leave no content scene at all — no camera, no title screen, nothing for the player to
                // do — because the Startup root deliberately holds only services and UI.
                if (!string.IsNullOrEmpty(_titleSceneName)) sceneFlow.SwapTo(_titleSceneName);
                else
                {
                    Debug.LogWarning("[JRPG.Session] ReturnToTitle: no title scene name configured on " +
                                     "GameBootstrap; unloading the world without a destination.");
                    sceneFlow.UnloadContent();
                }
            }
            else
            {
                Debug.LogWarning("[JRPG.Session] ReturnToTitle: no ISceneFlowService registered — the content " +
                                 "scene (if any) stays loaded.");
            }

            IsSessionActive = false;
        }

        // ---- Helpers --------------------------------------------------------------------------

        /// <summary>
        /// Lazy lookup of the scene loader. Lazy rather than injected because it is
        /// registered (if at all) after this service, and today not at all.
        /// </summary>
        private bool TryGetSceneFlow(out ISceneFlowService sceneFlow)
        {
            _services.TryResolve(out sceneFlow);
            return sceneFlow != null;
        }

        /// <summary>
        /// Hands the world transition to <see cref="ISceneFlowService"/> when it exists.
        ///
        /// Absent-service fallback: <c>SceneFlowService</c> registers itself from the Startup scene, so
        /// a misconfigured build or a scene opened directly in the editor can leave it missing. Rather
        /// than fail, this logs a clear warning and drops straight into the exploration layered state —
        /// the New Game flow still runs, it just does not change scenes.
        /// </summary>
        private void EnterContentScene(string sceneName, string caller, Action onSceneReady)
        {
            if (TryGetSceneFlow(out var sceneFlow))
            {
                sceneFlow.SwapTo(sceneName, () =>
                {
                    SetExplorationState();
                    onSceneReady?.Invoke();
                });
                return;
            }

            Debug.LogWarning($"[JRPG.Session] {caller}: no ISceneFlowService registered — scene '{sceneName}' was " +
                             "not loaded; entering Exploration in whatever scene is already open. " +
                             "SceneFlowService registers itself from the Startup scene — is that scene loaded?");

            SetExplorationState();
            onSceneReady?.Invoke();
        }

        /// The canonical "we are now playing" layered state. Matches GameBootstrap's interim
        /// autoStartExploration transition so both entry points agree.
        private void SetExplorationState()
            => _state.SetState(new LayeredState(GameMode.Exploration, OverlayState.None, InputContext.Exploration));
    }
}
