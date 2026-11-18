using System;
using UnityEngine;

namespace Pirates.OnFoot
{
    /// <summary>
    /// Golpes feitos à mão para o capitão, um jogo por arma (<see cref="Moveset"/>: espadão e machado; as outras armas entram aqui).
    /// Começou pelo golpe do espadão feito à mão para o capitão (17/11, chat "Animador"; Jhonata: "os braços ficam se atravessando, encurtam demais,
    /// passam por dentro do corpo, junto com o espadão... queria algo mais Dark Souls"). Os golpes gravados são de gente de braço comprido
    /// e, no capitão cabeçudo (braço 0,21 + antebraço 0,22 m, ombros a 0,36 m), as correções do IK brigavam com eles. Aqui o caminho da
    /// espada é desenhado: poses-chave da mão direita (no espaço do personagem, em metros na escala 1), direção da lâmina (giro e
    /// inclinação) e torção/inclinação do tronco, com preparo, corte rápido, continuação e volta à guarda da animação. Os dois braços vão ao
    /// cabo por IK de duas juntas com o cotovelo apontado sempre para o mesmo lado (sem virar), e a mão esquerda fica no cabo, mais perto do
    /// pomo. Os três golpes do combo (AttackVariant 0–2 da arma de tipo 4 do capitão): corte da direita para a esquerda, corte de volta
    /// da esquerda para a direita e a diagonal de cima que derruba. Cada golpe segura a continuação um pouco (<see cref="Sequence.hold"/>):
    /// o próximo do combo sai 0,9 s depois do anterior e parte de onde ele parou (a espada emenda sem voltar à guarda).
    /// </summary>
    [DefaultExecutionOrder(45)]
    public class GreatswordSwing : MonoBehaviour
    {
        [Serializable]
        public struct Key
        {
            [Tooltip("Segundos desde o começo do golpe.")] public float time;
            [Tooltip("Pulso direito no espaço do personagem (m na escala 1; x direita, y cima, z frente).")] public Vector3 grip;
            [Tooltip("Lâmina: giro (graus a partir da frente, + para a direita) e inclinação (+ para cima).")] public float bladeYaw, bladePitch;
            [Tooltip("Tronco: torção (+ vira para a direita) e inclinação para a frente (graus).")] public float torsoYaw, torsoPitch;
            [Tooltip("Como chega nesta pose: 0 = suave, 1 = acelerando (corte), 2 = freando.")] public int ease;
            [Tooltip("Giro da lâmina no próprio eixo (graus): 0 = largura deitada (o fio vai na frente de um corte horizontal).")] public float roll;
            [Tooltip("Armas de uma mão: pulso esquerdo (a mão livre) no espaço do personagem, para o contrapeso do golpe.")] public Vector3 free;
            public bool hasFree;
            public Key(float time, Vector3 grip, float yaw, float pitch, float torsoYaw, float torsoPitch, int ease, float roll = 0f)
            {
                this.time = time; this.grip = grip; bladeYaw = yaw; bladePitch = pitch; this.torsoYaw = torsoYaw; this.torsoPitch = torsoPitch; this.ease = ease; this.roll = roll;
                free = Vector3.zero; hasFree = false;
            }
            /// <summary>Esta pose com a mão livre (esquerda) num ponto do espaço do personagem.</summary>
            public Key Free(float x, float y, float z) { var k = this; k.free = new Vector3(x, y, z); k.hasFree = true; return k; }
        }

        [Serializable]
        public class Sequence
        {
            public string name;
            public Key[] keys;
            [Tooltip("Segura a última pose (s) antes de voltar à guarda: o próximo golpe do combo emenda daqui.")] public float hold;
            [Tooltip("Da última pose até a volta inteira à guarda da animação (s).")] public float recover = 0.35f;
            [Tooltip("Passo para a frente no corte (m na escala 1), dado na 2ª pose.")] public float step = 0.3f;
            [Tooltip("Giro do corpo inteiro (graus; − = para a esquerda) entre spinFrom e spinTo: as poses giram junto.")] public float spin, spinFrom, spinTo;
            [Tooltip("Salto (m na escala 1) entre jumpFrom e jumpTo; na aterrissagem levanta poeira.")] public float jump, jumpFrom, jumpTo;
            [Tooltip("Os pés seguem o corpo (giro, salto) em vez de ficarem plantados.")] public bool freeFeet;
            [Tooltip("Passo para o lado junto com o da frente (m na escala 1; + direita), e quanto tempo o passo leva (s; 0 = o padrão).")]
            public float side, stepTime;
            public float End => keys[keys.Length - 1].time + hold;
        }

        [Serializable]
        public class Moveset
        {
            public string name;
            [Tooltip("animatorWeaponType da arma (4 = espadão, 3 = machado).")] public int weaponType;
            [Tooltip("As duas mãos no cabo; senão a esquerda fica livre (Key.free).")] public bool twoHanded = true;
            [Tooltip("Para que lado do cabo fica o fio (1 = o eixo Y da arma vai na frente de um corte horizontal; −1 = o −Y).")] public float edgeSign = 1f;
            [Tooltip("Do pulso à ponta da arma (m na escala 1).")] public float bladeReach = 1.1f;
            [Tooltip("Quanto do cabo fica para trás da mão direita (m), contando o pomo.")] public float pommelBehind = 0.24f;
            [Tooltip("Duas mãos: a esquerda no cabo, quanto mais perto do pomo que a direita (m).")] public float leftOnHandle = 0.11f;
            [Tooltip("A mão escorrega no cabo para o pomo durante o golpe (m; a arma anda para a frente na mão) e volta na guarda.")] public float gripSlide;
            [Tooltip("Só para as armas que pedem este jogo pelo nome (WeaponDefinition.handMoves): o cutelo e a adaga têm o mesmo tipo.")] public bool byNameOnly;
            [Tooltip("Os tempos das poses andam com a velocidade da arma (WeaponDefinition.attackAnimSpeed): os sabres golpeiam mais rápido.")] public bool timeScaled;
            [Tooltip("Parte do giro do pulso (no eixo do antebraço) passada para o antebraço: o punho não torce sozinho mais de 90°.")]
            [Range(0f, 1f)] public float forearmTwist;
            [Tooltip("Na volta à guarda, a mão e a arma andam até a pose da animação pelo alvo da mão (misturar as juntas fazia a ponta passar pelo chão).")]
            public bool guidedRecovery;
            [Tooltip("A lâmina sai pelo −Z do visual (a adaga é montada na mão ao contrário do cutelo).")] public bool reverseBlade;
            [Tooltip("Mão livre: o braço estica pelo menos esta fração do comprimento (0 = sem limite; nos inimigos o braço dobrava todo e o cotovelo subia).")]
            [Range(0f, 1f)] public float freeMinReach;
            public Sequence[] combo;
        }

