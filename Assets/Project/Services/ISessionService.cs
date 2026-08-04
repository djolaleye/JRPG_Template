namespace JRPG.Services
{
    /// <summary>
    /// The session lifecycle authority: starting a brand-new game, restoring a saved one, and
    /// tearing the session back down to the title screen.
    ///
    /// Every gameplay service establishes its starting state in its
    /// <i>constructor</i>, and <c>ServiceRegistry.Register</c> throws on a duplicate registration —
    /// so "return to title, then New Game" can never re-run bootstrap. Instead each stateful
    /// service exposes a <c>ResetForNewGame()</c> that restores its own session-zero state in
    /// place, and the concrete session service (JRPG.Bootstrap, the only assembly allowed to see
    /// every domain) sequences those calls in dependency order.
    ///
    /// Scene work is delegated to <see cref="ISceneFlowService"/> and is therefore asynchronous:
    /// <see cref="NewGame"/> and <see cref="LoadGame"/> <b>start</b> the transition and return; they
    /// do not block until the world is playable.
    /// </summary>
    public interface ISessionService
    {
        /// <summary>
        /// True between a successful <see cref="NewGame"/>/<see cref="LoadGame"/> and the next
        /// <see cref="ReturnToTitle"/>. Menus use it to decide whether "Continue"/"Save" are
        /// meaningful. It flips as soon as the session is committed, which is before the content
        /// scene has finished loading.
        /// </summary>
        bool IsSessionActive { get; }

        /// <summary>
        /// Discards all session state, re-bakes the authored starting inventory, and requests the
        /// new-game content scene.
        /// </summary>
        void NewGame();

        /// <summary>
        /// Begins restoring <paramref name="slot"/>. Returns false when the slot is empty/unreadable
        /// or the save service is unavailable — i.e. when nothing was started. A <c>true</c> return
        /// means "the load was accepted and is in flight"; when a scene load is involved the
        /// contributor restore happens after the scene is live, so success of the restore itself is
        /// reported through the save system's own <c>GameLoaded</c> event.
        /// </summary>
        bool LoadGame(int slot);

        /// <summary>
        /// Closes every open menu, returns the layered state to the title screen, and unloads the
        /// content scene. Session state is intentionally left as-is: the next
        /// <see cref="NewGame"/>/<see cref="LoadGame"/> overwrites it wholesale.
        /// </summary>
        void ReturnToTitle();
    }
}
