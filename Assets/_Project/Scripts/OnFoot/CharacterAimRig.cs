using Pirates.Core;
using UnityEngine;

namespace Pirates.OnFoot
{
    /// <summary>
    /// Mira procedural da pistola (fica no modelo, junto do Animator). Depois da animação (LateUpdate), gira a coluna
    /// para os lados e o peito + braço para cima/baixo até o cano apontar para o alvo, suavizando a entrada/saída
    /// e o movimento do alvo: o braço segue o cursor sem trancos. Também faz o coice do tiro e o aviso da mira: o jogador vê um anel
    /// de latão no chão onde mira, ou onde a bala bate se algo fica no caminho (dourado em volta do alvo quando a mira trava nele); o atirador inimigo solta faíscas e fumaça na
    /// caçoleta enquanto mira (a pólvora acendendo), mais fortes logo antes do tiro. Sem linha nem laser (28/10, pedido do Jhonata:
    /// "em um jogo de pirata não faz muito sentido"). Vale nos estados do Animator com a tag "Mira" (pose de mira e tiro).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CharacterAimRig : MonoBehaviour
    {
        static readonly int AimTag = Animator.StringToHash("Mira");

        [SerializeField] CharacterCombat combat;
        [Tooltip("Boca da pistola: o eixo Z (forward) é o cano.")]
        [SerializeField] Transform muzzle;
        [Tooltip("Anel no ponto onde a bala para (só o jogador).")]
        [SerializeField] LineRenderer reticle;
        [SerializeField] Color markColor = new(0.62f, 0.4f, 0.14f, 0.95f);
        [Tooltip("Cor do anel quando a mira trava num alvo.")]
        [SerializeField] Color lockedColor = new(1f, 0.8f, 0.25f, 1f);
        [Tooltip("Raio do anel onde a bala para (arma de chumbo: o leque na distância, no mínimo isto).")]
        [SerializeField] float markRadius = 0.3f;
        [SerializeField] float markWidth = 0.065f;
        [Tooltip("Travado num alvo: raio do anel que contorna o corpo dele.")]
        [SerializeField] float lockedRingRadius = 0.5f;
        [SerializeField] float lockedWidth = 0.07f;

        [Header("Aviso do atirador inimigo")]
        [Tooltip("Faíscas na caçoleta enquanto mira (só os inimigos).")]
        [SerializeField] ParticleSystem fuseSparks;
        [SerializeField] ParticleSystem fuseSmoke;
        [Tooltip("Brasa piscando na caçoleta (o que se vê de longe, da câmera do jogo).")]
        [SerializeField] ParticleSystem fuseGlow;
        [Tooltip("Faíscas, fumaça e brasa por segundo mirando; no fim (ShotWarning = 1), o máximo.")]
        [SerializeField] Vector2 sparksRate = new(40f, 160f), smokeRate = new(8f, 24f), glowRate = new(14f, 40f);
        [Tooltip("Camada do tronco (onde ficam os estados de mira).")]
        [SerializeField] string upperLayer = "Tronco";
        [Tooltip("Camada do corpo inteiro (parado, a pose de mira usa o quadril do clipe).")]
        [SerializeField] string fullBodyLayer = "CorpoInteiro";

        [Header("Pose de mira (medida no clipe)")]
        [Tooltip("Graus do braço em relação à frente do personagem, só tronco animado (andando). Negativo = para a esquerda.")]
        [SerializeField] float armYawUpperBody = -45f;
        [Tooltip("Graus do braço em relação à frente do personagem, corpo inteiro animado (parado).")]
        [SerializeField] float armYawFullBody = -20f;
        [Tooltip("Arma de duas mãos (bacamarte): os mesmos graus, medidos na pose de mira dela.")]
        [SerializeField] float twoHandedYawUpperBody = -45f, twoHandedYawFullBody = -20f;

        [Header("Ajuste")]
        [Tooltip("Segundos para entrar/sair da mira.")]
        [SerializeField] float blendTime = 0.12f;
        [Tooltip("Rapidez com que o ponto mirado segue o cursor (maior = mais rápido).")]
        [SerializeField] float followSharpness = 18f;
        [SerializeField] float maxYaw = 80f;
        [SerializeField] float maxPitch = 45f;
        [Tooltip("Rapidez da volta do coice.")]
        [SerializeField] float recoilRecovery = 11f;

        Animator animator;
        Transform spine, chest, upperChest, upperArm, lowerArm;
        Transform[] bones = System.Array.Empty<Transform>();
        Quaternion[] animated = System.Array.Empty<Quaternion>();
        int upperIndex = -1, fullIndex = -1;
        float weight, weightVelocity, recoil, recoilDegrees;
        Vector3 aimPoint;

        /// <summary>Quanto a mira procedural está valendo agora (0–1).</summary>
        public float Weight => weight;
        /// <summary>
        /// Graus do braço da pose de mira em relação à frente do personagem antes da correção: o FootCharacter vira
        /// o corpo o contrário disso para o braço já sair apontado para o alvo.
        /// </summary>
        public float ArmYawOffset
        {
            get
            {
                float full = fullIndex >= 0 ? animator.GetLayerWeight(fullIndex) : 0f;
                return TwoHanded ? Mathf.Lerp(twoHandedYawUpperBody, twoHandedYawFullBody, full) : Mathf.Lerp(armYawUpperBody, armYawFullBody, full);
            }
        }

        bool TwoHanded => combat && combat.Current && combat.Current.twoHanded;
        /// <summary>Boca da arma na mão (cada arma de fogo tem a sua).</summary>
        Transform Muzzle => combat && combat.CurrentMuzzle ? combat.CurrentMuzzle : muzzle;

        /// <summary>Mira do jogador: o anel onde a bala para e as cores (normal; travado num alvo).</summary>
        public void ConfigureReticle(LineRenderer ring, Color mark, Color locked)
        {
            reticle = ring;
            markColor = mark;
            lockedColor = locked;
        }

        /// <summary>Aviso do atirador inimigo: faíscas e fumaça na caçoleta da arma.</summary>
        public void ConfigureFuse(ParticleSystem sparks, ParticleSystem smoke, ParticleSystem glow)
        {
            fuseSparks = sparks;
            fuseSmoke = smoke;
            fuseGlow = glow;
        }

        /// <summary>Pose de mira da arma de duas mãos (graus do braço, como em <see cref="Configure"/>).</summary>
        public void ConfigureTwoHanded(float upperBodyYaw, float fullBodyYaw)
        {
            twoHandedYawUpperBody = upperBodyYaw;
            twoHandedYawFullBody = fullBodyYaw;
        }

        public void Configure(CharacterCombat characterCombat, Transform gunMuzzle, float upperBodyArmYaw, float fullBodyArmYaw)
        {
            combat = characterCombat;
            muzzle = gunMuzzle;
            armYawUpperBody = upperBodyArmYaw;
            armYawFullBody = fullBodyArmYaw;
        }

        void Awake()
        {
            animator = GetComponent<Animator>();
            if (!combat) combat = GetComponentInParent<CharacterCombat>();
            if (animator.isHuman)
            {
                spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
                upperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                lowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                if (!upperChest) upperChest = chest;
                bones = new[] { spine, chest, upperChest, upperArm, lowerArm };
                if (System.Array.Exists(bones, b => !b)) bones = System.Array.Empty<Transform>();
                animated = new Quaternion[bones.Length];
            }
            upperIndex = animator.GetLayerIndex(upperLayer);
            fullIndex = animator.GetLayerIndex(fullBodyLayer);
            if (reticle) reticle.enabled = false;
            SetFuse(-1f);
        }

        void OnEnable()
        {
            if (combat) combat.Fired += OnFired;
        }

        void OnDisable()
        {
            if (combat) combat.Fired -= OnFired;
            weight = weightVelocity = recoil = 0f;
            if (reticle) reticle.enabled = false;
            SetFuse(-1f);
        }

        void OnFired(WeaponDefinition weapon, Vector3 from, Vector3 to)
        {
            recoil = 1f;
            recoilDegrees = weapon.recoilDegrees;
        }

        void LateUpdate()
        {
            var gun = Muzzle;
            if (!combat || !gun || bones.Length == 0) return;
            float dt = Time.deltaTime;
            var weapon = combat.Current;
            bool armUp = weapon && weapon.kind == WeaponKind.Firearm && InAimState();
            weight = Mathf.SmoothDamp(weight, armUp ? 1f : 0f, ref weightVelocity, blendTime);

            // Entrando na mira, o ponto começa no alvo (sem varrer do último lugar mirado).
            if (weight < 0.01f) aimPoint = combat.AimTarget;
            else aimPoint = Vector3.Lerp(aimPoint, combat.AimTarget, 1f - Mathf.Exp(-followSharpness * dt));

            bool twoHanded = TwoHanded;
            if (weight > 0.001f) PointAt(gun, aimPoint, weight, twoHanded);
            if (recoil > 0.001f)
            {
                Kick(gun, recoil * weight, twoHanded);
                recoil *= Mathf.Exp(-recoilRecovery * dt);
            }
            UpdateAimSigns(gun, weapon);
        }

        bool InAimState()
        {
            if (upperIndex < 0) return false;
            var state = animator.IsInTransition(upperIndex) ? animator.GetNextAnimatorStateInfo(upperIndex) : animator.GetCurrentAnimatorStateInfo(upperIndex);
            return state.tagHash == AimTag;
        }

        /// <summary>
        /// Gira tronco (lados) e peito + braço (cima/baixo) até o cano apontar para o alvo; mistura pelo peso. Arma de duas mãos: o
        /// cima/baixo é todo no peito (os dois braços vão juntos com a arma; girar só o braço direito soltava a outra mão do cano).
        /// </summary>
        void PointAt(Transform gun, Vector3 target, float w, bool twoHanded)
        {
            for (int i = 0; i < bones.Length; i++) animated[i] = bones[i].localRotation;

            float totalYaw = 0f, totalPitch = 0f;
            for (int iteration = 0; iteration < 2; iteration++)
            {
                Vector3 to = target - gun.position;
                float yaw = Mathf.DeltaAngle(Yaw(gun.forward), Yaw(to));
                yaw = Mathf.Clamp(totalYaw + yaw, -maxYaw, maxYaw) - totalYaw;
                totalYaw += yaw;
                // Torção dividida pela coluna, mais em cima.
                Rotate(spine, Vector3.up, yaw * 0.3f);
                Rotate(chest, Vector3.up, yaw * 0.3f);
                Rotate(upperChest, Vector3.up, yaw * 0.4f);

                to = target - gun.position;
                float pitch = Pitch(to) - Pitch(gun.forward);
                pitch = Mathf.Clamp(totalPitch + pitch, -maxPitch, maxPitch) - totalPitch;
                totalPitch += pitch;
                Vector3 right = RightOf(to);
                // Para cima é giro negativo no eixo "direita".
                if (twoHanded)
                {
                    Rotate(chest, right, -pitch * 0.4f);
                    Rotate(upperChest, right, -pitch * 0.6f);
                }
                else
                {
                    Rotate(upperChest, right, -pitch * 0.35f);
                    Rotate(upperArm, right, -pitch * 0.65f);
                }
            }

            if (w >= 0.999f) return;
            for (int i = 0; i < bones.Length; i++) bones[i].localRotation = Quaternion.Slerp(animated[i], bones[i].localRotation, w);
        }

        /// <summary>Coice: braço sobe e o peito recua um pouco, voltando sozinho (duas mãos: o peito todo recua com a arma).</summary>
        void Kick(Transform gun, float amount, bool twoHanded)
        {
            Vector3 right = RightOf(gun.forward);
            if (twoHanded)
            {
                Rotate(upperChest, right, -recoilDegrees * 0.6f * amount);
                Rotate(chest, right, -recoilDegrees * 0.25f * amount);
                return;
            }
            Rotate(upperArm, right, -recoilDegrees * 0.6f * amount);
            Rotate(lowerArm, right, -recoilDegrees * 0.4f * amount);
            Rotate(upperChest, right, -recoilDegrees * 0.15f * amount);
        }

        static readonly RaycastHit[] markHits = new RaycastHit[16];
        IOceanSampler ocean;
        float shownFuse = -2f;

        void UpdateAimSigns(Transform gun, WeaponDefinition weapon)
        {
            bool show = combat.IsAiming && weight > 0.7f;
            if (!reticle)
            {
                // Inimigo: a pólvora acendendo na caçoleta enquanto mira; nos últimos instantes antes do tiro (ShotWarning) as faíscas
                // aumentam: é a hora de rolar. O medalhão sobre a cabeça (CryptWarningHUD) avisa junto.
                SetFuse(show ? combat.ShotWarning : -1f);
                return;
            }
            reticle.enabled = show;
            if (!show) return;

            // Jogador: anel de latão no chão onde ele mira (o cursor, até o alcance da arma) ou, se algo fica no caminho antes, deitado
            // onde a bala bate; travado num alvo, um anel dourado pulsa em volta do corpo dele.
            Vector3 from = gun.position;
            Vector3 target = combat.AimTarget;
            if (combat.AimOnTarget)
            {
                reticle.widthMultiplier = lockedWidth;
                DrawRing(target, lockedRingRadius * (1f + 0.06f * Mathf.Sin(Time.time * 12f)), lockedColor, Vector3.zero);
                return;
            }
            Vector3 dir = target - from;
            float range = weapon ? weapon.range : 35f;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : gun.forward;
            float reach = Mathf.Min(range, Vector3.Distance(from, target));
            Vector3 end = from + dir * reach;
            Vector3 normal = Vector3.zero;
            float nearest = reach + 0.3f;
            int count = Physics.RaycastNonAlloc(from, dir, markHits, nearest, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = markHits[i];
                if (hit.distance >= nearest || hit.transform.IsChildOf(combat.transform)) continue;
                nearest = hit.distance;
                end = hit.point;
                normal = hit.normal;
            }
            // Nada no caminho: o anel fica no chão embaixo do ponto mirado (no fim do alcance a bala não bate em nada e o anel ficava
            // flutuando longe, fora da vista).
            if (normal == Vector3.zero)
            {
                nearest = reach;
                count = Physics.RaycastNonAlloc(end + Vector3.up * 0.5f, Vector3.down, markHits, 8f, ~0, QueryTriggerInteraction.Ignore);
                float below = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    var hit = markHits[i];
                    if (hit.distance >= below || hit.transform.IsChildOf(combat.transform)) continue;
                    below = hit.distance;
                    end = hit.point;
                    normal = hit.normal;
                }
                // Mar (a água não tem colisor e é opaca): na superfície dela.
                if (ocean != null || Services.TryGet(out ocean))
                {
                    float water = ocean.GetHeight(end);
                    if (normal == Vector3.zero || end.y < water)
                    {
                        end.y = water;
                        normal = Vector3.up;
                    }
                }
            }
            // Chumbo em leque: o anel mostra até onde ele se abre naquela distância.
            float radius = markRadius;
            if (weapon && weapon.pellets > 1)
                radius = Mathf.Clamp(nearest * Mathf.Tan(weapon.pelletSpread * 0.5f * Mathf.Deg2Rad), markRadius, 1.6f);
            reticle.widthMultiplier = markWidth;
            DrawRing(end + (normal != Vector3.zero ? normal * 0.04f : -dir * 0.05f), radius, markColor, normal);
        }