        [SerializeField] Moveset[] movesets =
        {
            // Espadão (Montante, Espadão do Barba-Negra): o golpe 1 acerta aos 0,42 s, o 2 aos 0,32 e o 3 aos 0,36 (variantHitDelays); o
            // impacto de cada um é a 4ª pose.
            new Moveset
            {
                name = "Espadão", weaponType = 4, twoHanded = true, edgeSign = 1f, bladeReach = 1.1f, pommelBehind = 0.24f, leftOnHandle = 0.11f,
                combo = new[]
                {
                    // Três versões sorteadas por posição do combo (WeaponDefinition.comboAlternates 3): 0–2, 3–5, 6–8.
                    new Sequence
                    {
                        name = "1º: corte da direita para a esquerda", hold = 0.2f, recover = 0.35f, step = 0.3f,
                        keys = new[]
                        {
                            new Key(0.26f, new Vector3(0.34f, 0.66f, 0.00f), 138f, 14f, 38f, -8f, 2),
                            new Key(0.31f, new Vector3(0.35f, 0.67f, -0.02f), 145f, 16f, 42f, -9f, 0),
                            new Key(0.37f, new Vector3(0.24f, 0.60f, 0.30f), 90f, 6f, 15f, 5f, 1),
                            new Key(0.42f, new Vector3(0.10f, 0.55f, 0.30f), -25f, 0f, -10f, 14f, 0),
                            new Key(0.60f, new Vector3(-0.05f, 0.50f, 0.16f), -95f, -12f, -42f, 10f, 2),
                        },
                    },
                    new Sequence
                    {
                        // Recolhe as mãos ao lado do quadril com a lâmina apontada e avança esticando os dois braços.
                        name = "1º b: estocada de duas mãos", hold = 0.15f, recover = 0.35f, step = 0.6f,
                        keys = new[]
                        {
                            new Key(0.20f, new Vector3(0.20f, 0.54f, -0.06f), 8f, 6f, 30f, -6f, 2, 90f),
                            new Key(0.26f, new Vector3(0.21f, 0.54f, -0.08f), 8f, 6f, 34f, -8f, 0, 90f),
                            new Key(0.34f, new Vector3(0.12f, 0.56f, 0.22f), 3f, 3f, 10f, 8f, 1, 90f),
                            new Key(0.40f, new Vector3(0.04f, 0.58f, 0.40f), 0f, 0f, -6f, 14f, 0, 90f),
                            new Key(0.56f, new Vector3(0.06f, 0.56f, 0.34f), 2f, -4f, -4f, 12f, 2, 90f),
                        },
                    },
                    new Sequence
                    {
                        // Abaixa e varre baixo, da direita para a esquerda, num arco bem largo.
                        name = "1º c: corte baixo varrendo", hold = 0.15f, recover = 0.38f, step = 0.3f,
                        keys = new[]
                        {
                            new Key(0.26f, new Vector3(0.34f, 0.50f, 0.00f), 130f, -5f, 38f, 12f, 2),
                            new Key(0.31f, new Vector3(0.35f, 0.49f, -0.02f), 136f, -6f, 42f, 14f, 0),
                            new Key(0.37f, new Vector3(0.28f, 0.40f, 0.30f), 80f, -20f, 15f, 15f, 1),
                            new Key(0.42f, new Vector3(0.10f, 0.36f, 0.32f), -25f, -24f, -10f, 17f, 0),
                            new Key(0.60f, new Vector3(0.02f, 0.40f, 0.28f), -78f, -18f, -38f, 14f, 2),
                        },
                    },
                    new Sequence
                    {
                        name = "2º: corte de volta, da esquerda para a direita", hold = 0.3f, recover = 0.35f, step = 0.25f,
                        keys = new[]
                        {
                            new Key(0.17f, new Vector3(-0.08f, 0.62f, 0.08f), -140f, 14f, -55f, -8f, 2),
                            new Key(0.22f, new Vector3(-0.09f, 0.63f, 0.06f), -148f, 16f, -60f, -9f, 0),
                            new Key(0.27f, new Vector3(0.00f, 0.58f, 0.34f), -75f, 6f, -35f, 5f, 1),
                            new Key(0.32f, new Vector3(0.14f, 0.56f, 0.36f), 30f, 2f, -10f, 14f, 0),
                            new Key(0.50f, new Vector3(0.30f, 0.52f, 0.26f), 95f, -10f, 8f, 10f, 2),
                        },
                    },
                    new Sequence
                    {
                        // Giro largo: o espadão esticado à direita e o corpo dá uma volta inteira para a esquerda (pega todos em volta).
                        name = "2º b: giro largo", hold = 0.12f, recover = 0.4f, step = 0.2f, spin = -360f, spinFrom = 0.22f, spinTo = 0.56f, freeFeet = true,
                        keys = new[]
                        {
                            new Key(0.16f, new Vector3(0.30f, 0.62f, 0.02f), 130f, 8f, 40f, -8f, 2),
                            new Key(0.22f, new Vector3(0.31f, 0.62f, 0.00f), 135f, 8f, 42f, -9f, 0),
                            new Key(0.28f, new Vector3(0.32f, 0.58f, 0.22f), 80f, 0f, 15f, 4f, 1),
                            new Key(0.48f, new Vector3(0.32f, 0.56f, 0.24f), 75f, 0f, 5f, 8f, 0),
                            new Key(0.66f, new Vector3(-0.04f, 0.50f, 0.18f), -90f, -12f, -40f, 10f, 2),
                        },
                    },
                    new Sequence
                    {
                        // Lâmina baixa à esquerda, corta subindo na diagonal até o alto da direita.
                        name = "2º c: corte subindo da esquerda", hold = 0.15f, recover = 0.38f, step = 0.3f,
                        keys = new[]
                        {
                            new Key(0.14f, new Vector3(-0.06f, 0.48f, 0.12f), -120f, -30f, -40f, 14f, 2, 30f),
                            new Key(0.19f, new Vector3(-0.07f, 0.47f, 0.10f), -125f, -32f, -44f, 16f, 0, 30f),
                            new Key(0.26f, new Vector3(0.02f, 0.52f, 0.32f), -55f, -10f, -20f, 8f, 1, 30f),
                            new Key(0.32f, new Vector3(0.18f, 0.74f, 0.32f), 25f, 30f, 0f, -4f, 0, 50f),
                            new Key(0.50f, new Vector3(0.28f, 0.84f, 0.20f), 70f, 42f, 10f, -10f, 2, 50f),
                        },
                    },
                    new Sequence
                    {
                        name = "3º: diagonal de cima (derruba)", hold = 0.15f, recover = 0.45f, step = 0.38f,
                        keys = new[]
                        {
                            new Key(0.20f, new Vector3(0.30f, 0.74f, 0.04f), 118f, 38f, 25f, -12f, 2, 30f),
                            new Key(0.26f, new Vector3(0.31f, 0.76f, 0.02f), 122f, 42f, 28f, -14f, 0, 30f),
                            new Key(0.31f, new Vector3(0.20f, 0.68f, 0.28f), 55f, 22f, 0f, 6f, 1, 40f),
                            new Key(0.36f, new Vector3(0.06f, 0.48f, 0.34f), -5f, -22f, -15f, 24f, 0, 45f),
                            new Key(0.55f, new Vector3(-0.02f, 0.46f, 0.30f), -28f, -26f, -20f, 22f, 2, 45f),
                        },
                    },
                    new Sequence
                    {
                        // Agacha com o espadão erguido, salta para a frente e desce o golpe de cima ao aterrissar (derruba, poeira).
                        name = "3º b: salto com golpe de cima (derruba)", hold = 0.1f, recover = 0.5f, step = 0.9f, jump = 0.4f, jumpFrom = 0.22f, jumpTo = 0.52f, freeFeet = true,
                        keys = new[]
                        {
                            new Key(0.16f, new Vector3(0.34f, 0.62f, -0.06f), 130f, 30f, 20f, 18f, 2, 30f),
                            new Key(0.22f, new Vector3(0.36f, 0.76f, -0.02f), 130f, 45f, 15f, -12f, 0, 30f),
                            new Key(0.42f, new Vector3(0.37f, 0.78f, 0.00f), 132f, 48f, 10f, -14f, 2, 30f),
                            new Key(0.50f, new Vector3(0.18f, 0.70f, 0.28f), 40f, 30f, 0f, 8f, 1, 40f),
                            new Key(0.55f, new Vector3(0.06f, 0.46f, 0.34f), -5f, -25f, -10f, 24f, 0, 45f),
                            new Key(0.72f, new Vector3(0.02f, 0.44f, 0.30f), -15f, -28f, -15f, 22f, 2, 45f),
                        },
                    },
                    new Sequence
                    {
                        // Abaixa com a lâmina baixa à direita e corta subindo pela frente (joga o inimigo para trás).
                        name = "3º c: corte subindo (derruba e empurra)", hold = 0.12f, recover = 0.45f, step = 0.35f,
                        keys = new[]
                        {
                            new Key(0.20f, new Vector3(0.30f, 0.44f, -0.04f), 150f, -30f, 25f, 18f, 2, 135f),
                            new Key(0.26f, new Vector3(0.31f, 0.42f, -0.06f), 155f, -32f, 28f, 20f, 0, 135f),
                            new Key(0.34f, new Vector3(0.24f, 0.50f, 0.26f), 50f, -12f, 8f, 10f, 1, 120f),
                            new Key(0.40f, new Vector3(0.16f, 0.72f, 0.36f), 5f, 32f, -6f, -6f, 0, 100f),
                            new Key(0.58f, new Vector3(0.14f, 0.80f, 0.32f), -10f, 42f, -12f, -8f, 2, 100f),
                        },
                    },
                },
            },
            // Machados do capitão (de abordagem e do carrasco): uma mão, cabeça na ponta +Z e fio no −Y da arma. Na guarda o pulso fica a
            // 0,40 m da ponta da cabeça e 0,51 m do fim do cabo (o cabo passava por cima do ombro no golpe): no golpe a mão escorrega até
            // perto do fim do cabo (gripSlide) e golpeia com o machado inteiro. Acertam aos 0,32, 0,42 e 0,45 s; o próximo do combo sai 0,85 s depois. O braço do gancho faz
            // o contrapeso: vai para a frente no preparo (mirando) e é puxado para trás no corte.
            new Moveset
            {
                name = "Machado", weaponType = 3, twoHanded = false, edgeSign = -1f, bladeReach = 0.80f, pommelBehind = 0.12f, leftOnHandle = 0f,
                gripSlide = 0.39f,
                combo = new[]
                {
                    // Cada posição do combo tem duas versões sorteadas (WeaponDefinition.comboAlternates 2): 0/1, 2/3, 4/5.
                    new Sequence
                    {
                        name = "1º a: machadada na diagonal, da direita para a esquerda", hold = 0.18f, recover = 0.3f, step = 0.25f,
                        keys = new[]
                        {
                            new Key(0.17f, new Vector3(0.36f, 0.74f, -0.02f), 140f, 28f, 32f, -10f, 2, 20f).Free(-0.18f, 0.60f, 0.42f),
                            new Key(0.21f, new Vector3(0.37f, 0.75f, -0.04f), 145f, 30f, 35f, -11f, 0, 20f).Free(-0.18f, 0.61f, 0.42f),
                            new Key(0.27f, new Vector3(0.22f, 0.70f, 0.26f), 70f, 18f, 10f, 6f, 1, 25f).Free(-0.24f, 0.55f, 0.20f),
                            new Key(0.32f, new Vector3(0.06f, 0.55f, 0.38f), -10f, -10f, -10f, 16f, 0, 30f).Free(-0.32f, 0.47f, -0.06f),
                            new Key(0.48f, new Vector3(-0.14f, 0.50f, 0.28f), -75f, -14f, -30f, 12f, 2, 30f).Free(-0.32f, 0.46f, -0.08f),
                        },
                    },
                    new Sequence
                    {
                        // O gancho fisga à frente (o combate puxa o inimigo aos 0,17 s), o corpo torce para a direita puxando e o machado desce.
                        name = "1º b: gancho que fisga e machadada", hold = 0.15f, recover = 0.3f, step = 0.2f,
                        keys = new[]
                        {
                            new Key(0.12f, new Vector3(0.42f, 0.68f, -0.08f), 140f, 25f, -15f, 6f, 2, 20f).Free(-0.10f, 0.62f, 0.50f),
                            new Key(0.17f, new Vector3(0.43f, 0.69f, -0.09f), 145f, 28f, -20f, 8f, 0, 20f).Free(-0.08f, 0.62f, 0.52f),
                            new Key(0.27f, new Vector3(0.39f, 0.74f, -0.04f), 148f, 30f, 35f, -8f, 2, 20f).Free(-0.32f, 0.50f, -0.08f),
                            new Key(0.36f, new Vector3(0.22f, 0.66f, 0.28f), 70f, 16f, 8f, 8f, 1, 25f).Free(-0.32f, 0.48f, -0.06f),
                            new Key(0.42f, new Vector3(0.06f, 0.55f, 0.38f), -10f, -10f, -12f, 16f, 0, 30f).Free(-0.32f, 0.47f, -0.06f),
                            new Key(0.58f, new Vector3(-0.14f, 0.50f, 0.28f), -75f, -14f, -30f, 12f, 2, 30f).Free(-0.32f, 0.46f, -0.08f),
                        },
                    },
                    new Sequence
                    {
                        name = "2º a: revés horizontal, da esquerda para a direita", hold = 0.25f, recover = 0.3f, step = 0.2f,
                        keys = new[]
                        {
                            new Key(0.26f, new Vector3(-0.10f, 0.70f, 0.12f), -140f, 30f, -48f, -8f, 2, 180f).Free(-0.28f, 0.44f, -0.12f),
                            new Key(0.31f, new Vector3(-0.11f, 0.70f, 0.10f), -148f, 28f, -52f, -9f, 0, 180f).Free(-0.28f, 0.44f, -0.12f),
                            new Key(0.37f, new Vector3(0.04f, 0.60f, 0.36f), -60f, 4f, -25f, 5f, 1, 180f).Free(-0.32f, 0.52f, 0.20f),
                            new Key(0.42f, new Vector3(0.26f, 0.60f, 0.34f), 40f, 0f, 0f, 12f, 0, 180f).Free(-0.38f, 0.52f, 0.28f),
                            new Key(0.58f, new Vector3(0.36f, 0.58f, 0.14f), 100f, -8f, 15f, 8f, 2, 180f).Free(-0.38f, 0.52f, 0.28f),
                        },
                    },
                    new Sequence
                    {
                        // Arma para a direita, gira o corpo inteiro uma volta para a esquerda com o machado esticado (pega todos em volta) e
                        // termina varrendo para a esquerda. Os pés acompanham o giro.
                        name = "2º b: giro de 360°", hold = 0.15f, recover = 0.35f, step = 0.15f, spin = -360f, spinFrom = 0.24f, spinTo = 0.56f,
                        freeFeet = true,
                        keys = new[]
                        {
                            new Key(0.16f, new Vector3(0.34f, 0.66f, -0.06f), 135f, 10f, 40f, -8f, 2).Free(-0.25f, 0.58f, 0.30f),
                            new Key(0.20f, new Vector3(0.35f, 0.66f, -0.08f), 140f, 10f, 42f, -9f, 0).Free(-0.25f, 0.58f, 0.30f),
                            new Key(0.26f, new Vector3(0.40f, 0.62f, 0.12f), 75f, 0f, 15f, 4f, 1).Free(-0.30f, 0.52f, 0.00f),
                            new Key(0.40f, new Vector3(0.40f, 0.60f, 0.16f), 70f, 2f, 5f, 8f, 0).Free(-0.32f, 0.52f, -0.02f),
                            new Key(0.62f, new Vector3(-0.10f, 0.52f, 0.30f), -60f, -10f, -30f, 8f, 2).Free(-0.32f, 0.50f, -0.05f),
                        },
                    },
                    new Sequence
                    {
                        name = "3º a: machadada de cima (derruba)", hold = 0.15f, recover = 0.45f, step = 0.35f,
                        keys = new[]
                        {
                            new Key(0.26f, new Vector3(0.36f, 0.82f, -0.02f), 150f, 50f, 22f, -14f, 2, 85f).Free(-0.14f, 0.64f, 0.42f),
                            new Key(0.32f, new Vector3(0.37f, 0.84f, -0.04f), 155f, 54f, 24f, -16f, 0, 85f).Free(-0.14f, 0.65f, 0.42f),
                            new Key(0.39f, new Vector3(0.22f, 0.72f, 0.30f), 30f, 36f, 5f, 8f, 1, 88f).Free(-0.30f, 0.54f, 0.26f),
                            new Key(0.45f, new Vector3(0.10f, 0.40f, 0.40f), 0f, -30f, -5f, 26f, 0, 90f).Free(-0.30f, 0.46f, -0.04f),
                            new Key(0.62f, new Vector3(0.08f, 0.36f, 0.36f), 0f, -35f, -8f, 24f, 2, 90f).Free(-0.30f, 0.46f, -0.06f),
                        },
                    },
                    new Sequence
                    {
                        // Agacha com o machado erguido atrás, salta para a frente (0,9 m) e crava o machado de cima ao aterrissar (poeira).
                        name = "3º b: salto do carrasco (derruba)", hold = 0.1f, recover = 0.5f, step = 0.9f, jump = 0.4f, jumpFrom = 0.22f,
                        jumpTo = 0.52f, freeFeet = true,
                        keys = new[]
                        {
                            new Key(0.16f, new Vector3(0.38f, 0.64f, -0.10f), 150f, 40f, 20f, 18f, 2, 85f).Free(-0.22f, 0.50f, 0.20f),
                            new Key(0.22f, new Vector3(0.36f, 0.82f, -0.06f), 155f, 54f, 15f, -12f, 0, 85f).Free(-0.30f, 0.66f, 0.12f),
                            new Key(0.42f, new Vector3(0.37f, 0.85f, -0.04f), 158f, 56f, 10f, -16f, 2, 85f).Free(-0.30f, 0.70f, 0.12f),
                            new Key(0.50f, new Vector3(0.20f, 0.72f, 0.30f), 30f, 36f, 0f, 10f, 1, 88f).Free(-0.28f, 0.56f, 0.08f),
                            new Key(0.55f, new Vector3(0.10f, 0.40f, 0.40f), 0f, -30f, -5f, 28f, 0, 90f).Free(-0.28f, 0.44f, 0.00f),
                            new Key(0.75f, new Vector3(0.08f, 0.38f, 0.36f), 0f, -32f, -6f, 26f, 2, 90f).Free(-0.28f, 0.44f, -0.02f),
                        },
                    },
                },
            },
            // Cutelo do capitão e as lâminas de uma mão da tela I (Toledo, sabres, São Telmo; 17/11): lâmina no +Z e fio no +Y do visual. Ágil:
            // cortes curtos e rápidos, com o gancho no contrapeso. Tempos na velocidade 1 (o cutelo); os sabres andam mais rápido (timeScaled).
            // Acertam aos 0,29 | 0,32 | 0,30 · 0,30 | 0,24 e 0,42 | 0,28 · 0,42 | 0,40 | 0,50 s; o próximo do combo sai 0,6 s depois.
            new Moveset
            {
                name = "Cutelo", weaponType = 0, byNameOnly = true, timeScaled = true, twoHanded = false, edgeSign = 1f, bladeReach = 0.70f,
                pommelBehind = 0.18f, leftOnHandle = 0f, forearmTwist = 0.5f, guidedRecovery = true,
                combo = new[]
                {
                    // Três versões sorteadas por posição do combo (WeaponDefinition.comboAlternates 3): 0–2, 3–5, 6–8.
                    new Sequence
                    {
                        name = "1º a: corte rápido da direita", hold = 0.12f, recover = 0.28f, step = 0.25f,
                        keys = new[]
                        {
                            new Key(0.14f, new Vector3(0.34f, 0.72f, -0.02f), 120f, 55f, 30f, -6f, 2, 150f).Free(-0.22f, 0.56f, 0.30f),
                            new Key(0.18f, new Vector3(0.35f, 0.73f, -0.03f), 125f, 58f, 33f, -7f, 0, 150f).Free(-0.22f, 0.56f, 0.32f),
                            new Key(0.24f, new Vector3(0.26f, 0.64f, 0.28f), 60f, 14f, 10f, 6f, 1, 150f).Free(-0.28f, 0.52f, 0.12f),
                            new Key(0.29f, new Vector3(0.10f, 0.58f, 0.38f), -15f, 0f, -12f, 12f, 0, 150f).Free(-0.32f, 0.48f, -0.06f),
                            new Key(0.44f, new Vector3(-0.06f, 0.56f, 0.32f), -65f, -6f, -28f, 8f, 2, 150f).Free(-0.32f, 0.46f, -0.08f),
                        },
                    },
                    new Sequence
                    {
                        // Recolhe a lâmina ao lado do quadril, apontada para a frente, e avança esticando o braço (o gancho vai para trás).
                        name = "1º b: estocada que avança", hold = 0.1f, recover = 0.3f, step = 0.5f,
                        keys = new[]
                        {
                            new Key(0.16f, new Vector3(0.26f, 0.62f, -0.10f), 5f, 4f, 28f, -6f, 2, 90f).Free(-0.20f, 0.62f, 0.30f),
                            new Key(0.21f, new Vector3(0.27f, 0.62f, -0.12f), 4f, 4f, 32f, -8f, 0, 90f).Free(-0.18f, 0.64f, 0.32f),
                            new Key(0.27f, new Vector3(0.18f, 0.62f, 0.20f), 2f, 2f, 12f, 6f, 1, 90f).Free(-0.30f, 0.54f, 0.05f),
                            new Key(0.32f, new Vector3(0.10f, 0.62f, 0.38f), 0f, 0f, -8f, 14f, 0, 90f).Free(-0.34f, 0.50f, -0.10f),
                            new Key(0.46f, new Vector3(0.12f, 0.60f, 0.34f), 0f, -4f, -6f, 12f, 2, 90f).Free(-0.34f, 0.50f, -0.10f),
                        },
                    },
                    new Sequence
                    {
                        // Espadada de lado: braço esticado, da direita bem aberta até a esquerda na altura do peito, o tronco girando junto.
                        name = "1º c: espadada de lado", hold = 0.12f, recover = 0.3f, step = 0.25f,
                        keys = new[]
                        {
                            new Key(0.14f, new Vector3(0.40f, 0.68f, 0.06f), 100f, 12f, 40f, -6f, 2, 150f).Free(-0.20f, 0.58f, 0.34f),
                            new Key(0.19f, new Vector3(0.42f, 0.68f, 0.04f), 108f, 12f, 45f, -8f, 0, 150f).Free(-0.20f, 0.58f, 0.34f),
                            new Key(0.25f, new Vector3(0.30f, 0.64f, 0.30f), 45f, 4f, 15f, 6f, 1, 150f).Free(-0.30f, 0.52f, 0.12f),
                            new Key(0.30f, new Vector3(0.00f, 0.62f, 0.38f), -30f, 0f, -20f, 10f, 0, 150f).Free(-0.36f, 0.50f, -0.08f),
                            new Key(0.46f, new Vector3(-0.22f, 0.60f, 0.28f), -75f, -4f, -42f, 8f, 2, 150f).Free(-0.34f, 0.48f, -0.12f),
                        },
                    },
                    new Sequence
                    {
                        name = "2º a: corte de volta, da esquerda para a direita", hold = 0.12f, recover = 0.28f, step = 0.2f,
                        keys = new[]
                        {
                            new Key(0.15f, new Vector3(-0.06f, 0.68f, 0.18f), -100f, 25f, -40f, -6f, 2, 180f).Free(-0.30f, 0.46f, -0.10f),
                            new Key(0.19f, new Vector3(-0.07f, 0.69f, 0.16f), -106f, 27f, -44f, -7f, 0, 180f).Free(-0.30f, 0.46f, -0.10f),
                            new Key(0.25f, new Vector3(0.04f, 0.61f, 0.36f), -45f, 8f, -20f, 5f, 1, 180f).Free(-0.32f, 0.50f, 0.14f),
                            new Key(0.30f, new Vector3(0.24f, 0.58f, 0.36f), 30f, 0f, 4f, 12f, 0, 180f).Free(-0.36f, 0.52f, 0.24f),
                            new Key(0.44f, new Vector3(0.38f, 0.58f, 0.30f), 50f, -6f, 15f, 8f, 2, 180f).Free(-0.36f, 0.52f, 0.24f),
                        },
                    },
                    new Sequence
                    {
                        // Dois cortes descendo em X: do alto da direita para baixo à esquerda, volta ao alto da esquerda e desce para a direita.
                        name = "2º b: corte em X (dois acertos)", hold = 0.1f, recover = 0.3f, step = 0.3f,
                        keys = new[]
                        {
                            new Key(0.10f, new Vector3(0.32f, 0.80f, 0.00f), 120f, 40f, 28f, -10f, 2, 45f).Free(-0.22f, 0.56f, 0.30f),
                            new Key(0.14f, new Vector3(0.33f, 0.81f, -0.01f), 124f, 42f, 30f, -11f, 0, 45f).Free(-0.22f, 0.57f, 0.30f),
                            new Key(0.19f, new Vector3(0.20f, 0.68f, 0.28f), 50f, 20f, 8f, 6f, 1, 45f).Free(-0.28f, 0.52f, 0.12f),
                            new Key(0.24f, new Vector3(0.02f, 0.48f, 0.34f), -30f, -25f, -15f, 16f, 0, 45f).Free(-0.32f, 0.48f, -0.06f),
                            new Key(0.32f, new Vector3(-0.06f, 0.78f, 0.16f), -110f, 40f, -36f, -8f, 0, 135f).Free(-0.32f, 0.48f, -0.06f),
                            new Key(0.37f, new Vector3(0.02f, 0.70f, 0.32f), -55f, 22f, -18f, 4f, 1, 135f).Free(-0.32f, 0.50f, 0.12f),
                            new Key(0.42f, new Vector3(0.24f, 0.48f, 0.32f), 30f, -25f, 6f, 16f, 0, 135f).Free(-0.36f, 0.50f, 0.22f),
                            new Key(0.56f, new Vector3(0.32f, 0.44f, 0.20f), 60f, -28f, 12f, 14f, 2, 135f).Free(-0.36f, 0.50f, 0.22f),
                        },
                    },
                    new Sequence
                    {
                        // O cutelo fica erguido à direita e o gancho varre da esquerda para a direita na frente (empurra).
                        name = "2º c: golpe com o gancho", hold = 0.12f, recover = 0.3f, step = 0.25f,
                        keys = new[]
                        {
                            new Key(0.12f, new Vector3(0.40f, 0.62f, 0.08f), 48f, 8f, -35f, -4f, 2, 180f).Free(-0.36f, 0.56f, -0.06f),
                            new Key(0.17f, new Vector3(0.41f, 0.62f, 0.06f), 50f, 8f, -40f, -5f, 0, 180f).Free(-0.38f, 0.58f, -0.10f),
                            new Key(0.23f, new Vector3(0.42f, 0.62f, 0.06f), 52f, 6f, -10f, 6f, 1, 180f).Free(-0.14f, 0.60f, 0.36f),
                            new Key(0.28f, new Vector3(0.43f, 0.61f, 0.06f), 54f, 5f, 20f, 10f, 0, 180f).Free(0.06f, 0.58f, 0.34f),
                            new Key(0.42f, new Vector3(0.43f, 0.60f, 0.08f), 55f, 4f, 28f, 8f, 2, 180f).Free(0.10f, 0.54f, 0.28f),
                        },
                    },
                    new Sequence
                    {
                        name = "3º a: corte de cima (derruba)", hold = 0.12f, recover = 0.4f, step = 0.35f,
                        keys = new[]
                        {
                            new Key(0.20f, new Vector3(0.34f, 0.82f, 0.00f), 140f, 55f, 18f, -14f, 2, 90f).Free(-0.16f, 0.62f, 0.40f),
                            new Key(0.27f, new Vector3(0.35f, 0.84f, -0.02f), 145f, 60f, 20f, -16f, 0, 90f).Free(-0.16f, 0.63f, 0.40f),
                            new Key(0.35f, new Vector3(0.18f, 0.78f, 0.26f), 10f, 45f, 4f, 6f, 1, 90f).Free(-0.28f, 0.54f, 0.20f),
                            new Key(0.42f, new Vector3(0.08f, 0.46f, 0.38f), 0f, -32f, -5f, 24f, 0, 90f).Free(-0.30f, 0.46f, -0.04f),
                            new Key(0.58f, new Vector3(0.06f, 0.42f, 0.34f), 0f, -36f, -8f, 22f, 2, 90f).Free(-0.30f, 0.46f, -0.06f),
                        },
                    },
                    new Sequence
                    {
                        // Abaixa com a lâmina baixa à direita e corta subindo na diagonal até o alto da esquerda, fio para cima (joga o inimigo para trás).
                        name = "3º b: corte subindo (derruba e empurra)", hold = 0.12f, recover = 0.4f, step = 0.3f,
                        keys = new[]
                        {
                            new Key(0.16f, new Vector3(0.32f, 0.40f, 0.10f), 55f, -45f, 22f, 16f, 2, -90f).Free(-0.24f, 0.56f, 0.32f),
                            new Key(0.22f, new Vector3(0.33f, 0.38f, 0.08f), 58f, -48f, 25f, 18f, 0, -90f).Free(-0.24f, 0.56f, 0.32f),
                            new Key(0.33f, new Vector3(0.24f, 0.52f, 0.30f), 20f, -10f, 8f, 8f, 1, -125f).Free(-0.30f, 0.50f, 0.10f),
                            new Key(0.40f, new Vector3(0.00f, 0.72f, 0.36f), -40f, 30f, -14f, -6f, 0, -150f).Free(-0.34f, 0.46f, -0.08f),
                            new Key(0.56f, new Vector3(-0.08f, 0.80f, 0.30f), -62f, 45f, -20f, -10f, 2, -150f).Free(-0.34f, 0.46f, -0.10f),
                        },
                    },
                    new Sequence
                    {
                        // Pirueta: o cutelo esticado à direita e o corpo dá uma volta inteira para a esquerda (pega todos em volta).
                        name = "3º c: pirueta", hold = 0.1f, recover = 0.35f, step = 0.2f, spin = -360f, spinFrom = 0.2f, spinTo = 0.5f,
                        freeFeet = true,
                        keys = new[]
                        {
                            new Key(0.14f, new Vector3(0.36f, 0.66f, 0.02f), 110f, 15f, 35f, -6f, 2, 150f).Free(-0.22f, 0.58f, 0.30f),
                            new Key(0.20f, new Vector3(0.37f, 0.66f, 0.00f), 115f, 15f, 40f, -8f, 0, 150f).Free(-0.22f, 0.58f, 0.30f),
                            new Key(0.26f, new Vector3(0.42f, 0.62f, 0.20f), 58f, 2f, 15f, 4f, 1, 150f).Free(-0.30f, 0.52f, 0.00f),
                            new Key(0.44f, new Vector3(0.42f, 0.60f, 0.22f), 55f, 2f, 5f, 8f, 0, 150f).Free(-0.32f, 0.52f, -0.02f),
                            new Key(0.58f, new Vector3(-0.06f, 0.56f, 0.32f), -55f, -6f, -28f, 8f, 2, 150f).Free(-0.32f, 0.48f, -0.06f),
                        },
                    },
                },
            },
            // Adagas do capitão (Punhal do contrabandista, Adaga da duelista; 17/11): curtas e rápidas, 3 golpes por posição, sorteados. Tempos na
            // velocidade 1 (as adagas andam a 1,35–1,45×). Acertam aos 0,26 | 0,28 | 0,32 · 0,24 + 0,40 | 0,44 | 0,20 + 0,38 · 0,38 | 0,50 | 0,40.
            new Moveset
            {
                name = "Adaga", weaponType = 0, byNameOnly = true, timeScaled = true, twoHanded = false, edgeSign = 1f, bladeReach = 0.34f,
                pommelBehind = 0.40f, leftOnHandle = 0f, forearmTwist = 0.5f, guidedRecovery = true,
                combo = new[]
                {
                    new Sequence
                    {
                        name = "1º a: estocada rápida", hold = 0.08f, recover = 0.25f, step = 0.60f,
                        keys = new[]
                        {
                            new Key(0.12f, new Vector3(0.26f, 0.64f, -0.04f), 12f, 2f, 25f, -4f, 2, 180f).Free(-0.22f, 0.60f, 0.30f),
                            new Key(0.16f, new Vector3(0.27f, 0.64f, -0.06f), 12f, 2f, 28f, -5f, 0, 180f).Free(-0.20f, 0.62f, 0.30f),
                            new Key(0.22f, new Vector3(0.18f, 0.63f, 0.20f), 4f, 1f, 10f, 6f, 1, 180f).Free(-0.30f, 0.54f, 0.06f),
                            new Key(0.26f, new Vector3(0.10f, 0.62f, 0.38f), 0f, 0f, -8f, 12f, 0, 180f).Free(-0.34f, 0.50f, -0.08f),
                            new Key(0.40f, new Vector3(0.14f, 0.60f, 0.32f), 5f, -3f, -4f, 10f, 2, 180f).Free(-0.34f, 0.50f, -0.08f),
                        },
                    },
                    new Sequence
                    {
                        // Corte curto de revés, da esquerda para a direita.
                        name = "1º b: corte curto de revés", hold = 0.08f, recover = 0.25f, step = 0.45f,
                        keys = new[]
                        {
                            new Key(0.12f, new Vector3(0.02f, 0.66f, 0.24f), -80f, 15f, -30f, -4f, 2, 180f).Free(-0.30f, 0.46f, -0.10f),
                            new Key(0.16f, new Vector3(0.01f, 0.66f, 0.22f), -85f, 16f, -33f, -5f, 0, 180f).Free(-0.30f, 0.46f, -0.10f),
                            new Key(0.22f, new Vector3(0.06f, 0.62f, 0.34f), -30f, 6f, -12f, 5f, 1, 180f).Free(-0.32f, 0.50f, 0.12f),
                            new Key(0.28f, new Vector3(0.24f, 0.60f, 0.34f), 35f, 0f, 6f, 10f, 0, 180f).Free(-0.34f, 0.52f, 0.20f),
                            new Key(0.40f, new Vector3(0.34f, 0.58f, 0.26f), 55f, -4f, 12f, 8f, 2, 180f).Free(-0.34f, 0.52f, 0.20f),
                        },
                    },
                    new Sequence
                    {
                        // Abaixa com a adaga baixa à direita e corta subindo na diagonal até o alto da esquerda.
                        name = "1º c: corte em diagonal subindo", hold = 0.1f, recover = 0.3f, step = 0.55f,
                        keys = new[]
                        {
                            new Key(0.14f, new Vector3(0.32f, 0.42f, 0.12f), 55f, -40f, 22f, 16f, 2, -125f).Free(-0.24f, 0.56f, 0.32f),
                            new Key(0.19f, new Vector3(0.33f, 0.40f, 0.10f), 58f, -42f, 25f, 18f, 0, -125f).Free(-0.24f, 0.56f, 0.32f),
                            new Key(0.26f, new Vector3(0.24f, 0.54f, 0.30f), 20f, -8f, 8f, 8f, 1, -125f).Free(-0.30f, 0.50f, 0.10f),
                            new Key(0.32f, new Vector3(0.02f, 0.74f, 0.36f), -40f, 30f, -14f, -6f, 0, -150f).Free(-0.34f, 0.46f, -0.08f),
                            new Key(0.46f, new Vector3(-0.08f, 0.80f, 0.30f), -62f, 42f, -20f, -10f, 2, -150f).Free(-0.34f, 0.46f, -0.10f),
                        },
                    },
                    new Sequence
                    {
                        // Duas estocadas seguidas (acertam aos 0,24 e 0,40).
                        name = "2º a: duas estocadas", hold = 0.08f, recover = 0.25f, step = 0.60f,
                        keys = new[]
                        {
                            new Key(0.10f, new Vector3(0.26f, 0.64f, -0.04f), 10f, 2f, 25f, -4f, 2, 180f).Free(-0.22f, 0.60f, 0.30f),
                            new Key(0.14f, new Vector3(0.27f, 0.64f, -0.06f), 10f, 2f, 28f, -5f, 0, 180f).Free(-0.22f, 0.60f, 0.30f),
                            new Key(0.20f, new Vector3(0.18f, 0.63f, 0.22f), 4f, 1f, 10f, 6f, 1, 180f).Free(-0.30f, 0.54f, 0.06f),
                            new Key(0.24f, new Vector3(0.10f, 0.62f, 0.38f), 0f, 0f, -8f, 12f, 0, 180f).Free(-0.34f, 0.50f, -0.08f),
                            new Key(0.31f, new Vector3(0.22f, 0.64f, 0.06f), 8f, 2f, 18f, 2f, 0, 180f).Free(-0.28f, 0.56f, 0.16f),
                            new Key(0.36f, new Vector3(0.16f, 0.62f, 0.26f), 3f, 1f, 4f, 8f, 1, 180f).Free(-0.30f, 0.54f, 0.04f),
                            new Key(0.40f, new Vector3(0.08f, 0.62f, 0.40f), -2f, 0f, -10f, 13f, 0, 180f).Free(-0.34f, 0.50f, -0.08f),
                            new Key(0.52f, new Vector3(0.12f, 0.60f, 0.34f), 3f, -3f, -6f, 10f, 2, 180f).Free(-0.34f, 0.50f, -0.08f),
                        },
                    },
                    new Sequence
                    {
                        // Giro com corte: a adaga esticada à direita e o corpo dá uma volta inteira para a esquerda (pega todos em volta).
                        name = "2º b: giro com corte", hold = 0.1f, recover = 0.3f, step = 0.45f, spin = -360f, spinFrom = 0.16f, spinTo = 0.44f,
                        freeFeet = true,
                        keys = new[]
                        {
                            new Key(0.12f, new Vector3(0.36f, 0.62f, 0.10f), 70f, 10f, 35f, -6f, 2, 150f).Free(-0.22f, 0.58f, 0.30f),
                            new Key(0.16f, new Vector3(0.37f, 0.62f, 0.08f), 72f, 10f, 40f, -8f, 0, 150f).Free(-0.22f, 0.58f, 0.30f),
                            new Key(0.22f, new Vector3(0.40f, 0.60f, 0.20f), 55f, 2f, 15f, 4f, 1, 150f).Free(-0.30f, 0.52f, 0.00f),
                            new Key(0.40f, new Vector3(0.40f, 0.60f, 0.22f), 50f, 2f, 5f, 8f, 0, 150f).Free(-0.32f, 0.52f, -0.02f),
                            new Key(0.52f, new Vector3(-0.04f, 0.56f, 0.34f), -45f, -6f, -26f, 8f, 2, 150f).Free(-0.32f, 0.48f, -0.06f),
                        },
                    },
                    new Sequence
                    {
                        // Cotovelada com o gancho na frente (aos 0,20) e a estocada logo depois (aos 0,38).
                        name = "2º c: gancho e estocada", hold = 0.08f, recover = 0.25f, step = 0.60f,
                        keys = new[]
                        {
                            new Key(0.10f, new Vector3(0.28f, 0.62f, 0.06f), 12f, 2f, 5f, -4f, 2, 180f).Free(-0.30f, 0.56f, -0.06f),
                            new Key(0.14f, new Vector3(0.29f, 0.62f, 0.04f), 12f, 2f, 0f, -5f, 0, 180f).Free(-0.32f, 0.58f, -0.10f),
                            new Key(0.20f, new Vector3(0.29f, 0.62f, 0.04f), 12f, 2f, -15f, 6f, 1, 180f).Free(-0.02f, 0.60f, 0.36f),
                            new Key(0.26f, new Vector3(0.28f, 0.62f, 0.02f), 12f, 2f, 15f, 4f, 0, 180f).Free(-0.04f, 0.58f, 0.32f),
                            new Key(0.33f, new Vector3(0.18f, 0.63f, 0.22f), 4f, 1f, 8f, 8f, 1, 180f).Free(-0.30f, 0.54f, 0.06f),
                            new Key(0.38f, new Vector3(0.10f, 0.62f, 0.38f), 0f, 0f, -8f, 12f, 0, 180f).Free(-0.34f, 0.50f, -0.08f),
                            new Key(0.50f, new Vector3(0.14f, 0.60f, 0.32f), 5f, -3f, -4f, 10f, 2, 180f).Free(-0.34f, 0.50f, -0.08f),
                        },
                    },
                    new Sequence
                    {
                        // Ergue a adaga e crava de cima (derruba).
                        name = "3º a: facada de cima (derruba)", hold = 0.1f, recover = 0.35f, step = 0.60f,
                        keys = new[]
                        {
                            new Key(0.18f, new Vector3(0.40f, 0.78f, 0.10f), 40f, 50f, 18f, -12f, 2, 180f).Free(-0.18f, 0.62f, 0.36f),
                            new Key(0.24f, new Vector3(0.41f, 0.80f, 0.08f), 42f, 55f, 20f, -14f, 0, 180f).Free(-0.18f, 0.63f, 0.36f),
                            new Key(0.32f, new Vector3(0.22f, 0.70f, 0.30f), 10f, 10f, 4f, 8f, 1, 180f).Free(-0.28f, 0.54f, 0.18f),
                            new Key(0.38f, new Vector3(0.18f, 0.50f, 0.42f), 5f, -40f, -5f, 10f, 0, 180f).Free(-0.30f, 0.46f, -0.04f),
                            new Key(0.54f, new Vector3(0.18f, 0.48f, 0.38f), 5f, -40f, -8f, 8f, 2, 180f).Free(-0.30f, 0.46f, -0.06f),
                        },
                    },
                    new Sequence
                    {
                        // Rola curto para a esquerda (uma volta agachado) e corta ao levantar: bom para chegar pelas costas.
                        name = "3º b: rolamento curto com corte", hold = 0.1f, recover = 0.3f, step = 0.50f, side = -1.1f, stepTime = 0.3f, spin = -360f, spinFrom = 0.1f, spinTo = 0.4f, freeFeet = true,
                        keys = new[]
                        {
                            new Key(0.08f, new Vector3(0.32f, 0.52f, 0.16f), 40f, -10f, 20f, 20f, 2, 180f).Free(-0.24f, 0.52f, 0.20f),
                            new Key(0.12f, new Vector3(0.32f, 0.50f, 0.16f), 40f, -10f, 20f, 25f, 0, 180f).Free(-0.24f, 0.52f, 0.20f),
                            new Key(0.38f, new Vector3(0.32f, 0.50f, 0.18f), 40f, -10f, 20f, 25f, 0, 180f).Free(-0.24f, 0.50f, 0.20f),
                            new Key(0.44f, new Vector3(0.38f, 0.62f, 0.18f), 60f, 10f, 28f, 6f, 2, 150f).Free(-0.22f, 0.56f, 0.30f),
                            new Key(0.50f, new Vector3(0.04f, 0.58f, 0.38f), -25f, 0f, -14f, 12f, 1, 150f).Free(-0.32f, 0.48f, -0.06f),
                            new Key(0.62f, new Vector3(-0.10f, 0.56f, 0.28f), -65f, -6f, -28f, 8f, 2, 150f).Free(-0.32f, 0.46f, -0.08f),
                        },
                    },
                    new Sequence
                    {
                        // Estocada voadora: agacha, salta para a frente com a adaga esticada e crava ao chegar.
                        name = "3º c: estocada voadora", hold = 0.1f, recover = 0.35f, step = 1.1f, stepTime = 0.24f, jump = 0.3f, jumpFrom = 0.16f,
                        jumpTo = 0.40f, freeFeet = true,
                        keys = new[]
                        {
                            new Key(0.10f, new Vector3(0.30f, 0.56f, 0.10f), 14f, 0f, 15f, 18f, 2, 180f).Free(-0.22f, 0.56f, 0.28f),
                            new Key(0.16f, new Vector3(0.30f, 0.56f, 0.08f), 14f, 0f, 18f, 20f, 0, 180f).Free(-0.22f, 0.56f, 0.28f),
                            new Key(0.32f, new Vector3(0.18f, 0.62f, 0.22f), 4f, -4f, 8f, 10f, 1, 180f).Free(-0.32f, 0.54f, -0.02f),
                            new Key(0.40f, new Vector3(0.10f, 0.58f, 0.40f), 0f, -10f, -8f, 16f, 0, 180f).Free(-0.36f, 0.50f, -0.10f),
                            new Key(0.56f, new Vector3(0.14f, 0.56f, 0.34f), 4f, -8f, -4f, 14f, 2, 180f).Free(-0.36f, 0.50f, -0.10f),
                        },
                    },
                },
            },
            // Cutelo e sabre dos inimigos (piratas, tripulações, mortos, Guarda Negra; 17/11): 3 golpes sorteados pela IA, com o impacto no
            // mesmo tempo de antes (0,30 · 0,30 · 0,29 s na velocidade 1; o cutelo pirata anda a 0,7×: 0,43 s) para o aviso "!"/"!!" e a
            // dificuldade não mudarem. Mesmas poses do cutelo do capitão; o passo é pequeno (a IA já avança no golpe).
            new Moveset
            {
                name = "CuteloInimigo", weaponType = 0, byNameOnly = true, timeScaled = true, twoHanded = false, edgeSign = 1f, bladeReach = 0.70f,
                pommelBehind = 0.18f, leftOnHandle = 0f, forearmTwist = 0.5f, guidedRecovery = true, freeMinReach = 0.8f,
                combo = new[]
                {
                    new Sequence
                    {
                        name = "corte da direita", hold = 0.12f, recover = 0.3f, step = 0.15f,
                        keys = new[]
                        {
                            new Key(0.15f, new Vector3(0.34f, 0.72f, -0.02f), 120f, 55f, 30f, -6f, 2, 150f).Free(-0.22f, 0.50f, 0.30f),
                            new Key(0.19f, new Vector3(0.35f, 0.73f, -0.03f), 125f, 58f, 33f, -7f, 0, 150f).Free(-0.22f, 0.50f, 0.32f),
                            new Key(0.25f, new Vector3(0.26f, 0.64f, 0.28f), 60f, 14f, 10f, 6f, 1, 150f).Free(-0.28f, 0.46f, 0.12f),
                            new Key(0.30f, new Vector3(0.10f, 0.58f, 0.38f), -15f, 0f, -12f, 12f, 0, 150f).Free(-0.32f, 0.42f, 0.02f),
                            new Key(0.45f, new Vector3(-0.06f, 0.56f, 0.32f), -65f, -6f, -28f, 8f, 2, 150f).Free(-0.32f, 0.40f, 0.02f),
                        },
                    },
                    new Sequence
                    {
                        name = "corte de volta", hold = 0.12f, recover = 0.3f, step = 0.15f,
                        keys = new[]
                        {
                            new Key(0.15f, new Vector3(-0.04f, 0.62f, 0.20f), -100f, 10f, -40f, -4f, 2, 180f).Free(-0.30f, 0.40f, 0.01f),
                            new Key(0.19f, new Vector3(-0.05f, 0.62f, 0.18f), -106f, 10f, -44f, -5f, 0, 180f).Free(-0.30f, 0.40f, 0.01f),
                            new Key(0.25f, new Vector3(0.04f, 0.60f, 0.34f), -55f, 2f, -20f, 4f, 1, 180f).Free(-0.32f, 0.44f, 0.14f),
                            new Key(0.30f, new Vector3(0.24f, 0.60f, 0.32f), 35f, 0f, 4f, 6f, 0, 180f).Free(-0.32f, 0.38f, 0.16f),
                            new Key(0.44f, new Vector3(0.38f, 0.60f, 0.26f), 60f, 0f, 15f, 4f, 2, 180f).Free(-0.32f, 0.38f, 0.16f),
                        },
                    },
                    new Sequence
                    {
                        name = "corte de cima", hold = 0.12f, recover = 0.35f, step = 0.15f,
                        keys = new[]
                        {
                            new Key(0.12f, new Vector3(0.34f, 0.82f, 0.00f), 140f, 55f, 18f, -14f, 2, 90f).Free(-0.16f, 0.56f, 0.40f),
                            new Key(0.17f, new Vector3(0.35f, 0.84f, -0.02f), 145f, 60f, 20f, -16f, 0, 90f).Free(-0.16f, 0.57f, 0.40f),
                            new Key(0.24f, new Vector3(0.18f, 0.78f, 0.26f), 10f, 45f, 4f, 6f, 1, 90f).Free(-0.28f, 0.48f, 0.20f),
                            new Key(0.29f, new Vector3(0.08f, 0.46f, 0.38f), 0f, -32f, -5f, 24f, 0, 90f).Free(-0.30f, 0.40f, 0.03f),
                            new Key(0.44f, new Vector3(0.06f, 0.42f, 0.34f), 0f, -36f, -8f, 22f, 2, 90f).Free(-0.30f, 0.40f, 0.02f),
                        },
                    },
                },
            },
            // Cutelo enferrujado dos esqueletos do cemitério (18/11): os mesmos 3 cortes dos piratas (a guarda é a corcunda do pacote do
            // esqueleto), no tempo dos golpes de antes: os dois lentos acertam aos 0,47 s (seguram o preparo, bem avisados) e o de volta aos 0,29.
            new Moveset
            {
                name = "CuteloEsqueleto", weaponType = 0, byNameOnly = true, timeScaled = true, twoHanded = false, edgeSign = 1f, bladeReach = 0.70f,
                pommelBehind = 0.18f, leftOnHandle = 0f, forearmTwist = 0.5f, guidedRecovery = true, freeMinReach = 0.8f,
                combo = new[]
                {
                    new Sequence
                    {
                        name = "corte da direita, lento", hold = 0.12f, recover = 0.3f, step = 0.15f,
                        keys = new[]
                        {
                            new Key(0.32f, new Vector3(0.34f, 0.72f, -0.02f), 120f, 55f, 30f, -6f, 2, 150f).Free(-0.22f, 0.50f, 0.30f),
                            new Key(0.36f, new Vector3(0.35f, 0.73f, -0.03f), 125f, 58f, 33f, -7f, 0, 150f).Free(-0.22f, 0.50f, 0.32f),
                            new Key(0.42f, new Vector3(0.26f, 0.64f, 0.28f), 60f, 14f, 10f, 6f, 1, 150f).Free(-0.28f, 0.46f, 0.12f),
                            new Key(0.47f, new Vector3(0.10f, 0.58f, 0.38f), -15f, 0f, -12f, 12f, 0, 150f).Free(-0.32f, 0.42f, 0.02f),
                            new Key(0.62f, new Vector3(-0.06f, 0.56f, 0.32f), -65f, -6f, -28f, 8f, 2, 150f).Free(-0.32f, 0.40f, 0.02f),
                        },
                    },
                    new Sequence
                    {
                        name = "corte de volta", hold = 0.12f, recover = 0.3f, step = 0.15f,
                        keys = new[]
                        {
                            new Key(0.14f, new Vector3(-0.04f, 0.62f, 0.20f), -100f, 10f, -40f, -4f, 2, 180f).Free(-0.30f, 0.40f, 0.01f),
                            new Key(0.18f, new Vector3(-0.05f, 0.62f, 0.18f), -106f, 10f, -44f, -5f, 0, 180f).Free(-0.30f, 0.40f, 0.01f),
                            new Key(0.24f, new Vector3(0.04f, 0.60f, 0.34f), -55f, 2f, -20f, 4f, 1, 180f).Free(-0.32f, 0.44f, 0.14f),
                            new Key(0.29f, new Vector3(0.24f, 0.60f, 0.32f), 35f, 0f, 4f, 6f, 0, 180f).Free(-0.32f, 0.38f, 0.16f),
                            new Key(0.43f, new Vector3(0.38f, 0.60f, 0.26f), 60f, 0f, 15f, 4f, 2, 180f).Free(-0.32f, 0.38f, 0.16f),
                        },
                    },
                    new Sequence
                    {
                        name = "corte de cima, lento", hold = 0.12f, recover = 0.35f, step = 0.15f,
                        keys = new[]
                        {
                            new Key(0.30f, new Vector3(0.34f, 0.82f, 0.00f), 140f, 55f, 18f, -14f, 2, 90f).Free(-0.16f, 0.56f, 0.40f),
                            new Key(0.35f, new Vector3(0.35f, 0.84f, -0.02f), 145f, 60f, 20f, -16f, 0, 90f).Free(-0.16f, 0.57f, 0.40f),
                            new Key(0.42f, new Vector3(0.18f, 0.78f, 0.26f), 10f, 45f, 4f, 6f, 1, 90f).Free(-0.28f, 0.48f, 0.20f),
                            new Key(0.47f, new Vector3(0.08f, 0.46f, 0.38f), 0f, -32f, -5f, 24f, 0, 90f).Free(-0.30f, 0.40f, 0.03f),
                            new Key(0.62f, new Vector3(0.06f, 0.42f, 0.34f), 0f, -36f, -8f, 22f, 2, 90f).Free(-0.30f, 0.40f, 0.02f),
                        },
                    },
                },
            },
            // Adaga da duelista (17/11): a estocada rápida (acerta aos 0,35 s na velocidade 1; a adaga dela anda a 1,15×: 0,30 s) e a lenta bem
            // avisada (0,85 → 0,74 s), que segura o preparo com o corpo torcido para trás antes do bote. Mesmos tempos e dano de antes.
            new Moveset
            {
                name = "AdagaInimiga", weaponType = 0, byNameOnly = true, timeScaled = true, twoHanded = false, edgeSign = 1f, bladeReach = 0.55f,
                pommelBehind = 0.10f, leftOnHandle = 0f, forearmTwist = 0.5f, guidedRecovery = true, freeMinReach = 0.8f,
                combo = new[]
                {
                    new Sequence
                    {
                        name = "estocada rápida", hold = 0.08f, recover = 0.25f, step = 0.1f,
                        keys = new[]
                        {
                            new Key(0.14f, new Vector3(0.26f, 0.56f, -0.02f), 20f, 2f, 25f, -4f, 2, 180f).Free(-0.28f, 0.52f, 0.34f),
                            new Key(0.20f, new Vector3(0.27f, 0.56f, -0.04f), 20f, 2f, 28f, -5f, 0, 180f).Free(-0.27f, 0.52f, 0.35f),
                            new Key(0.29f, new Vector3(0.18f, 0.54f, 0.22f), 12f, 1f, 10f, 6f, 1, 180f).Free(-0.32f, 0.48f, 0.12f),
                            new Key(0.35f, new Vector3(0.10f, 0.54f, 0.40f), 8f, 0f, -8f, 12f, 0, 180f).Free(-0.34f, 0.44f, 0.02f),
                            new Key(0.50f, new Vector3(0.14f, 0.52f, 0.34f), 13f, -3f, -4f, 10f, 2, 180f).Free(-0.34f, 0.44f, 0.02f),
                        },
                    },
                    new Sequence
                    {
                        // Torce o corpo para trás com a adaga recolhida e a mão livre apontando o alvo, segura, e dá o bote.
                        name = "estocada lenta", hold = 0.08f, recover = 0.3f, step = 0.1f,
                        keys = new[]
                        {
                            new Key(0.20f, new Vector3(0.32f, 0.60f, 0.04f), 26f, 8f, 40f, -10f, 2, 180f).Free(-0.16f, 0.54f, 0.38f),
                            new Key(0.62f, new Vector3(0.33f, 0.62f, 0.02f), 28f, 10f, 45f, -12f, 0, 180f).Free(-0.14f, 0.56f, 0.40f),
                            new Key(0.78f, new Vector3(0.18f, 0.56f, 0.22f), 13f, 2f, 10f, 8f, 1, 180f).Free(-0.30f, 0.48f, 0.04f),
                            new Key(0.85f, new Vector3(0.08f, 0.54f, 0.44f), 8f, 0f, -10f, 16f, 0, 180f).Free(-0.36f, 0.44f, 0.01f),
                            new Key(1.00f, new Vector3(0.12f, 0.52f, 0.38f), 12f, -3f, -6f, 12f, 2, 180f).Free(-0.36f, 0.44f, 0.01f),
                        },
                    },
                },
            },
            // Machado duplo do brutamontes (17/11): nas duas mãos, a cabeça na ponta +Z; preparo longo e bem avisado (segura no alto), corte
            // pesado. Mesmos tempos de antes (acerta aos 0,70 · 0,60 · 0,77 s), dano 35 e queda. Da mão à cabeça 0,74, cabo atrás 0,42 (na
            // escala 1; ele tem 1,35).
            new Moveset
            {
                name = "MachadoBruto", weaponType = 5, byNameOnly = true, twoHanded = true, edgeSign = -1f, bladeReach = 0.74f, pommelBehind = 0.42f,
                leftOnHandle = 0.25f, forearmTwist = 0.5f, guidedRecovery = true,
                combo = new[]
                {
                    new Sequence
                    {
                        name = "machadada de cima", hold = 0.15f, recover = 0.5f, step = 0.3f,
                        keys = new[]
                        {
                            new Key(0.30f, new Vector3(0.26f, 0.86f, 0.00f), 150f, 50f, 22f, -14f, 2, 90f),
                            new Key(0.55f, new Vector3(0.27f, 0.88f, -0.02f), 155f, 55f, 25f, -16f, 0, 90f),
                            new Key(0.64f, new Vector3(0.18f, 0.70f, 0.28f), 40f, 30f, 5f, 8f, 1, 90f),
                            new Key(0.70f, new Vector3(0.06f, 0.48f, 0.38f), 0f, -12f, -6f, 22f, 0, 90f),
                            new Key(0.92f, new Vector3(0.04f, 0.46f, 0.34f), 0f, -16f, -8f, 20f, 2, 90f),
                        },
                    },
                    new Sequence
                    {
                        name = "varrida da direita", hold = 0.15f, recover = 0.5f, step = 0.3f,
                        keys = new[]
                        {
                            new Key(0.28f, new Vector3(0.34f, 0.64f, 0.00f), 135f, 12f, 40f, -8f, 2, 180f),
                            new Key(0.48f, new Vector3(0.35f, 0.65f, -0.02f), 142f, 14f, 44f, -9f, 0, 180f),
                            new Key(0.55f, new Vector3(0.24f, 0.60f, 0.30f), 85f, 5f, 15f, 5f, 1, 180f),
                            new Key(0.60f, new Vector3(0.10f, 0.56f, 0.32f), -25f, 0f, -10f, 14f, 0, 180f),
                            new Key(0.80f, new Vector3(-0.05f, 0.52f, 0.18f), -90f, -10f, -40f, 10f, 2, 180f),
                        },
                    },
                    new Sequence
                    {
                        name = "diagonal por cima do ombro", hold = 0.15f, recover = 0.5f, step = 0.35f,
                        keys = new[]
                        {
                            new Key(0.32f, new Vector3(0.30f, 0.80f, 0.02f), 130f, 40f, 30f, -12f, 2, 220f),
                            new Key(0.62f, new Vector3(0.31f, 0.82f, 0.00f), 135f, 45f, 32f, -14f, 0, 220f),
                            new Key(0.71f, new Vector3(0.20f, 0.66f, 0.28f), 55f, 20f, 5f, 6f, 1, 220f),
                            new Key(0.77f, new Vector3(0.04f, 0.46f, 0.34f), -25f, -20f, -15f, 22f, 0, 225f),
                            new Key(0.98f, new Vector3(-0.04f, 0.44f, 0.28f), -50f, -24f, -22f, 20f, 2, 225f),
                        },
                    },
                },
            },
            // Espadão do Barba-Negra e de Lázaro (17/11): o modelo do capitão 1,4–1,45× maior, então as poses aprovadas do espadão do capitão
            // servem, encaixadas nos tempos de cada golpe deles (iguais aos de antes: 0,50 · 0,65 · 0,55 + 1,35 · 0,47 · 0,90 · 1,00 · chute ·
            // 0,65 + 1,55 + 2,49 s). O chute (6) fica com a animação gravada.
            new Moveset
            {
                name = "EspadaoChefe", weaponType = 4, byNameOnly = true, twoHanded = true, edgeSign = 1f, bladeReach = 1.1f, pommelBehind = 0.24f,
                leftOnHandle = 0.11f, guidedRecovery = true,
                combo = new[]
                {
                    new Sequence
                    {
                        name = "corte", hold = 0.15f, recover = 0.4f, step = 0.2f,
                        keys = new[]
                        {
                            new Key(0.34f, new Vector3(0.34f, 0.66f, 0.00f), 138f, 14f, 38f, -8f, 2),
                            new Key(0.39f, new Vector3(0.35f, 0.67f, -0.02f), 145f, 16f, 42f, -9f, 0),
                            new Key(0.45f, new Vector3(0.24f, 0.60f, 0.30f), 90f, 6f, 15f, 5f, 1),
                            new Key(0.50f, new Vector3(0.10f, 0.55f, 0.30f), -25f, 0f, -10f, 14f, 0),
                            new Key(0.68f, new Vector3(-0.05f, 0.50f, 0.16f), -95f, -12f, -42f, 10f, 2),
                        },
                    },
                    new Sequence
                    {
                        name = "corte largo", hold = 0.2f, recover = 0.4f, step = 0.2f,
                        keys = new[]
                        {
                            new Key(0.50f, new Vector3(-0.08f, 0.62f, 0.08f), -140f, 14f, -55f, -8f, 2),
                            new Key(0.55f, new Vector3(-0.09f, 0.63f, 0.06f), -148f, 16f, -60f, -9f, 0),
                            new Key(0.60f, new Vector3(0.00f, 0.58f, 0.34f), -75f, 6f, -35f, 5f, 1),
                            new Key(0.65f, new Vector3(0.14f, 0.56f, 0.36f), 30f, 2f, -10f, 14f, 0),
                            new Key(0.83f, new Vector3(0.30f, 0.52f, 0.26f), 95f, -10f, 8f, 10f, 2),
                        },
                    },
                    new Sequence
                    {
                        name = "corte duplo", hold = 0.15f, recover = 0.4f, step = 0.2f,
                        keys = new[]
                        {
                            new Key(0.39f, new Vector3(0.34f, 0.66f, 0.00f), 138f, 14f, 38f, -8f, 2),
                            new Key(0.44f, new Vector3(0.35f, 0.67f, -0.02f), 145f, 16f, 42f, -9f, 0),
                            new Key(0.50f, new Vector3(0.24f, 0.60f, 0.30f), 90f, 6f, 15f, 5f, 1),
                            new Key(0.55f, new Vector3(0.10f, 0.55f, 0.30f), -25f, 0f, -10f, 14f, 0),
                            new Key(0.73f, new Vector3(-0.05f, 0.50f, 0.16f), -95f, -12f, -42f, 10f, 2),
                            new Key(1.20f, new Vector3(-0.08f, 0.62f, 0.08f), -140f, 14f, -55f, -8f, 2),
                            new Key(1.25f, new Vector3(-0.09f, 0.63f, 0.06f), -148f, 16f, -60f, -9f, 0),
                            new Key(1.30f, new Vector3(0.00f, 0.58f, 0.34f), -75f, 6f, -35f, 5f, 1),
                            new Key(1.35f, new Vector3(0.14f, 0.56f, 0.36f), 30f, 2f, -10f, 14f, 0),
                            new Key(1.53f, new Vector3(0.30f, 0.52f, 0.26f), 95f, -10f, 8f, 10f, 2),
                        },
                    },
                    new Sequence
                    {
                        name = "golpe de cima (derruba)", hold = 0.15f, recover = 0.45f, step = 0.3f,
                        keys = new[]
                        {
                            new Key(0.31f, new Vector3(0.30f, 0.74f, 0.04f), 118f, 38f, 25f, -12f, 2, 30f),
                            new Key(0.37f, new Vector3(0.31f, 0.76f, 0.02f), 122f, 42f, 28f, -14f, 0, 30f),
                            new Key(0.42f, new Vector3(0.20f, 0.68f, 0.28f), 55f, 22f, 0f, 6f, 1, 40f),
                            new Key(0.47f, new Vector3(0.06f, 0.48f, 0.34f), -5f, -22f, -15f, 24f, 0, 45f),
                            new Key(0.66f, new Vector3(-0.02f, 0.46f, 0.30f), -28f, -26f, -20f, 22f, 2, 45f),
                        },
                    },
                    new Sequence
                    {
                        name = "giro (imparável)", hold = 0.12f, recover = 0.45f, step = 0f, spin = -360f, spinFrom = 0.64f, spinTo = 0.98f, freeFeet = true,
                        keys = new[]
                        {
                            new Key(0.58f, new Vector3(0.30f, 0.62f, 0.02f), 130f, 8f, 40f, -8f, 2),
                            new Key(0.64f, new Vector3(0.31f, 0.62f, 0.00f), 135f, 8f, 42f, -9f, 0),
                            new Key(0.70f, new Vector3(0.32f, 0.58f, 0.22f), 80f, 0f, 15f, 4f, 1),
                            new Key(0.90f, new Vector3(0.32f, 0.56f, 0.24f), 75f, 0f, 5f, 8f, 0),
                            new Key(1.08f, new Vector3(-0.04f, 0.50f, 0.18f), -90f, -12f, -40f, 10f, 2),
                        },
                    },
                    new Sequence
                    {
                        name = "salto (imparável)", hold = 0.1f, recover = 0.5f, step = 0f, jump = 0.4f, jumpFrom = 0.67f, jumpTo = 0.97f, freeFeet = true,
                        keys = new[]
                        {
                            new Key(0.61f, new Vector3(0.34f, 0.62f, -0.06f), 130f, 30f, 20f, 18f, 2, 30f),
                            new Key(0.67f, new Vector3(0.36f, 0.76f, -0.02f), 130f, 45f, 15f, -12f, 0, 30f),
                            new Key(0.87f, new Vector3(0.37f, 0.78f, 0.00f), 132f, 48f, 10f, -14f, 2, 30f),
                            new Key(0.95f, new Vector3(0.18f, 0.70f, 0.28f), 40f, 30f, 0f, 8f, 1, 40f),
                            new Key(1.00f, new Vector3(0.06f, 0.46f, 0.34f), -5f, -25f, -10f, 24f, 0, 45f),
                            new Key(1.17f, new Vector3(0.02f, 0.44f, 0.30f), -15f, -28f, -15f, 22f, 2, 45f),
                        },
                    },
                    new Sequence
                    {
                        // Chute: continua a animação gravada (perna); sem poses, o golpe à mão sai e o estado gravado toca.
                        name = "chute (gravado)", keys = new Key[0],
                    },
                    new Sequence
                    {
                        // Três cortes avançando (a IA avança no golpe): da direita, de volta e a diagonal de cima.
                        name = "sequência de três (fase 2)", hold = 0.15f, recover = 0.45f, step = 0f,
                        keys = new[]
                        {
                            new Key(0.49f, new Vector3(0.34f, 0.66f, 0.00f), 138f, 14f, 38f, -8f, 2),
                            new Key(0.54f, new Vector3(0.35f, 0.67f, -0.02f), 145f, 16f, 42f, -9f, 0),
                            new Key(0.60f, new Vector3(0.24f, 0.60f, 0.30f), 90f, 6f, 15f, 5f, 1),
                            new Key(0.65f, new Vector3(0.10f, 0.55f, 0.30f), -25f, 0f, -10f, 14f, 0),
                            new Key(0.83f, new Vector3(-0.05f, 0.50f, 0.16f), -95f, -12f, -42f, 10f, 2),
                            new Key(1.40f, new Vector3(-0.08f, 0.62f, 0.08f), -140f, 14f, -55f, -8f, 2),
                            new Key(1.45f, new Vector3(-0.09f, 0.63f, 0.06f), -148f, 16f, -60f, -9f, 0),
                            new Key(1.50f, new Vector3(0.00f, 0.58f, 0.34f), -75f, 6f, -35f, 5f, 1),
                            new Key(1.55f, new Vector3(0.14f, 0.56f, 0.36f), 30f, 2f, -10f, 14f, 0),
                            new Key(1.73f, new Vector3(0.30f, 0.52f, 0.26f), 95f, -10f, 8f, 10f, 2),
                            new Key(2.33f, new Vector3(0.30f, 0.74f, 0.04f), 118f, 38f, 25f, -12f, 2, 30f),
                            new Key(2.39f, new Vector3(0.31f, 0.76f, 0.02f), 122f, 42f, 28f, -14f, 0, 30f),
                            new Key(2.44f, new Vector3(0.20f, 0.68f, 0.28f), 55f, 22f, 0f, 6f, 1, 40f),
                            new Key(2.49f, new Vector3(0.06f, 0.48f, 0.34f), -5f, -22f, -15f, 24f, 0, 45f),
                            new Key(2.68f, new Vector3(-0.02f, 0.46f, 0.30f), -28f, -26f, -20f, 22f, 2, 45f),
                        },
                    },
                },
            },
        };
        [Tooltip("Segundos para entrar na pose do golpe (da guarda da animação).")]
        [SerializeField] float blendIn = 0.1f;
        [Tooltip("Para onde aponta o cotovelo direito e o esquerdo (espaço do personagem).")]
        [SerializeField] Vector3 rightElbow = new(0.7f, -0.6f, -0.25f), leftElbow = new(-0.7f, -0.6f, -0.25f);
        [Tooltip("Parte da torção do tronco desfeita no pescoço e na cabeça (o olhar fica no alvo).")]
        [SerializeField, Range(0f, 1f)] float headCounter = 0.7f;
        [Header("Ajuste automático da pegada")]
        [Tooltip("Até que fração do comprimento de cada braço a mão pode ir (o cotovelo nunca estica de todo).")]
        [SerializeField, Range(0.5f, 1f)] float reachUse = 0.88f;
        [Tooltip("Raio do tronco (m na escala 1), do quadril ao peito: o cabo e a lâmina passam por fora.")]
        [SerializeField] float bodyRadius = 0.2f;
        [Tooltip("Folga da espada fora da cabeça (1 = encostar no volume da cabeça com o chapéu).")]
        [SerializeField] float headClearance = 1.1f;
        [Tooltip("A ponta da espada nunca desce abaixo desta altura do chão (m na escala 1).")]
        [SerializeField] float tipClearance = 0.12f;
        [Tooltip("Em quanto tempo sai o passo do corte (s).")]
        [SerializeField] float stepSeconds = 0.16f;
        [Header("Pernas e quadril")]
        [Tooltip("Parte da torção do tronco feita pelo quadril (as pernas giram em cima dos pés plantados).")]
        [SerializeField, Range(0f, 0.6f)] float hipYawShare = 0.3f;
        [Tooltip("Quanto o quadril vai para a frente (+) ou para trás (−) por grau de inclinação do tronco (m na escala 1).")]
        [SerializeField] float hipShiftPerDegree = 0.003f;
        [Tooltip("Quanto o quadril abaixa por grau de inclinação do tronco para a frente (joelhos dobram no impacto).")]
        [SerializeField] float hipDropPerDegree = 0.0028f;
        [Tooltip("Quanto o pé sobe no passo (m na escala 1).")]
        [SerializeField] float stepLift = 0.07f;
        [Tooltip("Depois do impacto, em quanto tempo o pé de trás é arrastado para o lugar (s).")]
        [SerializeField] float rearSeconds = 0.22f;
        [Tooltip("Até que fração do comprimento a perna estica; mais que isso, o pé plantado escorrega junto.")]
        [SerializeField, Range(0.8f, 1f)] float legReach = 0.97f;

