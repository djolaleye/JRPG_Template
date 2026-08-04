using System;

namespace JRPG.Services
{
    /// <summary>
    /// Owns loading/unloading the additive <i>content</i> scene on top of the always-resident Bootstrap scene. Phase 12.2 supplies the concrete,
    /// coroutine-driven implementation; this interface exists now so
    /// <see cref="ISessionService"/> can be written against it and degrade gracefully while it is
    /// still absent from the registry.
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
    }
}
