using UnityEngine;

namespace JRPG.Services
{
    /// <summary>
    /// The exploration scene's presentation controls, as the battle transition needs them: freeze the
    /// player where they stand, frame what they attacked, hand the screen over, and put everything
    /// back afterwards.
    ///
    /// <para><b>Why an interface rather than direct component access.</b> The orchestrator lives on
    /// the persistent Startup scene; the player and camera live in a content scene that loads and
    /// unloads. This is the seam between them, and it keeps combat orchestration from depending on
    /// any particular exploration scene's object names.</para>
    ///
    /// <para>Input freezing is not here: setting the layered state to a non-Exploration input
    /// context already disables the exploration action map and the Cinemachine input axis controller,
    /// through <c>ExplorationInputBridge</c>.</para>
    /// </summary>
    public interface IExplorationPresentation
    {
        bool TryCapturePlayerPose(out Vector3 position, out float yaw);
        void RestorePlayerPose(Vector3 position, float yaw);

        /// Frames <paramref name="target"/> with a dedicated encounter camera, blending from wherever
        /// the follow camera currently is. Null releases the focus.
        void FocusOn(Transform target);

        void ReleaseFocus();
        void SetCameraActive(bool active);
    }

    /// <summary>
    /// Static locator for the exploration scene's presentation agent, set in <c>OnEnable</c> and
    /// cleared in <c>OnDisable</c>.
    ///
    /// <para>Same pattern, and the same reason, as <see cref="PassiveDialogueSink"/>:
    /// <c>ServiceRegistry.Register</c> throws on a duplicate and has no unregister, so a component in
    /// a scene that reloads cannot use the registry.</para>
    /// </summary>
    public static class ExplorationPresentationSink
    {
        public static IExplorationPresentation Current { get; set; }
    }
}