        CharacterCombat combat;
        FootCharacter character;
        Animator animator;
        Transform spine, chest, upperChest, neck, head;
        Transform rUpper, rLower, rHand, lUpper, lLower, lHand;
        Transform lThigh, lKnee, lFoot, rThigh, rKnee, rFoot;
        // Pés plantados no mundo (posição e giro) no começo do golpe, e os que estavam na tela no quadro anterior.
        Vector3 plantL, plantR, shownL, shownR;
        Quaternion plantLRot, plantRRot, shownLRot, shownRRot;
        Vector3 animatedHips, startHips, lastHips;
        bool plantPending, leftLeads;
        Transform[] bones;
        Quaternion[] animated, solved;
        SupportHandIK support;
        Transform hips;
        Vector3 fitOffset, fitVelocity;
        [Tooltip("Segundos para o ajuste da pegada acompanhar.")]
        [SerializeField] float fitSmooth = 0.04f;
        float started = -1f, fadeFrom = -1f, fadeStart;
        bool stepped;
        WeaponDefinition swingWeapon;

        /// <summary>Aterrissagem de um golpe com salto (ponto no chão): os efeitos levantam poeira.</summary>
        public static event Action<Vector3> HeavyLanding;

        /// <summary>O golpe à mão está mandando nos braços (o SupportHandIK fica de fora).</summary>
        public bool Active => started >= 0f && fadeFrom < 0f && (Time.time - started) * rate < seq.End;
        Sequence seq;
        Key[] swing;
        // Golpe do combo emendado no anterior: a pose de onde ele parte (o que o anterior mostrava, antes da 1ª pose deste).
        Key from, lastSampled;
        bool hasFrom;
        float lastWeight;
        /// <summary>Golpe do combo que está tocando (0–2; −1 nenhum).</summary>
        public int Variant { get; private set; } = -1;
        // Pose final do quadro anterior (com o IK que estava valendo): o golpe parte dela, sem tranco quando o outro IK solta a mão.
        Quaternion[] lastPose, startPose;
        int lastPoseFrame = -10;
        /// <summary>Um golpe à mão em andamento (preparo, corte ou volta à guarda).</summary>
        public bool Busy => started >= 0f;
        /// <summary>Na volta à guarda (depois da última pose).</summary>
        public bool Recovering => started >= 0f && !Active;
        public float Elapsed => started >= 0f ? (Time.time - started) * rate : -1f;
        public Key[] Keys => swing ?? movesets[0].combo[0].keys;
        Moveset set;
        /// <summary>Jogo de golpes da arma em uso (null fora do golpe).</summary>
        public string MovesetName => set != null ? set.name : null;

