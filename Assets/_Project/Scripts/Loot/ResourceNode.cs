using System;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.Loot
{
    /// <summary>
    /// Árvore ou veio de minério que se coleta a pé com a ferramenta certa (D1, 28/10): cada golpe treme o tronco e solta
    /// lascas; no último, a árvore tomba para longe de quem cortou, bate no chão, afunda e some, e fica o toco. O que ela dá vai para quem
    /// cortou (<see cref="Strike"/> devolve). Volta inteira depois de <see cref="regrowSeconds"/>, só com a câmera longe (não aparece do
    /// nada na frente do jogador). Não é salvo: carregar o jogo traz tudo de volta, como as ilhas.
    /// Fica no próprio objeto da árvore (o gerador põe em todas as árvores e palmeiras das matas) e só liga o Update enquanto treme, cai ou
    /// espera para voltar: as centenas paradas não custam nada.
    /// Veio (passo 2): o nó fica na pedra grande, mas só mexe no <see cref="body"/> (as pedrinhas de minério em volta da base): elas
    /// pulsam a cada golpe e, no último, encolhem e somem com poeira; a pedra fica (colisor e NavMesh iguais) e as pedrinhas voltam.
    /// </summary>
    public class ResourceNode : MonoBehaviour
    {
        public enum Kind { Tree, Ore }

        [SerializeField] Kind kind;
        [SerializeField] ItemDefinition item;
        [SerializeField] Vector2Int amount = new(2, 3);
        [SerializeField] int hits = 5;
        [Tooltip("Raio do tronco (m): quem corta fica a esta distância + o alcance da ferramenta.")]
        [SerializeField] float radius = 0.3f;
        [Tooltip("Toco que fica no lugar (molde desligado na cena, copiado na primeira vez).")]
        [SerializeField] GameObject stumpTemplate;
        [SerializeField] float stumpScale = 1f;
        [Tooltip("O CameraOccluder da árvore (translúcida quando tapa o capitão): desligado enquanto ele corta e enquanto ela cai (28/10, " +
                 "pedido do Jhonata: \"não ficar estranho o capitão quebrando a árvore e ela translúcida\"); volta quando ela cresce de novo.")]
        [SerializeField] Behaviour occluder;
        [Tooltip("O que treme, cai e some (vazio = o próprio objeto: a árvore). No veio, as pedrinhas de minério; a pedra fica.")]
        [SerializeField] Transform body;
        [SerializeField] float regrowSeconds = 480f;
        [Tooltip("Só volta com a câmera a mais disto (m).")]
        [SerializeField] float regrowDistance = 35f;
        [SerializeField] float fallSeconds = 1.5f;
        [Tooltip("Golpes sem terminar: depois disto a árvore esquece os golpes que levou.")]
        [SerializeField] float forgetSeconds = 20f;

        enum State { Standing, Falling, Sinking, Crumbling, Gone }

        State state;
        int taken;
        float lastStrike, stateSince, shakeUntil;
        Vector3 fallAxis, fallDir, basePosition;
        // Veio: cada pedrinha encolhe no lugar dela (escalar o conjunto puxaria todas para o meio da pedra).
        Transform[] pieces = Array.Empty<Transform>();
        Vector3[] pieceScales = Array.Empty<Vector3>();
        Quaternion baseRotation;
        Collider[] colliders;
        Renderer[] renderers;
        GameObject stump;
        bool landed;

        public Kind Type => kind;
        public ItemDefinition Item => item;
        public bool Standing => state == State.Standing;
        public float Radius => radius;
        /// <summary>Golpes que ainda faltam.</summary>
        public int HitsLeft => Mathf.Max(0, hits - taken);
        public int Hits => hits;

        /// <summary>Golpe que pegou: onde e a direção de quem bateu (para as lascas e o som).</summary>
        public static event Action<ResourceNode, Vector3, Vector3> Struck;
        /// <summary>Tombou (o último golpe): o pé da árvore e para onde ela cai.</summary>
        public static event Action<ResourceNode, Vector3, Vector3> Felled;
        /// <summary>Bateu no chão: onde.</summary>
        public static event Action<ResourceNode, Vector3> Landed;

        public void Configure(Kind type, ItemDefinition yield, Vector2Int yieldAmount, int strikes, float trunkRadius, GameObject stumpModel, float stumpSize,
            Behaviour cameraOccluder, Transform bodyPart = null, float regrow = 480f)
        {
            occluder = cameraOccluder;
            body = bodyPart;
            regrowSeconds = regrow;
            kind = type;
            item = yield;
            amount = yieldAmount;
            hits = strikes;
            radius = trunkRadius;
            stumpTemplate = stumpModel;
            stumpScale = stumpSize;
            enabled = false;
        }

        Transform Body => body ? body : transform;

        void Awake()
        {
            colliders = Body.GetComponentsInChildren<Collider>(true);
            renderers = Body.GetComponentsInChildren<Renderer>(true);
            basePosition = Body.position;
            baseRotation = Body.rotation;
            if (kind == Kind.Ore)
            {
                pieces = new Transform[Body.childCount];
                pieceScales = new Vector3[pieces.Length];
                for (int i = 0; i < pieces.Length; i++)
                {
                    pieces[i] = Body.GetChild(i);
                    pieceScales[i] = pieces[i].localScale;
                }
            }
            enabled = false;
        }

        void ScalePieces(float factor)
        {
            for (int i = 0; i < pieces.Length; i++) pieces[i].localScale = pieceScales[i] * factor;
        }

        /// <summary>Alguém começou (true) ou parou (false) de coletar aqui: opaca enquanto isso, mesmo tapando o capitão.</summary>
        public void Hold(bool on)
        {
            if (occluder && state == State.Standing) occluder.enabled = !on;
        }

        /// <summary>
        /// Um golpe de ferramenta vindo de <paramref name="from"/>: treme e solta lascas; no último, tomba. Devolve quanto deu (0 até tombar).
        /// </summary>
        public int Strike(Vector3 from, Vector3 point)
        {
            if (state != State.Standing) return 0;
            if (taken > 0 && Time.time - lastStrike > forgetSeconds) taken = 0;
            taken++;
            lastStrike = Time.time;
            Vector3 dir = point - from;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : transform.forward;
            Struck?.Invoke(this, point, dir);
            if (taken < hits)
            {
                shakeUntil = Time.time + 0.35f;
                fallAxis = Vector3.Cross(Vector3.up, dir);
                enabled = true;
                return 0;
            }
            Fell(dir);
            return item ? UnityEngine.Random.Range(amount.x, amount.y + 1) : 0;
        }

        void Fell(Vector3 away)
        {
            state = kind == Kind.Ore ? State.Crumbling : State.Falling;
            stateSince = Time.time;
            landed = false;
            fallAxis = Vector3.Cross(Vector3.up, away);
            fallDir = away;
            foreach (var c in colliders) c.enabled = false;
            ShowStump(true);
            enabled = true;
            Felled?.Invoke(this, basePosition, away);
            // O veio não tomba: as pedrinhas se soltam na hora, com a poeira em volta da pedra.
            if (kind == Kind.Ore)
            {
                landed = true;
                Landed?.Invoke(this, basePosition + Vector3.up * 0.3f);
            }
        }

        void ShowStump(bool on)
        {
            if (on && !stump && stumpTemplate)
            {
                stump = Instantiate(stumpTemplate, basePosition, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), transform.parent);
                stump.transform.localScale = stumpTemplate.transform.localScale * stumpScale;
                stump.name = name + "_Toco";
            }
            if (stump) stump.SetActive(on);
        }

        void Update()
        {
            float t = Time.time - stateSince;
            switch (state)
            {
                case State.Standing:
                    float k = Mathf.Clamp01((shakeUntil - Time.time) / 0.35f);
                    if (kind == Kind.Ore)
                        // As pedrinhas pulsam um pouco a cada golpe (a pedra grande não se mexe).
                        ScalePieces(1f + Mathf.Sin(k * Mathf.PI * 3f) * 0.08f * k);
                    else
                        // Tremida do golpe: inclina uns graus para longe de quem bateu e volta.
                        Body.SetPositionAndRotation(basePosition,
                            Quaternion.AngleAxis(Mathf.Sin(k * Mathf.PI * 3f) * 2.5f * k, fallAxis) * baseRotation);
                    if (k <= 0f) enabled = false;
                    break;
                case State.Crumbling:
                    // O minério se solta: as pedrinhas encolhem e afundam um pouco em 0,6 s.
                    float c = Mathf.Clamp01(t / 0.6f);
                    ScalePieces(1f - c * c);
                    Body.position = basePosition + Vector3.down * (0.2f * c);
                    if (c >= 1f)
                    {
                        state = State.Gone;
                        stateSince = Time.time;
                        foreach (var r in renderers) r.enabled = false;
                        // A pedra continua ali: volta a ficar translúcida quando tapa o capitão.
                        if (occluder) occluder.enabled = true;
                    }
                    break;
                case State.Falling:
                    // Começa devagar e acelera (como uma árvore de verdade), até deitar quase no chão.
                    float f = Mathf.Clamp01(t / fallSeconds);
                    Body.SetPositionAndRotation(basePosition, Quaternion.AngleAxis(84f * f * f, fallAxis) * baseRotation);
                    if (f >= 1f)
                    {
                        state = State.Sinking;
                        stateSince = Time.time;
                        if (!landed)
                        {
                            landed = true;
                            Landed?.Invoke(this, basePosition + fallDir * 2.5f + Vector3.up * 0.3f);
                        }
                    }
                    break;
                case State.Sinking:
                    // Deitada um instante, depois afunda no chão e some.
                    if (t > 1.2f)
                    {
                        float s = Mathf.Clamp01((t - 1.2f) / 1.4f);
                        Body.position = basePosition + Vector3.down * (2.5f * s);
                        if (s >= 1f)
                        {
                            state = State.Gone;
                            stateSince = Time.time;
                            foreach (var r in renderers) r.enabled = false;
                        }
                    }
                    break;
                case State.Gone:
                    if (t >= regrowSeconds && CameraFar()) Regrow();
                    break;
            }
        }

        bool CameraFar()
        {
            var cam = Camera.main;
            return !cam || (cam.transform.position - basePosition).sqrMagnitude > regrowDistance * regrowDistance;
        }

        /// <summary>Volta inteira no lugar (teste: <see cref="RegrowNow"/>).</summary>
        void Regrow()
        {
            state = State.Standing;
            taken = 0;
            Body.SetPositionAndRotation(basePosition, baseRotation);
            ScalePieces(1f);
            foreach (var r in renderers) r.enabled = true;
            foreach (var c in colliders) c.enabled = true;
            if (occluder) occluder.enabled = true;
            ShowStump(false);
            enabled = false;
        }

        /// <summary>Teste: volta agora, mesmo com a câmera perto.</summary>
        public void RegrowNow()
        {
            if (state != State.Standing) Regrow();
        }
    }
}