        /// <summary>Caçoleta acesa no nível do aviso (0–1; negativo = apagada). Só mexe na emissão quando o nível muda.</summary>
        void SetFuse(float level)
        {
            level = level < 0f ? -1f : Mathf.Round(level * 10f) / 10f;
            if (Mathf.Approximately(level, shownFuse)) return;
            shownFuse = level;
            Emit(fuseSparks, level < 0f ? 0f : Mathf.Lerp(sparksRate.x, sparksRate.y, level));
            Emit(fuseSmoke, level < 0f ? 0f : Mathf.Lerp(smokeRate.x, smokeRate.y, level));
            Emit(fuseGlow, level < 0f ? 0f : Mathf.Lerp(glowRate.x, glowRate.y, level));
        }

        static void Emit(ParticleSystem system, float rate)
        {
            if (!system) return;
            var emission = system.emission;
            emission.rateOverTime = rate;
            if (rate > 0f && !system.isPlaying) system.Play();
        }

        /// <summary>Anel deitado na superfície (pela normal) ou, sem ela, virado para a câmera.</summary>
        void DrawRing(Vector3 center, float radius, Color color, Vector3 normal)
        {
            const int segments = 28;
            Vector3 right, up;
            if (normal != Vector3.zero)
            {
                right = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.forward).normalized;
                up = Vector3.Cross(right, normal);
            }
            else
            {
                var cam = Camera.main;
                right = cam ? cam.transform.right : Vector3.right;
                up = cam ? cam.transform.up : Vector3.up;
            }
            reticle.loop = true;
            reticle.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                reticle.SetPosition(i, center + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius);
            }
            reticle.startColor = reticle.endColor = color;
        }

        static void Rotate(Transform bone, Vector3 axis, float degrees)
        {
            if (Mathf.Abs(degrees) > 0.0001f) bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }

        static Vector3 RightOf(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, direction).normalized : Vector3.right;
        }

        static float Yaw(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
        static float Pitch(Vector3 v) => Mathf.Asin(Mathf.Clamp(v.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
    }
}
