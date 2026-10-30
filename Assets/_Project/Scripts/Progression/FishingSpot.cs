using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using UnityEngine;

namespace Pirates.Progression
{
    /// <summary>
    /// Cardume (D2, passo 2, 30/10; pedido do Jhonata: "lugares com bolhas, onde você para o barco para pescar com rede"): bolhas subindo,
    /// espuma e um peixe pulando de vez em quando num círculo do mar aberto. A rede do navio (<see cref="FishingNet"/>) só pesca aqui
    /// dentro e tira os peixes do estoque dele; vazio, as bolhas param e o <see cref="FishingGrounds"/> leva o cardume para outro lugar.
    /// Os efeitos só rodam com a câmera perto.
    /// </summary>
    public class FishingSpot : MonoBehaviour
    {
        [SerializeField] float radius = 14f;
        [Tooltip("Bolhas, espuma (criados pelo gerador): tocam só com a câmera perto e param com o cardume vazio.")]
        [SerializeField] ParticleSystem[] effects = System.Array.Empty<ParticleSystem>();
        [Tooltip("Peixes que pulam para fora da água (modelos, desligados; o comprido de cada um no eixo Z).")]
        [SerializeField] Transform[] jumpers = System.Array.Empty<Transform>();
        [SerializeField] Vector2Int stockRange = new(8, 14);
        [Tooltip("Um peixe pula a cada tantos segundos (s), com a câmera perto.")]
        [SerializeField] Vector2 jumpEvery = new(1.2f, 3.2f);
        [Tooltip("Salto: altura e comprimento (m) e quanto dura (s).")]
        [SerializeField] Vector2 jumpHeight = new(1.2f, 2.2f), jumpLength = new(2f, 3.8f), jumpSeconds = new(0.8f, 1.05f);
        [Tooltip("Câmera mais longe que isto (m): sem efeitos.")]
        [SerializeField] float effectsRange = 170f;

        static readonly List<FishingSpot> active = new();

        /// <summary>Cardumes no mar agora (ligados).</summary>
        public static IReadOnlyList<FishingSpot> Active => active;

        public float Radius => radius;
        /// <summary>Peixes que ainda dá para tirar daqui.</summary>
        public int Stock { get; private set; }
        public bool Empty => Stock <= 0;
        /// <summary>A rede está na água aqui (o cardume não muda de lugar).</summary>
        public bool InUse { get; set; }
        public float BornAt { get; private set; }

        IOceanSampler ocean;
        Camera cam;
        float nextJump, nextCheck;
        bool playing;
        // Salto em andamento: qual peixe, de onde, para onde, quão alto, quando começou e quanto dura.
        Transform jumper;
        Vector3 jumpFrom, jumpDir;
        float jumpLen, jumpUp, jumpStart, jumpTime, jumpSpin;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();

        public void Configure(float spotRadius, ParticleSystem[] fx, Transform[] jumpingFish)
        {
            radius = spotRadius;
            effects = fx;
            jumpers = jumpingFish;
        }

        void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
            playing = false;
            nextCheck = 0f;
        }

        void OnDisable()
        {
            active.Remove(this);
            InUse = false;
            EndJump();
        }

        /// <summary>Cardume novo neste ponto (no nível do mar), com estoque sorteado.</summary>
        public void Spawn(Vector3 at)
        {
            at.y = 0f;
            transform.position = at;
            Stock = Random.Range(stockRange.x, stockRange.y + 1);
            BornAt = Time.time;
            InUse = false;
            gameObject.SetActive(true);
            playing = false;
            nextCheck = 0f;
            nextJump = Time.time + Random.Range(jumpEvery.x, jumpEvery.y);
        }

        /// <summary>Tira um peixe do cardume (falso se acabou).</summary>
        public bool Take()
        {
            if (Stock <= 0) return false;
            Stock--;
            if (Stock == 0) SetEffects(false);
            return true;
        }

        /// <summary>O ponto está dentro do círculo (no plano).</summary>
        public bool Contains(Vector3 point) => FlatDistance(point) <= radius;

        public float FlatDistance(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        void Update()
        {
            if (ocean == null) Services.TryGet(out ocean);
            if (ocean != null)
            {
                var p = transform.position;
                p.y = ocean.GetHeight(p);
                transform.position = p;
            }
            if (Time.time >= nextCheck)
            {
                nextCheck = Time.time + 0.5f;
                if (!cam) cam = Camera.main;
                bool near = cam && (cam.transform.position - transform.position).sqrMagnitude < effectsRange * effectsRange;
                SetEffects(near && !Empty);
            }
            if (jumper)
            {
                UpdateJump();
                return;
            }
            if (!playing || Time.time < nextJump) return;
            nextJump = Time.time + Random.Range(jumpEvery.x, jumpEvery.y);
            StartJump();
        }

        /// <summary>Um peixe sai da água num ponto do cardume, em arco para um lado qualquer (respingo na saída).</summary>
        void StartJump()
        {
            if (jumpers.Length == 0) return;
            jumper = jumpers[Random.Range(0, jumpers.Length)];
            if (!jumper) return;
            var ring = Random.insideUnitCircle * radius * 0.8f;
            jumpFrom = transform.position + new Vector3(ring.x, 0f, ring.y);
            if (ocean != null) jumpFrom.y = ocean.GetHeight(jumpFrom);
            float angle = Random.Range(0f, Mathf.PI * 2f);
            jumpDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            jumpLen = Random.Range(jumpLength.x, jumpLength.y);
            jumpUp = Random.Range(jumpHeight.x, jumpHeight.y);
            jumpTime = Random.Range(jumpSeconds.x, jumpSeconds.y);
            jumpSpin = Random.Range(-1f, 1f);
            jumpStart = Time.time;
            jumper.gameObject.SetActive(true);
            Splash(jumpFrom, 0.9f);
            UpdateJump();
        }

        /// <summary>No arco: o corpo vira para onde vai (nariz para cima na subida, para baixo na descida) e se torce um pouco.</summary>
        void UpdateJump()
        {
            float t = (Time.time - jumpStart) / jumpTime;
            if (t >= 1f)
            {
                var end = jumpFrom + jumpDir * jumpLen;
                if (ocean != null) end.y = ocean.GetHeight(end);
                Splash(end, 1.1f);
                EndJump();
                return;
            }
            jumper.position = jumpFrom + jumpDir * (jumpLen * t) + Vector3.up * (4f * jumpUp * t * (1f - t));
            var velocity = jumpDir * jumpLen + Vector3.up * (4f * jumpUp * (1f - 2f * t));
            jumper.rotation = Quaternion.LookRotation(velocity, Vector3.up) * Quaternion.Euler(0f, 0f, jumpSpin * 70f * t);
        }

        void EndJump()
        {
            if (jumper) jumper.gameObject.SetActive(false);
            jumper = null;
        }

        static void Splash(Vector3 point, float size)
        {
            if (Services.TryGet(out CombatEffects fx)) fx.Plop(point, size);
        }

        void SetEffects(bool on)
        {
            if (on == playing) return;
            playing = on;
            foreach (var ps in effects)
            {
                if (!ps) continue;
                if (on) ps.Play(true);
                else ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
