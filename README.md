# Pirate RPG (protótipo)

RPG de piratas single-player, 3D, câmera elevada. Unity 6.6 (6000.6.3f1) + URP.
Escopo e arquitetura: [ARCHITECTURE.md](ARCHITECTURE.md).

## Abrir o projeto

Esta pasta já é o projeto Unity (template Universal 3D, URP 17.6, Input System 1.20).

```bash
unity open "C:\Users\jhona\Projetos\PirateRPG"
```

Ou pelo Hub: **Add → Add project from disk** e selecione esta pasta.

O pacote `com.unity.pipeline` está instalado para a Unity CLI controlar o editor (só localmente).

## Protótipo 01: Navegação

Menu **Pirates → Protótipo → 01 - Criar cena de Navegação** → **Play**.

| Ação | Teclado | Gamepad |
|---|---|---|
| Subir / baixar velas (Ré, Recolhidas, Meia, Cheia) | W / S | RB / LB |
| Leme | A / D | Analógico esquerdo |
| Girar / inclinar câmera | Botão direito ou do meio + mouse (horizontal / vertical) | Analógico direito X / D-pad cima-baixo |
| Zoom | Scroll | Analógico direito Y |

**O que avaliar:** a sensação de peso e inércia, o raio de curva, a velocidade, a flutuação nas ondas,
a câmera, a colisão com ilhas e rochedos e a passagem pelo canal de boias.

## Protótipo 02: Combate naval (canhões)

Menu **Pirates → Protótipo → 02 - Criar cena de Combate Naval** → **Play**.

Mesmos controles de navegação, mais:

| Ação | Teclado | Gamepad |
|---|---|---|
| Canhões de bombordo (esquerda) | segurar **Q** mira, soltar dispara | LT |
| Canhões de estibordo (direita) | segurar **E** mira, soltar dispara | RT |

Quanto mais tempo segurando, mais longe: começa em 20 m e vai até 120 m em 2 s, devagar no início para dar controle fino
(o HUD mostra a distância; a linha laranja mostra o arco real). Durante a mira a câmera
se afasta para enquadrar navio e alvo. Alvos: balsas com bandeira e grupos de barris flutuantes.

**Dano e naufrágio (passo 2.2):** cada bala tira 12 de vida. Com dano, o casco embarca água e assenta
mais fundo; ao zerar, afunda de proa, de popa ou tombando. Alvos: **navio-alvo** escuro a 65 m por estibordo
(100 de vida), balsas (36) e barris (12). Barras de vida aparecem sobre o que levou dano. **F5** reinicia a cena.

## Protótipo 03: Duelo contra IA

Menu **Pirates → Protótipo → 03 - Criar cena de Duelo (IA)** → **Play**. Mesmos controles do Protótipo 02.

Um navio inimigo (casco e vela escuros) começa ao norte, a ~230 m. O rótulo sobre ele mostra o que a IA
está fazendo: **Patrulhando → Perseguindo** (te vê a 220 m) **→ Costado** (manobra para te deixar de través e
atira) **→ Fugindo** (abaixo de 25% de vida). Ele desvia de ilhas e rochedos. Seu casco tem 150; o dele, 100.
Se você afundar, **F5** recomeça.

## Protótipo 04: Loot e saque dos destroços

Menu **Pirates → Protótipo → 04 - Criar cena de Loot (duelo + carga)** → **Play**. É o duelo do Protótipo 03
com carga: quando o inimigo afunda, sobe à tona um **campo de destroços** (com facho de luz) carregando tudo o que ele levava.

**Saquear:** chegue a até ~15 m dos destroços, **recolha as velas** (abaixo de 3 nós) e aperte **F**. Na janela, clique
para mover os itens: **+1 / +10 / Tudo** leva para o porão; no porão, **-1 / -10 / Jogar** devolve aos destroços
(para abrir espaço). **Pegar tudo que couber** começa pelo que não ocupa espaço e segue do mais compacto ao mais volumoso.
**F** ou **Esc** fecham; acelerar ou se afastar também fecha. O jogo **não pausa**.

**Porão com limite:** 30 de espaço (barra no canto superior direito). Cada peça nos destroços some quando você leva o item.

| Item | Chance | Quantidade | Espaço por unidade |
|---|---|---|---|
| Ouro | 100% | 20–60 | 0,05 (20 moedas = 1) |
| Madeira | 90% | 5–15 | 1 |
| Pólvora | 70% | 2–6 | 2 |
| Ferro | 50% | 2–5 | 3 |
| Sobreviventes (tripulação) | 60% | 1–3 | 0 (vão no alojamento) |
| **Baú raro**: 1 esquema que você ainda não tem | 20% | 1 | 0 |

Esquemas: Estaleiro, Forja, Paiol de Pólvora (raros) e Fortaleza Costeira (épico, 3× mais difícil). O baú é aberto
ao começar o saque; se você já tiver todos os esquemas, ele dá 150 de ouro. Destroços não saqueados piscam e afundam
em 120 s (o tempo para com o saque aberto); vazios afundam logo depois de fechar.

**Onde ajustar (sem mexer em código):**
- `Assets/_Project/Data/LootTable_ChalupaInimiga`: chance e quantidade de cada item, chance do baú (para ver o baú
  sempre, ponha `Chest Chance` em 1), peso de cada esquema, prêmio reserva, tempo até afundar.
- `Assets/_Project/Data/Items/`: nome, cor, **espaço por unidade** e descrição de cada item e esquema.
- `PlayerShip → Inventory`: espaço do porão. `PlayerShip → WreckSalvager`: alcance e velocidade máxima para saquear.

## Protótipo 05: Porto-base, upgrades e construções (M3.1 a M3.3)

Menu **Pirates → Protótipo → 05 - Criar cena do Porto-base** → **Play**. É o Protótipo 04 (duelo + saque)
com o **Porto Alento** (até 16/10 "Porto da Base") na costa norte da **Ilha Sul**, ~160 m ao sul do início: píer com bandeira pirata, armazém e casas.

**Atracar:** chegue perto da ponta do píer (~20 m), **recolha as velas** (abaixo de 3 nós) e aperte **F**.
A mesma tecla F serve para saquear destroços; com uma janela aberta, F fecha.

Na janela do porto (com o mouse):
- **Porão → armazém** (`1 →`, `10 →`, `Tudo →`) e **Descarregar tudo**. O armazém da base não tem limite de espaço.
  Os sobreviventes ficam a bordo (são sua tripulação).
- **Armazém → porão** (`← 1`, `← 10`, `← Tudo`), até onde couber. Esquemas ficam guardados na base (para o M3.3).
- **Conserto do casco:** 1 madeira + 5 ouro a cada 10 de casco, pagando primeiro do armazém e depois do porão.
  **Consertar 30** ou **Consertar tudo**; sem material suficiente, conserta o quanto der. Casco consertado volta a flutuar mais alto.
- **Desatracar** com F, Esc, o botão, ou acelerando / se afastando.

**Zona segura:** num raio de ~60 m do cais (boias verdes), inimigos não perseguem nem atiram, e saem se entrarem.
O topo da tela avisa quando você está nela.

**Upgrades do navio (M3.2):** aba **Upgrades do navio** na janela do porto. 3 níveis cada; o custo sai do armazém + porão
na hora da encomenda. **Cada nível leva um tempo de obra** (20 s / 40 s / 75 s; a mira 15 / 35 / 60 s), um por vez, e
**só avança com o navio dentro da zona segura** (fora dela pausa). O topo da tela mostra o andamento; **Cancelar**
devolve o custo. A velocidade dos trabalhadores (`HarborDock → Build Speed`, hoje 1×) será melhorada pelas construções do M3.3.

| Upgrade | Efeito (nível 1 / 2 / 3) | Custo do nível 1 |
|---|---|---|
| Luneta e mira assistida | mira assistida 40% / 70% / 100%; dispersão −20% / −35% / −50% | 3 ferro, 2 pólvora, 40 ouro |
| Casco reforçado | casco máx. 190 / 240 / 300 (era 150) | 10 madeira, 3 ferro, 60 ouro |
| Canhões reforçados | dano por bala 15 / 18 / 22 (era 12) | 4 ferro, 2 pólvora, 50 ouro |
| Velame novo | velocidade máx. +8% / +16% / +25% | 10 madeira, 40 ouro |
| Artilheiros treinados | recarga −12% / −24% / −35% (era 4 s) | 3 pólvora, 60 ouro + **1 / 3 / 5 sobreviventes a bordo** (não são gastos) |

Os níveis seguintes custam cerca de 2× e 3× o primeiro.

**Mira assistida:** segurando Q/E com um inimigo daquele lado (até ~30° do través), a distância **gruda nele** quando a
sua carga passa perto, a carga **desacelera** ali e os canhões **giram** até 12° para ele (5° / 8° / 12° por nível).
O HUD mostra "(travado no inimigo)": soltar nessa hora acerta. No nível 3 a mira também antecipa o movimento do alvo. **Inimigos reaparecem:** ~25 s depois que um navio some no fundo,
outro igual surge no ponto mais longe de você (norte, leste ou oeste), para dar para juntar recursos.

**Construções da base (M3.3):** aba **Construções da base** na janela do porto. Fila própria (uma obra por vez,
separada do estaleiro do navio) que **anda mesmo com o navio longe**. Cada construção aparece na ilha quando fica pronta.

| Construção | Esquema | Efeito (nível 1 / 2 / 3) | Custo do nível 1 |
|---|---|---|---|
| Alojamento dos trabalhadores | — | **trabalhadores 1,25× / 1,5× / 2×** (todas as obras) | 20 madeira, 60 ouro |
| Estaleiro | Estaleiro | **libera o nível 3** de todos os upgrades do navio | 40 madeira, 10 ferro, 150 ouro |
| Forja | Forja | produz 1 ferro a cada 30 / 20 / 12 s no armazém | 25 madeira, 5 ferro, 80 ouro |
| Paiol de Pólvora | Paiol | produz 1 pólvora a cada 30 / 20 / 12 s no armazém | 30 madeira, 4 ferro, 80 ouro |
| Fortaleza Costeira | Fortaleza (épico) | 2 canhões no alto do morro atiram em inimigos a até 170 m; zona segura +25 m (boias novas) | 60 madeira, 30 ferro, 20 pólvora, 300 ouro |

O esquema precisa estar no armazém ou no porão (vem nos baús raros; no sandbox, **Ganhar** ou **Encher tudo**).

O armazém, os upgrades e as construções ainda não são salvos: F5 ou sair do Play zera tudo (salvar entra no M6).

**Onde ajustar:** `Assets/_Project/Data/HarborSettings_Base` (custo do conserto); `Assets/_Project/Data/Upgrades/`
(efeito, custo, tempo, tripulação e construção exigida de cada nível); `Assets/_Project/Data/Buildings/` (efeito, custo,
tempo e esquema de cada construção); `CannonStats_Fortaleza` (canhões da Fortaleza); `PortoBase → Cais → HarborDock` (nome e raio de atracação);
`PlayerShip → HarborVisitor` (velocidade máxima para atracar); `EnemyRespawner` (tempo e pontos de reaparição).

## Protótipo 06: Desembarque a pé (M4.1, M4.3, M4.4)

Menu **Pirates → Protótipo → 06 - Criar cena de Desembarque (a pé)** → **Play**. Só terra: uma ilha grande (morros,
árvores, pedras) logo ao sul do navio, com um acampamento pirata no meio.

**Desembarcar:** navegue até a **Praia do Norte** (bote e bandeira vermelha na areia), **recolha as velas** e aperte **F**.
O navio lança âncora e a câmera passa para o personagem.

| Ação a pé | Teclado | Gamepad |
|---|---|---|
| Andar | WASD / setas (relativo à câmera) | Analógico esquerdo |
| Correr | Shift | Apertar o analógico esquerdo |
| Pular | Espaço | A / Cruz |
| Girar / inclinar câmera, zoom | **Q / E** (girar) ou **botão do meio** (clique do scroll) + mouse, scroll; o botão direito fica para a mira (no primeiro desembarque aparece a dica na tela) | Analógico direito |
| Atacar (na direção do cursor) | Botão esquerdo | RT (para a frente) |
| Mirar a pistola (segurar) | Botão direito | LT |
| Cutelo / pistola / bacamarte (depois de comprado) | 1 / 2 / 3 | Y (troca) |
| Voltar ao navio | **F** perto do bote | X / Quadrado |

**Combate a pé (M4.3):**
- **Cutelo** (tecla 1): combo de 2 golpes (clique de novo durante o golpe e o 2º sai em seguida), 25 de dano em arco à frente
  (2,2 m). Parado/andando fica em pose de pronto; correndo, corre com a espada na mão.
- **Pistola de pederneira** (tecla 2): 1 tiro (45 de dano, até 35 m, mira no corpo do alvo mais perto do cursor), recarga de
  2,5 s girando a pistola, pose de mira logo depois do tiro. Dá para trocar para o cutelo durante a recarga.
- **Mirar** (segurar o botão direito, com a pistola): ergue a pistola e o braço/tronco seguem o cursor suavemente. Uma
  **linha branca** vai da pistola até onde a bala vai, com um **anel** ali; **travou num alvo** (pirata, boneco, caixote:
  o tiro vai no corpo dele), linha e anel ficam **vermelhos** e o anel contorna o alvo. O esquerdo atira **na hora** e com
  30% da dispersão, com coice. Mirando, anda devagar e não corre:
  as pernas seguem para onde ele anda (de ré, se for para trás) e o tronco gira para a mira. Sem mirar, o esquerdo ainda
  atira, mas primeiro ergue a pistola (~0,3 s).
