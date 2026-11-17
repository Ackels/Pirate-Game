using System;
using UnityEngine;

namespace Pirates.Progression
{
    /// <summary>Cores à venda no estaleiro para pintar o navio (16/11). A primeira "original" de cada parte é a do kit.</summary>
    [CreateAssetMenu(menuName = "Pirates/Pintura do navio", fileName = "Navio_Pinturas")]
    public class LiveryCatalog : ScriptableObject
    {
        public LiveryOption[] options = Array.Empty<LiveryOption>();

        public LiveryOption Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var o in options)
                if (o != null && o.id == id) return o;
            return null;
        }

        public LiveryOption Original(LiverySlot slot)
        {
            foreach (var o in options)
                if (o != null && o.slot == slot && o.original) return o;
            return null;
        }
    }
}