        void Awake()
        {
            animator = GetComponent<Animator>();
            combat = GetComponentInParent<CharacterCombat>();
            character = GetComponentInParent<FootCharacter>();
            if (!animator || !animator.isHuman) { enabled = false; return; }
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            rUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            lUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            lLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            lHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            lThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            lKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            lFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            rFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            support = GetComponent<SupportHandIK>();
            bones = new[] { spine, chest, upperChest, neck, head, rUpper, rLower, rHand, lUpper, lLower, lHand, hips, lThigh, lKnee, lFoot, rThigh, rKnee, rFoot };
            animated = new Quaternion[bones.Length];
            solved = new Quaternion[bones.Length];
            lastPose = new Quaternion[bones.Length];
            startPose = new Quaternion[bones.Length];
        }

        void OnEnable() { if (combat) combat.Attacked += OnAttacked; }
        void OnDisable() { if (combat) combat.Attacked -= OnAttacked; started = -1f; Slide(null, 0f); MoveBody(0f, 0f); }

        // A arma que escorregou na mão e o lugar dela na pegada de descanso.
        Transform slid;
        Vector3 slidBase;

        /// <summary>Anda a arma ao longo do próprio cabo dentro da mão (a mão vai para o pomo); 0 ou outra arma devolve a anterior.</summary>
        // O modelo (este objeto) girado/erguido no giro e no salto, e o lugar dele parado.
        bool bodyMoved;
        Quaternion bodyBaseRot;
        Vector3 bodyBasePos;
        Transform frame;
        bool landed;

