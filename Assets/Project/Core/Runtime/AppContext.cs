namespace JRPG.Core
{
    /// Static accessors for foundation singletons, populated by GameBootstrap.
    /// Other assemblies read services through here instead of taking a direct reference on JRPG.Bootstrap.
    public static class AppContext
    {
        public static IServiceRegistry Services { get; private set; }
        public static IEventBus Bus { get; private set; }
        public static GameStateController State { get; private set; }
        public static object SaveContributors { get; private set; }

        public static void Initialize(IServiceRegistry services, IEventBus bus, GameStateController state)
        {
            Services = services;
            Bus = bus;
            State = state;
        }

        /// Bootstrap calls this after constructing the SaveRegistry. The type is intentionally `object`
        /// here so JRPG.Core doesn't take a hard reference on JRPG.Save — consumers cast to SaveRegistry.

        public static void SetSaveContributors(object saveRegistry) => SaveContributors = saveRegistry;
    }
}
