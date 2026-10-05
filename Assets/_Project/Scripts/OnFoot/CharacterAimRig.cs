using UnityEngine;

namespace Pirates.OnFoot
{
    /// <summary>
    /// Mira procedural da pistola (fica no modelo, junto do Animator). Depois da animação (LateUpdate), gira a coluna
    /// para os lados e o peito + braço para cima/baixo até o cano apontar para o alvo, suavizando a entrada/saída
    /// e o movimento do alvo: o braço segue o cursor sem trancos. Também faz o coice do tiro, a linha de mira e (jogador)
    /// o laser até onde a bala bate, com um ponto ali (anel dourado em volta do alvo quando a mira trava nele).
    /// Vale nos estados do Animator com a tag "Mira" (pose de mira e tiro).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CharacterAimRig : MonoBehaviour
    {
        static readonly int AimTag = Animator.StringToHash("Mira");

        [SerializeField] CharacterCombat combat;
        [Tooltip("Boca da pistola: o eixo Z (forward) é o cano.")]
        [SerializeField] Transform muzzle;
        [SerializeField] LineRenderer aimLine;
        [Tooltip("Anel no ponto mirado (só o jogador). Com ele, a linha vira o laser: vai até onde a bala bate, com um ponto ali.")]
        [SerializeField] LineRenderer reticle;
        [Tooltip("Brilho largo e fraco em volta do laser (só o jogador).")]
        [SerializeField] LineRenderer laserGlow;
        [SerializeField] Color aimColor = new(1f, 0.12f, 0.08f, 1f);
        [Tooltip("Cor do anel quando a mira trava num alvo (o laser continua vermelho).")]
        [SerializeField] Color lockedColor = new(1f, 0.8f, 0.25f, 1f);
        [Tooltip("Raio do ponto do laser onde ele bate.")]
        [SerializeField] float dotRadius = 0.07f;
        [Tooltip("Travado num alvo: raio do anel que contorna o corpo dele.")]
        [SerializeField] float lockedRingRadius = 0.5f;
        [SerializeField, Range(0f, 1f)] float glowAlpha = 0.25f;
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

        /// <summary>Laser do jogador: ponto/anel, brilho e cores (laser; anel travado num alvo).</summary>
        public void ConfigureReticle(LineRenderer ring, LineRenderer glow, Color laser, Color locked)
        {
            reticle = ring;
            laserGlow = glow;
            aimColor = laser;
            lockedColor = locked;
        }

        /// <summary>Pose de mira da arma de duas mãos (graus do braço, como em <see cref="Configure"/>).</summary>
        public void ConfigureTwoHanded(float upperBodyYaw, float fullBodyYaw)
        {
            twoHandedYawUpperBody = upperBodyYaw;
            twoHandedYawFullBody = fullBodyYaw;
        }

        public void Configure(CharacterCombat characterCombat, Transform gunMuzzle, LineRenderer line, float upperBodyArmYaw, float fullBodyArmYaw)
        {
            combat = characterCombat;
            muzzle = gunMuzzle;
            aimLine = line;
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
            if (aimLine) aimLine.enabled = false;
            if (reticle) reticle.enabled = false;
            if (laserGlow) laserGlow.enabled = false;
        }

        void OnEnable()
        {
            if (combat) combat.Fired += OnFired;
        }

        void OnDisable()
        {
            if (combat) combat.Fired -= OnFired;
            weight = weightVelocity = recoil = 0f;
            if (aimLine) aimLine.enabled = false;
            if (reticle) reticle.enabled = false;
            if (laserGlow) laserGlow.enabled = false;
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
            UpdateAimLine(gun, weapon);
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

        static readonly RaycastHit[] laserHits = new RaycastHit[16];

        void UpdateAimLine(Transform gun, WeaponDefinition weapon)
        {
            if (!aimLine) return;
            bool show = combat.IsAiming && weight > 0.7f;
            aimLine.enabled = show;
            if (reticle) reticle.enabled = show;
            if (laserGlow) laserGlow.enabled = show;
            if (!show) return;
            Vector3 from = gun.position;
            if (!reticle)
            {
                // Inimigo: linha seguindo o cano (o aviso de que vai atirar), na cor do gerador.
                float length = Mathf.Max(1f, Vector3.Distance(from, aimPoint));
                aimLine.positionCount = 2;
                aimLine.SetPosition(0, from);
                aimLine.SetPosition(1, from + gun.forward * length);
                return;
            }

            // Jogador (05/10, pedido do Jhonata): laser vermelho da boca na direção do tiro até onde a bala para (a 1ª coisa no
            // caminho, ou o alcance), com um ponto ali; travado num alvo, um anel dourado pulsa em volta do corpo dele.
            Vector3 target = combat.AimTarget;
            Vector3 dir = target - from;
            float range = weapon ? weapon.range : 35f;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : gun.forward;
            Vector3 end = from + dir * range;
            float nearest = range;
            int count = Physics.RaycastNonAlloc(from, dir, laserHits, range, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = laserHits[i];
                if (hit.distance >= nearest || hit.transform.IsChildOf(combat.transform)) continue;
                nearest = hit.distance;
                end = hit.point;
            }
            aimLine.positionCount = 2;
            aimLine.SetPosition(0, from);
            aimLine.SetPosition(1, end);
            aimLine.startColor = aimColor;
            aimLine.endColor = new Color(aimColor.r, aimColor.g, aimColor.b, aimColor.a * 0.8f);
            if (laserGlow)
            {
                laserGlow.positionCount = 2;
                laserGlow.SetPosition(0, from);
                laserGlow.SetPosition(1, end);
                laserGlow.startColor = new Color(aimColor.r, aimColor.g, aimColor.b, glowAlpha);
                laserGlow.endColor = new Color(aimColor.r, aimColor.g, aimColor.b, glowAlpha * 0.6f);
            }
            if (combat.AimOnTarget) DrawRing(target, lockedRingRadius * (1f + 0.06f * Mathf.Sin(Time.time * 12f)), lockedColor);
            else DrawRing(end - dir * 0.05f, dotRadius, aimColor);
        }

        /// <summary>Anel virado para a câmera (pequeno e grosso, vira o ponto do laser).</summary>
        void DrawRing(Vector3 center, float radius, Color color)
        {
            const int segments = 24;
            var cam = Camera.main;
            Vector3 right = cam ? cam.transform.right : Vector3.right, up = cam ? cam.transform.up : Vector3.up;
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
