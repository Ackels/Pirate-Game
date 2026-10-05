using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pirates.Core
{
    /// <summary>
    /// Avisos entre módulos sem um conhecer o outro (ex.: navio afundado → missões). Quem publica não sabe quem ouve.
    /// Eventos são structs em GameEvents.cs. Assine no OnEnable e saia no OnDisable.
    /// </summary>
    public static class EventBus
    {
        static readonly Dictionary<Type, Delegate> handlers = new();

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            handlers.TryGetValue(typeof(T), out var current);
            handlers[typeof(T)] = Delegate.Combine(current, handler);
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (!handlers.TryGetValue(typeof(T), out var current)) return;
            var left = Delegate.Remove(current, handler);
            if (left == null) handlers.Remove(typeof(T));
            else handlers[typeof(T)] = left;
        }

        public static void Publish<T>(T evt) where T : struct
        {
            if (handlers.TryGetValue(typeof(T), out var current)) ((Action<T>)current).Invoke(evt);
        }

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => handlers.Clear();
    }
}
