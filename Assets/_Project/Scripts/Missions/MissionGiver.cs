using System.Collections.Generic;
using UnityEngine;

namespace Pirates.Missions
{
    /// <summary>
    /// Morador da vila que dá missões conversando (10/10, pedido do Jhonata: missões com NPCs, não só no quadro da taverna). As
    /// missões dele (<see cref="MissionDefinition.givenByNpc"/>) têm as falas; quem conduz a conversa é o <see cref="NpcTalker"/> do
    /// personagem. Conversando, para o que fazia (o Townsperson), vira para o capitão e gesticula (estados "Wave", "Yes", "No" do
    /// Animator dos civis). Sobre a cabeça o HUD mostra "!" (tem missão para dar) ou "?" (missão dele pronta para entregar).
    /// </summary>
    public class MissionGiver : MonoBehaviour
    {
        public enum Offer { None, Available, ReadyToTurnIn }

        static readonly List<MissionGiver> active = new();
        public static IReadOnlyList<MissionGiver> Active => active;

        [SerializeField] string displayName = "Morador";
        [Tooltip("O que ele é (aparece embaixo do nome): coveiro, pescador...")]
        [SerializeField] string role;
        [SerializeField] MissionDefinition[] missions = System.Array.Empty<MissionDefinition>();
        [Tooltip("Conversa à toa quando não tem missão para dar nem para receber (sorteada).")]
        [TextArea(1, 3)] [SerializeField] string[] smallTalk = System.Array.Empty<string>();
        [Tooltip("Conversa à toa depois do fim da História (Porto Alento livre do tributo; pacote H, passo 4). Vazio = a de sempre.")]
        [TextArea(1, 3)] [SerializeField] string[] endingTalk = System.Array.Empty<string>();
        [Tooltip("Distância (m) para o F conversar.")]
        [SerializeField] float talkRange = 2.6f;
        [Tooltip("Altura (m) do nome e do \"!\" sobre a cabeça.")]
        [SerializeField] float labelHeight = 2.2f;
        [SerializeField] Animator animator;
        [Tooltip("O que ele faz parado (Townsperson): desligado enquanto conversa.")]
        [SerializeField] Behaviour idle;
        [Tooltip("Perdoa o capitão procurado pela Coroa por uma multa (G, passo 2: o Almirante Teodoro).")]
        [SerializeField] bool pardons;

        Quaternion faceTarget;
        WorldMarker marker;

        public string DisplayName => displayName;
        public string Role => role;
        public IReadOnlyList<MissionDefinition> Missions => missions;
        /// <summary>A última oferta recusada ("Agora não"): na próxima conversa ele oferece outra, se tiver (30/10: a Iara tem várias encomendas).</summary>
        public MissionDefinition LastDeclined { get; set; }
        /// <summary>Conversa à toa (depois do fim da História, as falas da vila livre, se ele tiver).</summary>
        public IReadOnlyList<string> SmallTalk => StoryEnded && endingTalk != null && endingTalk.Length > 0 ? endingTalk : smallTalk;

        /// <summary>A História acabou (a Coroa de Prata foi achada): a StoryLine põe isto a cada quadro.</summary>
        public static bool StoryEnded { get; set; }

        public void SetEndingTalk(string[] lines) => endingTalk = lines ?? System.Array.Empty<string>();
        public float TalkRange => talkRange;
        public Vector3 LabelPosition => transform.position + Vector3.up * labelHeight;
        public bool IsTalking { get; private set; }
        /// <summary>Id do WorldMarker dele (o HUD de missões aponta para cá quando a missão dele está pronta).</summary>
        public string MarkerId => MarkerIdFor(displayName);
        public WorldMarker Marker => marker;
        public bool Pardons => pardons;

        public void SetPardons(bool value) => pardons = value;

        public static string MarkerIdFor(string giverName) => "npc:" + giverName;

        /// <summary>
        /// Falas de roteiro (tutorial): com elas, a próxima conversa é só isto (sem missão) e ele mostra "!" na cabeça. No fim da
        /// conversa, <see cref="ScriptedTalkFinished"/> e as falas saem.
        /// </summary>
        public string[] ScriptedLines { get; set; }
        public event System.Action<MissionGiver> ScriptedTalkFinished;

        public bool HasScriptedTalk => ScriptedLines != null && ScriptedLines.Length > 0;

        /// <summary>A conversa de roteiro chegou ao fim (NpcTalker).</summary>
        public void FinishScriptedTalk()
        {
            ScriptedLines = null;
            ScriptedTalkFinished?.Invoke(this);
        }

        /// <summary>Alguém abriu uma conversa com este NPC (tutorial: "fale com um morador").</summary>
        public static event System.Action<MissionGiver> TalkStarted;

        /// <summary>O NPC com este nome (o "giver" da missão), sem alocar.</summary>
        public static MissionGiver Find(string giverName)
        {
            for (int i = 0; i < active.Count; i++)
                if (active[i].displayName == giverName) return active[i];
            return null;
        }

        void Awake() => marker = GetComponent<WorldMarker>();

        void Start()
        {
            // Olha para o capitão quando ele chega perto (12/11), também durante a conversa.
            if (!animator) return;
            var look = animator.GetComponent<Pirates.Core.HeadLook>();
            if (!look) look = animator.gameObject.AddComponent<Pirates.Core.HeadLook>();
            look.SetAutoRadius(6f);
        }

        public void Configure(string name, string what, MissionDefinition[] given, string[] chat, Animator model, Behaviour idleBehaviour)
        {
            displayName = name;
            role = what;
            missions = given ?? System.Array.Empty<MissionDefinition>();
            smallTalk = chat ?? System.Array.Empty<string>();
            animator = model;
            idle = idleBehaviour;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            TalkStarted = null;
            StoryEnded = false;
        }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        public float FlatDistance(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        /// <summary>O que ele tem para o jogador agora ("!" ou "?" sobre a cabeça).</summary>
        public Offer Status(MissionBoard board)
        {
            if (HasScriptedTalk) return Offer.Available;
            if (!board) return Offer.None;
            bool available = false;
            for (int i = 0; i < missions.Length; i++)
            {
                var m = missions[i];
                if (!m) continue;
                var a = board.ActiveFor(m);
                if (a != null && board.IsComplete(a)) return Offer.ReadyToTurnIn;
                if (board.IsOfferable(m)) available = true;
            }
            return available ? Offer.Available : Offer.None;
        }

        /// <summary>Começou a conversa: para o que fazia, vira para o capitão e acena.</summary>
        public void BeginTalk(Vector3 player)
        {
            IsTalking = true;
            TalkStarted?.Invoke(this);
            if (idle) idle.enabled = false;
            if (animator) animator.SetFloat("Speed", 0f);
            Vector3 to = player - transform.position;
            to.y = 0f;
            faceTarget = to.sqrMagnitude > 0.01f ? Quaternion.LookRotation(to) : transform.rotation;
            Gesture("Wave");
        }

        public void EndTalk()
        {
            IsTalking = false;
            if (idle) idle.enabled = true;
        }

        /// <summary>Gesto do Animator dos civis ("Wave", "Yes", "No"); sem o estado, nada.</summary>
        public void Gesture(string state)
        {
            if (animator && animator.isActiveAndEnabled && animator.HasState(0, Animator.StringToHash(state)))
                animator.CrossFadeInFixedTime(state, 0.2f);
        }

        void Update()
        {
            if (IsTalking) transform.rotation = Quaternion.RotateTowards(transform.rotation, faceTarget, 240f * Time.deltaTime);
        }
    }
}
