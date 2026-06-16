using System;
using System.Collections.Generic;

namespace JRPG.Core
{
    public sealed class ServiceRegistry : IServiceRegistry
    {
        private readonly Dictionary<Type, object> _services = new();

        public void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            var t = typeof(T);

            if (_services.ContainsKey(t))
            {
                throw new InvalidOperationException($"Service '{t.Name}' is already registered.");
            }

            _services[t] = service;
        }

        public T Resolve<T>() where T : class
        {
            var t = typeof(T);

            if (!_services.TryGetValue(t, out var s))
            {
                throw new InvalidOperationException($"Service '{t.Name}' is not registered.");
            }

            return (T)s;
        }

        public bool TryResolve<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var s))
            {
                service = (T)s;
                return true;
            }
            
            service = null;
            return false;
        }
    }
}
