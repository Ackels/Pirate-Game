using System;
using Pirates.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Prólogo "A Coroa de Prata" (11/10): os 9 quadros da abertura (<c>UI/Prologo</c>, feitos pelo gerador) em tela cheia,
    /// cada um com um zoom lento e um passeio de câmera, a narração embaixo ({nome} = o nome do capitão) e o título do jogo
    /// no último. Toca depois do nome no "Novo jogo" (o jogo continua parado atrás) e pelo botão "Prólogo" do menu.
    /// Clique, Espaço ou Enter passam o quadro; Esc pula tudo. A música é a do menu (<see cref="TitleScreen"/>).
    /// Usa o tempo sem escala: o jogo está pausado.
    /// </summary>
    public class PrologueHUD : MonoBehaviour
    {
        [SerializeField] Texture2D[] frames = Array.Empty<Texture2D>();
        [Tooltip("Narração de cada quadro (mesma ordem). {nome} vira o nome do capitão.")]
        [SerializeField, TextArea(2, 4)] string[] lines =
        {
            "Dizem que no fundo do Mar de Prata dorme a Coroa de Prata, a nau capitânia da frota mais rica que já cruzou estas águas.",
            "Há cem anos, capitães morrem procurando por ela.",
            "{nome} chegou mais perto que todos. Mas não chegou sozinho.",
            "Lázaro Negro esperou o mapa ficar pronto... e então mostrou quem era.",
            "Ficou com o mapa. Ficou com a tripulação. Ficou com tudo.",
            "Deixou {nome} para o mar. O mar não quis.",
            "Três dias depois, as ondas o levaram a Porto Alento: uma vila esquecida que paga tributo a Lázaro.",
            "Lá, um velho almirante lhe ofereceu um barco remendado, um cais quebrado... e uma chance.",
            "Agora, o Mar de Prata vai aprender o seu nome.",
        };
        [SerializeField] string gameTitle = "A Coroa de Prata";
        [Tooltip("Segundos mínimos de cada quadro; textos longos ficam mais (segundos por letra).")]
        [SerializeField] float minFrameSeconds = 6.5f, secondsPerLetter = 0.055f;
        [Tooltip("O último quadro (título) fica mais um pouco.")]
        [SerializeField] float titleExtraSeconds = 2.5f;
        [SerializeField] float fadeSeconds = 0.9f;

        VisualElement root, image, veil;
        Label text, title, hint;
        Action done;
        string captain;
        int index = -1;
        float frameStart, frameLength, playStart;

        public bool IsPlaying => index >= 0;

        /// <summary>Quadro mostrado agora (testes; −1 = parado).</summary>
        public int Frame => index;

        public int FrameCount => frames.Length;

        /// <summary>Quadros da abertura (o gerador põe os PNG de UI/Prologo).</summary>
        public void Configure(Texture2D[] prologueFrames) => frames = prologueFrames;

        /// <summary>Começa do primeiro quadro; <paramref name="onDone"/> roda no fim ou ao pular.</summary>
        public void Play(string captainName, Action onDone)
        {
            done = onDone;
            if (frames.Length == 0 || !Build())
            {
                Finish();
                return;
            }
            captain = string.IsNullOrWhiteSpace(captainName) ? "O capitão" : captainName.Trim();
            playStart = Time.unscaledTime;
            Hud.Show(root, true);
            TitleScreen.Showing = true;
            Show(0);
        }

        /// <summary>Próximo quadro (no último, termina).</summary>
        public void Next()
        {
            if (!IsPlaying) return;
            if (index + 1 >= frames.Length) Finish();
            else Show(index + 1);
        }

        /// <summary>Pula o resto.</summary>
        public void Skip()
        {
            if (IsPlaying) Finish();
        }

        void Finish()
        {
            index = -1;
            if (root != null) Hud.Show(root, false);
            var callback = done;
            done = null;
            callback?.Invoke();
        }

        void OnDisable()
        {
            if (IsPlaying) Finish();
        }

        bool Build()
        {
            if (root != null) return true;
            var hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            root = Hud.Box("hud-prologue");
            root.pickingMode = PickingMode.Position;
            image = Hud.Box("hud-prologue__image");
            root.Add(image);
            title = Hud.Text(gameTitle, "hud-prologue__title");
            root.Add(title);
            var band = Hud.Box("hud-prologue__band");
            text = Hud.Text("", "hud-prologue__text");
            band.Add(text);
            root.Add(band);
            veil = Hud.Box("hud-prologue__veil");
            root.Add(veil);
            hint = Hud.Text("Clique ou Espaço: continuar   ·   Esc: pular", "hud-prologue__hint");
            root.Add(hint);
            hud.Menu.Add(root);
            return true;
        }

        void Show(int i)
        {
            index = i;
            frameStart = Time.unscaledTime;
            image.style.backgroundImage = new StyleBackground(frames[i]);
            string line = i < lines.Length ? lines[i].Replace("{nome}", captain) : "";
            Hud.Set(text, line);
            bool last = i == frames.Length - 1;
            frameLength = Mathf.Max(minFrameSeconds, 2.5f + line.Length * secondsPerLetter) + (last ? titleExtraSeconds : 0f);
            Animate();
        }

        void Update()
        {
            if (!IsPlaying) return;
            // Um instante de guarda: o clique que abriu (Zarpar!, Prólogo) não passa o primeiro quadro.
            bool ready = Time.unscaledTime - frameStart > 0.35f && Time.unscaledTime - playStart > 0.6f;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Finish();
                return;
            }
            bool advance = (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame ||
                                                 keyboard.numpadEnterKey.wasPressedThisFrame))
                           || (mouse != null && mouse.leftButton.wasPressedThisFrame);
            if (ready && advance)
            {
                Next();
                return;
            }
            if (Time.unscaledTime - frameStart >= frameLength)
            {
                Next();
                return;
            }
            Animate();
        }

        /// <summary>Zoom lento (para dentro ou para fora, alternando), passeio para um lado e fade do preto nas pontas.</summary>
        void Animate()
        {
            float age = Time.unscaledTime - frameStart;
            float t = Mathf.Clamp01(age / frameLength);
            float ease = t * t * (3f - 2f * t);
            bool zoomIn = index % 2 == 0;
            float scale = zoomIn ? Mathf.Lerp(1.04f, 1.14f, ease) : Mathf.Lerp(1.14f, 1.05f, ease);
            float side = (index % 3 - 1) * 26f;
            image.style.scale = new Scale(new Vector3(scale, scale, 1f));
            image.style.translate = new Translate(Mathf.Lerp(-side, side, ease), Mathf.Lerp(8f, -8f, ease));

            float fadeIn = Mathf.Clamp01(age / fadeSeconds);
            float fadeOut = Mathf.Clamp01((frameLength - age) / fadeSeconds);
            veil.style.opacity = 1f - Mathf.Min(fadeIn, fadeOut);
            text.style.opacity = Mathf.Clamp01((age - 0.6f) / 0.8f) * fadeOut;

            bool last = index == frames.Length - 1;
            title.style.opacity = last ? Mathf.Clamp01((age - 1.2f) / 1.5f) * fadeOut : 0f;
            hint.style.opacity = Mathf.Clamp01((Time.unscaledTime - playStart - 1f) / 1f) * 0.8f;
        }
    }
}