- **Trocar de arma** guarda o cutelo na bainha (cintura esquerda) e saca de novo com animação; a pistola fica no cinto.
- Quatro **bonecos de treino** na praia (100 de vida) balançam ao levar dano, caem e se levantam em ~4 s.

**Acampamento pirata (M4.4):** ~50 m ao sul da praia, uma paliçada com o portão virado para você, fogueira, barracas e
bandeira preta. **Quatro piratas de cutelo** (vermelhos, 60 de vida) guardam: dois no portão, dois lá dentro; e dois atiradores (abaixo).
- Eles te **veem** até ~16 m à frente (mais perto que 4,5 m, até de costas; e na hora em que apanham), um "**!**" aparece
  sobre a cabeça e o acampamento inteiro **acorda**. Correm atrás de você pelo **NavMesh** da ilha (contornam a paliçada,
  árvores e pedras), um pouco mais devagar que você correndo.
- **Dois atacam de cada vez**, golpes espaçados e um pouco mais lentos que os seus (12 de dano, às vezes em combo); os
  outros esperam em volta, encarando. Acertar um pirata **interrompe** o golpe dele.
- Fugindo para longe do acampamento (~40 m; nos postos de guarda, ~24 m) ou ficando 6 s fora da vista deles, desistem e voltam aos postos.
- **Dois atiradores de pistola** (azul-marinho, M4.5): um logo atrás do portão, outro nos fundos. Mantêm ~10 m de você,
  **seguram a mira ~1 s com uma linha vermelha** (o aviso: saia da frente!) e atiram (18 de dano). A mira deles segue você
  com atraso: **correndo de lado dá para desviar**; parado, você leva o tiro. Recarregando (~3 s, girando a pistola)
  recuam e ficam vulneráveis; se você chega perto, recuam mirando. Não acertam outros piratas.
- Você tem **100 de vida** (barra no HUD; a tela pisca vermelho ao levar golpe). **Derrotado**, cai, e em 3,5 s acorda na
  **praia de desembarque** com a vida cheia (sem perder nada, por enquanto).
- Animações de **levar golpe e morrer** (Mixamo, sorteadas sem repetir a anterior; valem para você e para os piratas):
  golpe = Head Hit, Hit To Body, Rib Hit, Stomach Hit; morte = Dying Backwards, Dying (começa já perto da queda) e Shoulder
  Hit And Fall. Parado, usam o corpo todo; andando, só o tronco. "Hit On Side Of Head" ficou de fora (o corpo anda 6 m
  no clipe). A lista, o início e a velocidade de cada clipe ficam no topo de `PrototypeSceneBuilder.FootCombat.cs`;
  faltando clipe, tranco e queda por código. Seu golpe não é cortado pela reação (o dos piratas é).

As animações vêm do **Mixamo** (`Assets/_Project/Animation/Mixamo`, configuradas sozinhas: Humanoid, no lugar, loop só nos
ciclos) e ficam na camada "Tronco" do Animator, com uma cópia sincronizada de **corpo inteiro** ("CorpoInteiro"): parado, os
golpes, o saque e a pistola usam o corpo todo do clipe (quadril e pernas na postura de luta); andando, só o tronco (as pernas
andam). O golpe quase para o personagem para sair com o corpo inteiro. A mira da pistola é procedural (`CharacterAimRig`, no
modelo): depois da animação, gira coluna e braço até o cano apontar para o alvo. Usadas: Stable Sword Outward/Inward
Slash, Withdrawing/Sheathing Sword, Run With Sword, Shooting, Gunplay, Pistol Idle, Pistol Aim. Guardadas para depois: Shooting
Gun e Sword Fight One (longas: viram combo especial). Ajuste das armas em `Assets/_Project/Data/Weapons/`; velocidade e início
de cada clipe no topo de `PrototypeSceneBuilder.FootCombat.cs`.

Dá para entrar no mar até a cintura; mais fundo, ele para. Se cair no mar fundo, volta para a praia.
O personagem é o **Capitão Barbarossa** do Quaternius Pirate Kit (M6.5, abaixo) com as animações do **Starter Assets –
Character Controllers | URP** (Unity, grátis, em `Assets/Starter Assets`): parado, andar, correr, pular, cair e aterrissar. O Animator é o nosso
`Assets/_Project/Animation/PlayerCharacter.controller` (cópia do do pacote com o blend tree nas nossas velocidades); sem o
pacote no projeto, o gerador volta para uma cápsula provisória. Os scripts de controle do pacote não são usados.

**Onde ajustar:** `FootCharacter` (velocidades, pulo, gravidade, profundidade máxima na água, giro do tronco ao mirar),
`Model → CharacterAimRig` (rapidez da mira, limites, coice), `Model → CharacterAnimatorDriver` (até que velocidade usa o
corpo inteiro), `Data/Weapons` (mira: velocidade andando, dispersão, coice), `PlayerModes → PlayerModeSwitcher`
(zoom da câmera a pé e no navio, arrasto da âncora), `Praia_Desembarque → LandingPoint` (alcance do navio e do bote).
**Saque em terra (M4.2):**
- **Mochila** (15 de espaço, metade do porão): tudo o que você pega a pé vai para ela (painel no canto direito). Ao **voltar
  ao navio**, o conteúdo passa para o **porão** no que couber; o resto fica na mochila. Morrendo, não perde nada (por enquanto).
- **Caixotes e barris** no acampamento e espalhados pela ilha (perto da praia, no caminho, atrás dos morros): um golpe de
  cutelo ou um tiro quebra, eles voam em lascas e deixam um **saco de saque** no chão (madeira, ouro, ferro; barril dá
  pólvora). Passe por cima para pegar o que couber; mochila cheia, o saco fica ali.
- **Baú do tesouro** nos fundos do acampamento: com **cadeado** enquanto houver pirata vivo. Derrotou todos, o cadeado some,
  acende um facho dourado e **F** abre a janela de escolha (baú ↔ mochila, como nos destroços): 60–120 de ouro, ferro,
  pólvora, madeira e, metade das vezes, um **esquema de construção** que você ainda não tem. "Deixar" devolve ao baú.
- O F a pé abre o baú ou, perto do bote, volta ao navio.

Piratas: `Pirata_*`/`Atirador_* → EnemyFootAI` (visão, alcance, ritmo de ataque, combo; atirador: distância, tempo
mirando, atraso da mira), `Data/Weapons/Weapon_Pistola_Pirata` (dano, dispersão, recarga dos atiradores), `Acampamento_Pirata → EnemyCamp` (quantos
atacam de uma vez, espaço entre golpes, distância em que desistem), `Data/Weapons/Weapon_Cutelo_Pirata` (dano e velocidade
do golpe deles), `Health` de cada um (vida), `PlayerModes → PlayerFootRespawn` (tempo caído). Saque: `Data/LootTable_BauAcampamento`,
`LootTable_Caixote`, `LootTable_Barril` (o que cai e a chance), `PlayerCharacter → Inventory` (espaço da mochila),
`PlayerCharacter → LandLooter` (alcance do baú e do saco).

## Protótipo 07: Mundo com missões (M5)

Menu **Pirates → Protótipo → 07 - Criar cena do Mundo (missões)** → **Play**. Tudo junto: o mar e o **porto-base** da
cena 05 (upgrades, construções, navio inimigo que reaparece), a **Ilha da Caveira** com o acampamento pirata da cena 06
(a leste, ~300 m de costa a costa do porto) e um **tesouro enterrado** na Ilha Oeste.

**Mapa (espaçado em 16/10, ~1,6×):** o porto fica onde estava; a **Ilha Oeste** a ~43 s de chalupa de vela cheia, a **Ilha Norte**
a ~50 s, a **Caveira** a ~57 s (praia sul) ou ~62 s (praia norte, contornando a ilha). As chalupas piratas patrulham os caminhos
entre as ilhas; o brigue, o Tormenta, a lula e o megalodonte ficam bem longe (~1 a 1,5 min de viagem). Sobram 4 lugares vazios para
ilhas novas.

**Missões:** atraque no porto e entre na **taverna** (M6.6, abaixo): contratos dos NPCs com o pagamento. Aceite até **3** de cada
vez; o progresso aparece no alto da tela, e um **★ com a distância** aponta o destino (uma seta na borda quando está fora
da vista). Cumpriu o objetivo, a estrela passa a apontar o porto: volte à taverna e **Entregar**. O pagamento vai para o
**armazém**.

| Contrato | Quem dá | O que fazer | Paga |
|---|---|---|---|
| Caça no mar | Capitã Rosa Negra | Afundar 2 chalupas inimigas | 150 ouro |
| O acampamento da Caveira | Velho Bento | Derrotar os 8 piratas do acampamento da Ilha da Caveira (os postos de guarda não contam; se já estava limpo, eles voltam) | 200 ouro, 6 pólvora |
| Madeira para o estaleiro | Mestra Joana | Levar 12 de madeira e 4 de ferro (no porão ou já no armazém; saem ao entregar) | 140 ouro |
| O mapa do Zé Caolho | Zé Caolho | Desembarcar no lado leste da Ilha Oeste e **cavar o X** (F) na gruta de pedras no lado sul do morro, depois da pedra grande | 60 ouro + o baú (120–200 ouro, ferro, chance de esquema) |
| Os contrabandistas da Ilha Oeste | Dona Mercês | Derrotar os 4 contrabandistas do esconderijo na enseada norte da Ilha Oeste (o baú do contrabando abre) | 160 ouro, 6 madeira |
| O mapa do contrabandista | Rato Velho | Cavar o X na praia noroeste da Ilha Oeste, junto do bote virado | 50 ouro + o baú (100–180 ouro, pólvora, ferro, chance de esquema) |
| Calar o forte da Ilha Norte | Almirante Teodoro | Derrubar as muralhas do forte a tiros de canhão até os canhões dele calarem | 280 ouro, 8 ferro |
| Pólvora para a fortaleza | Sargento Duarte | Levar 10 de pólvora | 90 ouro, 5 ferro |
| O brigue Santa Clara | Contramestre Ferraz | Afundar o brigue que patrulha ao norte da Ilha Norte (o ★ segue o navio) | 300 ouro, 8 ferro |

Todas voltam ao quadro depois de entregues (dá para repetir). Com você em terra, o **navio ancorado fica numa zona
segura**: os navios inimigos não o atacam. Ajuste dos contratos em `Assets/_Project/Data/Missions/`.

**Brigue Santa Clara (M2.5):** um navio grande de velas brancas, sozinho, patrulhando as águas ao norte da Ilha Norte
(~880 m do cais). Bem mais forte que a chalupa: **casco 400**, **6 canhões por bordo** (14 de dano, recarga 4,5 s) e
tripulação veterana (erra pouco o alcance, dispara logo que alinha, luta até o fim). Mais lento (9 contra os 11 do seu
navio): dá para fugir dele. Parado no costado dele, seu navio aguenta umas 4 bordadas; manobre e não fique de lado
para ele. Paga bem mais saque (80–160 ouro, 15–30 madeira, pólvora, ferro, sobreviventes)
e tem **50% de chance de baú com esquema**. Afundado, volta depois de **150 s**, ou **na hora** ao aceitar a missão dele.
Afundá-lo também conta para "Caça no mar". Ajustes em `Data/ShipStats_Brigue`, `CannonStats_Brigue` e `LootTable_Brigue`.

**Jogo salvo (M6.1):** ao dar Play aparece o **menu inicial** (o jogo fica parado atrás dele): **Continuar**, com o resumo
do save (quando foi salvo, o que há no armazém, upgrades, construções, missões), ou **Novo jogo** (se há save, pede
confirmação; o save vai para `save.json.bak`, dá para voltar renomeando). O jogo **salva sozinho ao atracar no porto, ao
sair de cada casa do porto e ao zarpar** (e ao fechar o jogo atracado); aparece "Jogo salvo" no canto de baixo. Guarda **só o progresso**:
porão, mochila, casco, upgrades, armazém, construções e obras em andamento, missões aceitas (com o progresso) e entregues.
As ilhas não são salvas: acampamento, baús e caixotes voltam cheios. Continuar põe o navio parado na vaga do porto. O
arquivo é um só, `save.json` em `%USERPROFILE%/AppData/LocalLow/Jhonata/Pirate RPG/` (o caminho aparece no sandbox). F5
recarrega a cena sem salvar e volta ao menu.

**Economia (E1):** missão repetível entregue some do quadro por **10 min de jogo** ("volta ao quadro em X min"; o tempo fica
salvo). O baú raro não traz esquema que você já tem no porão, na mochila ou no armazém (vem o ouro de reserva). O baú do acampamento
**se enche de novo** quando você aceita a missão outra vez (os piratas voltam e ele tranca até limpar de novo).
**Onde ajustar:** "Cooldown Seconds" em cada missão de `Data/Missions` (0 = sem descanso).

## Visual low-poly (M6.2): navios e objetos

Todas as cenas usam modelos do **[Kenney Pirate Kit](https://kenney.nl/assets/pirate-kit)** (CC0, crédito ao Kenney é
bem-vindo mas não obrigatório) em `Assets/ThirdParty/Kenney_PirateKit/`. Os modelos são **só visual**: colisores,
flutuação e física continuam os dos protótipos, então trocar um modelo não muda o jogo.