        /// <summary>Gira o modelo inteiro em volta do eixo vertical e o ergue (giro e salto); 0/0 devolve.</summary>
        void MoveBody(float yaw, float lift)
        {
            if (Mathf.Abs(yaw) < 0.01f && lift < 0.0005f)
            {
                if (bodyMoved) { transform.localRotation = bodyBaseRot; transform.localPosition = bodyBasePos; bodyMoved = false; }
                return;
            }
            if (!bodyMoved) { bodyBaseRot = transform.localRotation; bodyBasePos = transform.localPosition; bodyMoved = true; }
            transform.localRotation = Quaternion.AngleAxis(yaw, Vector3.up) * bodyBaseRot;
            Vector3 up = transform.parent ? transform.parent.InverseTransformVector(Vector3.up * lift) : Vector3.up * lift;
            transform.localPosition = bodyBasePos + up;
        }

        void Slide(Transform visual, float distance)
        {
            if (slid && (slid != visual || distance <= 0f)) { slid.localPosition = slidBase; slid = null; }
            if (!visual || distance <= 0f) return;
            if (!slid) { slid = visual; slidBase = visual.localPosition; }
            // Em metros no mundo (os ossos do kit têm escala 100: o espaço do pai não é em metros).
            Vector3 d = visual.forward * distance;
            visual.localPosition = slidBase + (visual.parent ? visual.parent.InverseTransformVector(d) : d);
        }

