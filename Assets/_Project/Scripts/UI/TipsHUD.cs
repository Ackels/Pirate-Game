using Pirates.Controls;
using Pirates.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// Diário de dicas (12/11, análise de 10/11: muita mecânica que o tutorial não explica): aba "Dicas" na tela do capitão. À esquerda os
    /// assuntos, à direita as dicas do escolhido. Os textos ficam aqui (mecânica nova = um assunto ou uma linha a mais).
    /// </summary>
    public class TipsHUD : MonoBehaviour
    {
        static readonly (string title, string[] tips)[] Topics =
        {
            ("Luta a pé", new[]
            {
                "<b>Botão direito</b> com a lâmina na mão ergue a guarda: o golpe de frente vira arranhão (gasta fôlego).",
                "<b>Aparar:</b> erga a guarda quando o aviso de espadas fica <b>dourado</b> (\"!!\"). O inimigo cambaleia e o próximo golpe tira 50% a mais.",
                "Aviso <b>azul com caveira</b> = a guarda não segura (tiro, salto, almas do chão): <b>role com Ctrl</b>.",
                "<b>V</b> chuta e derruba (o brutamontes e os chefes não caem). Bater pelas costas com a adaga dobra o dano.",
                "O fôlego (barra verde) volta sozinho: rolar custa 25, segurar a guarda gasta aos poucos.",
            }),
            ("Armas e itens a pé", new[]
            {
                "<b>1, 2, 3</b> trocam de arma. A recarga só anda com a arma na mão.",
                "O bacamarte pega todo mundo no leque: quanto mais perto, mais chumbo acerta.",
                "<b>Segure G</b> para mirar a bomba (o círculo fica vermelho se você está dentro da explosão); solte para lançar.",
                "<b>B</b> estoura uma bomba de fumaça: quem está dentro não vê você e os piratas pegos nela ficam cegos.",
                "<b>R</b> come uma ração (ou o peixe mais barato). Barris vermelhos explodem com tiro, bala ou outra explosão.",
            }),
            ("Navio e canhões", new[]
            {
                "<b>Q / E</b> armam a mira do bombordo / estibordo; o cursor no mar escolhe distância e ângulo; o <b>clique</b> dispara.",
                "<b>1–4</b> trocam a munição: corrente rasga velas (o inimigo fica lento), metralha mata tripulação, incendiária põe fogo.",
                "Os canhões de caça (melhoria do Estaleiro) atiram para a proa e para a popa quando o cursor está à frente ou atrás.",
                "<b>R</b> no navio chama o carpinteiro (gasta madeira; só sem levar tiro). O vento por trás e de lado faz o navio andar mais.",
            }),
            ("Abordagem", new[]
            {
                "Avarie o casco do inimigo até a metade, chegue a ~24 m devagar e aperte <b>F</b> para lançar os ganchos.",
                "Derrube a tripulação no convés; depois escolha <b>Saquear o porão</b> ou <b>Recrutar os rendidos</b>.",
                "O navio tomado fica à deriva e afunda sozinho em ~1,5 min (afundado, só solta madeira).",
            }),
            ("Procurado e Esquadra Real", new[]
            {
                "Atacar ou afundar <b>mercantes</b> (bandeira azul) é pirataria: rende estrelas de procurado.",
                "Com 2 estrelas ou mais, a Esquadra Real vem atrás de você. <b>Saia da vista</b> dela por 60 s para despistar.",
                "As estrelas caem com o tempo; o <b>Almirante Teodoro</b>, no estaleiro, perdoa por uma multa.",
            }),
            ("Pesca", new[]
            {
                "Com a vara, de frente para água funda: <b>F</b> lança, <b>F</b> de novo fisga quando a boia afunda.",
                "Na briga, segure <b>Espaço</b> para a barra verde acompanhar o peixe até o medidor encher.",
                "Iscas (cozinha da taverna e encomendas da Iara) atraem peixes raros; a dourada, os lendários.",
                "No navio quase parado dentro das bolhas de um <b>cardume</b>: <b>F</b> lança a rede. O diário do pescador fica na tecla <b>P</b>.",
            }),
            ("Cozinha, Forja e relíquias", new[]
            {
                "A <b>cozinha da taverna</b> faz pratos com peixe; coma pela tela <b>I</b> para ter um bônus por alguns minutos.",
                "A <b>Forja</b> faz munição, bombas, veneno para a lâmina, bombas de fumaça, carga incendiária e as relíquias.",
                "Relíquias (encaixe na tela I) dão mais ouro, mais chance de baú e de equipamento.",
            }),
            ("Marujos em terra", new[]
            {
                "Ao desembarcar numa ilha, dois marujos da tripulação descem e lutam com você.",
                "<b>Segure T</b> para a roda de ordens: seguir, ficar, ir até lá, atacar um alvo, proteger ou não lutar.",
                "Marujo caído levanta quando a briga acaba: a tripulação não perde ninguém.",
            }),
            ("Exploração", new[]
            {
                "<b>M</b> abre a carta do mar. A ilha ganha nome na carta quando você chega perto dela.",
                "Longe, os objetivos aparecem só como uma seta na borda da tela; o lugar surge da névoa ao se aproximar.",
                "Três <b>pedaços de mapa</b> montam um mapa do tesouro: vá ao X e cave com <b>F</b>.",
                "Garrafas com mensagem e moedas da Frota de Prata ficam escondidas nas ilhas (contador no painel <b>C</b>).",
                "Machadinha e picareta (mercado) cortam árvores e mineram veios; tudo volta depois de um tempo.",
            }),
        };

        GameHud hud;
        HudWindow window;
        bool open;
        int selected;

        public bool IsOpen => open;

        void OnDisable() => SetOpen(false);
        void OnDestroy() => CaptainBook.Unregister(CaptainBook.Tab.Tips);

        void Update()
        {
            if (window == null && !Build()) return;
            var input = GameInput.Instance;
            if (open && (!input.GameplayEnabled || input.Cancel.WasPressedThisFrame())) SetOpen(false);
            if (open)
            {
                window.RefreshAll();
                hud.LowerPickups();
            }
        }

        /// <summary>Testes pela CLI.</summary>
        public void OpenTab() => CaptainBook.Open(CaptainBook.Tab.Tips);

        void SetOpen(bool value)
        {
            if (value && window == null) return;
            if (open == value) return;
            open = value;
            ScreenPanel.Open = value;
            if (value) CaptainBook.RefreshTabs(window);
            window?.Show(value);
            if (hud != null && hud.Prompts != null) Hud.Show(hud.Prompts, !value);
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            window = new HudWindow(hud.Windows, "hud-window--tips");
            window.SetTitle("Dicas do capitão", "Esc fecha");
            CaptainBook.AddTabs(window, CaptainBook.Tab.Tips);
            CaptainBook.Register(CaptainBook.Tab.Tips, () => open, SetOpen);
            var row = Hud.Row();
            row.style.alignItems = Align.FlexStart;
            window.Body.Add(row);
            var list = Hud.Box();
            list.style.width = 260;
            list.style.flexShrink = 0;
            row.Add(list);
            var page = Hud.Box("hud-grow");
            page.style.marginLeft = 18;
            row.Add(page);
            window.AddPane(list, () => selected, BuildList);
            window.AddPane(page, () => selected, BuildPage);
            Win.Spacer(window.Footer);
            window.Footer.Add(Win.Button("Fechar  <b>[Esc]</b>", () => SetOpen(false)));
            return true;
        }

        void BuildList(VisualElement list)
        {
            for (int i = 0; i < Topics.Length; i++)
            {
                int k = i;
                var b = Win.Button(Topics[i].title, () => selected = k, true, "hud-book-tab");
                b.style.marginBottom = 4;
                if (i == selected) b.AddToClassList("hud-book-tab--on");
                list.Add(b);
            }
        }

        void BuildPage(VisualElement page)
        {
            var topic = Topics[Mathf.Clamp(selected, 0, Topics.Length - 1)];
            Win.Text(page, topic.title, "hud-window__subtitle-strong");
            foreach (var tip in topic.tips)
            {
                var t = Win.Text(page, tip, "hud-window__text");
                t.style.marginTop = 6;
                t.style.whiteSpace = WhiteSpace.Normal;
            }
        }
    }
}
