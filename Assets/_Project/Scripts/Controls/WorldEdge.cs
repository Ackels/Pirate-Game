using System;
using System.Collections;
using Pirates.Core;
using Pirates.Ships;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// Borda do mundo (12/11, análise de 10/11): antes dava para navegar para sempre no mar vazio. Um círculo em volta do mapa: perto da borda
    /// sobe uma névoa e o aviso; passando dela, a tela escurece e o navio volta para dentro, virado para o centro (como a névoa da Ilha
    /// Amaldiçoada). A névoa é um emissor só, que fica no ponto da borda mais perto do navio.
    /// </summary>
    public class WorldEdge : MonoBehaviour
    {
        [SerializeField] PlayerModeSwitcher switcher;
        [Header("Tamanho do mundo (mude aqui: ilha nova longe, inimigo novo no mar)")]
        [Tooltip("Navio a esta distância do centro (a posição deste objeto) = perdido na névoa, volta para dentro. O círculo aparece na janela "
            + "Scene com o objeto selecionado.")]
        [SerializeField] float radius = 1350f;
        [Tooltip("O aviso aparece a esta distância antes da borda.")]
        [SerializeField] float warnMargin = 110f;
        [Tooltip("O navio reaparece a esta distância para dentro da borda, virado para o centro.")]
        [SerializeField] float returnMargin = 180f;
        [Tooltip("A névoa aparece com o navio a esta distância da borda.")]
        [SerializeField] float fogShowDistance = 450f;
        [SerializeField] ParticleSystem fog;
        [SerializeField] float fadeInSeconds = 0.7f, holdSeconds = 0.6f, fadeOutSeconds = 1f;

        float fogRate, fogHeight;
        Coroutine lost;

        public static WorldEdge Current { get; private set; }

        public float Radius => radius;
        float WarnRadius => radius - warnMargin;
        float ReturnRadius => radius - returnMargin;
        public Vector3 Center => transform.position;
        public bool Warning { get; private set; }
        public float Fade { get; private set; }
        public bool IsLost => lost != null;
        public event Action Lost;

        public void Configure(PlayerModeSwitcher modeSwitcher, ParticleSystem edgeFog, float edgeRadius)
        {
            switcher = modeSwitcher;
            fog = edgeFog;
            radius = edgeRadius;
        }

        void Awake()
        {
            Current = this;
            if (fog)
            {
                fogRate = fog.emission.rateOverTimeMultiplier;
                fogHeight = fog.transform.localPosition.y;
                // Sem névoa pronta no começo: o emissor fica no centro do mapa (ao lado do porto) até o navio chegar perto da borda.
                var main = fog.main;
                main.prewarm = false;
                fog.Clear();
            }
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void OnDisable()
        {
            Warning = false;
            Fade = 0f;
            lost = null;
        }

        void Update()
        {
            var ship = switcher && !switcher.IsOnFoot ? switcher.Ship : null;
            if (!ship)
            {
                Warning = false;
                SetFog(0f, Vector3.forward);
                return;
            }
            var offset = ship.transform.position - Center;
            offset.y = 0f;
            float distance = offset.magnitude;
            var dir = distance > 0.01f ? offset / distance : Vector3.forward;
            Warning = distance > WarnRadius;
            SetFog(Mathf.InverseLerp(radius - fogShowDistance, radius - 60f, distance), dir);
            if (distance > radius && lost == null) lost = StartCoroutine(GetLost(ship, dir));
        }

        void SetFog(float k, Vector3 dir)
        {
            if (!fog) return;
            var e = fog.emission;
            e.rateOverTimeMultiplier = fogRate * k;
            if (k <= 0f) return;
            fog.transform.SetPositionAndRotation(Center + dir * (radius - 20f) + Vector3.up * fogHeight, Quaternion.LookRotation(dir, Vector3.up));
        }

        IEnumerator GetLost(ShipController ship, Vector3 dir)
        {
            for (float t = 0f; t < fadeInSeconds; t += Time.unscaledDeltaTime)
            {
                Fade = t / fadeInSeconds;
                yield return null;
            }
            Fade = 1f;
            if (ship)
            {
                var spot = Center + dir * ReturnRadius;
                spot.y = ship.transform.position.y;
                ship.Teleport(spot, Quaternion.LookRotation(-dir, Vector3.up));
                if (Services.TryGet(out ICameraFollow cam)) cam.SnapToTarget();
            }
            Lost?.Invoke();
            for (float t = 0f; t < holdSeconds; t += Time.unscaledDeltaTime) yield return null;
            for (float t = 0f; t < fadeOutSeconds; t += Time.unscaledDeltaTime)
            {
                Fade = 1f - t / fadeOutSeconds;
                yield return null;
            }
            Fade = 0f;
            lost = null;
        }

#if UNITY_EDITOR
        // A borda, o aviso e a volta desenhados na janela Scene (selecione o objeto "BordaDoMundo").
        void OnDrawGizmosSelected()
        {
            Circle(radius, new Color(1f, 0.3f, 0.2f));
            Circle(WarnRadius, new Color(1f, 0.8f, 0.2f));
            Circle(ReturnRadius, new Color(0.3f, 0.9f, 0.4f));
        }

        void Circle(float r, Color color)
        {
            Gizmos.color = color;
            Vector3 c = Center, last = c + new Vector3(r, 0f, 0f);
            for (int i = 1; i <= 128; i++)
            {
                float a = i * Mathf.PI * 2f / 128f;
                var p = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                Gizmos.DrawLine(last, p);
                last = p;
            }
        }
#endif

        /// <summary>Sandbox e testes: o navio se perde agora.</summary>
        public void LoseNow()
        {
            if (lost != null || !switcher || !switcher.Ship) return;
            var offset = switcher.Ship.transform.position - Center;
            offset.y = 0f;
            lost = StartCoroutine(GetLost(switcher.Ship, offset.sqrMagnitude > 0.01f ? offset.normalized : Vector3.forward));
        }
    }
}