        void OnAttacked(WeaponDefinition weapon, int variant)
        {
            Moveset found = null;
            // O capitão (todas as armas dele) e os inimigos cuja arma pede um jogo pelo nome.
            if (weapon && weapon.kind == WeaponKind.Melee && (combat.MeleeAssist || !string.IsNullOrEmpty(weapon.handMoves)))
                {
                // Pelo nome que a arma pede; sem nome, pelo tipo (os jogos "só pelo nome" ficam de fora: a adaga é do tipo do cutelo).
                string wanted = weapon.handMoves;
                foreach (var m in movesets)
                {
                    if (m == null) continue;
                    if (!string.IsNullOrEmpty(wanted) ? m.name == wanted : !m.byNameOnly && m.weaponType == weapon.animatorWeaponType) { found = m; break; }
                }
            }
            if (found == null || found.combo == null || variant < 0 || variant >= found.combo.Length || found.combo[variant].keys == null
                || found.combo[variant].keys.Length < 2) { Stop(); return; }
            // Trocou de arma no meio: não emenda (a pose do outro jogo de golpes não serve).
            if (found != set) started = -1f;
            // Emenda: o anterior ainda está na tela (segurando a continuação ou voltando) e o novo parte dele.
            hasFrom = started >= 0f && fadeFrom < 0f && Time.frameCount - lastPoseFrame <= 1 && lastWeight > 0.3f;
            from = lastSampled;
            set = found;
            rate = set.timeScaled && weapon.attackAnimSpeed > 0.05f ? weapon.attackAnimSpeed : 1f;
            seq = set.combo[variant];
            swing = seq.keys;
            Variant = variant;
            started = Time.time;
            startFromLast = Time.frameCount - lastPoseFrame <= 1;
            if (startFromLast) { Array.Copy(lastPose, startPose, lastPose.Length); startHips = lastHips; }
            plantPending = true;
            // De onde a arma parte (a mão e a arma mostradas agora), no espaço do personagem: a entrada vai daqui até a 1ª pose pelo alvo da
            // mão, com o ajuste da pegada valendo (misturar as juntas fazia o cabo passar pela barriga por um ou dois quadros).
            var shownVisual = combat.WeaponVisual(combat.CurrentIndex);
            var body0 = character ? character.transform : transform.parent ? transform.parent : transform;
            hasEntry = shownVisual && rHand;
            if (hasEntry)
            {
                entryWrist = body0.InverseTransformPoint(rHand.position);
                entryRot = Quaternion.Inverse(body0.rotation) * BladeRot(shownVisual, found);
            }
            fadeFrom = -1f;
            stepped = false;
            landed = false;
            swingWeapon = weapon;
        }

        void Stop()
        {
            Variant = -1;
            if (started < 0f || fadeFrom >= 0f) return;
            fadeFrom = Weight((Time.time - started) * rate);
            fadeStart = Time.time;
        }

        float Weight(float t)
        {
            float end = seq.End;
            if (t < blendIn) return Smooth(t / blendIn);
            if (t <= end) return 1f;
            return 1f - Smooth((t - end) / seq.recover);
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        bool startFromLast;
        // Velocidade das poses (as armas mais rápidas do mesmo jogo de golpes).
        float rate = 1f;
        float slideLeft;
        bool hasEntry;
        Vector3 entryWrist;
        Quaternion entryRot;
        static readonly int[] armBones = { 5, 6, 7, 8, 9, 10 };

        void LateUpdate()
        {
            if (started < 0f)
            {
                for (int i = 0; i < bones.Length; i++) if (bones[i]) lastPose[i] = bones[i].localRotation;
                RememberShown();
                lastPoseFrame = Time.frameCount;
                return;
            }
            float t = (Time.time - started) * rate;
            if (combat.Current != swingWeapon || (character && (character.IsDodging || character.IsDown))) Stop();
            float w = Weight(t);
            if (fadeFrom >= 0f)
            {
                w = Mathf.Min(w, fadeFrom * (1f - Smooth((Time.time - fadeStart) / 0.12f)));
                if (Time.time - fadeStart >= 0.12f) w = 0f;
            }
            if (w <= 0f && (fadeFrom >= 0f || t > blendIn)) { started = -1f; Variant = -1; lastWeight = 0f; Slide(null, 0f); MoveBody(0f, 0f); return; }
            var visual = combat.WeaponVisual(combat.CurrentIndex);
            if (!visual || !rHand || !lHand) return;
            // A mão escorrega para o pomo no preparo e volta antes de o braço descer para a guarda (comprida, a arma raspava o chão).
            float slideWeight = t > seq.End || fadeFrom >= 0f ? Smooth((w - 0.45f) / 0.55f) : Smooth(Mathf.Min(1f, t / 0.08f));
            Slide(visual, set.gripSlide * (character ? character.transform.lossyScale.y : 1f) * slideWeight);
            // O que falta escorregar fica de cabo atrás da mão (e sai da frente).
            slideLeft = set.gripSlide * (1f - slideWeight);

            // Passo para a frente quando o corte arranca.
            if (!stepped && character && t >= swing[1].time && fadeFrom < 0f)
            {
                stepped = true;
                if (seq.step > 0f || seq.side != 0f)
                {
                    float dur = (seq.stepTime > 0f ? seq.stepTime : stepSeconds) / rate;
                    var ct = character.transform;
                    character.StepFor((ct.forward * seq.step + ct.right * seq.side) / dur, dur);
                }
            }

            var body = character ? character.transform : transform.parent ? transform.parent : transform;
            float bodyScale = body.lossyScale.y;
            float spinNow = seq.spin != 0f && seq.spinTo > seq.spinFrom ? seq.spin * Ease((t - seq.spinFrom) / (seq.spinTo - seq.spinFrom), 0) : 0f;
            float liftNow = seq.jump > 0f && seq.jumpTo > seq.jumpFrom ? seq.jump * bodyScale * Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - seq.jumpFrom) / (seq.jumpTo - seq.jumpFrom))) : 0f;
            if (fadeFrom >= 0f) { spinNow *= w; liftNow *= w; }
            MoveBody(spinNow, liftNow);
            if (seq.jump > 0f && !landed && t >= seq.jumpTo && fadeFrom < 0f)
            {
                landed = true;
                HeavyLanding?.Invoke(body.position + body.forward * (0.6f * bodyScale));
            }
            if (!frame)
            {
                frame = new GameObject("QuadroDoGolpe").transform;
                frame.SetParent(body, false);
            }
            frame.SetPositionAndRotation(body.position, Quaternion.AngleAxis(spinNow, body.up) * body.rotation);

