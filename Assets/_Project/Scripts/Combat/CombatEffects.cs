using Pirates.Core;
using UnityEngine;

namespace Pirates.Combat
{
    /// <summary>
    /// Avisa os momentos visíveis/audíveis do combate naval: boca do canhão disparou (posição e direção do tiro), bala caiu
    /// na água, bala acertou algo (posição e normal da superfície). Quem desenha (Pirates.VFX) e quem toca (Pirates.Audio)
    /// só ouvem estes eventos; o combate não conhece nenhum dos dois.
    /// </summary>
    public class CombatEffects : MonoBehaviour
    {
        /// <summary>Posição da boca do canhão e direção do tiro.</summary>
        public event System.Action<Vector3, Vector3> Blasted;
        /// <summary>Ponto da água onde a bala caiu.</summary>
        public event System.Action<Vector3> Splashed;
        /// <summary>Ponto e normal da superfície atingida.</summary>
        public event System.Action<Vector3, Vector3> Impacted;
        /// <summary>Explosão (barril de pólvora): centro e raio.</summary>
        public event System.Action<Vector3, float> Exploded;
        /// <summary>Terra revolvida (esqueleto amaldiçoado saindo da cova = true; virando pó ao amanhecer = false): ponto no chão.</summary>
        public event System.Action<Vector3, bool> Unearthed;
        /// <summary>Água estourando para cima (tentáculo ou cabeça da lula rompendo a superfície, tentáculo batendo no mar): ponto e
        /// tamanho (1 = um tentáculo).</summary>
        public event System.Action<Vector3, float> Surged;
        /// <summary>Urro de monstro marinho (a lula gigante): ponto.</summary>
        public event System.Action<Vector3> Roared;
        /// <summary>Respingo pequeno na água (a boia da pesca caindo, o peixe mordendo, o peixe saindo): ponto e tamanho (1 = a boia).</summary>
        public event System.Action<Vector3, float> Plopped;

        void OnEnable() => Services.Register(this);
        void OnDisable() => Services.Unregister(this);

        public void Splash(Vector3 position) => Splashed?.Invoke(position);

        public void MuzzleBlast(Vector3 position, Vector3 direction) => Blasted?.Invoke(position, direction);

        public void Impact(Vector3 position, Vector3 normal) => Impacted?.Invoke(position, normal);

        public void Explosion(Vector3 center, float radius) => Exploded?.Invoke(center, radius);

        public void Unearth(Vector3 point, bool rising) => Unearthed?.Invoke(point, rising);

        public void Surge(Vector3 point, float size) => Surged?.Invoke(point, size);

        public void Roar(Vector3 point) => Roared?.Invoke(point);

        public void Plop(Vector3 point, float size) => Plopped?.Invoke(point, size);
    }
}
