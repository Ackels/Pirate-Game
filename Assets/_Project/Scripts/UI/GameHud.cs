using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Raiz do HUD de jogo em UI Toolkit (um UIDocument por cena; o PanelSettings escala com a tela, referência
    /// 1600×900). Cria as regiões da tela onde cada HUD põe as suas peças — elas se empilham sozinhas, sem retângulos
    /// fixos: canto de cima à esquerda (navio / a pé), meio de cima (faixa do porto), direita (porão, missões), avisos
    /// "[F]" embaixo, faixas grandes no meio, anúncios (missões) e ganhos ("+3 Madeira") — e a camada dos rótulos no mundo.
    /// Também cuida da ajuda dos controles (tecla H) e do clarão vermelho de golpe levado.
    /// Camada de janelas (porto, baú, destroços: recebem o mouse) e, por cima de tudo, a do menu inicial.
    /// Nada aqui aloca por quadro: avisos e rótulos vêm de listas reaproveitadas, texto só muda quando o valor muda.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameHud : MonoBehaviour
    {
        public enum FeedKind { Pickup, Notice }

        [SerializeField] StyleSheet styleSheet;
        [Tooltip("Segundos dos ganhos (+3 Madeira) e dos anúncios (missão aceita).")]
        [SerializeField] float pickupSeconds = 4f, noticeSeconds = 5f;
        [SerializeField] int maxFeedLines = 6;

        static GameHud current;
        static bool searched;

        UIDocument document;
        VisualElement root;
        VisualElement overlay;
        float flashUntil, flashSeconds = 0.25f;
        bool pickupsLowered, lowerRequest;

        struct FeedLine
        {
            public Label label;
            public float start, seconds;
            public bool rise;
        }

        readonly List<FeedLine> pickups = new(), notices = new();
        readonly Stack<Label> feedPool = new();

        /// <summary>O HUD da cena (ou null, nas cenas sem ele). A busca acontece uma vez só.</summary>
        public static GameHud Current
        {
            get
            {
                if (current) return current;
                if (searched) return null;
                searched = true;
                current = FindAnyObjectByType<GameHud>();
                return current;
            }
        }

        public VisualElement World { get; private set; }
        public VisualElement TopLeft { get; private set; }
        public VisualElement TopCenter { get; private set; }
        public VisualElement TopRight { get; private set; }
        public VisualElement Prompts { get; private set; }
        public VisualElement Banners { get; private set; }
        public VisualElement BottomRight { get; private set; }
        /// <summary>Canto de baixo à esquerda: o capitão a pé (retrato, vida, fôlego).</summary>
        public VisualElement BottomLeft { get; private set; }
        /// <summary>Canto de baixo à direita, rente à borda: as armas e os itens a pé.</summary>
        public VisualElement Loadout { get; private set; }
        /// <summary>Janelas com o mouse (porto, baú, destroços), no meio da tela.</summary>
        public VisualElement Windows { get; private set; }
        /// <summary>Menu inicial: tela inteira, por cima de tudo.</summary>
        public VisualElement Menu { get; private set; }
        VisualElement pickupArea, noticeArea;

        /// <summary>Ajuda dos controles na tela (H liga e desliga).</summary>
        public bool HelpVisible { get; private set; }
        /// <summary>Alguém está digitando (nome do capitão no menu): as teclas do HUD (H) não valem.</summary>
        public bool TypingText { get; set; }

        public void Configure(StyleSheet hudStyle) => styleSheet = hudStyle;

        // Suporte a "Enter Play Mode" sem domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            current = null;
            searched = false;
        }

        void Awake()
        {
            current = this;
            searched = true;
        }

        void OnDestroy()
        {
            if (current == this) current = null;
        }

        /// <summary>Monta as regiões na primeira vez que alguém pede (o UIDocument cria a raiz no OnEnable dele).</summary>
        public bool EnsureBuilt()
        {
            if (root != null) return true;
            if (!document) document = GetComponent<UIDocument>();
            if (!document || document.rootVisualElement == null) return false;
            root = document.rootVisualElement;
            if (styleSheet) root.styleSheets.Add(styleSheet);
            root.AddToClassList("hud");
            root.pickingMode = PickingMode.Ignore;

            World = Region("hud-world");
            TopCenter = Region("hud-top-center");
            TopLeft = Region("hud-top-left");
            TopRight = Region("hud-top-right");
            // Janelas por cima dos cartões; anúncios, faixas, ganhos e avisos por cima das janelas.
            Windows = Region("hud-windows");
            noticeArea = Region("hud-notices");
            Banners = Region("hud-banners");
            pickupArea = Region("hud-pickups");
            Prompts = Region("hud-prompts");
            BottomLeft = Region("hud-bottom-left");
            Loadout = Region("hud-loadout");
            BottomRight = Region("hud-bottom-right");
            overlay = Region("hud-hurt");
            overlay.style.opacity = 0f;
            Menu = Region("hud-menu");
            return true;
        }

        VisualElement Region(string className)
        {
            var region = new VisualElement { name = className, pickingMode = PickingMode.Ignore };
            region.AddToClassList(className);
            root.Add(region);
            return region;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.hKey.wasPressedThisFrame && !TypingText) ToggleHelp();
        }

        void LateUpdate()
        {
            if (root == null) return;
            UpdateFeed(pickups, pickupSeconds);
            UpdateFeed(notices, noticeSeconds);

            // Janela aberta no meio da tela: os ganhos descem para baixo dela.
            if (lowerRequest != pickupsLowered)
            {
                pickupsLowered = lowerRequest;
                pickupArea.EnableInClassList("hud-pickups--low", pickupsLowered);
            }
            lowerRequest = false;

            if (Time.time < flashUntil) overlay.style.opacity = (flashUntil - Time.time) / flashSeconds;
            else if (overlay.resolvedStyle.opacity > 0f) overlay.style.opacity = 0f;
        }

        public void ToggleHelp() => HelpVisible = !HelpVisible;

        /// <summary>Borda vermelha rápida (golpe levado).</summary>
        public void Flash(float seconds = 0.25f)
        {
            flashSeconds = Mathf.Max(0.01f, seconds);
            flashUntil = Time.time + flashSeconds;
        }

        /// <summary>Pede, neste quadro, que os ganhos fiquem embaixo da janela aberta (não atrás dela).</summary>
        public void LowerPickups() => lowerRequest = true;

        // ---------- Avisos ----------

        /// <summary>Ganho ("+3 Madeira", sobe e some) ou anúncio (missão aceita, obra pronta). Texto pode ter rich text.</summary>
        public void Feed(FeedKind kind, string text, Color color)
        {
            if (!EnsureBuilt()) return;
            var lines = kind == FeedKind.Pickup ? pickups : notices;
            var area = kind == FeedKind.Pickup ? pickupArea : noticeArea;
            if (lines.Count >= maxFeedLines) Recycle(lines, 0);

            var label = feedPool.Count > 0 ? feedPool.Pop() : NewFeedLabel();
            label.text = text;
            label.style.color = color;
            label.style.opacity = 1f;
            label.style.translate = new StyleTranslate(new Translate(0f, 0f));
            label.EnableInClassList("hud-feed--notice", kind == FeedKind.Notice);
            area.Add(label);
            lines.Add(new FeedLine
            {
                label = label,
                start = Time.time,
                seconds = kind == FeedKind.Pickup ? pickupSeconds : noticeSeconds,
                rise = kind == FeedKind.Pickup,
            });
        }

        static Label NewFeedLabel()
        {
            var label = new Label { pickingMode = PickingMode.Ignore, enableRichText = true };
            label.AddToClassList("hud-feed");
            return label;
        }

        void UpdateFeed(List<FeedLine> lines, float seconds)
        {
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                var line = lines[i];
                float age = Time.time - line.start;
                if (age > line.seconds)
                {
                    Recycle(lines, i);
                    continue;
                }
                line.label.style.opacity = Mathf.Clamp01(line.seconds - age);
                if (line.rise) line.label.style.translate = new StyleTranslate(new Translate(0f, -age * 6f));
            }
        }

        void Recycle(List<FeedLine> lines, int index)
        {
            var label = lines[index].label;
            label.RemoveFromHierarchy();
            feedPool.Push(label);
            lines.RemoveAt(index);
        }
    }
}
