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
com o **Porto da Base** na costa norte da **Ilha Sul**, ~160 m ao sul do início: píer com bandeira pirata, armazém e casas.

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
(a leste, ~360 m do começo) e um **tesouro enterrado** na Ilha Oeste.

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
(~500 m do porto). Bem mais forte que a chalupa: **casco 400**, **6 canhões por bordo** (14 de dano, recarga 4,5 s) e
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
  porto) e **combate** (pirata de terra te perseguindo por perto ou navio inimigo vindo para cima; segura 8 s depois).
- **Mar e navio:** ondas no mar aberto, madeira rangendo, velas subindo e descendo, âncora ao desembarcar e embarcar, sino
  ao atracar e ao sair do porto; canhões (disparo, bala caindo na água, bala acertando madeira ou pedra) e o navio afundando.
- **A pé:** passos pelo tipo de chão (areia, grama, terra, pedra, madeira do píer, água rasa), pulo, golpe e acerto do
  cutelo, tiro e recarga da pistola, sacar e guardar, dor e morte (a voz dos piratas é mais grossa), corpo caindo, caixote
  quebrando, baú abrindo, tesouro sendo cavado, saque entrando na mochila.
- **Porto:** porta das casas abrindo e fechando, carga, conserto (martelo), encomenda (moedas), obra pronta, missão aceita /
  cumprida / paga (vinhetas de steel drum), jogo salvo.
- **Ambiente:** ondas quebrando nas praias de todas as ilhas, gaivotas, fogo nas fogueiras e na forja, pássaros nas matas
  (Caveira e morros do porto).

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
