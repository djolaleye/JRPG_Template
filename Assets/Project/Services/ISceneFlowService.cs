using System;

namespace JRPG.Services
{
    /// <summary>
    /// Owns loading/unloading the additive <i>content</i> scene on top of the always-resident Startup
    /// scene. <c>SceneFlowService</c>, a MonoBehaviour on that scene, is the concrete coroutine-driven
    /// implementation and registers itself. Callers still degrade gracefully when it is absent from the
    /// registry, since a scene opened directly in the editor will not have it.
    ///
    /// Deliberate constraints:
    /// <list type="bullet">
    /// <item>No <c>UnityEngine.SceneManagement</c> types appear in any signature — JRPG.Services
    /// references JRPG.Core and nothing else, and callers should never need a <c>Scene</c> handle.</item>
    /// <item>Completion is reported through an optional <see cref="Action"/> callback rather than a
    /// return value, because the implementation is asynchronous (<c>LoadSceneAsync</c>). Callers
    /// that must sequence work after the scene exists — notably
    /// <see cref="ISessionService.LoadGame"/>, which cannot restore the player payload until the
    /// scene's save contributors have registered — put that work in the callback.</item>
    /// <item>Every callback is invoked exactly once, on the main thread. An implementation that
    /// completes synchronously (e.g. the scene is already current) must still invoke it.</item>
    /// </list>
    /// </summary>
    public interface ISceneFlowService
    {
        /// <summary>
        /// Name of the content scene currently loaded, or <c>null</c>/empty when only the bootstrap
        /// scene is resident (i.e. we are sitting on the title screen).
        /// </summary>
        string CurrentContentScene { get; }

        /// <summary>
        /// True when <paramref name="sceneName"/> is a scene this service could actually load — i.e. it
        /// is present and enabled in Build Settings.
        ///
        /// <para>Exists because a save file records the scene it was written in, and that name can go
        /// stale: scenes get renamed, removed, or split between the save being written and being read.
        /// Callers use this to fall back to a known-good destination <i>before</i> committing to a
        /// transition, rather than discovering the problem with the screen already faded to black.</para>
        /// </summary>
        bool IsSceneAvailable(string sceneName);

        /// <summary>
        /// Loads <paramref name="sceneName"/> additively as the content scene and makes it active.
        /// Does not unload an existing content scene — use <see cref="SwapTo"/> for that.
        /// <paramref name="onComplete"/> runs after the scene is fully loaded and its objects have
        /// had their <c>OnEnable</c> called.
        /// </summary>
        void LoadContent(string sceneName, Action onComplete = null);

        /// <summary>
        /// Unloads the current content scene, leaving only the bootstrap scene resident. Safe to
        /// call when nothing is loaded (the callback still fires).
        /// </summary>
        void UnloadContent(Action onComplete = null);

        /// <summary>
        /// Unload-then-load convenience: the canonical "go to a different place" call. Equivalent to
        /// <see cref="UnloadContent"/> followed by <see cref="LoadContent"/>, with a single callback
        /// once the destination is live.
        /// </summary>
        void SwapTo(string sceneName, Action onComplete = null);

        // ---- Overlay slot -------------------------------------------------------------------------
        //
        // A second, independent scene layered *on top of* the content scene rather than replacing it.
        // Combat is the reason it exists: the battle needs its own environment while the exploration
        // scene stays resident, holding the player where they were standing.
        //
        // The overlay deliberately does not fade. A content swap owns its own cover because it is the
        // whole transition; an overlay load is one step inside a longer sequence the caller is
        // choreographing, and a fade of its own would fight that.

        /// <summary>Name of the overlay scene currently loaded, or <c>null</c>/empty when none is.</summary>
        string CurrentOverlayScene { get; }

        /// <summary>
        /// Loads <paramref name="sceneName"/> additively alongside the content scene, without
        /// unloading or touching it.
        ///
        /// <para><paramref name="makeActive"/> hands the overlay the active-scene role, which decides
        /// whose lighting settings and skybox the frame renders with — an arena lit by the world
        /// scene's environment is the symptom of getting this wrong. <see cref="UnloadOverlay"/> gives
        /// the role back to the content scene.</para>
        /// </summary>
        void LoadOverlay(string sceneName, bool makeActive = true, Action onLoaded = null);

        /// <summary>
        /// Unloads the overlay and returns the active-scene role to the content scene. Safe to call
        /// when no overlay is loaded — the callback still fires.
        /// </summary>
        void UnloadOverlay(Action onUnloaded = null);
    }
}
