using System;
using System.Collections;
using System.Collections.Generic;
using Pirates.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Pirates.Combat
{
    public enum BroadsideSide { Port = -1, Starboard = 1 }

    public enum BatteryState { Ready, Aiming, Reloading }

    /// <summary>
    /// Uma bateria lateral (bombordo ou estibordo). Segurar mira (a elevação sobe com o tempo),
    /// soltar dispara a salva. Jogador e IA usam a mesma API: BeginAim / ReleaseAndFire.
    /// Com mira assistida (CannonStats.aimAssist, upgrade de luneta) a distância gruda no inimigo daquele lado,
    /// a carga desacelera perto dele e os canhões giram um pouco na direção dele. Munição especial (<see cref="Ammo"/>, M9)
    /// muda dano, espalhamento, alcance e projéteis da bordada; navio com a tripulação abatida recarrega mais devagar
    /// (<see cref="ReloadMultiplier"/>).
    /// </summary>
    public class CannonBattery : MonoBehaviour
    {
        [SerializeField] CannonStats stats;
        [SerializeField] BroadsideSide side = BroadsideSide.Starboard;
        [SerializeField] Transform[] muzzles = Array.Empty<Transform>();
        [SerializeField] Rigidbody shipBody;
        [SerializeField] LineRenderer aimLine;
        [SerializeField] Transform impactMarker;
        [Tooltip("Multiplicador do coice no navio.")]
        [SerializeField] float recoilScale = 1f;

        readonly List<Vector3> arc = new();
        CannonballSystem cannonballs;
        CombatEffects effects;
        IOceanSampler ocean;

        public event Action Fired;

        public BroadsideSide Side => side;
        public BatteryState State { get; private set; } = BatteryState.Ready;
        public float AimCharge { get; private set; }
        public float ReloadRemaining { get; private set; }
        public CannonStats Stats => stats;
        public Vector3 PredictedImpact { get; private set; }
        /// <summary>Inimigo que a mira assistida está seguindo (null sem assistência ou sem alvo do lado).</summary>
        public Transform AssistTarget { get; private set; }
        /// <summary>A distância está grudada no inimigo: soltar agora acerta.</summary>
        public bool AssistLocked { get; private set; }
        /// <summary>Munição da próxima bordada (null = bala comum).</summary>
        public AmmoDefinition Ammo { get; set; }
        /// <summary>Tempo de recarga × isto (tripulação abatida pela metralha: maior que 1).</summary>
        public float ReloadMultiplier { get; set; } = 1f;
        /// <summary>Alcance máximo com a munição atual (metralha alcança menos).</summary>
        public float MaxRange => Ammo && Ammo.maxRange > 0f ? Mathf.Min(stats.maxRange, Ammo.maxRange) : stats.maxRange;
        float SpreadDegrees => stats.spreadDegrees * (Ammo ? Ammo.spreadMultiplier : 1f) * StormSpread;

        /// <summary>Tempestade (M11): o convés jogando espalha a salva (vale para todos, IA e fortes também).</summary>
        static float StormSpread => Services.TryGet(out IWeather weather) ? 1f + StormSpreadBonus * weather.Storm : 1f;

        /// <summary>Espalhamento a mais com a tempestade cheia (0,5 = +50%).</summary>
        public const float StormSpreadBonus = 0.5f;

        float assistedRange, assistYaw, assistCloseness;

        public void Configure(CannonStats cannonStats, BroadsideSide broadside, Transform[] muzzlePoints,
            Rigidbody body, LineRenderer line, Transform marker)
        {
            stats = cannonStats;
            side = broadside;
            muzzles = muzzlePoints;
            shipBody = body;
            aimLine = line;
            impactMarker = marker;
        }

        /// <summary>Troca os stats em jogo (upgrades usam uma cópia em memória; o asset não muda).</summary>
        public void SetStats(CannonStats cannonStats) => stats = cannonStats;

        void Start() => SetPreviewVisible(false);

        public bool BeginAim()
        {
            if (State != BatteryState.Ready || !stats || !isActiveAndEnabled) return false;
            State = BatteryState.Aiming;
            AimCharge = 0f;
            UpdateAssist();
            SetPreviewVisible(true);
            return true;
        }

        public void CancelAim()
        {
            if (State != BatteryState.Aiming) return;
            State = BatteryState.Ready;
            ClearAssist();
            SetPreviewVisible(false);
        }

        public void ReleaseAndFire()
        {
            if (State != BatteryState.Aiming) return;
            Fire(Elevation, assistYaw);
            ClearAssist();
        }

        /// <summary>Disparo direto a uma distância, sem fase de mira (usado pela IA).</summary>
        public bool FireAtRange(float range)
        {
            if (State != BatteryState.Ready || !stats || !isActiveAndEnabled) return false;
            Fire(SolveElevation(range, MuzzleHeight()), 0f);
            return true;
        }

        void Fire(float elevation, float yaw)
        {
            SetPreviewVisible(false);
            // Mira assistida total segue o alvo durante a salva (os canhões disparam um depois do outro).
            Rigidbody follow = null;
            if (AssistTarget && Mathf.InverseLerp(0.7f, 1f, stats.aimAssist) > 0f) AssistTarget.TryGetComponent(out follow);
            // A munição vai junto com a salva: quem consome (ShipAmmo, no Fired) pode trocar a seleção logo depois.
            StartCoroutine(FireSalvo(elevation, yaw, Ammo, follow, AimRange));
            State = BatteryState.Reloading;
            ReloadRemaining = stats.reloadTime * ReloadMultiplier;
            Fired?.Invoke();
        }

        /// <summary>Alcance (m) só pela carga, sem a mira assistida.</summary>
        float ChargedRange => Mathf.Lerp(stats.minRange, MaxRange, stats.aimRangeCurve.Evaluate(AimCharge));

        /// <summary>Alcance desejado (m): o da carga, puxado para o inimigo pela mira assistida.</summary>
        public float AimRange => AssistTarget ? assistedRange : ChargedRange;

        // O jogador controla a distância; a elevação sai dela. Controlar o ângulo direto era sensível demais:
        // em ângulos baixos, 1° a mais levava a bala uns 25 m adiante.
        float Elevation => SolveElevation(AimRange, MuzzleHeight());

        /// <summary>
        /// Segundos de voo de uma bala que cai a <paramref name="range"/> metros (mar plano), com a mesma elevação que
        /// <see cref="FireAtRange"/> usaria: a IA e os fortes antecipam o alvo por este tempo.
        /// </summary>
        public float FlightTime(float range)
        {
            if (!stats) return 0f;
            float height = MuzzleHeight();
            float e = SolveElevation(range, height) * Mathf.Deg2Rad;
            float g = -Physics.gravity.y;
            float vUp = stats.muzzleVelocity * Mathf.Sin(e);
            return (vUp + Mathf.Sqrt(vUp * vUp + 2f * g * height)) / g;
        }

        /// <summary>Centro das bocas dos canhões (de onde a bala sai).</summary>
        public Vector3 MuzzleOrigin => MuzzleCenter();

        /// <summary>Velocidade horizontal que a bala herda do navio (vai junto com ele).</summary>
        public Vector3 InheritedFlatVelocity => InheritedVelocity;

        float MuzzleHeight()
        {
            if (ocean == null) Services.TryGet(out ocean);
            Vector3 origin = MuzzleCenter();
            return Mathf.Max(0f, origin.y - (ocean != null ? ocean.GetHeight(origin) : 0f));
        }

        /// <summary>Elevação (graus) que atinge <paramref name="range"/> sobre o mar plano, partindo de <paramref name="height"/>.</summary>
        float SolveElevation(float range, float height)
        {
            float lo = stats.minElevation, hi = stats.maxElevation;
            if (FlatRange(lo, height) >= range) return lo;
            if (FlatRange(hi, height) <= range) return hi;
            for (int i = 0; i < 16; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (FlatRange(mid, height) < range) lo = mid;
                else hi = mid;
            }
            return (lo + hi) * 0.5f;
        }

        float FlatRange(float elevationDegrees, float height)
        {
            float g = -Physics.gravity.y;
            float e = elevationDegrees * Mathf.Deg2Rad;
            float vUp = stats.muzzleVelocity * Mathf.Sin(e);
            float vFlat = stats.muzzleVelocity * Mathf.Cos(e);
            float time = (vUp + Mathf.Sqrt(vUp * vUp + 2f * g * height)) / g;
            return vFlat * time;
        }

        Vector3 MuzzleCenter()
        {
            if (muzzles.Length == 0) return transform.position;
            Vector3 sum = Vector3.zero;
            foreach (Transform m in muzzles) sum += m.position;
            return sum / muzzles.Length;
        }

        void Update()
        {
            switch (State)
            {
                case BatteryState.Aiming:
                    // Perto da distância do inimigo a carga desacelera: dá tempo de soltar na hora certa.
                    float slow = Mathf.Lerp(1f, 0.3f, stats.aimAssist * assistCloseness);
                    AimCharge = Mathf.Min(1f, AimCharge + slow * Time.deltaTime / Mathf.Max(0.05f, stats.aimChargeTime));
                    UpdateAssist();
                    UpdatePreview();
                    break;
                case BatteryState.Reloading:
                    ReloadRemaining -= Time.deltaTime;
                    if (ReloadRemaining <= 0f)
                    {
                        ReloadRemaining = 0f;
                        State = BatteryState.Ready;
                    }
                    break;
            }
        }

        /// <summary>Direção de disparo: perpendicular ao casco no plano horizontal, erguida pela elevação.</summary>
        Vector3 FireDirection(float elevationDegrees, float yawOffsetDegrees = 0f)
        {
            Transform ship = shipBody ? shipBody.transform : transform;
            Vector3 sideways = Vector3.ProjectOnPlane(ship.right * (int)side, Vector3.up).normalized;
            sideways = Quaternion.AngleAxis(yawOffsetDegrees, Vector3.up) * sideways;
            float e = elevationDegrees * Mathf.Deg2Rad;
            return sideways * Mathf.Cos(e) + Vector3.up * Mathf.Sin(e);
        }

        // Só a componente horizontal: herdar o sobe-e-desce das ondas deixava a mira instável (±0,8° de elevação).
        Vector3 InheritedVelocity => shipBody ? Vector3.ProjectOnPlane(shipBody.linearVelocity, Vector3.up) : Vector3.zero;

        /// <param name="follow">Alvo da mira assistida total (null = sem): cada canhão gira o quanto o alvo andou desde o primeiro
        /// tiro (a salva leva até ~1 s; sem isso, contra navio cruzando, os últimos tiros passavam atrás dele).</param>
        IEnumerator FireSalvo(float elevation, float yaw, AmmoDefinition ammo, Rigidbody follow = null, float range = 0f)
        {
            float started = Time.time;
            Vector3 aimFlat = Flat(FireDirection(0f, yaw)).normalized * Mathf.Max(1f, range);
            if (cannonballs == null) Services.TryGet(out cannonballs);
            if (effects == null) Services.TryGet(out effects);

            var order = new List<Transform>(muzzles);
            for (int i = 0; i < order.Count; i++)
            {
                int j = Random.Range(i, order.Count);
                (order[i], order[j]) = (order[j], order[i]);
            }

            float spread = stats.spreadDegrees * (ammo ? ammo.spreadMultiplier : 1f) * StormSpread;
            int pellets = ammo ? Mathf.Max(1, ammo.pellets) : 1;
            float damage = stats.damagePerBall * (ammo ? ammo.damageMultiplier : 1f) / pellets;
            float sail = ammo ? ammo.sailDamagePerHit : 0f, crew = ammo ? ammo.crewDamagePerHit : 0f;
            float scale = ammo ? ammo.visualScale : 1f;
            foreach (Transform muzzle in order)
            {
                if (!muzzle) continue;
                float ballYaw = yaw;
                if (follow)
                {
                    Vector3 moved = (Flat(follow.linearVelocity) - InheritedVelocity) * (Time.time - started);
                    float traverse = stats.maxAssistTraverse * stats.aimAssist;
                    ballYaw = Mathf.Clamp(yaw + Vector3.SignedAngle(aimFlat, aimFlat + moved, Vector3.up), -traverse, traverse);
                }
                Vector3 direction = FireDirection(elevation, ballYaw);
                for (int p = 0; p < pellets; p++)
                {
                    Vector3 pelletDirection = FireDirection(
                        elevation + Random.Range(-spread, spread) * 0.5f,
                        ballYaw + Random.Range(-spread, spread));
                    cannonballs?.Fire(muzzle.position, pelletDirection * stats.muzzleVelocity + InheritedVelocity,
                        damage, stats.ballMass / pellets, stats.ballLifetime, shipBody, sail, crew, scale, withEffects: p == 0,
                        chain: ammo && ammo.chainVisual);
                }
                effects?.MuzzleBlast(muzzle.position, direction);

                if (shipBody)
                    shipBody.AddForceAtPosition(-direction * (stats.ballMass * stats.muzzleVelocity * recoilScale),
                        muzzle.position, ForceMode.Impulse);

                yield return new WaitForSeconds(Random.Range(0f, stats.maxFireDelay));
            }
        }

        void UpdatePreview()
        {
            if (muzzles.Length == 0) return;
            if (ocean == null) Services.TryGet(out ocean);

            Vector3 origin = MuzzleCenter();
            Vector3 velocity = FireDirection(Elevation, assistYaw) * stats.muzzleVelocity + InheritedVelocity;
            PredictedImpact = Ballistics.Predict(origin, velocity, ocean, shipBody, arc);

            if (aimLine)
            {
                aimLine.positionCount = arc.Count;
                for (int i = 0; i < arc.Count; i++) aimLine.SetPosition(i, arc[i]);
            }

            if (impactMarker)
            {
                float range = Vector3.Distance(new Vector3(origin.x, 0f, origin.z), new Vector3(PredictedImpact.x, 0f, PredictedImpact.z));
                float spreadWidth = 2f * range * Mathf.Tan(SpreadDegrees * Mathf.Deg2Rad);
                float shipLength = 5f;
                float width = Mathf.Max(3f, shipLength + spreadWidth);
                impactMarker.position = PredictedImpact + Vector3.up * 0.15f;
                impactMarker.rotation = Quaternion.LookRotation(FireDirection(0f, assistYaw), Vector3.up);
                impactMarker.localScale = new Vector3(width, 0.02f, Mathf.Max(3f, spreadWidth));
            }
        }

        // ---------- Mira assistida ----------

        /// <summary>Ângulo máximo (graus) entre o través e o inimigo para a assistência considerá-lo.</summary>
        const float AssistSearchAngle = 30f;
        /// <summary>Perto assim da distância do inimigo (fração da janela) a mira "trava" (o HUD mostra) e puxa por inteiro.</summary>
        const float LockCloseness = 0.7f;

        void ClearAssist()
        {
            AssistTarget = null;
            AssistLocked = false;
            assistYaw = 0f;
            assistCloseness = 0f;
        }

        /// <summary>
        /// Acha o inimigo mais alinhado com o través e puxa a distância e o giro para ele. Força = aimAssist:
        /// a janela de "grude" vai de 15 m a 60 m; com assistência total (nível 3) antecipa o movimento do alvo.
        /// </summary>
        void UpdateAssist()
        {
            ClearAssist();
            float assist = stats.aimAssist;
            if (assist <= 0f || muzzles.Length == 0) return;

            Vector3 origin = MuzzleCenter();
            Vector3 beam = FireDirection(0f);
            Transform own = shipBody ? shipBody.transform : transform.root;
            EnemyShipAI best = null;
            float bestAngle = AssistSearchAngle;
            for (int i = 0; i < EnemyShipAI.Active.Count; i++)
            {
                var ai = EnemyShipAI.Active[i];
                if (!ai || ai.IsDead || ai.transform == own) continue;
                Vector3 to = Flat(ai.transform.position - origin);
                float distance = to.magnitude;
                if (distance < stats.minRange * 0.5f || distance > MaxRange * 1.1f) continue;
                float angle = Mathf.Abs(Vector3.SignedAngle(beam, to, Vector3.up));
                if (angle >= bestAngle) continue;
                best = ai;
                bestAngle = angle;
            }
            if (!best) return;

            // Antecipação: onde o alvo vai estar quando a bala chegar (velocidade relativa, a bala herda a nossa).
            Vector3 aimPoint = best.transform.position;
            float lead = Mathf.InverseLerp(0.7f, 1f, assist);
            if (lead > 0f && best.TryGetComponent(out Rigidbody targetBody))
            {
                // Tempo de voo de verdade (com a elevação; a distância ÷ a velocidade da bala errava ~10% a 100 m), duas voltas
                // porque o ponto antecipado muda a distância.
                Vector3 relative = Flat(targetBody.linearVelocity) - InheritedVelocity;
                Vector3 basePoint = aimPoint;
                for (int i = 0; i < 2; i++)
                    aimPoint = basePoint + relative * (FlightTime(Flat(aimPoint - origin).magnitude) * lead);
            }

            Vector3 toAim = Flat(aimPoint - origin);
            float signed = Vector3.SignedAngle(beam, toAim, Vector3.up);
            float traverse = stats.maxAssistTraverse * assist;
            assistYaw = Mathf.Clamp(signed, -traverse, traverse);
            float targetRange = toAim.magnitude * Mathf.Cos((signed - assistYaw) * Mathf.Deg2Rad);

            AssistTarget = best.transform;
            float charged = ChargedRange;
            float diff = targetRange - charged;
            float window = Mathf.Lerp(15f, 60f, assist);
            assistCloseness = Mathf.Clamp01(1f - Mathf.Abs(diff) / window);
            // Travada (perto da distância do inimigo), a assistência puxa inteira: com assistência total a bala vai na distância
            // exata. Antes puxava só raiz(proximidade) e, "travada", ainda caía 3 a 5 m antes do casco (na água).
            float pull = assist * Mathf.Sqrt(Mathf.Clamp01(assistCloseness / LockCloseness));
            assistedRange = charged + diff * pull;
            AssistLocked = assistCloseness > LockCloseness;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        void SetPreviewVisible(bool visible)
        {
            if (aimLine) aimLine.enabled = visible;
            if (impactMarker) impactMarker.gameObject.SetActive(visible);
        }
    }
}
