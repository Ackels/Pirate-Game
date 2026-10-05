using System.Collections.Generic;
using UnityEngine;

namespace Pirates.Missions
{
    /// <summary>
    /// Lugar do mundo que uma missão aponta (ilha do acampamento, tesouro, o porto). O HUD de missões mostra o nome e a
    /// distância sobre ele enquanto houver missão ativa com este id como alvo ("porto" = voltar para entregar).
    /// </summary>
    public class WorldMarker : MonoBehaviour
    {
        static readonly List<WorldMarker> active = new();
        public static IReadOnlyList<WorldMarker> Active => active;

        [SerializeField] string markerId = "marcador";
        [SerializeField] string label = "Destino";
        [Tooltip("Altura do rótulo acima do objeto (m).")]
        [SerializeField] float height = 8f;

        public string Id => markerId;
        public string Label => label;
        public Vector3 LabelPosition => transform.position + Vector3.up * height;

        public void Configure(string id, string text, float labelHeight)
        {
            markerId = id;
            label = text;
            height = labelHeight;
        }

        public static WorldMarker Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var m in active) if (m.markerId == id) return m;
            return null;
        }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);
    }
}