- **Seu navio:** pirata, velas pretas com caveira. **Inimigos:** velas brancas. As **velas sobem e descem** com o nível de
  vela (recolhidas ficam enroladas na verga).
- **Canhões** do kit no convés (4 por bordo), **barris-alvo** boiando, **destroços** (caixotes de ouro e de balas, barril de
  pólvora, baú), **baú do acampamento** (a tampa do modelo abre), **caixotes e barris quebráveis** e o **bote** da praia.
- A barra de vida dos navios fica acima do mastro.
- Ainda são primitivas: personagens e as balsas-alvo (o porto e as construções ganharam modelos no M6.4).

## Ilhas geradas (M6.3)

As ilhas de todas as cenas são **malhas low-poly geradas pelo código** (`PrototypeSceneBuilder.Terrain.cs`), com as
cores da mesma paleta dos modelos do Kenney: litoral irregular, praia de areia que entra na água, grama em dois tons,
morros com paredões de pedra, palmeiras, pedras (arenito na praia, cinza no miolo) e tufos de grama. A mesma malha é o
colisor (o navio encalha na beira, o personagem anda, o NavMesh dos piratas é assado em cima).

- **Ilha da Caveira** (cenas 06 e 07): praia de desembarque fixa ao norte; desde o M6.7, 2× maior com acampamento grande,
  postos de guarda, ruínas, naufrágio e mata (abaixo).
- **Ilha Sul (porto):** virou a vila do M6.4 (abaixo).
- **Ilha Oeste:** praia de desembarque a leste, X do tesouro ao sul e a **pedra grande** no caminho.
- **Ilha Norte** e os **rochedos** em volta do começo (agora pedras do kit).

Formato de cada ilha (raios, morros, áreas planas, quantas palmeiras) nos `IslandSpec` de `CreateIslands` e
`CreateBigIsland`. As malhas ficam em `Assets/_Project/Meshes/` (uma por ilha, compartilhada entre as cenas).

Pacotes baixados ficam em `_Downloads/` (fora de `Assets/`, o Unity não importa sozinho); só os modelos revisados e
usados são copiados para `Assets/ThirdParty/`, configurados por `ThirdPartyImportSettings` (sem materiais próprios: um
material URP, `M_Kenney`, com a paleta `colormap.png`).

## Porto vira vila (M6.4)

A **Ilha Sul** ficou **2× maior** (~135 × 115 m; o centro foi 28 m para o sul, então o litoral norte, o píer, o cais e a
vaga do navio continuam no mesmo lugar) e virou uma **vila pirata** (`PrototypeSceneBuilder.Port.cs`):

- **Frente do porto:** píer com tábuas do kit, lanternas, caixotes e a bandeira; **armazém** (portão largo, pátio com carga
  e carroça) e **taverna** de dois andares (estandarte, lanternas, bancos, barris); **cais lateral** a oeste com dois botes
  amarrados; **torre de vigia**; dois **navios ancorados** (um pirata, um mercante) balançando na água (com colisor).
- **Praça do mercado:** chafariz, quatro barracas com peixe, caixas e baldes, lanternas, árvores.
- **Ruas de terra** com **9 casas** (mais uma na roça) montadas peça por peça (Fantasy Town Kit: térreo de pedra ou madeira, 1 ou 2 andares,
  telhado verde ou vermelho alto, chaminés), canteiros de flores, lenha e barris.
- **Morro da Fortaleza** no meio, com trilha desde a rua principal; **roça** cercada ao sul (milho, abóbora, trigo) com casa.
- **Construções da base** com modelos e lugares novos (aparecem quando ficam prontas, como antes): Alojamento (3 casas no
  bairro oeste), Estaleiro (galpão na costa leste, carreira que entra no mar com um bote em obra, guindaste, tábuas e toras),
  Forja (oficina com bigorna e brasa acesa; nível 2 chaminé alta com fumaça; nível 3 fundição), Paiol (casa de pedra com
  barris; nível 2 depósito; nível 3 **moinho** com pás girando), **Fortaleza** (torre de pedra do kit com os dois canhões no
  alto, muralha em anel com portão para a trilha).
- **Mata** nos morros e no sul (árvores, palmeiras, arbustos, flores, capim, troncos e pedras do Nature Kit), palmeiras
  na praia.
- **Vida:** 18 **moradores** parados (personagens do Quaternius desde o M6.5), conversando em pares na praça e na taverna, nas barracas, no píer, no estaleiro, na forja, na roça e no portão
  da Fortaleza; **gaivotas** voando sobre o porto e em volta da ilha; **caranguejos** andando de lado na areia;
  **fumaça** low-poly nas chaminés; **fogueira** na praia e brasa na forja com luz que treme.
- **Desembarcar no porto (cena 07):** desde o M6.6 é o próprio atracar: F no cais e o capitão desce no píer (abaixo). O
  cais lateral ficou só como cenário.

Nas cenas 01 a 04 a ilha aparece só com a mata e as ruas (a vila é das cenas do porto, 05 e 07).

**Pacotes novos** (Kenney, CC0, em `Assets/ThirdParty/`): **Fantasy Town Kit** (casas modulares, feira, chafariz,
lanternas, moinho), **Survival Kit** (barris, caixas, ferramentas, peixes, fogueira) e **Nature Kit** (árvores, arbustos,
flores, plantações, troncos, pedras, cercas). O kit pirata ganhou peças de píer, torre e muralha. O Nature Kit pinta com
uma cor por material: entra com materiais URP da cor do FBX, e o gerador troca por cópias compartilhadas (`M_N_*`).

**Onde ajustar:** layout, ruas, morros e áreas sem mata em `PortIslandSpec` / `PortWild`; quantidade de mata em
`PlantLush(...)` (`CreateIslands`); casas, praça, moradores, gaivotas e caranguejos em `AddVillage` e vizinhos; cada casa
é um `HouseSpec` (largura, fundo, andares, madeira/pedra, telhado alto, porta, estandarte); construções da base em
`AddBuildings` (`PrototypeSceneBuilder.Buildings.cs`).

## Personagens (M6.5)