            for (int i = 0; i < bones.Length; i++) if (bones[i]) animated[i] = bones[i].localRotation;
            Vector3 animWrist = body.InverseTransformPoint(rHand.position);
            Quaternion animRot = Quaternion.Inverse(body.rotation) * BladeRot(visual, set);
            Sample(t, out var grip, out float yaw, out float pitch, out float torsoYaw, out float torsoPitch, out float roll);
            lastSampled = new Key(t, grip, yaw, pitch, torsoYaw, torsoPitch, 0, roll);
            SampleFree(t);
            if (hasFreeHand) lastSampled = lastSampled.Free(freeHand.x, freeHand.y, freeHand.z);
            var root = frame;
            float scale = bodyScale;
            Vector3 up = root.up, right = root.right;

            // Pernas e quadril primeiro (o quadril leva o tronco e os braços junto).
            if (hips) animatedHips = hips.localPosition;
            bool legs = Legs(t, torsoYaw, torsoPitch, root, scale);
            float spineShare = legs ? 1f - hipYawShare : 1f;

            // Tronco: torção e inclinação repartidas na coluna; pescoço e cabeça desfazem parte da torção.
            Twist(spine, 0.3f, torsoYaw * spineShare, torsoPitch, up, right);
            Twist(chest, 0.35f, torsoYaw * spineShare, torsoPitch, up, right);
            Twist(upperChest, 0.35f, torsoYaw * spineShare, torsoPitch, up, right);
            if (neck) neck.rotation = Quaternion.AngleAxis(-torsoYaw * headCounter * 0.5f, up) * neck.rotation;
            if (head) head.rotation = Quaternion.AngleAxis(-torsoYaw * headCounter * 0.5f, up) * Quaternion.AngleAxis(-torsoPitch * 0.6f, right) * head.rotation;

            // Corpo (tronco, cabeça, quadril, pernas) misturado agora: os braços são resolvidos em cima do corpo que vai aparecer.
            // A mão vai do lugar mostrado à 1ª pose no tempo do preparo (com 0,1 s a arma chicoteava para o outro lado).
            float entryTime = Mathf.Max(blendIn, swing[0].time);
            bool entering = hasEntry && t < entryTime && fadeFrom < 0f;
            for (int i = 0; i < bones.Length; i++)
            {
                if (!bones[i] || (i >= 5 && i <= 10)) continue;
                solved[i] = bones[i].localRotation;
                Quaternion start = startFromLast && t < blendIn && fadeFrom < 0f ? startPose[i] : animated[i];
                bones[i].localRotation = Quaternion.Slerp(start, solved[i], w);
            }
            if (hips)
            {
                Vector3 solvedHips = hips.localPosition;
                Vector3 hipStart = startFromLast && t < blendIn && fadeFrom < 0f ? startHips : animatedHips;
                hips.localPosition = Vector3.Lerp(hipStart, solvedHips, w);
            }

            // Lâmina e mão direita (a ponta não entra no chão: a lâmina sobe o que faltar).
            float lowest = Mathf.Clamp((tipClearance - grip.y) / set.bladeReach, -1f, 1f);
            pitch = Mathf.Max(pitch, Mathf.Asin(lowest) * Mathf.Rad2Deg);
            Vector3 bladeLocal = BladeDir(yaw, pitch);
            Vector3 edgeLocal = new Vector3(-Mathf.Cos(yaw * Mathf.Deg2Rad), 0f, Mathf.Sin(yaw * Mathf.Deg2Rad)) * set.edgeSign;
            edgeLocal = Quaternion.AngleAxis(roll, bladeLocal) * edgeLocal;
            Vector3 blade = root.TransformDirection(bladeLocal), edge = root.TransformDirection(edgeLocal);
            Quaternion inHand = Quaternion.Inverse(rHand.rotation) * BladeRot(visual, set);
            Quaternion goalRot = Quaternion.LookRotation(blade, edge);
            Vector3 wrist = root.position + root.rotation * (grip * scale);
            if (entering)
            {
                var body0 = character ? character.transform : transform.parent ? transform.parent : transform;
                float u = Smooth(t / entryTime);
                wrist = Vector3.Lerp(body0.TransformPoint(entryWrist), wrist, u);
                // A direção anda por giro e inclinação (o caminho mais curto atravessava o corpo: pendurada → atrás do ombro).
                Vector3 from = entryRot * Vector3.forward;
                float fromYaw = Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg, fromPitch = Mathf.Asin(Mathf.Clamp(from.y, -1f, 1f)) * Mathf.Rad2Deg;
                // O fio também anda pelo giro da lâmina no próprio eixo (misturar o "para cima" virava a arma num quadro).
                Vector3 fromBlade = BladeDir(fromYaw, fromPitch);
                float fromRoll = Vector3.SignedAngle(EdgeDir(fromYaw), Vector3.ProjectOnPlane(entryRot * Vector3.up, fromBlade), fromBlade);
                // Ângulos de partida "desembrulhados" em volta da 1ª pose (fixa): a pose amostrada anda e o caminho curto trocava de lado.
                fromYaw = swing[0].bladeYaw + Mathf.DeltaAngle(swing[0].bladeYaw, fromYaw);
                fromRoll = swing[0].roll + Mathf.DeltaAngle(swing[0].roll, fromRoll);
                float yawU = Mathf.Lerp(fromYaw, yaw, u), pitchU = Mathf.Lerp(fromPitch, pitch, u), rollU = Mathf.Lerp(fromRoll, roll, u);
                // A ponta não desce no chão no caminho (a arma escorregando na mão ainda pendurada raspava).
                float wristUp = body0.InverseTransformPoint(wrist).y / Mathf.Max(0.01f, scale);
                pitchU = Mathf.Max(pitchU, Mathf.Asin(Mathf.Clamp((tipClearance - wristUp) / Mathf.Max(0.1f, set.bladeReach - slideLeft), -1f, 1f)) * Mathf.Rad2Deg);
                Vector3 pathLocal = BladeDir(yawU, pitchU);
                Vector3 edgeU = Quaternion.AngleAxis(rollU, pathLocal) * EdgeDir(yawU);
                goalRot = Quaternion.LookRotation(body0.TransformDirection(pathLocal), body0.TransformDirection(edgeU));
                blade = goalRot * Vector3.forward;
                edge = goalRot * Vector3.up;
            }
            // Volta à guarda guiada: do golpe (u = 0) à pose da animação (u = 1), pelo mesmo caminho de giro e inclinação da entrada.
            bool exiting = set.guidedRecovery && fadeFrom < 0f && t > seq.End;
            if (exiting)
            {
                var body0 = character ? character.transform : transform.parent ? transform.parent : transform;
                float u = 1f - w;
                wrist = Vector3.Lerp(wrist, body0.TransformPoint(animWrist), u);
                Vector3 to = animRot * Vector3.forward;
                float toYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, toPitch = Mathf.Asin(Mathf.Clamp(to.y, -1f, 1f)) * Mathf.Rad2Deg;
                Vector3 toBlade = BladeDir(toYaw, toPitch);
                float toRoll = Vector3.SignedAngle(EdgeDir(toYaw), Vector3.ProjectOnPlane(animRot * Vector3.up, toBlade), toBlade);
                toYaw = yaw + Mathf.DeltaAngle(yaw, toYaw);
                toRoll = roll + Mathf.DeltaAngle(roll, toRoll);
                float yawU = Mathf.Lerp(yaw, toYaw, u), pitchU = Mathf.Lerp(pitch, toPitch, u), rollU = Mathf.Lerp(roll, toRoll, u);
                float wristUp = body0.InverseTransformPoint(wrist).y / Mathf.Max(0.01f, scale);
                pitchU = Mathf.Max(pitchU, Mathf.Asin(Mathf.Clamp((tipClearance - wristUp) / Mathf.Max(0.1f, set.bladeReach), -1f, 1f)) * Mathf.Rad2Deg);
                Vector3 pathLocal = BladeDir(yawU, pitchU);
                Vector3 edgeU = Quaternion.AngleAxis(rollU, pathLocal) * EdgeDir(yawU);
                goalRot = Quaternion.LookRotation(body0.TransformDirection(pathLocal), body0.TransformDirection(edgeU));
                blade = goalRot * Vector3.forward;
                edge = goalRot * Vector3.up;
            }
            Quaternion handGoal = goalRot * Quaternion.Inverse(inHand);
            // O ajuste muda devagar (empurrar a mão de uma vez virava o cotovelo num quadro).
            Vector3 fitted = Fit(wrist, blade, root, scale);
            Vector3 wanted = root.InverseTransformVector(fitted - wrist);
            fitOffset = t < 0.02f ? wanted : Vector3.SmoothDamp(fitOffset, wanted, ref fitVelocity, fitSmooth);
            wrist += root.TransformVector(fitOffset);
            FitWrist = wrist;
            TwoBone(rUpper, rLower, rHand, wrist, root.TransformDirection(rightElbow));
            rHand.rotation = handGoal;
            if (set.forearmTwist > 0f) ShareTwist(rLower, rHand, animated[7], set.forearmTwist, handGoal);

