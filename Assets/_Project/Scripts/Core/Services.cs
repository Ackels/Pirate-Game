using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pirates.Core
{
    /// <summary>
    /// Registro mínimo de serviços globais (oceano, depois inventário, missões...).
    /// Sistemas se registram no OnEnable e saem no OnDisable.
    /// </summary>
    public static class Services
    {
        static readonly Dictionary<Type, object> registry = new();

        public static void Register<T>(T service) where T : class => registry[typeof(T)] = service;

        public static void Unregister<T>(T service) where T : class
        {
            if (registry.TryGetValue(typeof(T), out var current) && ReferenceEquals(current, service))
                registry.Remove(typeof(T));
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (registry.TryGetValue(typeof(T), out var obj) && obj is T typed)
            {
                service = typed;
                return true;
            }
            service = null;
            return false;
        }

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => registry.Clear();
    }
}