Personagens do **[Quaternius Pirate Kit](https://quaternius.com/packs/piratekit.html)** (CC0) em
`Assets/ThirdParty/Quaternius_PirateKit/` (só os FBX dos personagens e das armas e a textura `Atlas_Pirate.png`; os
`.blend` ficaram de fora):

- **Você é o Capitão Barbarossa** (barba ruiva, chapéu, perna de pau, gancho e o papagaio). Controle e combate iguais:
  combo do cutelo, pistola com mira, sacar e guardar, levar golpe e morrer, com as mesmas animações do Starter Assets e do
  Mixamo.
- **Vilões do acampamento:** homens-tubarão (Mako e Sharky) de cutelo e esqueletos (um sem cabeça) de pistola.
- **Armas** do kit: cutelo e pistola de verdade no lugar das primitivas (jogador e piratas).
- **Moradores da vila:** Anne e Henry em 8 visuais (o original e 3 versões de cada com outras cores de roupa, bandana,
  cabelo e pele). 18 parados, que de vez em quando acenam, dizem "sim"/"não" e olham em volta, e **10 andando pela vila**
  (NavMesh do porto) entre praça, taverna, armazém, píer, cais, ruas, estaleiro, forja e roça, parando um pouco em cada lugar.
  Usam as animações do próprio kit.

**Como funciona o esqueleto:** o do kit não é humanoide (os pés são soltos, controlados por IK, e não há osso de mão).
O gerador adapta a cópia de cada personagem: prende o pé na canela, cria a mão no pulso, monta a pose T (pernas esticadas e
o quadril erguido até o pé tocar o chão) e gera o avatar humanoide (`Assets/_Project/Characters/<personagem>_Humano.asset`).
A empunhadura, a bainha e a mira são medidas no corpo de cada personagem.

**Onde ajustar:** quem é quem em `PlayerKitModel` (`PrototypeSceneBuilder.Characters.cs`) e nos `posts` de
`CreatePirates` (`PrototypeSceneBuilder.LandEnemies.cs`); cores dos civis em `CivilianPalette`; tamanho e posição das armas
do kit nas chamadas `KitWeapon(...)` (`PrototypeSceneBuilder.FootCombat.cs`); paradas dos passantes em `TownStops` e a
velocidade deles no `NavMeshAgent` de cada `Passante_*`.

## Porto a pé (M6.6)

Na cena 07 o porto não abre mais um menu no navio: você desce e vai às casas da vila.

- **Atracar:** perto do cais, devagar, **F = atracar e desembarcar**. O **porão descarrega sozinho no armazém** (a
  tripulação fica a bordo) e o capitão desce na ponta do píer, com a câmera virada para a vila.
- **Quatro casas de serviço**, todas com a porta para o porto, placa, estandarte e um atendente na porta (o nome e o que
  ela faz aparecem em cima quando você está perto). Na porta, **F = entrar**; a janela tem só a parte daquela casa:

  | Casa | Onde | O que faz |
  |---|---|---|
  | **Armazém** | frente do porto, a leste do píer | carga: porão ↔ armazém (pegar coisas de volta para o navio, descarregar tudo) |
  | **Estaleiro** | casa de madeira de estandarte verde, a leste, perto da praia (bote em conserto, tábuas, bancada) | conserto do casco e upgrades do navio |
  | **Mestre de obras** | casa de pedra de estandarte vermelho, a oeste da taverna (pedras, lenha, bigorna) | construções da base |
  | **Taverna** | frente do porto, a oeste do píer | quadro de missões: aceitar, entregar e receber |

- Com a janela aberta o jogo não pausa; os cliques são da janela (não ataca nem mira) e a câmera vira para a fachada.
  **F, Esc, "Sair" ou se afastar** fecha. Sair de uma casa **salva o jogo**.
- **Entregas** das missões contam o porão **e o armazém** (como o porão descarrega ao chegar, a carga já estará lá).
- **Voltar ao mar:** volte à ponta do píer e **F = voltar ao navio**. Ainda atracado, F no navio desce de novo (sem
  descarregar outra vez). Solte as velas e saia: ao se afastar, o navio desatraca e o jogo salva.
- **Ao desembarcar em qualquer praia**, a câmera agora vira para o lado em que o personagem olha (para a terra), não fica
  na direção do navio.
- A cena 05 continua com o menu do porto no navio (abas), para testes rápidos.

**Onde ajustar:** casas, nomes, placas e atendentes em `AddVillage` / `ServiceDoor` (`PrototypeSceneBuilder.Port.cs`); a
distância da porta e a altura do nome no `HarborBuilding` de cada `Entrada_*`; o ponto do píer em `CreatePierLanding`.

## Ilha da Caveira 2× e câmera que não esconde o capitão (M6.7)

A **Ilha da Caveira** (cena 06 e a leste no mundo, cena 07) ficou **2× maior**, uns 260 × 210 m
(`PrototypeSceneBuilder.Skull.cs`). A praia norte de desembarque continua no mesmo lugar; a ilha cresceu para os lados e
para o sul.

- **Acampamento grande** no meio (~100 m da praia norte): paliçada de estacas do Survival Kit com o portão grande ao norte
  e um menor ao sul, fogueira, 4 cabanas de palha, 2 barracas, torre de vigia, bandeira, colchonetes, caixotes e barris, e
  o baú nos fundos. **8 piratas**: 5 tubarões de cutelo (dois no portão norte, fogueira, cabanas, portão sul) e 3
  esqueletos de pistola. É o da missão e o que tranca o baú.
- **Três postos de guarda** pela ilha, cada um com caixotes e barris para quebrar (não contam para a missão):
  - **Posto do naufrágio** (leste): 2 tubarões e 1 esqueleto atirador, fogueira e barraca; logo depois, o **navio
    naufragado** encalhado e adernado na praia leste, com caixotes e barris na areia.
  - **Ruínas da Caveira**, no alto do morro sudoeste (~11 m): anel de colunas (algumas quebradas, uma caída), a cabeça de
    pedra, obelisco, arco, pedaços de muralha e a base de uma torre. 2 tubarões e o esqueleto sem cabeça.
  - **Posto da trilha sul**: 2 tubarões, fogueira e barraca, no caminho entre o portão sul e a praia sul.
- **Trilhas de terra** em cruz: praia norte → portão norte; portão leste → posto → naufrágio; portão oeste → ruínas;
  portão sul → posto → **praia sul**.
- **Segunda praia de desembarque**, ao sul, com bote e bandeirinha: dá para chegar por trás do acampamento. Morrendo, você
  acorda na praia em que desceu.
- **Mata fechada** do Nature Kit no resto (árvores, arbustos, flores, capim, troncos, pedras grandes), palmeiras nas praias,
  6 morros (pedra nas encostas íngremes).

**Câmera:** casas, árvores, palmeiras, pedras grandes, torres, cabanas, barracas, ruínas, o naufrágio, os navios ancorados
e a fumaça ficam **translúcidos** quando estão entre a câmera e o capitão (ou o navio), e voltam ao normal quando saem da
frente. A sombra continua no chão. Vale em todas as cenas.

**Onde ajustar:** formato da ilha, morros e trilhas em `SkullIslandSpec` e nas `Trail*`; postos e guardas em `Outposts`;
acampamento em `CreateCampSite` e os piratas em `CreatePirates` (`PrototypeSceneBuilder.LandEnemies.cs`); caixotes e barris
em `CampCrates`, `IslandBreakables` e `WreckBreakables` (`PrototypeSceneBuilder.LandLoot.cs`); quantidade de mata em
`PlantSkullJungle`. A transparência: `CameraOcclusionFader` na câmera (opacidade, velocidade, folga) e `Occluder(...)` no
gerador para marcar algo novo (`PrototypeSceneBuilder.Occlusion.cs`).

## Ilhas Oeste e Norte (M6.9)

As duas ficaram **2× maiores** (Oeste ~80 m, Norte ~105 m), com mata do Nature Kit em todas as cenas; o que se joga nelas
é do mundo (cena 07):

- **Ilha Oeste — esconderijo de contrabandistas.** Desembarque a leste. Na enseada norte: palafitas, cabanas, cais com botes
  e a chalupa deles ancorada, pilhas de contrabando, fogueira e **4 contrabandistas** (3 de cutelo, 1 de pistola). O **baú do
  contrabando** abre depois de derrotar o bando. No lado sul do morro, uma **gruta de pedras** guarda o tesouro do Zé Caolho;
  na praia noroeste, junto de um bote virado, o **segundo tesouro** (missão nova).
- **Ilha Norte — forte inimigo.** Na costa sul, de frente para o mar: muralha de pedra em anel, torre, **4 canhões** no alto da
  muralha que **atiram no seu navio** quando você chega a ~170 m (não atiram no navio ancorado com você em terra). Para
  **calar o forte**, acerte as **muralhas** a tiros de canhão (150 de vida; só bala de navio conta). Dica: mire um pouco
  **além** do forte; mirando nele mesmo, a bala bate na praia antes. Calado: a bandeira cai, sobe fumaça e fogo e os canhões
  param. Desembarque a leste e enfrente a **guarnição** (5 piratas); o **paiol** (baú com muito ouro, pólvora e ferro) abre
  depois de derrotá-los. No teste, parado de lado para o forte, calei ele em ~40 s e perdi 80 de casco.
- No painel a pé aparecem só os bandos a menos de 180 m.

**Onde ajustar:** layout das ilhas em `WestIslandSpec` / `NorthIslandSpec` (`PrototypeSceneBuilder.Isles.cs`); vida do forte
em `FortHealth`; canhões dele em `Data/CannonStats_ForteInimigo` (dano 8, recarga 8 s, alcance); saque em
`LootTable_Contrabando`, `LootTable_PaiolForte` e `LootTable_TesouroContrabandista`. No **F1**: calar / restaurar o forte e
pôr o navio na frente dele.

## HUD de pirata (M6.10) — todas as cenas

O HUD de jogo saiu do IMGUI (caixas cinza de tamanho fixo) para o **UI Toolkit**: escala com a tela (referência 1600×900:
em Full HD fica 1,2×; numa janela pequena encolhe junto) e a letra é nítida em qualquer tamanho. Visual com o **Kenney UI
Pack** (madeira escura, pergaminho, faixa vermelha) e as fontes **Pirata One** (títulos, nomes no mundo, anúncios) e
**Alegreya** (texto) — créditos em `Assets/ThirdParty/UI/CREDITOS.txt`.

- **Alto à esquerda:** o navio (casco, velas em bolinhas, leme, velocidade, cada bateria: pronta / mirando / recarregando com
  barra) ou, a pé, o lugar, a vida, as armas e os bandos por perto. **H** mostra/esconde a ajuda dos controles (antes ficava
  sempre na tela). A linha "Velocidade" a pé saiu (era só de debug).
- **Direita:** porão ou mochila (cor do item, quantidade, espaço) e, embaixo, o cartão das **missões**.
- **Alto ao meio:** zona segura do porto e obras em andamento (só a do porto: o navio ancorado não mostra mais "Zona segura").
- **Embaixo:** avisos de ação com a tecla ("F Desembarcar", "F Abrir baú"...); no meio, faixa vermelha para navio afundado e
  derrota; anúncios de missão e obra pronta no alto, ganhos ("+3 Madeira") sobem no meio da tela.
- **No mundo:** nomes de desembarques, porto, casas, baús e destroços; o destino das missões com estrela e distância (fora da
  tela, preso na borda com uma seta apontando); "!" sobre o pirata que te viu; barras de vida sobre quem levou dano.
- **Janelas (passo 2):** as casas do porto (Armazém, Estaleiro, Mestre de obras, Taverna), a janela com abas da cena 05,
  o baú e os destroços abrem num pergaminho no meio da tela, com botões de madeira (o mouse funciona como antes; botão
  apagado = não dá agora, com o motivo escrito nele). Os ganhos ("+3 Madeira") aparecem embaixo da janela.
- **Menu inicial:** pergaminho no meio da tela escura, título em Pirata One, Continuar (com o resumo do jogo salvo) e Novo
  jogo (com confirmação).
- O painel F1 e o medidor F3 continuam no visual antigo (são ferramentas de teste).

**Dica do editor:** se o jogo aparecer serrilhado na janela Game, confira no alto dela o **Scale em 1x** e desmarque
**Low Resolution Aspect Ratios** (estava em 1,5× no seu layout); para jogar, **Full HD** e **Play Maximized**.

**Onde ajustar:** visual em `Assets/_Project/UI/Hud.uss` (cores, tamanhos, peças); escala em
`Assets/_Project/UI/HudPanelSettings.asset` (Reference Resolution). Fontes geradas em `_Project/UI/Fonts` pelo menu
**Pirates → Protótipo → HUD - Criar assets do HUD** (as cenas chamam sozinhas).

## Efeitos visuais (M6.11) — todas as cenas

No mar (passo 1):


- **Tiro de canhão:** clarão laranja na boca, faíscas, e uma nuvem de fumaça que fica no ar uns 5–7 s e vai com o vento.
- **Bala na água:** coluna d'água, gotas, anel de espuma.
- **Bala no casco:** lascas de madeira voando (cubinhos 3D), faísca e poeira.
- **Rastro:** espuma atrás do navio e nas laterais da proa, maior quanto mais rápido; borrifo na proa em velocidade alta
  (vale para os inimigos também).
- **Avaria:** com o casco abaixo de 60% sai fumaça do convés; abaixo de 30% pega fogo. **Naufrágio:** espirro grande, fumaça
  escura e bolhas/espuma onde ele afunda.
- Os canhões do forte da Ilha Norte e da sua Fortaleza usam o mesmo efeito.

A pé (passo 2; jogador e piratas):

- **Cutelo:** rastro branco em fita seguindo a lâmina, só na parte rápida do golpe. **Acerto** (cutelo ou bala): estrela de
  brilho e faíscas; em caixote/barril, lasquinhas de madeira.
- **Pistola:** clarão, faíscas e um pouco de fumaça na boca; poeira onde a bala bate (chão, pedra, árvore).
- **Pés:** correndo, uma nuvenzinha de poeira por passada na cor do chão (areia clara, terra, pedra; fraca na grama; nada no
  píer/convés); poeira em anel ao pular e ao cair de altura; respingos e espuma correndo na água rasa; poeira quando um pirata
  cai morto.
- **Saque:** poeira quando o caixote quebra; baú destrancado com coisa dentro solta brilhos dourados (e um punhado ao abrir);
  tesouro desenterrado joga torrões de terra, poeira e brilho dourado.
- Longe da câmera (> 70 m) nada disso é feito.

**Onde ajustar:** objeto **VFX** da cena (`GameVfx`: vento, velocidade mínima e espaçamento do rastro, fração do casco para
fumaça e fogo, segundos de bolhas; a pé: velocidade da poeira, distância máxima, duração e velocidade mínima do rastro do
cutelo, brilhos por segundo do baú); materiais em `Assets/_Project/Materials/VFX`. Texturas: Kenney Particle Pack (CC0), em
`Assets/ThirdParty/VFX`.

## Chefe no mar: o galeão Tormenta (M7.1) — cena 07

- No **mar do sudeste**, além da Ilha da Caveira, patrulha o **galeão Tormenta** do Capitão Lázaro Negro (velas pretas).
  Casco 700, seis canhões por bordo que acertam muito, mais rápido que o brigue (ainda dá para fugir, por pouco) e **nunca foge**.
- Durante a luta aparece uma **barra de vida no topo da tela** com o nome dele.
- Com o casco **pela metade**, ele **chama a escolta**: dois corsários (chalupas pirata) chegam por trás dele, e o aviso aparece
  na tela. Eles contam para "Caça no mar".
- Afundado, deixa destroços com muito ouro, madeira, pólvora, ferro, sobreviventes e um **baú raro garantido** (esquema de
  construção que você ainda não tem). Volta depois de 5 min, ou na hora ao aceitar a missão.
- Missão na taverna: **"O galeão Tormenta"** (Velho Tobias): 450 de ouro, 12 de pólvora e 10 de ferro.

**Onde ajustar:** `Data/ShipStats_Galeao`, `CannonStats_Galeao`, `LootTable_Galeao` (com o jogo parado); no galeão da cena,
`ShipBoss` (quantos corsários, em que fração do casco, a que distância) e `EnemyShipAI` (pontaria).

**Pontaria dos navios inimigos (todas as cenas):** eles antecipam onde você vai estar quando a bala cair (contando a própria
velocidade, que a bala herda) e viram o costado para esse ponto. Andar em círculo com a vela fixa já não protege: para desviar,
mude o rumo ou a vela quando eles alinham o costado. No `EnemyShipAI`: "Erro da antecipação" e "Erro aleatório do alcance" (maiores =
erram mais; chalupas 0,35/0,1, brigue 0,2/0,04, galeão 0,12/0,03) e "leme" da troca de tiros.

## Tripulação (M8.1) — cenas 05 e 07

- Os **sobreviventes** resgatados dos destroços e os **marinheiros** contratados são a tripulação do navio. Eles ficam nos
  **beliches**, que têm **16 vagas**. O painel do navio mostra "Tripulação 8 / 16".
- **Quanto mais gente a bordo, melhor o navio:** com os beliches cheios, a recarga fica **20% mais rápida** e as velas e o leme
  ficam **15% melhores** (aceleração e giro). Com metade da tripulação, metade disso. Sem ninguém, o navio fica como sempre foi.
  O bônus soma com o de "Artilheiros treinados".
- **Tiros no casco matam tripulantes:** em média 1 a cada 40 de dano (cada tiro sorteia). Aparece o aviso "Baixas a bordo".
- **Taverna** (cena 07; na 05, aba "Carga e conserto"): contrate marinheiros por **30 de ouro** cada (o ouro sai do armazém e
  depois do porão), um por vez, 5 de uma vez ou até encher os beliches.
- Com os beliches cheios, os sobreviventes dos destroços ficam lá ("beliches cheios").

**Onde ajustar:** `Data/CrewSettings_Navio` (beliches, bônus com os beliches cheios, dano por baixa, preço), com o jogo parado.
F1 → Navio: tripulação −1/+1, encher e zerar.

## Navio novo: a escuna (M8.2) — cenas 05 e 07

- O **baú raro** agora pode trazer o esquema **"Escuna"** (planta de navio).
- Precisa da **construção Estaleiro** já feita na base (Mestre de obras) e do esquema no armazém ou no porão. O **Estaleiro** (a casa)
  mostra o cartão dourado "Navio novo: Escuna" com o que muda. A obra custa
  **500 de ouro, 80 de madeira e 25 de ferro** e leva **3 min**: uma obra por vez, e só anda com o navio na zona do porto, como os upgrades.
- Pronta, a escuna **substitui a chalupa** e sai com o casco cheio. A tripulação continua, mas os **upgrades voltam ao nível 0**: compre
  de novo para a escuna, pelo mesmo preço. Nela **casco, velocidade e recarga rendem ×1,2** (no nível 3: casco +180, velocidade +30%,
  recarga −42%; na chalupa +150, +25% e −35%); dano e mira rendem igual (os 2 canhões a mais já somam dano).
- Os **corsários do galeão Tormenta** usam o mesmo modelo da escuna, mas com **bandeira vermelha**.

| | Chalupa | Escuna |
|---|---|---|
| Casco | 150 | 260 |
| Canhões por bordo | 4 | 6 |
| Porão | 30 | 50 |
| Beliches | 16 | 24 |
| Velocidade | 11 | 10,5 |
| Aceleração | 1,6 | 1,4 |
| Giro | 28°/s | 24°/s |

- O jogo salvo guarda o navio e a obra em andamento.

**Onde ajustar:** `Data/Ships/ShipClass_Escuna` (casco, porão, beliches, multiplicadores dos upgrades, custo, tempo, esquema, construção exigida), `Data/ShipStats_Escuna` e
`CannonStats_Escuna` (com o jogo parado). F1 → Navio: troca de navio na hora.

## Mais coisas no porto (M9) — cenas 05 e 07

**Munição especial (no navio: 1 comum, 2 corrente, 3 metralha; gamepad: botão norte troca).** Cada bordada gasta uma carga do porão;
sem carga, volta para a bala comum (infinita). O painel do navio mostra uma ficha por munição (tecla, nome, cargas), com a escolhida
em dourado; a primeira carga a bordo avisa a tecla. A bala de corrente voa como duas meias-balas presas, girando. Porão: chalupa 45,
escuna 75.
- **Corrente:** metade do dano no casco, mas rasga as velas: o navio acertado fica mais lento (até metade da velocidade).
- **Metralha:** 4 balinhas por canhão, espalha muito e só alcança 60 m, mas de perto faz mais dano e abate a tripulação inimiga
  (ela recarrega mais devagar, até +60%).
- Feitas pelo **ferreiro da Forja** (a porta aparece com a Forja construída): 6 cargas de corrente por 3 ferro + 2 pólvora (Forja 1);
  6 de metralha por 2 ferro + 3 pólvora (Forja 2). Também vendidas no mercado (18 e 16 de ouro cada). Ficam a bordo ao desembarcar.

Os estragos (velas e tripulação) se recuperam: 10 s depois do último acerto daquele tipo, voltam 3% por segundo. Para manter o
inimigo lento ou mudo, continue acertando. A recarga dos seus canhões nunca fica abaixo de 2,5 s (upgrades + tripulação); o que passar disso vira precisão: a bordada
espalha menos (até −40%; ex.: escuna com Artilheiros 4 e tripulação cheia, −37%). A loja do estaleiro mostra o "(limite)".

**Carpinteiro a bordo (R):** conserta o casco no mar, 2 por segundo, gastando 1 madeira do porão a cada 5 de casco (no porto é mais
barato e na hora). Depois de consertar, descansa 90 s antes de poder ser chamado de novo. Só trabalha 8 s depois do último tiro e com pelo menos 2 tripulantes; desliga com o casco inteiro. Ao desembarcar
no porto, 10 madeiras ficam a bordo para ele.

**Mercado (banca verde da praça):** compra madeira 10, pólvora 12, ferro 20 e vende munição; paga madeira 4, pólvora 5, ferro 8. Ele
só compra até 20 de cada item; a procura volta 1 a cada 45 s (a produção da Forja e do Paiol não vira ouro infinito).

**Nível 4:** Alojamento (trabalhadores 2,5×), Forja e Paiol (1 a cada 9 s); **Estaleiro 2** libera o nível 4 dos upgrades do navio
(casco +200, canhões +13, velame +31%, artilheiros −42%; a mira já termina em 100%); **Fortaleza 2**: mais 2 canhões em cima da
muralha e zona segura +45 m.

**Onde ajustar:** `Data/Ammo/Ammo_*` (dano, espalhamento, alcance, balinhas, velas/tripulação por acerto), `Data/CrewSettings_Navio`
(carpinteiro), `Data/HarborSettings_Base` (preços, demanda, receitas do ferreiro), `Keep Aboard` de cada item em `Data/Items`.

## Ajustes de 05/10 (A1)

- **Forja com tempo (cenas 05 e 07):** encomende 1, 5 ou o máximo (até 10 receitas; paga na hora). O ferreiro trabalha mesmo com o navio no
  mar (6 cargas de corrente = 1:30, de metralha = 2:00 por receita; o Alojamento e o bônus "Mestre de obras" aceleram). Pronta, aparece o
  aviso e a faixa do porto mostra "pronta: retire na Forja"; volte na Forja e clique **Retirar** (vai para o porão; o que não couber, armazém).
  Uma encomenda por vez; **Cancelar** devolve o que foi pago.
- **Ração (cenas 06 e 07):** a pé, **R** come uma e cura 40 de vida (não come com a vida cheia; 8 s entre uma e outra; 0,25 de espaço).
  O capitão dá um gole numa garrafa de rum (~2 s): pode andar devagar, mas não ataca nem atira até terminar. Ao desembarcar o capitão leva 5 rações do
  porão na mochila (as que sobrarem voltam ao embarcar). Mercado: 6 de ouro (não compra de volta); caixotes, barris e o baú do acampamento
  também dão. 20 rações ficam a bordo ao descarregar no porto.
- **Mochilas maiores (mercado da praça):** de couro, 25 de espaço, 200 de ouro (Marujo de Respeito); de contrabandista, 35, 500 de ouro
  (Corsário). A de lona tem 15. Compra única, fica guardada no armazém e vale a maior que você tiver (o mercado não vende a menor depois).
- **Cartuchos da pistola (cenas 05 a 07):** cada tiro gasta 1 cartucho da mochila; sem cartucho a pistola só faz clique (aviso na tela;
  o cutelo continua). Ao desembarcar o capitão leva 20 do porão. Mercado: 2 de ouro cada; Forja: 20 por 1 ferro + 1 pólvora (45 s);
  barris e o baú do acampamento também dão. O painel a pé mostra quantos restam. Jogo novo começa com 20 cartuchos e 3 rações.
- **Descartar (Armazém):** "Descartar itens…" mostra −1 / −10 / Tudo nos itens que o mercado não compra (munição, ração,
  cartuchos) e nos esquemas repetidos do porão. "Tudo" e esquema pedem confirmação. Não devolve nada. Ao carregar o jogo, esquema
  repetido no porão ou na mochila some sozinho (o do armazém fica).
- **Piratas de terra:** levou tiro ou golpe, o acampamento todo vem atrás e persegue até 85 m do centro por 15 s (renova a cada acerto),
  sabendo onde você está. Antes dava para matar todos de fora do raio deles (corriam até a borda e voltavam).
- **Mira assistida:** quando trava, a salva vai na distância exata do inimigo (antes caía antes do casco, na água); com a luneta no máximo,
  a antecipação usa o tempo de voo de verdade e cada canhão da salva acompanha o navio que está passando.

## Laser da mira e bacamarte (05/10, A3) — cenas 06 e 07

- **Laser:** segurando o botão direito com uma arma de fogo, sai da boca da arma um laser vermelho com brilho, até onde a bala vai bater
  (para no primeiro obstáculo ou no alcance), com um ponto vermelho no fim. Travou num alvo: o laser para no corpo dele e um anel dourado
  pulsa em volta. (Antes era uma linha branca fina que quase não se via.)
- **Pistola maior** (~56 cm, era ~37), na mão e no cinto. A pistola guardada no cinto tinha ~1 cm (sumia) desde o M4.3: corrigido;
  agora fica deitada na faixa, do lado direito (cabo para a frente, cano para trás).
- **Bacamarte (tecla 3):** a "doze" de pederneira, de duas mãos (animações de rifle do Mixamo). Cada tiro solta **7 chumbos em leque**
  (14 de dano cada, até 16 m): colado derruba um pirata (98), a 8 m pegam uns 3. Dá para ver o leque: cada chumbo é um risco amarelo
  que voa da boca até onde parou (os que batem em algo param antes, com poeira), com faíscas abrindo na frente do cano.
- **Recarga só na mão (pistola e bacamarte):** guardou a arma no meio da recarga, ela para; sacou de novo, continua de onde parou (não dá
  mais para atirar com o bacamarte, trocar para a pistola e voltar com ele carregado). No painel a pé, a arma guardada sem bala aparece "(vazia)". Recarga de 3,5 s. Compra única no **mercado da praça**,
  350 de ouro (patente Marujo de Respeito); fica nas costas, na diagonal, quando guardado; parado, segura a arma baixa com as duas mãos. Gasta **cargas de chumbo**: mercado 5 de ouro (não compra de
  volta), Forja 10 por 1 ferro + 1 pólvora (60 s, Forja 1), barris e o baú do acampamento. O capitão leva 12 do porão ao desembarcar
  (40 ficam a bordo ao descarregar). Antes de comprar, o 3 não faz nada e o painel a pé não mostra a arma.

**Onde ajustar:** `Data/Weapons/Weapon_Bacamarte` (dano por chumbo, `pellets`, `pelletSpread`, alcance, recarga, coice),
`Data/HarborSettings_Base` (preço, patente, receita), `Data/Items/Item_Chumbo`. Cor e espessura do laser: `CharacterAimRig` no `Model` do
personagem (cores, ponto, anel) e os objetos `LinhaDeMira`/`BrilhoDoLaser` (largura).

## Conversar com os NPCs da vila — cena 07

- Alguns moradores da vila têm serviço para você: **"!"** amarelo sobre a cabeça. Chegue perto e aperte **F** para conversar.
- **F** passa a fala. Na última, **Aceitar** (F) ou **Agora não** (Esc), com o pagamento e a fama na caixa.
- Missão de NPC se entrega **com ele** (não na taverna): quando estiver pronta, aparece **"?"** sobre ele e a seta do HUD aponta para ele.
  Ele conta o fim da história e paga (vai para o armazém).
- Quem dá missão na vila:
  - **Tobias, o coveiro** (a oeste da praça, com uma pá): "Noite no cemitério".
  - **Velho Bento**, contramestre aposentado (começo do píer): "O carrasco da Caveira".
  - **Dona Mercês**, dona do armazém (ao lado do armazém): "A lâmina da enseada".
  - **Almirante Teodoro** (no estaleiro): "O guardião do forte" (patente Corsário).
  - **Zé Caolho** (porta da taverna): "O mapa do Zé Caolho".
  - **Rato Velho** (canto perto do Mestre de obras): "O mapa do contrabandista".
- O quadro da taverna continua com os contratos comuns (afundar navios, entregas, acampamentos, forte, Santa Clara, Tormenta).

**Onde ajustar:** as falas ficam na própria missão (`Data/Missions`, cabeçalho "Conversa com o NPC").

## Missões dos inimigos novos — cena 07

Na taverna (quadro de missões), a partir da patente Marujo de Respeito:
- **O carrasco da Caveira:** derrote o brutamontes do acampamento da Caveira (190 de ouro e 6 de ferro).
- **A lâmina da enseada:** derrote a duelista do esconderijo da Ilha Oeste (160 de ouro e 3 bombas).
- **O guardião do forte** (patente Corsário): derrote o brutamontes do forte da Ilha Norte (230 de ouro e 8 de pólvora).
- **Noite no cemitério:** limpe o cemitério amaldiçoado da Caveira, à noite (240 de ouro e 20 cartuchos).

O destino aparece marcado na tela. Se o alvo já estava morto, ele volta quando você aceita a missão. Dá para repetir depois de um tempo (15 min; o cemitério, 10).

## História: "A Coroa de Prata" (M18, passo 1) — cena 07

A história do jogo (HISTORIA.md) em **capítulos**, cada um aberto por uma patente. O cartão **História** fica à direita, embaixo da fama e
do relógio: capítulo, título, o que está acontecendo e os objetivos (✓ quando feitos). Capítulo trancado mostra a patente e a fama que faltam
(contratos, tesouros e saques dão fama). O marcador com a **joia dourada** aponta para o próximo alvo.

1. **Náufrago** (Grumete): falar com o Almirante Teodoro, afundar 2 navios da Âncora Negra, ter 4 tripulantes a bordo e construir o
   Alojamento.
2. **O tributo** (Marujo de Respeito): falar com Dona Mercês e limpar o acampamento da Ilha da Caveira.
3. **Carta de corso** (Corsário): receber a carta de corso do Almirante Teodoro, calar o Forte São Brás (Ilha Norte) e afundar o
   brigue Santa Clara.
4. **Os tenentes de Lázaro** (Capitão Temido): conversar com o Velho Bento, derrotar o Barba-Negra nas ruínas e afundar o galeão Tormenta.
5. **As Águas Negras** (Lenda dos Sete Mares): falar com Iara e vencer a lula gigante.
6. **A Ilha Amaldiçoada**: falar com Tobias, o coveiro; o resto em breve.

Cada capítulo começa com uma **conversa**: o personagem ganha "!" na cabeça e o marcador da joia aponta para ele (F para conversar).

O mesmo feito conta para a história e para os contratos. Feito antes de o capítulo abrir não conta; quando ele abre, o alvo volta ao lugar
(acampamento cheio, brigue no mar, chefe de pé). Faixa no meio ao abrir e ao concluir cada capítulo; a fala do fim aparece nos avisos.
O cartão some durante o tutorial (o progresso conta mesmo assim). O save guarda o capítulo. F1 → **História**: voltar, pular, recomeçar.

## Mapas do tesouro em pedaços (C1) — cena 07

Pedaços de mapa caem de baús de terra, navios grandes e chefes (o Tormenta e a lula sempre dão um). Com **3 pedaços** (na mochila, no porão
ou no armazém) o mapa se monta sozinho e um **X** aparece numa das ilhas: o cartão "Mapa do tesouro" (à direita) diz onde, e a estrela
vermelha aponta o caminho. Desembarque, ache o X e aperte **F** para cavar: o baú sobe com ouro, pólvora, ferro e às vezes um esquema.
Um mapa por vez. F1 → **Mapas do tesouro**: +1 pedaço, montar agora, desfazer.

## Prisioneiros (C2) — cena 07

Três acampamentos têm uma **gaiola** com um prisioneiro: o acampamento da Caveira (Joaquim, o pescador), o esconderijo dos contrabandistas
(Marta, a marinheira) e as ruínas do Barba-Negra (Tenente Duarte, da Coroa). A gaiola só abre depois de derrotar todo o acampamento; aí
**F** solta o prisioneiro: ele vira **tripulante** do seu navio (com os beliches cheios, paga em ouro) e cada um traz algo a mais (ouro,
pedaço de mapa) e fama. Quando o acampamento volta, há outro prisioneiro na gaiola.

## Tutorial (M17, passos 1 a 3) — cena 07

**Novo jogo** começa com o navio **atracado na vaga do porto**. Na tela do nome há o botão **Tutorial: sim/não**. Com tutorial, o
**Velho Bento** guia o capitão em 15 etapas. Cada etapa avança sozinha quando você faz o que ela pede:

1. Descer do navio (**F** no cais) e conversar com o Bento (**F**; ele tem um **!** na cabeça).
2. Conhecer a vila, entrando em cada casa: Taverna, Mestre de obras, um morador com **!**, Mercado, Armazém e Estaleiro.
3. Treinar no **campo de treino** ao lado do estaleiro: 3 golpes de cutelo, um tiro de pistola, chute (**V**), rolar (**Ctrl**) e
   guarda (botão direito com o cutelo).
4. Voltar ao navio (**F** no píer) e zarpar (sair do porto).

Ao sair do porto começa a parte 2, **o mar**:

1. Os canhões: segure **Q** (bombordo) ou **E** (estibordo) para mirar e solte para disparar.
2. Afundar uma chalupa pirata: a mais perto vem atrás de você. Se o casco ficar ruim, volte ao porto (zona segura).
3. Saquear os destroços: pare ao lado (**S** recolhe as velas), aperte **F** e pegue algo.
4. Voltar a Porto Alento e atracar no cais (**F**).

Depois de atracar começa a parte 3, **as ilhas**:

1. Navegar até a praia norte da Ilha da Caveira e desembarcar (**F** perto da areia).
2. Derrotar os 3 guardas do posto do naufrágio (o de pistola avisa com uma linha vermelha antes de atirar).
3. Quebrar um caixote ou barril do posto e passar por cima do saco: o saque vai para a mochila.
4. Abrir o painel da fama (**C**).
5. Voltar ao navio (**F** na praia): a mochila descarrega no porão.

O cartão do tutorial fica à esquerda (etapa, fala do Bento e o que fazer). Um marcador amarelo aponta o lugar, com seta na borda
quando está fora da tela. O jogo salvo lembra a etapa. No **F1** há próxima etapa, recomeçar, encerrar e ir para a parte 2 ou 3.

O **Novo jogo** guarda o jogo antigo em `save_anterior.json`, na pasta do save. Para voltar a ele, renomeie para `save.json`.
O brigue Santa Clara (ao norte da Ilha Norte) e o galeão Tormenta (ao sul da Caveira) ficam mais longe das ilhas desde 16/10.

## Tela de título, prólogo e opções (M16) — cena 07

Ao dar Play na cena 07 abre a tela de título **"A Coroa de Prata"**, com o jogo parado atrás e a música do menu:

- **Continuar** (cinza quando não há jogo salvo; embaixo, o capitão, a fama e o resumo do save), **Novo jogo**, **Prólogo**,
  **Opções**, **Créditos** e **Sair**.
- **Novo jogo** pede o nome do capitão e passa o **prólogo**: os 9 quadros com zoom lento, a narração com o nome digitado e o
  título no fim. **Clique/Espaço** passa o quadro, **Esc** pula tudo. Depois o jogo começa.
- **Opções** (salvas em `opcoes.json`, ao lado do save; o "Novo jogo" não mexe nelas):
  - Gráficos: modo de tela e resolução (só no jogo compilado), VSync, limite de FPS (com o VSync desligado), qualidade
    Baixa/Média/Alta (Alta = como o jogo foi feito).
  - Áudio: volume geral, música, efeitos, ambiente e interface.
  - Controles: sensibilidade e inversão da câmera, e a lista de teclas.
  - **Padrão** volta a aba aberta ao normal; **Voltar** (ou Esc) grava.
- **Créditos:** o texto fica em `Assets/_Project/UI/Creditos.txt` (pode editar à vontade).

**Músicas:** menu e prólogo tocam o *Adventure Inn*; no jogo, cada momento tem uma lista que emenda uma música na outra
(porto 3, mar 4, ilha 2, combate 3). Para trocar, tirar ou pôr faixas, ou mudar o volume de uma, use `Data/Audio/Som_Musica*`
(listas **Clips** e **Clip Volumes**, mesma ordem), com o jogo parado.

## Megalodonte (chefe do mar) — cena 07

- Um tubarão pré-histórico de ~22 m (maior que o galeão), cinza-escuro, com a bocarra aberta. Mora no fundo da **Fossa do
  Megalodonte**, mar aberto a **nordeste** (marcador no mapa).
- Chegando perto, ele sobe e **circula o navio**: só a barbatana enorme corta a água. De tempos em tempos ele para, abre a boca (bolhas
  na frente) e **investe contra o costado**: dano e um tranco que aderna o navio. Depois mergulha e volta.
- **Só leva tiro perto da superfície** (circulando, avisando, investindo ou saltando); mergulhado, as balas não pegam.
- Com metade da vida fica **furioso**: mais rápido, investe mais e **salta para fora d'água** ao lado do navio. A água estoura onde ele
  vai cair: saia de baixo.
- Fugir: afaste-se da Fossa (~230 m) e ele desiste. Morto, deixa destroços com saque e volta em 15 min.
- **Missão "O rei das profundezas"** com a Iara, a pescadora (patente Capitão Temido). A Iara também tem a da lula ("O guardião das
  Águas Negras", patente Lenda dos Sete Mares).
- F1 (sandbox): atacar aqui, ir à Fossa, enfurecer, matar.

## Lula gigante (chefe do mar, passo 1) — cena 07

- Mora nas **Águas Negras**, mar aberto bem a oeste-sudoeste (~850 m do cais, ~650 m da Ilha Oeste), marcadas por dois cascos afundados e
  pedras num anel em volta. Entrar com o navio a menos de ~55 m do centro = emboscada.
- **Emboscada:** a água ferve em volta do navio (~2,4 s: dá para tentar fugir) e dois tentáculos sobem colados no casco e **agarram** o
  navio (passam por cima da amurada e deitam no convés): com os dois, o navio fica travado (só gira, não anda); com um, anda 25%.
  Agarrando, apertam o casco (8 a cada 2 s cada um).
- **Pancadas:** outros tentáculos sobem dos lados e batem no convés (30 de dano, 38 na fase 2; a tripulação morre como num tiro). O aviso é o tentáculo
  **recuando e tremendo** ~1 s antes de descer.
- **Cabeça:** emerge de um lado a ~26 m (olhos âmbar; a mira assistida trava nela), fica ~11 s, afunda e volta do outro lado. Só
  aparece com o navio preso.
- **Atirar nos tentáculos = à queima-roupa:** Q ou E (qualquer distância) acerta sempre os tentáculos colados naquele bordo: um tiro
  de cada canhão a até 2,5 m dele (proa-popa), com faíscas e a barra de vida. Metralha dá 1,5× por canhão (a melhor aqui), corrente 0,5×.
  Cada tentáculo tem 120 de vida e passa 40% do dano para a lula. A cabeça (a 26 m) é com a mira de distância de sempre e leva o dano
  inteiro. Tentáculo destruído se debate, afunda e volta em 16 s. Sem nenhum agarrando, o navio se solta; se você não fugir para longe do covil, ela agarra de novo em 14 s.
- **Fase 2** com metade da vida (1400): urra, sobe um terceiro tentáculo de pancada, pancadas mais rápidas (aviso 0,8 s) e mais fortes (38).
- Dificuldade (medida): o galeão nível 4 vence com folga, a escuna nível 4 apertado, a escuna nível 2 só com metralha, o galeão sem
  melhorias perde.
- Solta e longe dela (sem fugir do covil de vez) por 35 s, ela desiste, se cura e volta a esperar.
- Derrotada: tudo afunda, destroços com baú garantido (esquema que falta ou ouro) onde estava a cabeça, **100 de fama**. Volta 8 min
  depois com você longe. Fugiu? Ela se cura e volta a esperar.
- Barra de chefe no topo ("Lula Gigante — o terror das Águas Negras").
- F1 (sandbox), em Inimigos: estado e vida, **Atacar aqui** (muda o covil para onde o navio está e ataca), **Ir ao covil**, Enfurecer, Matar.

**Onde ajustar:** componente `GiantSquid` no objeto "LulaGigante" (raio, agarrão, pancadas, cabeça, fase 2); vida da lula e dos
tentáculos e o lugar do covil em `PrototypeSceneBuilder.Squid.cs` (regerar a 07); saque em `Data/LootTable_Lula`.

## Chefe pirata a pé: o Capitão Barba-Negra (pacote B, passo 4) — cenas 06 e 07

- Mora nas **ruínas da Caveira**, no alto do morro oeste (no fim da trilha que sai do portão oeste do acampamento), com a sua
  **Guarda Negra** (passo 2): dois **espadachins** de sabre e uma **mosqueteira** (avisa com a linha vermelha antes de atirar, de longe),
  de preto e vermelho. Um espadachim ataca por vez.
- **Arena:** enquanto a guarda luta, o Barba-Negra só observa do meio das ruínas. Ele entra quando a guarda cai, quando você chega
  perto dele (5 m) ou quando você bate nele. Tochas acendem à noite em volta das colunas; bandeiras pretas marcam a entrada.
- **Missão "O Carniceiro das Ruínas"** com o Almirante Teodoro (patente Capitão Temido): 600 de ouro, ferro e pólvora, e 100 de fama.
- O rival do Barbarossa: o mesmo tipo de capitão, **40% maior**, de casaco preto, chapéu e calça vermelho-sangue, barba negra e
  **olhos vermelhos que brilham**, com um **espadão** dourado. Grita ao ver você; a barra dele aparece no topo da tela.
- **900 de vida.** Nada o derruba (nem barril ou bomba) e golpe nenhum o interrompe: a brecha é depois de cada golpe dele.
- Golpes: cortes de 28–38 (às vezes dois ou três emendados) e um golpe de cima que derruba. Esses dá para **segurar na guarda** e
  **aparar** (ele cambaleia e fica aberto).
- **Imparáveis** (ele brilha em vermelho no preparo; não dá para segurar nem aparar, só rolar ou sair do alcance):
  - **giro** em volta (30, derruba), quando você está nas costas dele ou bate sem parar;
  - **chute** (10, derruba), quando você fica atrás da guarda;
  - **salto** de longe (42, derruba), quando você se afasta 5,5–11 m.
- **Fase 2** com metade da vida: ele se enfurece (fica invencível uns 2,6 s), solta uma **onda de choque** que derruba quem estiver
  perto e volta **mais forte e mais rápido**: lâmina em brasa, brasas nos pés, dano +20%, menos tempo entre golpes e uma
  **sequência de três cortes** avançando.
- Fugiu dele (saiu das ruínas ou morreu)? Ele volta ao lugar com a vida cheia, na fase 1.
- Derrotar dá 60 de fama.
- F1 (sandbox): na linha "Ruínas do Barba-Negra", vida e fase dele e "Enfurecer (fase 2)" durante a luta.

**Onde ajustar:** `Data/Weapons/Weapon_Espadao_Chefe` (dano, alcance e abertura de cada golpe em "Golpes especiais"); quando cada
golpe sai, as pausas e a fase 2 no componente `LandBoss` do chefe; vida, tamanho e velocidade em `PrototypeSceneBuilder.Chief.cs`
(regerar as cenas).

## Esqueletos amaldiçoados (pacote B, passo 3) — cenas 06 e 07

- Cemitério na Ilha da Caveira, ao lado da trilha oeste (a caminho das ruínas), com covas, placas tortas e uma lanterna.
- De dia não tem ninguém. **À noite**, chegando perto, **10 esqueletos** de osso esverdeado **saem das covas** e atacam com cutelos
  (três de cada vez; alguns avançam no golpe e vão atrás de quem rola). Saindo da terra, não levam golpe.
- **Amaldiçoados:** derrubado pela primeira vez, o esqueleto levanta de novo da terra com 70% da vida. **Tiro, bacamarte, bomba ou
  barril** acabam com ele de vez.
- Ao amanhecer viram pó; na noite seguinte voltam. Limpar o cemitério dá um pouco de fama.
- F1 (sandbox): "Sair das covas" / "Voltar às covas" no cemitério; mude a hora para a noite no F1.

**Onde ajustar:** raio, horários, tempo e vida ao levantar de novo no objeto `Cemiterio` (componente `CursedGraveyard`); vida e
velocidade em `PrototypeSceneBuilder.Graveyard.cs` (regerar as cenas).

## Duelista (pacote B, passo 2) — cenas 06 e 07

- Pirata de **adaga**, de roupa escura e mangas vermelhas: uma no acampamento da Caveira e uma no esconderijo dos contrabandistas (Ilha Oeste).
- Rápida, 100 de vida. Rodeia você a uns 3 m, entra com estocadas de 14 (até três seguidas) e sai. Mistura estocada rápida e lenta:
  rolar cedo demais não salva (ela vem atrás de quem rola e ataca quando o rolamento acaba).
- Vê o seu golpe vindo: às vezes **apara** (você cambaleia e ela contra-ataca) e às vezes **rola** para longe.
- Quando você ataca sem parar ela **ergue a guarda**: o golpe vira arranhão e ela responde na hora (dá para aparar a resposta).
  **Chute** e **tiro** passam pela guarda: o chute a derruba.

**Onde ajustar:** `Data/Weapons/Weapon_Adaga_Duelista` (dano, alcance, espera); vida, velocidade e as chances de aparar, rolar e erguer a
guarda em `PrototypeSceneBuilder.Duelist.cs` (regerar as cenas).

## Brutamontes (pacote B, passo 1) — cenas 06 e 07

- Pirata-tubarão **gigante** com **machado duplo**: um no pátio do acampamento da Caveira e um na guarnição do forte (Ilha Norte).
- 330 de vida, lento (dá para fugir e rodear). Os golpes são avisados, mas tiram 35 (três matam) e **derrubam**; dois dos três são
  lentos (pegam quem rola cedo) e ele avança no golpe até uns 4,5 m. Role na hora do impacto, não no começo do golpe. Golpe não o interrompe e
  chute não o derruba; a **explosão** derruba.
- Na **guarda** o golpe vira um arranhão, mas cansa muito (3× o fôlego). **Aparar** o machado deixa ele cambaleando e aberto.
- Grita quando vê o capitão.

**Onde ajustar:** `Data/Weapons/Weapon_Machado_Brutamontes` (dano, alcance, espera, quanto cansa a guarda); vida, tamanho e velocidade em
`PrototypeSceneBuilder.Brute.cs` (regerar as cenas).

## Fôlego (pacote A, passo 6) — cenas 06 e 07

- Barra **Fôlego** embaixo da vida. **Rolar** gasta 25 (sem fôlego não rola: a barra pisca vermelha); **segurar a guarda** gasta aos
  poucos e mais a cada golpe segurado; **aparar** devolve um pouco. Recupera sozinho logo depois que você para de gastar.
- Fôlego zerado com a guarda erguida = **guarda quebrada**: ela cai, o golpe passa pela metade e você fica ~1 s sem erguer de novo.
- Andar, correr, atacar e chutar não gastam. Começa cheio a cada desembarque.
- A guarda e o aparar soam como lâminas se batendo (tinidos feitos por código: `Tools/Audio/espadas.py`).

**Onde ajustar:** componente `Stamina` do capitão (máximo, recuperação, custos, guarda quebrada).

## Defesa e aparar (pacote A, passo 5) — cenas 06 e 07

- Com o **cutelo** na mão, **segure o botão direito** para erguer a guarda (com a pistola ou o bacamarte o mesmo botão continua sendo a
  mira). Anda devagar e fica de frente para o cursor; para atacar, solte.
- Golpe de cutelo **de frente** na guarda vira só um arranhão (20% do dano). De lado ou pelas costas passa inteiro; tiro, chute e
  explosão não são segurados.
- **Aparar:** erga a guarda **logo antes** do golpe chegar (até 0,3 s): nada de dano, faísca e tinido, e o pirata cambaleia para trás e
  fica ~1,4 s sem atacar (hora do contra-ataque). Apertar sem parar não funciona: a guarda precisa ficar meio segundo baixada.

**Onde ajustar:** `CharacterCombat` do capitão, grupo "Defesa" (arco, fração do dano, janela de aparar, atordoamento).

## Bomba de arremesso (pacote A, passo 4) — cenas 06 e 07

- **Segure G** para mirar: aparece o arco do voo e o círculo da explosão no chão, seguindo o mouse (vermelho = você está dentro da
  explosão, por exemplo jogando contra uma parede perto; cinza = ainda esperando a próxima bomba). **Solte G** para lançar; o botão
  direito cancela. Um toque rápido no G lança direto no cursor.
- **G** (gamepad: direcional para a direita) lança uma **bomba de pólvora** da mochila no ponto do cursor (de 4 a 14 m). Ela explode
  ~1,6 s depois: raio de 3,5 m, até 55 de dano no centro, derruba quem estiver perto e acende os barris de pólvora. Caiu na água, apaga.
- O capitão leva 3 bombas do porão ao desembarcar. Compre no mercado (15 de ouro), faça na Forja (3 por 1 ferro + 2 pólvora) ou ache em
  barris e baús. Espera de 2,5 s entre uma e outra; se for derrubado no meio do arremesso, a bomba não é gasta.

**Onde ajustar:** `ThrownBomb` no molde "Bombas/Bomba_Molde" (explosão e pavio), `BombThrower` do capitão (alcance, espera, quantas leva),
`Data/Items/Item_Bomba`, `HarborSettings_Base` (preço e receita).

## Chute e quedas (pacote A, passo 3) — cenas 06 e 07

- **V** (gamepad: LB) chuta para a mira: pouco dano (8), mas **derruba** o pirata da frente: ele cambaleia, cai sentado e leva ~1,5 s
  para levantar (dá 1 ou 2 golpes). Espera de 3 s entre um chute e outro; quem acabou de levantar fica 2 s sem cair de novo.
- **Barril de pólvora derruba** quem estiver a até ~3 m: os piratas ficam ~2,9 s no chão; você ~1,3 s, e depois de 0,8 s caído o **Ctrl
  levanta rolando**. Ao levantar você fica meio segundo invencível.
- No chão o personagem leva dano, mas não é derrubado de novo. Quem morre caindo fica deitado.

**Onde ajustar:** `CharacterCombat` (Chute: dano, alcance, arco, tempos, empurrão), `FootHealthReactions` (Derrubado: segundos no chão),
`PowderKeg` (até onde derruba).

## Rolamento / esquiva (pacote A, passo 2) — cenas 06 e 07

- **Ctrl** (gamepad: RB) rola ~4,5 m para onde você está andando (parado: para a frente). No começo e no meio do rolamento nada
  acerta: golpe, tiro, bala de chumbo ou explosão. Use para atravessar o leque do bacamarteiro ou sair do raio do barril.
- Cancela o golpe ou tiro que ia sair; espera curta (0,4 s) entre um rolamento e outro.

**Onde ajustar:** `FootCharacter` do capitão (Rolamento: distância, duração, espera, trecho invencível).

## Barris de pólvora (pacote A, passo 1) — cenas 06 e 07

- Barris com **faixas vermelhas e tampa preta** (diferentes dos barris de saque) perto de onde os piratas ficam: 4 no acampamento da
  Caveira (fogueira, portão norte, torre, portão sul), 2 no esconderijo dos contrabandistas e 4 no forte da Ilha Norte (3 juntos no
  paiol e 1 no pátio).
- **Tiro** (pistola, bacamarte, piratas inimigos e balas de canhão) acende; **golpe de cutelo não**. Depois de um pavio curto
  (0,1–0,25 s) explode: até 80 de dano no centro e 12 na borda (raio de 5 m), empurra quem estiver perto e quebra caixotes.
  Outro barril no raio também explode, um pouco depois (cadeia). **Cuidado: pega você também** (um barril sozinho não mata o capitão;
  dois ou três juntos matam de perto). Paliçada, casa, muralha e morro protegem; o tiro dos piratas também acende.
- A mira da pistola "gruda" no barril como nos piratas: mire nele e atire de longe.
- Os barris voltam quando o acampamento é repovoado (missão aceita de novo) e no F1 → restaurar caixotes.

**Onde ajustar:** componente `PowderKeg` de cada barril (raio, dano, empurrão, pavio); posições em
`PrototypeSceneBuilder.PowderKegs.cs` (`SkullCampKegs`, `HideoutKegs`) e no forte (`EnemyFortAt`).

## Inimigos novos (M13) — bacamarteiros (cenas 06 e 07) e caçadores de recompensas (cena 07)

- **Bacamarteiros** (tubarões de bacamarte): 2 no acampamento da Caveira, 1 no esconderijo dos contrabandistas e 1 na guarnição do forte.
  Chegam perto (~9 m), miram com a linha vermelha por 1 a 1,5 s e soltam um leque de chumbo (até 42 de dano colado); a recarga é longa
  (4,5 s): saia do leque correndo de lado e aproveite a recarga para atacar.
- **Caçadores de recompensas**: com a fama vem a cabeça a prêmio. A partir da patente Corsário, de tempos em tempos (uns 7 a 12 min
  navegando fora do porto) um navio de velas brancas aparece longe e vem atrás de você pelo mapa todo, com aviso e barra de vida no topo.
  Fica mais forte a cada patente; na Lenda dos Sete Mares é mais forte que o Tormenta. Afundar dá fama e saque. Se você ficar no porto (ou
  ancorado, a pé) ou fugir para longe, ele desiste depois de uns 90 s.
- F1 → Inimigos → "Chamar caçador de recompensas" (testar na hora).

**Onde ajustar:** `Data/Weapons/Weapon_Bacamarte_Pirata` (dano, chumbos, alcance, recarga); objeto "CacadoresDeRecompensa" na cena 07
(patentes, casco e fama de cada caçador, intervalo, distância, desistência); `Data/ShipStats_Cacador`, `Data/CannonStats_Cacador_1..3`,
`Data/LootTable_Cacador`.

## Galeão, o 3º navio (M8.3) — cenas 05 e 07

- Depois da chalupa e da escuna: o **galeão**, o maior navio (o mesmo casco do Tormenta). Casco 420, **8 canhões por bordo**, porão 110,
  32 beliches; o mais lento e o mais pesado para virar. Os upgrades de casco e recarga rendem 40% a mais nele, e o de velocidade 30% (e voltam a zero no
  navio novo, como na escuna).
- **Para construir** (Estaleiro do porto, casa do Estaleiro): o esquema "Galeão" — sai no baú do galeão Tormenta ou no mercado na patente
  Lenda dos Sete Mares (1600) —, o **Estaleiro da base no nível 2**, 1200 de ouro, 160 de madeira, 60 de ferro e 40 de pólvora; 5 min de obra.
  Dá para ir direto da chalupa para o galeão.
- F1 → Navio: troca na hora (para testar).

**Onde ajustar:** `Data/ShipS/ShipClass_Galeao` (casco, porão, beliches, custo, obra, multiplicadores, nível do Estaleiro),
`Data/ShipStats_GaleaoJogador` (velocidade, giro) e `Data/CannonStats_GaleaoJogador` (canhões); chance do esquema no `Data/LootTable_Galeao`.

## Marujos no convés (M12, passo 1) — cenas 05 e 07

- A sua **tripulação aparece no navio**: com os beliches cheios, 4 marujos na chalupa e 6 na escuna (com menos gente, menos marujos).
- Eles andam pelo convés, param e conversam; na escuna alguns ficam lá em cima no tombadilho, perto do leme.
- **Mudou a vela?** Dois correm até o mastro e puxam os cabos.
- **Combate** (tiro dado ou levado): correm para trás dos canhões, se abaixam no disparo e socam a bala nova enquanto a bateria recarrega;
  uns 25 s depois do último tiro voltam ao normal. Tiro no casco faz alguns se encolherem; cada baixa na tripulação derruba um marujo.
- Só visual: não muda velocidade, recarga nem nada do jogo (a tripulação continua valendo como antes).

**Onde ajustar:** no navio do jogador, componente `DeckCrew` (velocidade de andar e correr, quanto tempo dura o combate, distância em que param,
tempo puxando o cabo).

## Chuva e tempestade (M11, passo 2) — todas as cenas

- **O tempo muda sozinho** ao longo dos dias de jogo: céu limpo na maior parte do tempo, nublado (~1/4 do tempo), chuva (~1/6) e, de vez em
  quando, tempestade (~6% do tempo: mais ou menos uma a cada 2 ou 3 dias de jogo, durando uns 3 minutos). O jogo novo começa com céu limpo.
  O relógio mostra o tempo: "tarde · chuva", "noite · tempestade".
- **Céu fechado:** o dia escurece e fica cinza-azulado, a névoa fecha (enxerga-se menos longe) e as sombras somem.
- **Chuva:** gotas finas caindo, inclinadas pelo vento; onde batem na água abrem anéis e pulam pinguinhos (no chão, no píer e no convés só os
  pinguinhos); com chuva o vento também aumenta. Som de chuva gravado (e a chuva pesada por cima na tempestade).
- **Tempestade:** chuva forte, vento forte, raios (clarão em toda a cena e o risco do raio) e trovões, lanternas acesas mesmo de dia. Todos os
  navios balançam muito mais e viram um pouco pior (−12%), e os canhões de todo mundo (inimigos e fortes também) espalham 50% mais.
- **F1 → Clima:** céu limpo, nublado, chuva ou tempestade (a troca leva uns 20 s), "Raio agora" e "Muda sozinho".

**Onde ajustar:** `Assets/_Project/Data/Clima` (com que frequência chove e vem a tempestade, quanto dura a troca, vento mínimo na chuva e na
tempestade, intervalo e distância dos raios, força do clarão); `Data/DayNight_Ciclo` → "Céu fechado" (quanto escurece, cor da luz e da névoa,
névoa, lanternas na tempestade); chuva no objeto "Clima" (`RainEffect`: gotas por segundo, velocidade, cor, respingos e anéis por segundo, tamanho e duração do anel); balanço dos navios no
`ShipController` (Storm Roll, Storm Turn Loss); sons no objeto "Audio" (`WeatherAudio`).

## Vento (M11, passo 2, parte 1) — todas as cenas

- **Vento que muda:** a direção e a força mudam devagar ao longo do dia de jogo (de calmaria a vento forte), em volta de um vento que mais
  sopra para lés-nordeste. Com o relógio acelerado no F1 ele muda junto.
- **Na vela:** com o vento por trás ou de lado o navio anda mais (com vento forte, +12% com o vento bem por trás e +10% uns 45° fora dele); de bolina e contra
  o vento, menos (até −30% com o vento bem de frente). Com vento fraco a diferença é menor. Vale para os inimigos também: numa perseguição
  em linha reta os dois pegam o mesmo vento.
- **Painel do navio:** linha "Vento" com uma seta (proa para cima: a seta mostra para onde o vento sopra em relação ao navio), a força
  (fraco/moderado/forte) e quanto ele ajuda ou segura nesse rumo ("forte, a favor +10%").
- **No mundo:** a fumaça das chaminés, fogueiras e tiros vai com o vento; com vento forte o mar tem mais espuma nas cristas.
- **Riscos de vento:** fitas brancas finas passam voando pela tela na direção do vento, ondulando, e algumas dão um laço (como no Zelda
  Wind Waker). Com vento forte vêm muitas, rápidas e compridas; com vento fraco, uma de vez em quando; de noite ficam mais apagadas.
- **Som do vento:** um sopro contínuo que fica mais alto e mais agudo com vento forte, com rajadas; em terra fica mais baixo. O som foi
  gerado pelo próprio projeto (`Assets/_Project/Audio/Vento_Gerado.wav`); para trocar por uma gravação, ponha o arquivo no `Data/Audio/Som_Vento`.
- **F1 → Vento:** fixar de onde vem (norte, leste, sul, oeste), fraco ou forte, ou voltar ao vento que muda sozinho.

**Onde ajustar:** `Assets/_Project/Data/Vento` (vento dominante, quanto vira, quão rápido muda, força mínima/máxima, curva de velocidade pelo
ângulo, efeito com vento fraco); espuma do mar no objeto Ocean ("Wind Foam"); riscos no objeto "Vento" (`WindStreaks`: quantos por segundo,
velocidade, chance de laço, largura, transparência); som no objeto "Audio" (`WindAudio`: volume com calmaria/forte, tom, rajadas, volume a pé).

## Dia e noite (M11, passo 1) — todas as cenas (o horário é salvo na 07)

- **Relógio:** o dia (6h às 19h) dura 14 minutos de jogo e a noite (19h às 6h), 6 minutos. A hora só anda com o jogo rodando (no menu fica
  parada). Plaquinha no canto de cima à direita, embaixo da fama: "Dia 3 · 18:50" e a parte do dia, com um disco amarelo (dia), laranja
  (amanhecer/anoitecer) ou azul-claro (noite). O jogo salvo guarda o dia e a hora; jogo novo (e save de antes) começa às 8h.
- **Luz:** o sol anda de leste a oeste; no fim da tarde a luz fica dourada com sombras longas, e de noite a mesma luz vira a lua azulada.
  O céu, a névoa e o mar escurecem juntos (o mar deixa de refletir o céu do dia; a lua e as lanternas refletem na água). Ao meio-dia o
  jogo fica como era antes.
- **Lanternas:** as 9 do porto (píer, frente da taverna, praça) e a da popa do seu navio acendem ao anoitecer e iluminam em volta. Os
  navios inimigos têm a lanterna da popa só brilhando (dá para achá-los no escuro). As fogueiras continuam acesas o dia todo.
- **Casas e construções:** as janelas acendem à noite (umas 3 de cada 4; cada casa acende numa hora, a vila vai acendendo aos poucos) e
  uma luz na frente de cada casa ilumina a rua. Vale para a vila, as construções da base (o paiol e o depósito de pólvora ficam apagados)
  e as torres de pedra (Fortaleza, moinho, forte inimigo). Lanternas novas nos portões da Fortaleza, do forte inimigo e do acampamento da
  Caveira, e no cais e nas palafitas do esconderijo dos contrabandistas.
- **Piratas de terra** veem menos à noite: até ~10 m (de dia, 16). Bem perto (~4,5 m) percebem igual, e tiro ou golpe provoca o
  acampamento como sempre.
- **F1 → Dia e noite:** pular para o amanhecer, meio-dia, pôr do sol ou noite, e acelerar o relógio (×10, ×60).

**Onde ajustar:** `Assets/_Project/Data/DayNight_Ciclo` (durações, horas do nascer/pôr do sol, altura do sol e da lua, cores e intensidade
da luz, luz ambiente e névoa da noite e do anoitecer, reflexo e céu da noite, quando as lanternas acendem); visão noturna dos piratas em
`EnemyFootAI → Night Sight` (gerado: mude no gerador ou no prefab do pirata).

## Fama do capitão (M10) — cenas 05 e 07

**Tecla C** (no navio e a pé; gamepad: Select) abre o painel da fama: retrato e nome do capitão, patente com estrelas, feitos (navios,
acampamentos e fortes, tesouros, missões), barra até a próxima patente, o que cada patente libera e os bônus. A plaquinha no canto de
cima à direita mostra a patente e fica dourada quando há bônus para escolher. O nome se escolhe no **Novo jogo** (save antigo: "Capitão Barbarossa").

**Fama:** chalupa afundada 6, corsário 8, brigue 25, galeão 80; acampamento da Caveira 20, esconderijo/guarnição 15, posto de guarda 5;
forte calado 40; tesouro 10; missão entregue 10 a 120 (o quadro mostra quanto). **Patentes:** Grumete (0), Marujo de Respeito (60),
Corsário (200), Capitão Temido (450), Lenda dos Sete Mares (900). Cada patente nova:
- ouro das missões +10% (acumula; as entregas pagam fixo);
- contratos que pedem patente: Santa Clara, contrabandistas e mapa (1), forte e "O terror das rotas" (2), Tormenta e "Encomenda para o
  arsenal" (3), "O senhor dos mares" (4); os trancados aparecem no fim do quadro com a patente que falta;
- esquemas à venda no mercado (uma vez, se você ainda não tem): Estaleiro 250 (1), Forja e Paiol 300 (2), Escuna 650 (3), Fortaleza 900 (3);
- 1 bônus para escolher entre 3 sorteados (para sempre): dano +10%, velocidade +8%, casco +15%, mercado +20%/−10%, obras +25%,
  ouro das missões +20%, carpinteiro +50%, porão +15%, metade das baixas na tripulação.

Ajustes da revisão (05/10): "Encomenda para o arsenal" paga 250 de ouro e volta ao quadro só depois de 30 min (era 420 e 10 min);
cada navio afundado conta para **uma** missão de afundar sem alvo (a aceita primeiro; a do brigue/galeão conta o alvo dela à parte).

**Onde ajustar:** `Data/FameSettings_Capitao` (patentes, bônus, ouro por patente), `Fame Reward`/`Required Rank` em cada `Data/Missions`,
`Required Rank` no mercado (`Data/HarborSettings_Base`), `Fame` nos navios/acampamentos/forte/tesouros (gerador).

## Oceano (M6.12) — todas as cenas

**Mar aberto (passo 2):** o alto das ondas fica num verde-água mais claro e, nas cristas mais altas, aparecem manchas de espuma
que vão e voltam com as ondas. Ajuste no objeto **Ocean**, seção "Mar aberto (visual)": cor da crista, onde ela começa a
clarear, onde começa a espuma e quanto de espuma.

**Costa (passo 1):**

- Perto das ilhas a água fica **turquesa** (até ~28 m do litoral) e mais **clara no raso**, junto da areia.
- **Espuma** na linha d'água, que sobe e desce com as ondas, e **faixas de espuma** recortadas chegando na praia.
- A água continua opaca (o raso é só cor). O gerador faz um mapa por cena (`Assets/_Project/Textures/Ocean/<cena>_Costa`),
  com a altura do chão e a distância até o litoral de cada ilha: mudou uma ilha, regere a cena.

**Onde ajustar:** objeto **Ocean** da cena (`OceanSurface`, seção "Costa (visual)"): cor do turquesa, do raso e da espuma, largura
do turquesa, profundidade da espuma, tamanho das manchas, alcance, espaçamento, velocidade e espessura das faixas. Vale com o jogo
rodando (para manter, mude com o jogo parado ou no gerador).

## Som e música (M6.8)

Todas as cenas têm som (pacotes CC0 em `Assets/ThirdParty/Audio/`, créditos em `CREDITOS.txt`):

- **Música** que muda sozinha com fade: **porto** (dentro da zona do porto), **mar** (navegando), **ilha** (a pé fora do
  porto) e **combate** (pirata de terra te perseguindo por perto ou navio inimigo vindo para cima; segura 8 s depois). Desde o
  M16 cada um é uma lista de músicas (uma emenda na outra), e o menu e o prólogo têm a sua.
- **Mar e navio:** ondas no mar aberto, madeira rangendo, velas subindo e descendo, âncora ao desembarcar e embarcar, sino
  ao atracar e ao sair do porto; canhões (disparo, bala caindo na água, bala acertando madeira ou pedra) e o navio afundando.
- **A pé:** passos pelo tipo de chão (areia, grama, terra, pedra, madeira do píer, água rasa), pulo, golpe e acerto do
  cutelo, tiro e recarga da pistola, sacar e guardar, dor e morte (a voz dos piratas é mais grossa), corpo caindo, caixote
  quebrando, baú abrindo, tesouro sendo cavado, saque entrando na mochila.
- **Porto:** porta das casas abrindo e fechando, carga, conserto (martelo), encomenda (moedas), obra pronta, missão aceita /
  cumprida / paga (vinhetas de steel drum), jogo salvo.
- **Ambiente:** ondas quebrando nas praias de todas as ilhas, gaivotas, fogo nas fogueiras e na forja, pássaros nas matas
  (Caveira e morros do porto).
- **Estampidos fortes (09/10):** explosão, tiro de canhão e tiros das armas usam versões mais encorpadas dos mesmos sons
  (`ThirdParty/Audio/Efeitos/Forte`: grave no início, eco curto, mais volume). Para ajustar só um deles, mexa no volume do
  `Data/Audio/Som_Explosao`, `Som_CanhaoTiro` ou `Som_PistolaTiro` (fora do Play).

Os sons vêm de onde acontecem (3D): o "ouvido" fica no navio ou no capitão, não na câmera. **Volumes:** no **F1** (sandbox)
há controles de Geral, Música, Efeitos, Ambiente e Interface, e o nome da música tocando; os volumes iniciais ficam em
`Data/Audio/SoundSettings`. Cada som é um asset `Data/Audio/Som_*` (variações, volume, tom, alcance): ajuste ali, com o
jogo parado.

## Modo sandbox (F1) — cenas 04 a 07

Aperte **F1** em Play para abrir o painel de testes e **F2** para teleportar direto para a vaga do porto (na 07, depois **F** atraca e desce no píer) (arraste pela barra de título). Só existe no editor e em builds de desenvolvimento.

- **A pé** (cena 06): levar 30 de dano, vida cheia (levanta se estiver derrotado), **invencível a pé**, **piratas
  distraídos** (não te veem; só reagem se apanharem), **levantar piratas** (todos nos postos, vida cheia), **derrotar todos**,
  **ir até lá** (para a ~20 m; os guardas já te veem), em cada grupo: acampamento e os três postos da Caveira, **esvaziar mochila** e **repor saque da ilha**
  (baú fechado de novo, caixotes inteiros). Na cena 06 o navio tem porão: a seção de itens dá coisas direto para ele.
- **Som:** volumes de cada canal e o nome da música tocando (porto, mar, ilha, combate).
- **Missões** (cena 07): **cumprir objetivos ativos** (afundar, acampamento e tesouro contam como feitos; entregas ganham a
  carga no porão) e **zerar missões**.
- **Jogo salvo** (cena 07): **salvar agora** (em qualquer lugar; ao carregar o navio vai para o porto), **carregar o save**
  (recarrega a cena e continua, sem o menu) e **novo jogo** (o save vai para o .bak e a cena recomeça). Mostra o caminho do arquivo.
- **Itens:** +1 / +10 / +100 de qualquer item no **Porão** ou no **Armazém** (cena 05); esquemas com **Ganhar**.
  **Encher tudo** dá +2000 de cada recurso, +5 sobreviventes e todos os esquemas (o suficiente para comprar todos os upgrades).
  **Esvaziar porão** e **Porão sem limite de espaço**.
- **Navio:** levar 50 de dano, consertar tudo, **casco invencível**, **Concluir obra agora**, **Teleportar para o porto (F2)**
  (para ao lado do cais, já de velas recolhidas: é só apertar F), **Zerar upgrades** e **Derrubar construções**.
  "Concluir obras agora" termina a do estaleiro e a da base.
- **Inimigos:** afundar na hora (para testar o loot) ou trazer a **chalupa** ou o **brigue** (cena 07) para 90 m à sua frente.
- **Velocidade do jogo:** 1× / 2× / 4×.

Teste rápido dos upgrades: F1 → **Encher tudo** → **F2** → F → aba **Upgrades do navio**.

## Onde ajustar o combate e a navegação (todas as cenas, sem mexer em código)

- `NavioInimigo → EnemyShipAI`: distâncias de detecção e combate, erro de mira, tempo de reação, vida para fugir.
- `Assets/_Project/Data/ShipStats_ChalupaInimiga` e `CannonStats_Inimigo`: velocidade e recarga do inimigo.
- Componentes `Health` (vida máxima) e `SinkOnDeath` (alagamento, tempo de naufrágio) em cada alvo na cena.
- `Assets/_Project/Data/CannonStats_Chalupa`: canhões por lado, recarga, velocidade da bala, tempo de mira, dispersão.
- `Assets/_Project/Data/ShipStats_Chalupa`: velocidade, aceleração, giro, inclinação, níveis de vela.
- `Assets/_Project/Data/WaveSettings_Default`: tamanho, altura e direção das ondas.
- `Main Camera → ElevatedCameraRig`: distância, inclinação e suavização.