            if (set.twoHanded)
            {
                // Mão esquerda no cabo, mais perto do pomo, com o mesmo vão pulso → cabo da direita.
                Vector3 alongHandle = set.reverseBlade ? -visual.forward : visual.forward;
                Vector3 leftWrist = rHand.position - alongHandle * (set.leftOnHandle * scale);
                TwoBone(lUpper, lLower, lHand, leftWrist, root.TransformDirection(leftElbow));
                lHand.rotation = Quaternion.LookRotation(blade, -edge) * Quaternion.Inverse(inHand);
                LeftReachGap = (lHand.position - leftWrist).magnitude;
            }
            else if (hasFreeHand)
            {
                // Mão livre (o gancho): contrapeso, dentro do alcance do braço.
                Vector3 leftWrist = root.position + root.rotation * (freeHand * scale);
                float reachL = ((lLower.position - lUpper.position).magnitude + (lHand.position - lLower.position).magnitude) * reachUse;
                Vector3 fromShoulder = leftWrist - lUpper.position;
                if (fromShoulder.magnitude > reachL) leftWrist = lUpper.position + fromShoulder.normalized * reachL;
                else if (set.freeMinReach > 0f && fromShoulder.magnitude < reachL * set.freeMinReach / reachUse && fromShoulder.sqrMagnitude > 1e-6f)
                    leftWrist = lUpper.position + fromShoulder.normalized * (reachL * set.freeMinReach / reachUse);
                TwoBone(lUpper, lLower, lHand, leftWrist, root.TransformDirection(leftElbow));
                LeftReachGap = (lHand.position - leftWrist).magnitude;
            }
            else LeftReachGap = 0f;
            RightReachGap = (rHand.position - wrist).magnitude;
            foreach (int i in armBones)
            {
                if (!bones[i]) continue;
                // Entrando, o braço da arma (e o esquerdo, se está no cabo) já foi resolvido no alvo misturado: fica inteiro.
                bool full = entering && (i <= 7 || set.twoHanded);
                if (full) continue;
                solved[i] = bones[i].localRotation;
                Quaternion start = startFromLast && t < blendIn && fadeFrom < 0f ? startPose[i] : animated[i];
                float armW = exiting && (i <= 7 || set.twoHanded) ? Smooth(w / 0.3f) : w;
                bones[i].localRotation = Quaternion.Slerp(start, solved[i], armW);
            }
            for (int i = 0; i < bones.Length; i++) if (bones[i]) lastPose[i] = bones[i].localRotation;
            // A pose mostrada fica guardada: o próximo golpe do combo parte dela.
            RememberShown();
            lastPoseFrame = Time.frameCount;
            lastWeight = w;
        }

        /// <summary>
        /// Passa parte do giro do punho (no eixo do antebraço, medido contra a pose da animação) para o antebraço e põe a mão de volta no
        /// giro pedido: o kit não tem osso de torção e o punho girando sozinho 100°+ parecia quebrado.
        /// </summary>
        void ShareTwist(Transform lower, Transform hand, Quaternion handRef, float share, Quaternion handWorld)
        {
            Quaternion delta = hand.localRotation * Quaternion.Inverse(handRef);
            Vector3 axis = hand.localPosition.normalized;
            Vector3 v = new Vector3(delta.x, delta.y, delta.z);
            Vector3 proj = Vector3.Dot(v, axis) * axis;
            var twist = new Quaternion(proj.x, proj.y, proj.z, delta.w);
            float n = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
            if (n < 1e-5f) return;
            twist = new Quaternion(twist.x / n, twist.y / n, twist.z / n, twist.w / n);
            twist.ToAngleAxis(out float angle, out Vector3 tAxis);
            if (angle > 180f) angle -= 360f;
            if (Vector3.Dot(tAxis, axis) < 0f) angle = -angle;
            float moved = angle * share;
            lower.rotation = Quaternion.AngleAxis(moved, lower.TransformDirection(axis)) * lower.rotation;
            hand.rotation = handWorld;
        }

        /// <summary>Giro da arma com a lâmina no +Z (a adaga vem montada ao contrário).</summary>
        static Quaternion BladeRot(Transform visual, Moveset m) => m != null && m.reverseBlade ? visual.rotation * Quaternion.Euler(0f, 180f, 0f) : visual.rotation;

        void RememberShown()
        {
            if (hips) lastHips = hips.localPosition;
            if (lFoot) { shownL = lFoot.position; shownLRot = lFoot.rotation; }
            if (rFoot) { shownR = rFoot.position; shownRRot = rFoot.rotation; }
        }

        /// <summary>Pé de trás arrastado (0–1) e pé da frente no passo (0–1), para os testes.</summary>
        public float LeadTravel { get; private set; }
        public float RearTravel { get; private set; }

        /// <summary>
        /// O peso do golpe nas pernas: os pés ficam plantados onde estavam quando o golpe começou (não escorregam com o corpo), o pé da frente
        /// dá o passo quando o corte arranca (sobe um pouco e pousa no lugar da animação) e o de trás é arrastado depois do impacto. O quadril
        /// gira parte da torção do tronco, vai para trás no preparo e para a frente no corte, e abaixa com o tronco inclinado (joelhos dobram).
        /// </summary>
        bool Legs(float t, float torsoYaw, float torsoPitch, Transform root, float scale)
        {
            if (!hips || !lThigh || !lKnee || !lFoot || !rThigh || !rKnee || !rFoot) return false;
            // Pés da animação agora (já no chão pelo ProceduralTouches) e o joelho de cada um (para onde dobra).
            Vector3 animL = lFoot.position, animR = rFoot.position;
            Quaternion animLRot = lFoot.rotation, animRRot = rFoot.rotation;
            Vector3 kneeL = lKnee.position - (lThigh.position + animL) * 0.5f, kneeR = rKnee.position - (rThigh.position + animR) * 0.5f;
            if (plantPending)
            {
                plantPending = false;
                bool fresh = Time.frameCount - lastPoseFrame <= 1;
                plantL = fresh ? shownL : animL; plantR = fresh ? shownR : animR;
                plantLRot = fresh ? shownLRot : animLRot; plantRRot = fresh ? shownRRot : animRRot;
                // O pé mais à frente (na guarda, o esquerdo) é o que dá o passo.
                leftLeads = Vector3.Dot(animL - animR, root.forward) >= 0f;
            }

            // Quadril: gira com o tronco, vai para trás/frente e abaixa.
            Vector3 up = root.up;
            float shift = torsoPitch * hipShiftPerDegree * scale;
            float drop = Mathf.Max(0f, torsoPitch) * hipDropPerDegree * scale;
            hips.position += root.forward * shift - up * drop;
            hips.rotation = Quaternion.AngleAxis(torsoYaw * hipYawShare, up) * hips.rotation;

            // Quando cada pé anda: o da frente no arranque do corte, o de trás depois do impacto.
            float leadFrom = swing[1].time, leadDur = stepSeconds + 0.06f;
            float rearFrom = swing[Mathf.Max(1, swing.Length - 2)].time, rearDur = rearSeconds;
            LeadTravel = seq.freeFeet ? 1f : Ease((t - leadFrom) / leadDur, 0);
            RearTravel = seq.freeFeet ? 1f : Ease((t - rearFrom) / rearDur, 0);
            Vector3 targetL, targetR; Quaternion rotL, rotR;
            Foot(leftLeads ? LeadTravel : RearTravel, plantL, plantLRot, animL, animLRot, scale, leftLeads ? stepLift : stepLift * 0.4f, out targetL, out rotL);
            Foot(leftLeads ? RearTravel : LeadTravel, plantR, plantRRot, animR, animRRot, scale, leftLeads ? stepLift * 0.4f : stepLift, out targetR, out rotR);
            // Perna esticada demais: o pé plantado escorrega (arrastado pelo corpo).
            targetL = KeepInReach(lThigh, lKnee, lFoot, targetL, animL, ref plantL);
            targetR = KeepInReach(rThigh, rKnee, rFoot, targetR, animR, ref plantR);
            LegIK(lThigh, lKnee, lFoot, targetL, kneeL);
            LegIK(rThigh, rKnee, rFoot, targetR, kneeR);
            lFoot.rotation = rotL; rFoot.rotation = rotR;
            return true;
        }

        void Foot(float travel, Vector3 plant, Quaternion plantRot, Vector3 anim, Quaternion animRot, float scale, float lift, out Vector3 target, out Quaternion rot)
        {
            target = Vector3.Lerp(plant, anim, travel) + Vector3.up * (Mathf.Sin(travel * Mathf.PI) * lift * scale);
            rot = Quaternion.Slerp(plantRot, animRot, travel);
        }

        Vector3 KeepInReach(Transform thigh, Transform knee, Transform foot, Vector3 target, Vector3 anim, ref Vector3 plant)
        {
            float reach = ((knee.position - thigh.position).magnitude + (foot.position - knee.position).magnitude) * legReach;
            Vector3 hip = thigh.position;
            if ((target - hip).magnitude <= reach) return target;
            // Escorrega em direção ao lugar da animação até caber (e o pé plantado fica ali daqui para a frente).
            Vector3 a = target, b = anim;
            for (int i = 0; i < 8; i++)
            {
                Vector3 m = (a + b) * 0.5f;
                if ((m - hip).magnitude > reach) a = m; else b = m;
            }
            plant += b - target;
            return b;
        }

        /// <summary>IK de duas juntas na perna, com o joelho dobrando para o lado em que a animação o dobra.</summary>
        static void LegIK(Transform thigh, Transform knee, Transform foot, Vector3 target, Vector3 kneeDir)
        {
            Vector3 a = thigh.position, b = knee.position, c = foot.position;
            float l1 = (b - a).magnitude, l2 = (c - b).magnitude;
            if (l1 < 1e-4f || l2 < 1e-4f) return;
            Vector3 toT = target - a;
            float d = Mathf.Clamp(toT.magnitude, Mathf.Abs(l1 - l2) + 1e-4f, (l1 + l2) * 0.999f);
            Vector3 n = toT.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(kneeDir, n);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.ProjectOnPlane(b - a, n);
            bend.Normalize();
            float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
            Vector3 newKnee = a + n * (l1 * cosA) + bend * (l1 * Mathf.Sqrt(1f - cosA * cosA));
            thigh.rotation = Quaternion.FromToRotation(b - a, newKnee - a) * thigh.rotation;
            Vector3 b2 = knee.position, c2 = foot.position;
            knee.rotation = Quaternion.FromToRotation(c2 - b2, a + n * d - b2) * knee.rotation;
        }

        /// <summary>Onde o pulso direito foi posto depois do ajuste.</summary>
        public Vector3 FitWrist { get; private set; }

        /// <summary>
        /// A pegada respeita o corpo: a espada inteira (do pomo à ponta) sai do tronco e da cabeça empurrando a mão para fora, e as duas
        /// mãos ficam dentro do alcance dos braços (o alcance vence: braço esticado demais é pior que a espada raspando).
        /// </summary>
        Vector3 Fit(Vector3 wrist, Vector3 blade, Transform root, float scale)
        {
            float reachR = ((rLower.position - rUpper.position).magnitude + (rHand.position - rLower.position).magnitude) * reachUse;
            float reachL = ((lLower.position - lUpper.position).magnitude + (lHand.position - lLower.position).magnitude) * reachUse;
            float length = (set.bladeReach - slideLeft) * scale;
            float pommel = set.pommelBehind + slideLeft;
            Vector3 up = root.up;
            for (int iter = 0; iter < 4; iter++)
            {
                Vector3 a = wrist - blade * (pommel * scale), b = wrist + blade * length;
                // Tronco: cápsula do quadril ao peito.
                if (hips && upperChest)
                {
                    Vector3 c0 = hips.position + up * (0.05f * scale), c1 = upperChest.position;
                    ClosestOnSegments(a, b, c0, c1, out var onSword, out var onBody);
                    Vector3 away = onSword - onBody;
                    float dist = away.magnitude, need = bodyRadius * scale;
                    away -= up * Vector3.Dot(away, up);
                    if (dist < need)
                    {
                        if (away.sqrMagnitude < 1e-6f) away = root.forward;
                        wrist += away.normalized * (need - dist);
                    }
                }
                // Cabeça (com o chapéu).
                if (support && support.TryGetHeadVolume(out var hc, out var axes, out var radii))
                {
                    a = wrist - blade * (pommel * scale); b = wrist + blade * length;
                    Quaternion inv = Quaternion.Inverse(axes);
                    float best = 9f; Vector3 bestP = a;
                    for (int k = 0; k <= 12; k++)
                    {
                        Vector3 p = Vector3.Lerp(a, b, k / 12f), q = inv * (p - hc);
                        float d = new Vector3(q.x / radii.x, q.y / radii.y, q.z / radii.z).magnitude;
                        if (d < best) { best = d; bestP = p; }
                    }
                    if (best < headClearance)
                    {
                        Vector3 away = bestP - hc;
                        away -= up * Mathf.Max(0f, Vector3.Dot(away, up));
                        if (away.sqrMagnitude < 1e-6f) away = root.right;
                        wrist += away.normalized * ((headClearance - best) * Mathf.Min(radii.x, radii.z));
                    }
                }
                // Alcance: a direita do ombro direito; a esquerda (no cabo, mais perto do pomo) do ombro esquerdo.
                Vector3 r = wrist - rUpper.position;
                if (r.magnitude > reachR) wrist = rUpper.position + r.normalized * reachR;
                if (set.twoHanded)
                {
                    Vector3 l = wrist - blade * (set.leftOnHandle * scale) - lUpper.position;
                    if (l.magnitude > reachL) wrist -= l.normalized * (l.magnitude - reachL);
                }
            }
            return wrist;
        }

        static void ClosestOnSegments(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out Vector3 c1, out Vector3 c2)
        {
            float best = float.MaxValue; c1 = p1; c2 = p2;
            for (int i = 0; i <= 16; i++)
            {
                Vector3 p = Vector3.Lerp(p1, q1, i / 16f), d = q2 - p2;
                float u = Mathf.Clamp01(Vector3.Dot(p - p2, d) / Mathf.Max(1e-6f, d.sqrMagnitude));
                Vector3 q = p2 + d * u;
                float dist = (p - q).sqrMagnitude;
                if (dist < best) { best = dist; c1 = p; c2 = q; }
            }
        }

        /// <summary>Quanto cada pulso ficou longe do alvo (alcance curto = braço esticado sem chegar).</summary>
        public float LeftReachGap { get; private set; }
        public float RightReachGap { get; private set; }

        /// <summary>Fio sem giro da lâmina: na horizontal, perpendicular à lâmina (o lado da arma pelo edgeSign).</summary>
        Vector3 EdgeDir(float yaw) => new Vector3(-Mathf.Cos(yaw * Mathf.Deg2Rad), 0f, Mathf.Sin(yaw * Mathf.Deg2Rad)) * set.edgeSign;

        static Vector3 BladeDir(float yaw, float pitch)
        {
            float y = yaw * Mathf.Deg2Rad, p = pitch * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(y) * Mathf.Cos(p), Mathf.Sin(p), Mathf.Cos(y) * Mathf.Cos(p));
        }

        static void Twist(Transform bone, float share, float yaw, float pitch, Vector3 up, Vector3 right)
        {
            if (!bone) return;
            bone.rotation = Quaternion.AngleAxis(yaw * share, up) * Quaternion.AngleAxis(pitch * share, right) * bone.rotation;
        }

        void Sample(float t, out Vector3 grip, out float yaw, out float pitch, out float torsoYaw, out float torsoPitch, out float roll)
        {
            int n = swing.Length;
            if (t <= swing[0].time)
            {
                var k = swing[0];
                float u = Ease(t / Mathf.Max(0.01f, k.time), 2);
                if (hasFrom)
                {
                    // Emendado: vai da pose em que o golpe anterior estava até o preparo deste.
                    grip = Vector3.Lerp(from.grip, k.grip, u);
                    yaw = Mathf.Lerp(from.bladeYaw, k.bladeYaw, u); pitch = Mathf.Lerp(from.bladePitch, k.bladePitch, u);
                    torsoYaw = Mathf.Lerp(from.torsoYaw, k.torsoYaw, u); torsoPitch = Mathf.Lerp(from.torsoPitch, k.torsoPitch, u);
                    roll = Mathf.Lerp(from.roll, k.roll, u);
                    return;
                }
                // Da guarda da animação: o peso cuida da mistura dos braços; o tronco gira aos poucos.
                grip = k.grip; yaw = k.bladeYaw; pitch = k.bladePitch; roll = k.roll;
                torsoYaw = k.torsoYaw * u; torsoPitch = k.torsoPitch * u;
                return;
            }
            if (t >= swing[n - 1].time)
            {
                var k = swing[n - 1];
                grip = k.grip; yaw = k.bladeYaw; pitch = k.bladePitch; torsoYaw = k.torsoYaw; torsoPitch = k.torsoPitch; roll = k.roll;
                return;
            }
            int i = 1;
            while (i < n - 1 && swing[i].time < t) i++;
            var a = swing[i - 1]; var b = swing[i];
            float s = Ease((t - a.time) / Mathf.Max(0.001f, b.time - a.time), b.ease);
            Vector3 p0 = i >= 2 ? swing[i - 2].grip : a.grip, p3 = i + 1 < n ? swing[i + 1].grip : b.grip;
            grip = CatmullRom(p0, a.grip, b.grip, p3, s);
            yaw = Mathf.Lerp(a.bladeYaw, b.bladeYaw, s);
            pitch = Mathf.Lerp(a.bladePitch, b.bladePitch, s);
            torsoYaw = Mathf.Lerp(a.torsoYaw, b.torsoYaw, s);
            torsoPitch = Mathf.Lerp(a.torsoPitch, b.torsoPitch, s);
            roll = Mathf.Lerp(a.roll, b.roll, s);
        }

        Vector3 freeHand;
        bool hasFreeHand;

        /// <summary>Mão livre das armas de uma mão (as poses sem <see cref="Key.hasFree"/> deixam o braço da animação).</summary>
        void SampleFree(float t)
        {
            int n = swing.Length;
            hasFreeHand = swing[0].hasFree;
            if (!hasFreeHand) return;
            if (t <= swing[0].time)
            {
                float u = Ease(t / Mathf.Max(0.01f, swing[0].time), 2);
                freeHand = hasFrom && from.hasFree ? Vector3.Lerp(from.free, swing[0].free, u) : swing[0].free;
                return;
            }
            if (t >= swing[n - 1].time) { freeHand = swing[n - 1].free; return; }
            int i = 1;
            while (i < n - 1 && swing[i].time < t) i++;
            var a = swing[i - 1]; var b = swing[i];
            float s = Ease((t - a.time) / Mathf.Max(0.001f, b.time - a.time), b.ease);
            freeHand = Vector3.Lerp(a.free, b.free, s);
        }

        static float Ease(float x, int mode)
        {
            x = Mathf.Clamp01(x);
            switch (mode)
            {
                case 1: return x * x;                           // acelerando: o corte
                case 2: return 1f - (1f - x) * (1f - x) * (1f - x); // freando: preparo e continuação
                default: return x * x * (3f - 2f * x);
            }
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        /// <summary>IK de duas juntas com o cotovelo para o lado do <paramref name="pole"/> (sempre o mesmo: não vira).</summary>
        static void TwoBone(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 pole)
        {
            Vector3 sh = upper.position, el = lower.position, wr = hand.position;
            float la = (el - sh).magnitude, lb = (wr - el).magnitude;
            Vector3 toT = target - sh;
            float d = Mathf.Clamp(toT.magnitude, Mathf.Abs(la - lb) + 0.001f, (la + lb) * 0.999f);
            Vector3 n = toT.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(pole, n);
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.ProjectOnPlane(el - sh, n);
            bend.Normalize();
            float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
            Vector3 newEl = sh + n * (la * cosA) + bend * (la * Mathf.Sqrt(1f - cosA * cosA));
            upper.rotation = Quaternion.FromToRotation(el - sh, newEl - sh) * upper.rotation;
            Vector3 el2 = lower.position, wr2 = hand.position;
            lower.rotation = Quaternion.FromToRotation(wr2 - el2, sh + n * d - el2) * lower.rotation;
        }
    }
}
