using System;
using System.Collections.Generic;

namespace JRPG.Core
{
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, Delegate> _handlers = new();

        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var t = typeof(T);

            _handlers[t] = _handlers.TryGetValue(t, out var existing)
                ? Delegate.Combine(existing, handler)
                : handler;
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;

            var t = typeof(T);

            if (!_handlers.TryGetValue(t, out var existing)) return;

            var remaining = Delegate.Remove(existing, handler);

            if (remaining == null)
            {
                _handlers.Remove(t);
            }

            else 
            {
                _handlers[t] = remaining;
            }
        }

        public void Publish<T>(T evt) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var del)) return;

            // Snapshot the invocation list so handlers can subscribe/unsubscribe during dispatch.
            var invocations = del.GetInvocationList();
            
            for (int i = 0; i < invocations.Length; i++)
            {
                ((Action<T>)invocations[i]).Invoke(evt);
            }
        }
    }
}
