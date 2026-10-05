# CLAUDE.md — Pirate RPG

Projeto **pessoal** do Jhonata (não é da FilmaEu): RPG de piratas single-player, 3D **low-poly estilizado** (decidido em 02/10; antes era "realista"), câmera elevada.
Unity **6.6 (6000.6.3f1)**, URP 17.6, Input System 1.20, C#. Escopo e arquitetura em [ARCHITECTURE.md](ARCHITECTURE.md);
como rodar cada protótipo em [README.md](README.md).

## Como trabalhar neste projeto

1. **Passo a passo, sempre com revisão.** Entregue um passo pequeno, testado, e **pare para o Jhonata revisar**
   antes de expandir qualquer sistema. Não implemente o próximo marco sem aprovação.
2. **Teste antes de entregar.** Compile e rode em Play pela Unity CLI (receita abaixo); relate o que foi e o que
   **não** foi possível verificar (ex.: input real de teclado, HUD IMGUI não aparece em captura de câmera).
3. **Use as skills Unity** quando o assunto bater.
   - **Plugin oficial `unity`** (prefira sempre que houver): `unity:unity-cli`, `unity:unity-package-management`,
     `unity:physics-3d-collision`, `unity:initialize-ai-navigation`, `unity:urp-postprocessing`, `unity:ui`
     (+ `ui-uitk`/`ui-ugui`), `unity:audio-setup-mixers`, `unity:optimize-audio`, `unity:shader-graph-create-custom-node`.
   - **Cópias manuais** em `~/.claude/skills` (sem equivalente no plugin): `unity-animation`, `unity-physics`,
     `unity-input-system`, `unity-scriptableobjects`, `unity-navmesh`, `unity-build-pipeline`, `unity-ecs-patterns`.
     Foram escritas para o Unity 6.3: confira as APIs contra os pacotes instalados (6.6 marca várias como obsoletas).
4. **Segurança:** nunca coloque credenciais/tokens no projeto. Antes de instalar skill, pacote ou ferramenta de
   terceiros, revise o conteúdo. Nenhuma integração externa (Unity Cloud, analytics, serviços) sem o Jhonata pedir.
5. Responda em **português**.

## Arquitetura (resumo)

Um **assembly (.asmdef) por módulo** em `Assets/_Project/Scripts/`, sem dependências circulares:
`Core` (Services, EventBus + GameEvents, IOceanSampler, IDamageable, ICameraAimFocus, IInteraction, ILockSource) ← `Ocean`, `Ships` ← `Combat` ←
`Controls` (→ Base: atracar e desembarcar) ← `CameraSystem`; `Economy` (ItemDefinition, Inventory) ← `Loot`, `Progression` (upgrades) ← `Base` (porto);
`Ambient` (→ Core: vida do cenário só visual, barcos ancorados, gaivotas, caranguejos, moradores, fogo);
`Audio` (→ quase todos, como a UI: só ouve eventos e põe ShipAudio/CharacterAudio nos navios e personagens; ninguém o conhece);
`VFX` (→ Core, Ships, Combat, OnFoot, Loot: efeitos visuais, só ouve o `CombatEffects` e o `CharacterCombat`, acompanha navios, personagens e saque; ninguém o conhece);
`UI` → Ships/Combat/Loot/Base/OnFoot/LandCombat/Missions/Save; a tecla F (`PlayerInteractInput`, no navio e no personagem) só conhece `IInteraction`; `OnFoot`
(`FootCharacter`: jogador e piratas "apertam os mesmos botões", `FootCommand` com direção no mundo) ← `LandCombat` (IA de terra,
acampamento, reações de vida) ← `Controls` (`PlayerModeSwitcher` troca navio ↔ a pé; câmera via `ICameraFollow`; `PlayerFootRespawn`);
`Editor` (só editor) gera as cenas e os prefabs de loot (`Assets/_Project/Prefabs/Loot`, regerados a cada build).

- Navio e oceano não se conhecem: o navio pede a altura da água via `IOceanSampler`.
- `Save` (→ Core, Economy, Combat, Ships, Progression, Base, Missions) lê e devolve o estado pelas APIs de cada módulo
  (`Inventory.SetContents`, `ShipUpgrades.SetLevels`, `HarborDock.RestoreJob`, `BaseBuildings.Restore`, `MissionBoard.Restore`,
  `Health.SetCurrent`), sem eventos de HUD; ninguém conhece o `Save`. Ids no save = nome do asset (itens, upgrades,
  construções) e `missionId`: **renomear esses assets apaga o progresso deles no save**.
- Navio com nome nas missões: `EnemyShipAI.targetId` vai no `ShipSunkEvent`; missão `SinkShips` com `targetId` só conta
  aquele navio (sem alvo, conta qualquer um). `EnemyRespawner.targetId` traz o navio na hora quando a missão é aceita.
  Cascos no gerador: `ShipHull` (`SloopHull`, `BrigHull`: colisor, massa, inércia, pontos de flutuação, faixa dos canhões).
- `ShipBoss` (Combat) marca o navio chefe (escolta + barra no HUD). `Missions` (→ Core, Economy) não conhece combate nem terra: ouve o `EventBus` (navio afundado, acampamento derrotado,
  tesouro achado) e publica "missão aceita" (o acampamento se repovoa, o tesouro é enterrado). Ids de alvo são textos
  iguais na missão (Data/Missions) e na cena (`EnemyCamp.campId`, `BuriedTreasure.treasureId`, `WorldMarker`).
- Jogador e IA pilotam o mesmo `ShipController` via `IShipInput` (`PlayerShipInput` / `EnemyShipAI`).
- Ajustes de game design ficam em ScriptableObjects em `Assets/_Project/Data/` (ShipStats, CannonStats, WaveSettings,
  LootTable, HarborSettings, `Items/`, `Upgrades/`). **Nunca altere esses assets em Play** (fica salvo): o navio do
  jogador usa cópias em memória (`ShipUpgrades`).
- Arte de terceiros (CC0) em `Assets/ThirdParty/<Pacote>/` (revisada; os downloads crus ficam em `_Downloads/`, fora de
  `Assets/`). `ThirdPartyImportSettings` importa sem materiais/colisores; o gerador põe os modelos como filhos visuais
  (`PrototypeSceneBuilder.Kenney.cs`: `Kenney(...)`/`Model(Kit.X, ...)`, um material por paleta: `M_Kenney`, `M_KenneyTown`,
  `M_KenneySurvival`; o Nature Kit pinta por material e vira `M_N_<cor>` compartilhado; `KenneyColors` para as primitivas
  que sobraram). Física nunca vem do modelo. Casas: `HouseSpec` + `House(...)` (`PrototypeSceneBuilder.Village.cs`, peças de
  1 m do Fantasy Town Kit × 3 m); layout do porto em `PrototypeSceneBuilder.Port.cs`.
- Cenas de protótipo são **geradas por código** (menu **Pirates → Protótipo**): mudou o gerador, regere a cena.
  O gerador reabre a cena do disco após salvar (sem isso, referências a ScriptableObjects somem no primeiro Play).

## Desempenho (chat de otimização)

Máquina do Jhonata: i5-1235U + **Intel Iris Xe (vídeo integrado)**: a GPU é o limite em tela cheia. Perfil ativo = "PC" (`Assets/Settings/PC_RPAsset.asset` + `PC_Renderer.asset`).
- Gráficos (02/10, O1): **SSAO desligado** (era o maior custo de GPU; sem diferença visível na nossa câmera), HDR desligado (não há pós-processamento),
  sem cópia de textura de cor/profundidade (nada usa), sombras com **2 cascatas** e suavidade média, **static batching desligado** (o GPU Resident
  Drawer agrupa as peças repetidas; static batching tira os objetos dele). Não religue sem medir.
  **Efeito de renderer desligado = REMOVA, não desative** (O7): o SSAO ficou no `PC_Renderer` com `m_Active: 0`; o build tira os
  recursos dele e o URP falhava ao criar o renderizador (`ScreenSpaceAmbientOcclusion.TryPrepareResources` NullReference, depois
  "Blitter is already initialized" **a cada quadro**, ~48 KB de lixo/quadro e 350 MB de log). No editor não aparece. Removido em 04/10
  (`PC_Renderer` sem features, como o `Mobile_Renderer`). Depois de mexer no URP, rode um build e procure "Exception" no `player.log`.
- **Oceano na GPU (O4):** `M_Water` usa o shader `Pirates/Ocean` (`_Project/Shaders/Ocean.shader` + `OceanWaves.hlsl`): é o
  URP Lit do pacote (mesmos `LitInput`/`LitForwardPass`, mesmas propriedades) com o vértice subindo na altura da onda e a normal
  pela grade. `OceanSurface` só anda com a malha (parada) e manda as ondas como globais (`_OceanWaves` = kx, kz, amplitude, fase;
  a fase já leva a posição da malha e o tempo, somada em double e reduzida a uma volta: o seno da GPU perde precisão com número
  grande). A física continua em `WaveSettings.Evaluate` (CPU); a diferença GPU × CPU é < 0,5 mm. Com outro shader no material,
  `OceanSurface` volta a deformar a malha na CPU (senos por linha × coluna). Oceano: ~2–3 ms de CPU → ~0. O gerador
  (`CreateOcean`) põe o shader no `M_Water`. Mudou a fórmula das ondas? Mude nos dois lugares (`WaveSettings` e `OceanWaves.hlsl`).
  **Costa (M6.12):** o passo ForwardLit usa `OceanLitFragment`, cópia do `LitPassFragment` do URP 17.6 com `OceanShore`
  (`OceanShore.hlsl`) mexendo só em albedo/smoothness antes da luz (DepthOnly/DepthNormals iguais; sem keyword nova). Ao atualizar o
  URP, compare o `LitPassFragment` do pacote com a cópia. Dados: mapa por cena `Textures/Ocean/<cena>_Costa.asset` (RG8, ≤ 512 px,
  R = altura do chão −6…+2 m, G = distância até o litoral 0…64 m; `BakeShoreMap` em `PrototypeSceneBuilder.Shore.cs`, pela mesma
  `IslandHeight` da malha; ilhas registradas em `CreateIslandTerrain`, lista zerada no `CreateOcean`) + `Ocean_RuidoEspuma` (64², repete).
  Cores e medidas = globais do `OceanSurface` (Inspector; `OnValidate` reenvia). Mar aberto (passo 2): crista mais clara e espuma nas
  cristas pela altura (`positionWS.y − _OceanSeaLevel` sobre a soma das amplitudes, `_OceanCrest`), recortada pelo mesmo ruído: 1 amostra
  (ruído, amostra normal no topo da função, antes de qualquer desvio: `_GRAD` é bem mais lento na Intel) fora do mapa, 2 dentro. `M_Water` fica em smoothness 0,93: testado 0,7/0,8
  (o reflexo do sol vira um lençol branco largo) e 0,97 (igual); o `GetMaterial` não sobrescreve o material que existe.
  MaterialPropertyBlock no oceano não vale (GPU Resident Drawer): para testar o material em Play use `renderer.material` (cópia).
  **Custo (03/10, carregador ligado, `renderScale` 1,75 ≈ 1080p, 5 rodadas):** oceano inteiro × cópia com o URP Lit puro no mar aberto
  = 4,08 × 3,39 ms (**≤ ~0,7 ms**; a costa é ~0,1 dele, o resto é o que roda em todo pixel). Medido ainda com o ruído em `_GRAD`; a troca
  para a amostra normal só pode baratear (não deu para medir: GPU instável). O Jhonata aceitou o custo. Se precisar cortar: tirar a
  espuma das cristas fora do mapa (sem amostra de ruído no mar aberto).
  **Medir GPU com o notebook no carregador** (`Win32_Battery.BatteryStatus` = 2) e bateria acima de ~50%: na bateria (ou carregando
  com ~20%) a Iris Xe fica ~2× mais lenta e instável, e o A/B some no ruído. Em 04/10, mesmo no carregador, com 60% e "Melhor
  desempenho", CPU e GPU ficaram ~2,5× mais lentas que em 03/10 (mar: CPU ~17 ms, GPU ~5,8 ms; só a câmera do jogo desenhando, sem
  trava de FPS). Causa não confirmada: talvez o Windows 11 economizando no Unity em segundo plano. Antes de medir, rode a base
  (`c_sea`: CPU ~7, GPU ~2,3 ms em escala 1). Se não bater, compare só dentro da mesma sessão (A/B) e não anote números absolutos.
  **Chefe (M7.1, 04/10, relativo):** luta com o galeão e os 2 corsários ~+7% de CPU contra o mar vazio (sobretudo `Buoyancy` dos
  navios a mais), GPU igual, lixo de memória igual: leve.
- **Build (O7, 04/10):** com o Smart App Control ligado o `.exe` não abria (o Jhonata desligou em 04/10; o Burst também passa a
  funcionar no editor). **Jogo compilado (build de desenvolvimento, Mono, tela cheia 1366×768, Iris Xe no carregador):** menu ~275 FPS,
  mar ~200 FPS (CPU ~5,6 ms, GPU ~1,5 ms), luta com o chefe ~185 FPS, vila a pé ~160 FPS (CPU ~6,5 ms, GPU ~2,5 ms, pior quadro
  ~8,8 ms), lixo ~1 KB/quadro. Limite = CPU. Release deve ser um pouco mais rápido. **VSync ligado no perfil PC** (05/10, pedido do
  Jhonata: `QualitySettings` PC `vSyncCount: 1`): no jogo compilado trava na taxa da tela (60 Hz) e o notebook não esquenta à toa.
  No editor o VSync é o da janela Game (não muda as medidas pela CLI). Para medir o FPS máximo num build, rode
  `QualitySettings.vSyncCount = 0` pelo eval do player.
  Roteiros do player (scratchpad `bench/rt`): o eval do player não vê `Pirates.*` (use reflexão) e "Triangles/GPU Frame Time" do
  ProfilerRecorder vêm zerados lá: use `FrameTimingManager` + `Time.frameCount`. O player usa o MESMO save do Jhonata: backup e
  restauro pelo editor (eval_file) antes e depois.
  Tela do notebook = **1366×768** (~1,05 Mpx, 1,5× a janela Game do editor): as medidas "1080p" (`renderScale` 1,75) eram pessimistas;
  para simular a tela cheia dele use `renderScale` ~1,22. Medir pelo `--runtime` exige build de desenvolvimento + Pipeline →
  Runtime `enableInBuilds` (servidor local com senha): ligue só para o build e apague depois (`ProjectSettings/Packages/` não existia).
  **A água continua opaca** (o raso é cor, não transparência: o O5 cortou a ilha abaixo de −2 m). Ilha nova fora do `CreateIslandTerrain`
  (pedra solta, rochedos) não entra no mapa.
  Ao atualizar o URP, confira se o Lit ganhou/perdeu `#pragma` no passo ForwardLit (o `Ocean.shader` copia a lista).
- **F3** (editor e builds de desenvolvimento): medidor de FPS, pior quadro, CPU, GPU e lixo de memória (GC) por quadro (`Sandbox/PerformanceOverlay`, cria-se sozinho).
- Medir pela CLI: posicione o cenário pausado, depois `ProfilerRecorder` ("CPU Main Thread Frame Time", "GPU Frame Time",
  "GC Allocated In Frame", "PreLateUpdate.ScriptRunBehaviourLateUpdate", "Triangles Count", "Shadow Casters Count") guardados em
  `AppDomain.SetData`, **despause** ~8 s e leia num segundo `eval_file` (com `Step()` numa chamada só os gravadores não fecham quadro).
  A GPU integrada oscila: repita cada medida 2×. **Chame `GameSave.NewGame()` antes** (senão o menu inicial fica aberto atrás e conta).
  Quem aloca: `ProfilerDriver.enabled = true`, despause, depois `ProfilerDriver.GetHierarchyFrameDataView(..., columnGcMemory)` e some
  o GC próprio de cada item. Para ver dentro do Unity: `ProfilerDriver.deepProfiling = true` + `EditorUtility.RequestScriptReload()` (desligue depois).
  Confira `play=True` antes de mover o navio: se o Play não entrou, o script roda na edição (e o NewGame mexe no save).
- **Lixo de memória do HUD (O2):** era ~18–23 KB/quadro, agora ~5–8; com o HUD em UI Toolkit (M6.10) o código do
  jogo aloca ~0,3 KB/quadro (o resto é do aviso do F1) e o UI Toolkit custa < 0,3 ms. No UI Toolkit: `Label.text` só quando
  muda, peças reaproveitadas (pools), nada de `Query`/`schedule`/lambda por quadro; janelas só remontam quando os dados
  mudam. Regras para IMGUI (hoje só F1 e F3):
  - Texto que muda: campo `CachedText` (`if (x.Stale(valores)) x.Text = $"..."`) e desenhe `x.Content`; texto fixo: `HudDraw.Fixed("...")`
    (só texto que não muda: o dicionário dele não esvazia). No Unity 6 o IMGUI **copia toda string** que recebe (`GUIContent.Temp`).
  - Listas do porão/mochila: remonte só quando `Inventory.Version` muda.
  - Nada de `foreach` em `IReadOnlyList` (`X.Active`, `inventory.Items`) em Update/OnGUI: aloca o enumerador; use `for`.
    Nada de lambda que captura (`RemoveAll(f => ...feedSeconds)`): use `HudDraw.ExpireFeed`.
  - `useGUILayout = false` quando o HUD não usa GUILayout naquele estado (Update liga/desliga; no OnGUI, só desenhe a janela se `useGUILayout`).
  - O que sobra (~5 KB) é do próprio IMGUI (medir/desenhar cada caixa): só some trocando para UI Toolkit.
- **Longe da câmera, dorme (O3):** `EnemyCamp` (> `sleepDistance` 180 m da câmera, fora de combate; acorda a 160) desliga nos
  piratas tudo menos `Health` e `EnemyFootAI` (ficam na pose, `keepAnimatorStateOnDisable`); golpe num pirata, `ResetAll` e
  `WakeUp()` acordam na hora (e um golpe segura o acampamento acordado 3 s, para a queda terminar). `Townsperson` para
  (Animator e agente) a > 150 m e grava uma pose antes. Economiza ~1–1,3 ms/quadro longe deles.
  **Nos testes:** IA de terra com a câmera longe não anda nem vê ninguém: leve a câmera (navio/personagem) até lá ou chame `camp.WakeUp()`.
- **Cenário mais leve na GPU (O5, no gerador: `PrototypeSceneBuilder.Rendering.cs`):** `OptimizeSceneRendering()` roda antes de salvar
  cada cena e tira a sombra da vegetação (malhas `grass*`, `flower_*`, `plant_*`, `mushroom_*`, `crop*`, `corn`) e de tudo parado
  menor que 0,6 m (navios e personagens ficam como estão; barris, caixotes, canhões e tocos mantêm sombra): ~3.900 → ~1.900 objetos
  com sombra na 07. **Objeto novo de vegetação de outro kit/nome? Ponha o prefixo em `NoShadowMeshes`.** A ilha desenhada é
  `Meshes/<ilha>_Visual.asset` (sem os triângulos inteiros abaixo de −2 m: ~45% da malha, sempre sob a água opaca); o
  `MeshCollider` (navio encalha, NavMesh) continua com a malha inteira. Foto do mesmo quadro com e sem o corte: idêntica.
  Fumaça: `Meshes/Fumaca_Bolinha.asset` (icosaedro de 80 triângulos, raio 1 como o `Sphere.fbx`, que tem 760): ~97 mil → ~10 mil
  triângulos na vila. GPU na vila ~3,9 → ~3,35 ms (−14%, com e sem o O5 na mesma sessão, 03/10), na Caveira ~2,7 → ~2,4 ms
  (editor). Testado e descartado: "Small-Mesh Screen-Percentage" do GPU Resident Drawer (0 × 1%: diferença zero na vila e no mar).
  **Testes que mexem no URP asset em Play** (`renderScale`, `smallMeshScreenPercentage`...): o valor fica na memória do editor depois
  do Play (é um ScriptableObject). Volte o valor no fim do teste e confira (`PC_RPAsset`: renderScale 1, smallMesh 0).
- **Efeitos visuais (O6, M6.11 medido):** leves. Combate pesado simulado (12 tiros a cada 3 s + balas na água, acertos, navio em
  chamas) com `renderScale` 1,75 (≈ pixels de 1080p): ≤ ~0,5 ms de GPU; na vila, efeitos ligados × desligados: diferença zero.
  Materiais `Particles/Unlit` sem soft particles/distorção (não pedem a textura de profundidade: não religue isso). O `Scan()` do
  `GameVfx` (a cada 2 s) custa ~0,3 ms. Para medir GPU "de tela cheia" no editor: `renderScale` 1,75 e volte para 1.
- Quadro no editor (03/10, depois do O6): renderização (CPU) ~2,6–2,9 ms (corte do GPU Resident Drawer, luzes do Forward+ e envio; os
  jobs rodam sem Burst neste editor, no build ficam mais rápidos), física ~0,3–0,4 + `Buoyancy` ~0,26, animação ~0,4, NavMesh ~0,3
  (vila), UI Toolkit ~0,1–0,4, IMGUI do F1/F3 ~0,25 (só editor/build de desenvolvimento). CPU ~7–8,5 ms, GPU ~2,3–2,6 ms no mar e
  ~3,35 na vila (janela 1193×584; ~3,9 com `renderScale` 1,75 em combate). Nenhum item grande sobrando: o próximo passo útil é medir
  num **build** (sem o peso do editor; conferir se o Burst funciona lá com o Smart App Control).

## Estado atual

| Marco | Status |
|---|---|
| M1 Navegação (`Proto01_Navegacao`) | ✅ aprovado |
| M2.1 Canhões / M2.2 Dano e naufrágio (`Proto02_CombateNaval`) | ✅ aprovados |
| M2.3 Inimigo com IA (`Proto03_Duelo`) | ✅ aprovado (pode receber ajustes depois) |
| M2.4 Loot + saque dos destroços (`Proto04_Loot`) | ✅ aprovado (visual fica para o M6) |
| M3.1 Porto-base: armazém, descarregar/carregar, conserto (`Proto05_Base`) | ✅ aprovado |
| M3.2 Upgrades do navio (com tempo de obra) + mira assistida + zona segura do porto + inimigos reaparecem (`Proto05_Base`) | ✅ aprovado (vai ganhar mais mecânicas depois) |
| Ferramenta: modo sandbox F1 (`Pirates.Sandbox`, cenas 04 a 07) | ✅ em uso; ao criar sistemas novos, acrescente atalhos nele |
| M3.3 Construções da base (Alojamento, Estaleiro, Forja, Paiol, Fortaleza) (`Proto05_Base`) | ✅ aprovado (vai ganhar mais coisas depois) |
| M4.1 Desembarque + personagem a pé com modelo e animações do Starter Assets (`Proto06_Desembarque`) | ✅ aprovado (física/level design melhoram no M6) |
| M4.3 Combate a pé: cutelo (combo, sacar/guardar) + pistola (tiro, recarga, mira segurando o botão direito, braço segue o cursor), animações do Mixamo com corpo inteiro parado, bonecos de treino (`Proto06_Desembarque`) | ✅ aprovado |
| M4.4 Inimigos em terra: acampamento com 4 piratas de cutelo (IA no NavMesh, vez de atacar, alarme), vida a pé e volta à praia ao morrer (`Proto06_Desembarque`) | ✅ aprovado |
| M4.2 Saque em terra: mochila (15) que descarrega no porão ao embarcar, baú do tesouro trancado até derrotar o acampamento (escolha de itens, chance de esquema), caixotes/barris quebráveis que soltam saco de saque (`Proto06_Desembarque`) | ✅ aprovado |
| M4.5 Piratas pistoleiros (2 no acampamento, azul-marinho): miram com aviso (linha vermelha), mira atrasada (correndo de lado dá para desviar), mantêm distância, recuam recarregando (`Proto06_Desembarque`) | ✅ aprovado |
| M5.1 Mundo + missões: cena 07 juntando porto (05) + Ilha da Caveira (06, a leste) + tesouro na Ilha Oeste; quadro de missões no menu do porto (afundar, limpar acampamento, entregar, desenterrar), até 3 ativas, destino marcado no HUD, pagamento no armazém (`Proto07_Mundo`) | ✅ aprovado (vai melhorar depois) |
| M6.1 Jogo salvo: menu inicial (Continuar/Novo jogo, jogo parado atrás), salva sozinho ao atracar e ao sair do porto, só o progresso (ilhas voltam cheias), um save (`save.json` + `.bak`), navio volta na vaga do porto (`Proto07_Mundo`) | ✅ aprovado |
| M6.2 Visual low-poly (1): navios e objetos do Kenney Pirate Kit (navio pirata do jogador, inimigos de velas brancas, velas que sobem/descem com o nível de vela, canhões, barris-alvo, destroços, baú, caixotes/barris, bote), todas as cenas | ✅ aprovado |
| M2.5 Brigue inimigo "Santa Clara" (cena 07): ship-large de velas brancas, sozinho, patrulha ao norte da Ilha Norte; casco 400, 6 canhões por bordo (14 de dano, recarga 4,5 s), pontaria de veterano (`EnemyShipAI.ConfigureGunnery`), mais lento (9); dificuldade aumentada a pedido (aprovada); saque maior (baú 50%); volta em 150 s ou ao aceitar a missão "O brigue Santa Clara" (o ★ segue o navio); conta para "Caça no mar" | ✅ aprovado |
| M6.3 Ilhas geradas: malha low-poly com litoral irregular, praia, morros com pedra, cores da paleta do Kenney, palmeiras/pedras/grama do kit; pontos de jogo fixos (desembarques, acampamento, anel dos prédios e topo da Fortaleza no porto, tesouro); acampamento com cabanas de palha e bandeira do kit; todas as cenas | ✅ aprovado (pediu ilhas maiores e mais vivas: M6.4+) |
| M6.4 Porto vira vila: Ilha Sul 2× (centro 28 m ao sul; litoral do píer, cais e vaga iguais), vila com casas modulares (Fantasy Town Kit), armazém, taverna, praça do mercado, ruas de terra, roça, torre de vigia, cais lateral com botes, navios ancorados, mata do Nature Kit; construções da base com modelos novos (Fortaleza = torre + muralha do kit, canhões no alto da torre; moinho no Paiol 3); moradores parados, gaivotas, caranguejos, fumaça e fogo; desembarque no porto pelo cais lateral (cena 07) | ✅ aprovado |
| M6.5 Personagens do Quaternius Pirate Kit: jogador = Capitão Barbarossa (esqueleto adaptado para humanoide: mesmas animações Starter/Mixamo e combate), vilões = tubarões (Mako, Sharky) de cutelo e esqueletos de pistola, armas do kit, moradores Anne/Henry em 8 visuais recoloridos (18 parados com gestos, 10 andando no NavMesh do porto) | ✅ aprovado |
| M6.6 Porto a pé (cena 07): F no cais = atracar e desembarcar (porão descarrega sozinho no armazém, capitão desce no píer); 4 casas de serviço com placa e atendente, porta para o porto: Armazém (carga), Estaleiro (conserto + upgrades do navio), Mestre de obras (construções), Taverna (missões); janela só daquela casa, sem pausar, salva ao sair; entregas contam porão + armazém; câmera vira para a terra ao desembarcar e para a fachada ao entrar; cena 05 mantém o menu | ✅ aprovado |
| M6.7 Ilha da Caveira 2× (cenas 06 e 07, ~260 × 210 m, praia norte no lugar): acampamento grande (8 piratas, paliçada de estacas, 2 portões, cabanas, barracas, torre), 3 postos de guarda que não contam para a missão (naufrágio a leste, ruínas no morro sudoeste, trilha sul), trilhas de terra, segunda praia de desembarque ao sul, navio naufragado, ruínas, mata fechada; câmera deixa translúcido o que tapa o capitão (todas as cenas) | ✅ aprovado |
| O1 Otimização (1): oceano barato na CPU, configurações do URP para GPU integrada (SSAO off, 2 cascatas, sem HDR/cópias, GPU Resident Drawer sem static batching), medidor F3 (todas as cenas) | ✅ aprovado |
| O2 Otimização (2): lixo de memória por quadro −70% (HUDs com texto guardado e GUIContent, sem layout quando não precisa, laços sem alocação em Update, raycasts NonAlloc do cursor e da mira da pistola) | ✅ aprovado |
| O3 Otimização (3): acampamentos e moradores dormem longe da câmera (sem IA, animação, NavMesh), acordam ao chegar perto, ao levar golpe ou com o reset do acampamento | ✅ aprovado |
| O4 Otimização (4): ondas do mar na GPU (shader `Pirates/Ocean` = URP Lit + vértice da onda), visual idêntico, física igual (< 0,5 mm), oceano ~2–3 ms de CPU → ~0 | ✅ aprovado |
| O5 Otimização (5): cenário mais leve na GPU (todas as cenas, no gerador): vegetação e miudezas sem sombra, ilha desenhada sem a parte sempre submersa (colisor igual), fumaça low-poly (80 em vez de 760 triângulos por bolinha); GPU na vila ~3,9 → ~3,35 ms (−14%) | ✅ aprovado |
| O6 Otimização (6): medição dos efeitos visuais (M6.11) e do quadro inteiro: efeitos leves (≤ ~0,5 ms de GPU em combate pesado em 1080p), nada a cortar; renderização na CPU caiu para ~2,7 ms; sem mudança de código | ✅ aprovado |
| O7 Otimização (7): jogo compilado medido em tela cheia 1366×768: ~160 FPS na vila, ~200 no mar, ~185 contra o chefe; corrigido erro do build (SSAO desativado no `PC_Renderer` fazia o URP falhar a cada quadro; agora removido); revisão da economia entregue (correções 1–3 feitas) | ⏳ **aguardando revisão** |
| M6.8 Som e música (todas as cenas): módulo `Pirates.Audio` só ouvindo eventos (canhões, navio, combate a pé, passos por tipo de chão, porto, missões, saque, jogo salvo), música porto/mar/ilha/combate com fade, ambiente (ondas nas praias, gaivotas, fogo, pássaros), ouvido no alvo da câmera, volumes por canal no F1, pacotes CC0 em `ThirdParty/Audio` | ✅ aprovado |
| M6.9 Ilhas Oeste e Norte 2× (mata em todas as cenas; conteúdo na 07): Oeste = esconderijo de contrabandistas (palafitas, cais, chalupa ancorada, 4 piratas, baú do contrabando), tesouro do Zé numa gruta de pedras, segundo tesouro na praia noroeste; Norte = forte inimigo na costa sul (muralha, torre, 4 canhões do kit que atiram no seu navio), calado a tiros de canhão nas muralhas (`EnemyFort`, 150 de vida, só dano de navio), guarnição de 5 e paiol; 3 missões novas | ✅ aprovado |
| M6.10 HUD de pirata (todas as cenas): tudo em UI Toolkit (`GameHud` + Hud.uss; escala com a tela, ref. 1600×900; Kenney UI Pack + fontes Pirata One/Alegreya SDF). Passo 1: status do navio/a pé, porão/mochila, missões, avisos [F], faixas, anúncios e ganhos, rótulos/marcadores/barras no mundo, ajuda dos controles no H. Passo 2: janelas das casas do porto (e abas da cena 05), baú, destroços e menu inicial, com botões de madeira | ✅ aprovado (passos 1 e 2) |
| M6.11 Efeitos visuais, passo 1 (todas as cenas): módulo `Pirates.VFX` (`GameVfx`, só ouve o `CombatEffects` e acompanha os navios): tiro com clarão, faíscas e fumaça que fica no ar e vai com o vento; bala na água com coluna, gotas, anel e espuma; acerto com lascas de madeira 3D, faísca e poeira; rastro de espuma conforme a velocidade (e borrifo na proa); casco avariado solta fumaça (< 60%) e pega fogo (< 30%); naufrágio com espirro, fumaça escura e bolhas. Texturas do Kenney Particle Pack | ✅ aprovado |
| M6.11 Efeitos visuais, passo 2 (todas as cenas): a pé, para jogador e piratas: rastro em fita da lâmina do cutelo (só na parte rápida do golpe), clarão/faíscas/fumaça da pistola e poeira onde a bala bate, estrela e faíscas no acerto (lasquinhas em caixote), poeira dos pés na cor do chão ao correr/pular/cair, respingo na água rasa, poeira do pirata caindo morto e do caixote quebrando, baú destrancado brilhando (e brilho ao abrir), tesouro desenterrado com torrões e ouro; nada a > 70 m da câmera | ✅ aprovado |
| M6.12 Oceano, passo 1 (todas as cenas): costa no shader do mar: água turquesa até ~28 m das ilhas, raso claro perto da areia, espuma na linha d'água (vai e volta com a onda) e faixas de espuma recortadas chegando na praia; mapa por cena feito pelo gerador (altura do chão + distância do litoral), sem textura de profundidade e com a água opaca; ajustes no objeto Ocean | ✅ aprovado |
| M6.12 Oceano, passo 2 (todas as cenas): mar aberto com a crista da onda num verde-água mais claro e espuma recortada no alto das cristas (pela altura da onda, mesmo ruído da costa); reflexo do sol mantido (testado mais fosco e mais liso: pior ou igual); ajustes no objeto Ocean, "Mar aberto (visual)" | ⏳ **aguardando revisão** |
| M7.1 Chefe no mar (cena 07): galeão pirata "Tormenta" do Capitão Lázaro Negro (ship-pirate-large de velas pretas, casco do brigue), mar do sudeste além da Caveira; casco 700, 6 canhões por bordo (16 de dano, recarga 4 s), 9,8 de velocidade, pontaria de elite, nunca foge; com o casco pela metade chama 2 corsários (chalupas pirata ship-pirate-medium, stats/saque das chalupas) que chegam por trás dele; barra do chefe no topo da tela; missão "O galeão Tormenta" na taverna (450 ouro, pólvora, ferro); saque grande com baú garantido (esquema); volta em 300 s ou ao aceitar a missão | ⏳ **aguardando revisão** |
| M8.1 Tripulação, passo 1 (cenas 05 e 07): sobreviventes + marinheiros = tripulação nos beliches (16); bônus proporcional (cheio: recarga −20%, aceleração/giro +15%; zero = navio de antes); tiros no casco matam tripulantes (1 a cada ~40 de dano, sorteado; aviso "Baixas a bordo"); contratar na taverna (30 ouro cada; na 05, aba Carga); linha "Tripulação" no painel do navio; destroços com beliches cheios = "beliches cheios"; F1 com atalhos | ✅ aprovado ("Boa", 04/10) |
| M8.2 Navio novo (cenas 05 e 07): escuna (ship-pirate-medium, 2 mastros) construída no Estaleiro do porto com o esquema "Escuna" (baú raro, peso 3 em todas as tabelas com esquemas) **+ a construção Estaleiro na base** + 500 ouro, 80 madeira, 25 ferro, 3 min de obra (só com o navio na zona do porto); substitui a chalupa no mesmo objeto (casco 150 → 260, canhões 4 → 6 por bordo, porão 30 → 50, beliches 16 → 24, velocidade 11 → 10,5, giro 28 → 24); **upgrades voltam a 0 no navio novo**; nele casco, velocidade e recarga rendem ×1,2 (dano e mira iguais aos da chalupa; custo igual), decidido pelo Jhonata em 04/10; tripulação continua; save guarda a classe e a obra; F1 troca na hora (zera os upgrades); corsários do Tormenta com bandeira vermelha (mesmo modelo da escuna) | ✅ aprovado (04/10) |
| E1 Correções da economia (cena 07; revisão do chat de otimização, aprovadas pelo Jhonata em 04/10): (1) missão repetível descansa 10 min de jogo depois de entregue (`MissionDefinition.cooldownSeconds`, salvo; quadro mostra "volta ao quadro em X min"): acaba o ouro infinito da entrega de pólvora com o Paiol; (2) baú raro não repete esquema que está no porão, mochila **ou armazém** (`Economy/PlayerStorage`); (3) baú do acampamento esvazia e sorteia de novo quando o acampamento é repovoado (trancado de novo depois de aberto) | ✅ aprovado (04/10) |
| M9 Mais coisas no porto (cenas 05 e 07): (1) munição especial: carga de corrente (metade do dano, rasga velas: inimigo até −50% de velocidade) e de metralha (4 balinhas por canhão, 60 m, ×1,5 de dano, abate tripulação: recarga inimiga até +60%), teclas 1/2/3 no navio, uma carga por bordada, ficam a bordo ao desembarcar; (2) carpinteiro a bordo (R): conserta 2/s no mar, 1 madeira a cada 5 de casco, só 8 s sem levar tiro e com 2+ tripulantes; 10 madeiras ficam a bordo ao desembarcar; (3) mercado na praça (banca verde) com demanda (compra até 20 de cada, volta 1 a cada 45 s) e ferreiro na Forja (corrente com a Forja 1, metralha com a Forja 2); (4) nível 4 no Alojamento, Forja e Paiol, Estaleiro 2 (libera o nível 4 dos upgrades, menos a mira) e Fortaleza 2 (mais 2 canhões na muralha, zona segura +45 m) | ⏳ **aguardando revisão** |
| M10 Fama do capitão (cenas 05 e 07): fama por navio afundado (chalupa 6, corsário 8, brigue 25, galeão 80), acampamento (Caveira 20, esconderijo/guarnição 15, posto 5), forte calado (40), tesouro (10) e missão entregue (10 a 120); 5 patentes (Grumete 0, Marujo de Respeito 60, Corsário 200, Capitão Temido 450, Lenda dos Sete Mares 900); cada patente: +10% de ouro nas missões (menos entregas, revisão de 05/10), contratos que pedem patente (Santa Clara/contrabandistas 1, forte 2, Tormenta 3) + 3 novos (Terror das rotas 2, Encomenda para o arsenal 3, O senhor dos mares 4), esquemas à venda no mercado (Estaleiro 1, Forja/Paiol 2, Escuna 3, Fortaleza 3 (era 4, revisão); uma vez) e 1 bônus para escolher entre 3 sorteados (9 bônus: dano, velocidade, casco, mercado, obras, ouro das missões, carpinteiro, porão, tripulação); painel na tecla C (retrato do capitão com o papagaio, nome, patente com estrelas, feitos, barra até a próxima, escada das patentes e o que cada uma libera, bônus), plaquinha no canto direito, faixa de patente nova; nome digitado no Novo jogo (save antigo = "Capitão Barbarossa", fama retroativa pelas missões já entregues) | ✅ aprovado (05/10) |
| A1 Ajustes (05/10, cenas 05 a 07): (1) Forja com tempo: encomenda (paga na hora, 1 por vez, até 10 receitas), o ferreiro trabalha mesmo com o navio longe (corrente 90 s, metralha 120 s por receita; Alojamento e bônus aceleram), fica pronta na Forja e você retira (porão, o resto no armazém); cancelar devolve; aviso e linha na faixa do porto; salva; (2) ração: comer com R a pé (+40 de vida, 8 s entre uma e outra), o capitão leva 5 do porão ao desembarcar, 0,25 de espaço, mercado 6 de ouro (não compra de volta), caixotes/barris/baú do acampamento; mochilas maiores no mercado (couro 25 por 200, patente 1; contrabandista 35 por 500, patente 2; compra única, a maior vale); (3) piratas de terra provocados por tiro/golpe: o acampamento todo vem, persegue até 85 m do centro por 15 s (renova a cada acerto) e sabe onde você está; (4) mira assistida: travada vai na distância exata (antes caía 3 a 5 m antes do casco), antecipação pelo tempo de voo real e, no máximo, cada canhão da salva segue o alvo | ⏳ **aguardando revisão** |
| A2 Cartuchos da pistola (05/10, cenas 05 a 07): cada tiro gasta 1 cartucho da mochila (sem cartucho: clique seco e aviso "Sem cartuchos!"; o cutelo continua), o capitão leva 20 do porão ao desembarcar (60 ficam a bordo ao descarregar), mercado 2 de ouro (não compra de volta), Forja 20 por 1 ferro + 1 pólvora (45 s, Forja 1), barris e baú do acampamento; painel a pé mostra quantos; jogo novo traz 20 cartuchos e 3 rações; piratas da IA atiram sem gastar; animação de comer: gole numa garrafa de rum (Mixamo "Drinking" espelhado, mão direita, ~1,9 s; a arma some da mão e não ataca/atira durante, anda mais devagar) | ⏳ **aguardando revisão** |
| A3 Laser e bacamarte (05/10, cenas 06 e 07): mira a pé vira laser vermelho com brilho até onde a bala bate (ponto no fim; travado = anel dourado no alvo); pistola maior (0,3 → 0,45), corrigida a pistola do cinto (1 cm) e agora deitada na faixa (em pé quase tocava o chão); bacamarte (modelo × 0,6, ~1,7 m: o capitão é cabeçudo) = arma 3 de duas mãos (animações de rifle do Mixamo), 7 chumbos × 14 em leque de 16°, 16 m, recarga 3,5 s; compra única no mercado (350, patente 1), nas costas; munição própria "Cargas de chumbo" (mercado 5, Forja 10 por 1 ferro + 1 pólvora em 60 s, barris/baú; leva 12 ao desembarcar) | ✅ aprovado (05/10) |
| Depois | Progressão (galeão como 3ª classe; tripulação: salário/moral, papéis como carpinteiro e artilheiro, marujos no convés); Conteúdo (novo tipo de inimigo, missões repetíveis, ilha nova); Mundo (dia/noite, clima) |

**Para o Jhonata testar (lembrete pedido em 03/10):** cavar o baú de uma missão de tesouro de verdade (aceitar na taverna, ir até o X,
F para cavar, baú sobe, saquear, missão cumprida e paga no armazém). Pela CLI só o efeito de cavar foi visto (`BuriedTreasure.Dig()`).

Preferências já combinadas: personagens do **Quaternius Pirate Kit** (jogador = Capitão Barbarossa; vilões tubarões e
esqueletos; civis Anne/Henry recoloridos) com as animações do **Starter Assets** da Unity (`Assets/Starter Assets`, revisado;
**não edite arquivos do pacote** e não use os scripts de controle dele; nosso Animator é `_Project/Animation/PlayerCharacter.controller`,
com a camada "Tronco" (Avatar Mask do tronco) para golpe/tiro andando e a "CorpoInteiro" sincronizada para as ações paradas: a máscara do
tronco não leva o quadril, e golpe do Mixamo só com o tronco fica torto); animações de ataque do **Mixamo** (FBX sem skin, auto-importados por
`MixamoImportSettings`); mira a pé no **cursor do mouse** (botão esquerdo ataca, **botão direito segura a mira da pistola**, 1/2 trocam
arma; a pé a câmera gira com **Q/E** ou **botão do meio** + mouse; o botão do meio gira também no navio, onde o direito segue girando — decidido em 02/10 para ter um jeito só; dica na tela no 1º desembarque da sessão); animação pelo Animator com blend tree, parâmetros por hash (skill `unity-animation`); porto é **zona segura**; upgrades levam **tempo de obra** (uma por vez, só avança com o navio
no porto); o Alojamento acelera os trabalhadores (`BaseBuildings.WorkerSpeed` → `HarborDock.BuildSpeed`); construções da
base andam mesmo com o navio longe; mira assistida é upgrade; a pé, morrer = acordar na praia de desembarque (sem perder nada por enquanto); animações
de golpe/morte vêm do Mixamo (o Jhonata baixa); inimigos de terra começam só de cutelo; saque a pé vai para uma
**mochila com limite** que descarrega no porão ao embarcar; o baú do acampamento só abre depois de derrotar os piratas;
recursos em terra = caixotes/barris quebráveis; missões num mundo único (cena 07), pegas no **quadro da taverna**, até 3
ativas, pagamento no armazém; **porto a pé** (cena 07): atracar = desembarcar no píer com o porão descarregando sozinho, e
cada parte do porto numa casa da vila (armazém, estaleiro, mestre de obras, taverna); jogo salvo: só o progresso (ilhas voltam
cheias), menu inicial Continuar/Novo jogo, um save só, salva ao atracar e ao sair do porto; loot com ouro, madeira, pólvora, ferro, sobreviventes e baú raro (20%) com esquemas
de construção (usados na base do M3), num **campo de destroços único** saqueado por escolha (F, navio quase parado,
sem pausar) para um **porão com limite de espaço** (vão entrar muitos itens novos); barco "pesado" (giro ~12,5°/s); mira controla **distância** (20→120 m em 2 s desde 05/10, era 185 m; inimigos e forte também 120, Fortaleza do porto 140; começa
devagar); câmera inclina com botão direito + mouse vertical; visual com **assets grátis CC0 low-poly** (Kenney Pirate Kit,
Quaternius Pirate Kit; texturas/céu do Poly Haven se precisar): o Jhonata baixa, eu reviso antes de entrar em `Assets/`;
a troca começa por navios e objetos (o navio grande do pacote vira o brigue inimigo depois).

## Testar pela Unity CLI

- CLI: `C:\Users\jhona\AppData\Local\Unity\bin\unity.exe` (pode faltar no PATH de shells antigos). Use
  `UNITY_NO_BANNER=1 UNITY_NO_PAGER=1` e `--project-path` com barras normais.
- Pacote `com.unity.pipeline` instalado → `unity command <nome>`: `console_status`, `menu --path`, `editor_play`,
  `editor_stop`, `eval_file`, `capture_game_view`, `set_timescale`, `clear_console`, `delete_asset`.
- Log do editor: `Logs/Editor.log` (linhas antigas de `error CS` ficam lá; confie em `console_status` e na data
  das DLLs em `Library/ScriptAssemblies`). Erro de compilação ao abrir → Safe Mode, CLI não conecta.
- **Play não avança com o Unity fora de foco.** Receita: `eval_file` com
  `EditorApplication.isPaused = true; EditorApplication.Step();` por frame (dt 0,02; `Time.timeScale` 3–4 acelera).
  Dirija o jogo chamando APIs públicas via `eval_file`.
- `eval_file`: sem `using`, tipos totalmente qualificados; evite APIs obsoletas do 6.6 (`FindObjectsSortMode`,
  `GetInstanceID`). Scripts de teste vão no scratchpad, nunca no projeto.
- Teclado simulado (`InputSystem.QueueStateEvent`) não chega com o editor sem foco, nem liberando o foco nas
  InputSettings: teste a lógica chamando as APIs públicas (ex.: `HarborVisitor.Open()`), não as teclas.
- Andar a pé sem teclado: `FootCharacter.SetCommandOverride(new FootCommand { move = ..., run = ... })`, depois `null`
  (`move` é direção no mundo: x = X, y = Z).
- Trocar navio ↔ terra duas vezes **no mesmo frame** é barrado de propósito (a tecla F): nos testes, avance um frame
  entre `Embark()` e `Disembark()`.
- Ilha grande do gerador: tudo é relativo a `BigIslandCenter` (cena 06 em (0,0,-150), mundo em (360,0,-40)); o NavMesh é
  salvo por cena (`<cena>_NavMesh.asset`).
- Classe parcial do gerador: campo `static readonly` que usa outro de **outro arquivo** pode inicializar antes dele (o
  acampamento nasceu no mar). Use propriedade (`static Vector3 X => ...`).
- Tempos de animação: meça no clipe com `clip.SampleAnimation(instânciaTemporária, t)` (em edição o PlayableGraph não move
  os ossos) — ex.: mão mais rápida = impacto do golpe; mão perto do quadril = pegar/guardar a espada. Direção de arma na mão:
  procure no espaço do osso da mão a que satisfaz várias poses (lâmina para fora do corpo no impacto, fio na frente do
  movimento); uma pose só engana.
- Camada sincronizada (`syncedLayerIndex`) começa com os estados **sem clipe**: use `layer.SetOverrideMotion(estado, clipe)`.
- **Não edite scripts com o Play rodando**: o Unity recompila no meio, zera o estado dos scripts (a IA trava) e o teste
  não vale. Pare o Play, edite, recompile e entre de novo.
- `System.Environment.GetEnvironmentVariable` no `eval_file` lê o ambiente do editor, não o do shell: gere um script por caso.
- Depois de teleportar o navio, avance 1 frame antes de `Open()`: `Nearby` (porto/destroços) só atualiza no Update.
- `capture_game_view --source camera` (o modo padrão devolve frame velho quando pausado; não mostra HUD IMGUI).
  Para ver o **HUD IMGUI**: despause o editor (`EditorApplication.isPaused = false` no `eval_file`; o `runInBackground` faz o
  jogo rodar) e use o modo padrão (`source=screen`). O `save_path` cai dentro de `Assets/` → apague a pasta depois com
  `delete_asset --asset <pasta> --confirm true`.
- **Primeiro Play depois de recompilar** (pela CLI, editor sem foco): o IMGUI aparece sem skin (texto preto, sem botões) em
  qualquer cena, até a 06. Não é bug do jogo: o Play seguinte fica normal. Capture HUD só a partir do 2º Play.
- **Jogo salvo nos testes:** fica em `AppData/LocalLow/Jhonata/Pirate RPG/save.json` (o do Jhonata! empresa/produto do Player Settings definem a
  pasta: não mude sem avisar). Antes de
  testar, veja se existe e guarde uma cópia; no fim, restaure (ou apague os saves criados pelo teste).
  **Faça o backup, a restauração e a conferência pelo Unity** (`eval_file` com `System.IO.File.Copy` e um MD5 do arquivo), não pelo shell: o
  shell do Claude pode rodar num sandbox que mostra uma cópia virtual do `AppData/LocalLow` (em 04/10 o shell "restaurou" e mostrava o
  save certo, mas no disco de verdade só havia um save de teste; o backup no scratchpad salvou o progresso). A cena 07 começa no
  menu (jogo parado): chame `GameSave.NewGame()`/`Continue()` pelo `eval_file`. Os scripts de passo que mudam
  `Time.timeScale` passam por cima da pausa do menu.
- Ver um modelo antes de usar: `EditorSceneManager.NewPreviewScene()` + câmera com `camera.scene = cena` renderizando
  para RenderTexture → PNG no scratchpad (não suja a cena aberta). Kenney: pivô embaixo, frente em +Z, 1 unidade = 1 m.
- Ilhas: `IslandSpec` (raios, litoral irregular, `anchors` que fixam o litoral numa direção, `hills`, `paths` de terra batida (só cor), `flats` que fixam
  a altura) → `CreateIslandTerrain` (malha chapada, UV num quadradinho da paleta por triângulo, MeshCollider, asset em
  `Meshes/<ilha>.asset` atualizado no lugar para manter o GUID nas outras cenas). Se mudar posições de prédios, desembarques
  ou acampamento, ajuste os `flats`/`anchors`. Confira alturas com raios só contra o colisor da ilha (palmeiras e pedras
  também têm colisor).
- Canhões no convés: o gerador mede o modelo (`DeckProbe`: raios contra a malha do casco) e põe cada canhão na altura do
  convés que há ali (principal, castelo de proa, popa). `MeshCollider` côncavo **dentro de um Rigidbody que se mexe não
  colide**: para medir, use um objeto à parte, sem Rigidbody.
- O mar (`OceanSurface`) é uma malha em volta da **câmera principal**: uma câmera de teste longe dela vê fundo cinza (no
  jogo a água anda com o jogador). Para fotos longe do navio, mova o navio do jogador ou aceite o fundo.
- `eval_file` logo depois de `menu` (gerar cena) pode dar "Main thread operation timed out": o editor ainda está ocupado;
  repita. O erro fica contado no console (é da CLI, não do jogo).
- **Chão plano de ilha tem de ficar a ≥ 1,3 m**: as quatro ondas somam ~1,1 m e a crista aparece através do chão (manchas
  de "água" no meio da vila com o platô a 0,8 m). A Ilha Sul usa platô 1,4.
- Avisos "JobTempAlloc ... older than 4 frames" / "deleting an allocation older than its permitted lifetime" aparecem quando o
  `eval_file` avança muitos frames com `Step()` numa chamada só (cenas grandes). Com o jogo rodando normal (despausado) somem:
  não é bug do jogo. Loops longos de `Step()` passam dos 5 s do `eval_file`: ~100 passos por chamada.
- Animator dos moradores usa `CullCompletely`: numa câmera de teste longe da principal eles aparecem em pose T (não estão
  sendo animados). Para foto, troque o `cullingMode` no teste.
- **Personagens do Quaternius** (`PrototypeSceneBuilder.Characters.cs`): rig genérico com IK (pé filho do Root, sem mão).
  `HumanoidKitCharacter` adapta a cópia (pé → canela, mão nova no pulso, pose T com o quadril erguido até o pé tocar o
  chão: o repouso do kit é de joelho dobrado e, sem erguer, o Unity calcula a escala pela perna dobrada e ele anda agachado)
  e salva o avatar em `_Project/Characters`. Mapa: Hips = `Body` (pai da coluna e das pernas), Spine = `Hips`. Bainha e
  cinto vão em `Quadril_Ref` (alinhado ao quadril do Starter); empunhadura/bainha/mira medidas em `MeasureCopy(model)`.
  Em edição, `clip.SampleAnimation` humanoide mexe os ossos, mas a malha não atualiza na foto: confira pose em Play.
  Civis usam o rig genérico com os clipes do kit (`Civil.controller` com os do Henry + override por personagem).
- **Porto a pé** (cena 07): `HarborVisitor.menuOnDock` desligado (o F do navio é o `HarborShoreLeave`); `IsDocked` ≠
  `IsOpen` (janela no navio, só na 05). Casas = `HarborBuilding` na frente da porta (filho `Porta` do `House`) +
  `HarborBuildingVisitor` no personagem. Para testar: navio na vaga (`dock.Berth`), avance 3 frames,
  `HarborShoreLeave.Open()`, ande até a entrada e `HarborBuildingVisitor.Open()`. Nos `eval_file`, pegue o navio do
  jogador por `PlayerModeSwitcher.Ship` (`FindAnyObjectByType<ShipController>` às vezes devolve o inimigo).
- **Som** (`Pirates.Audio`, `PrototypeSceneBuilder.Audio.cs`): cada som é um `SoundCue` em `Data/Audio/Som_*` (preservado
  se existe; só ganha clipes se estiver vazio). Importação por pasta no `ThirdPartyImportSettings` (Musica = streaming,
  Ambiente = comprimido em memória, resto = ADPCM; 3D em mono). **Sem AudioMixer**: o Unity não cria mixer por código (skill
  `audio-setup-mixers`); os canais são volumes no `SoundMix` (o Jhonata pode criar um mixer na janela Audio Mixer depois e
  rotear). O AudioListener é o "Ouvido" (`ListenerAnchor`) no alvo da câmera. O projeto tem **32 vozes reais**: prioridade
  por canal (música 0, interface 40, efeitos 128, ambiente 200).
  Testar pela CLI: **com o editor pausado (`Step()`) nada toca** (isPlaying fica falso): despause e leia o `clip` das vozes do
  `SoundPlayer` (filhos `Voz_*`, o clipe fica depois de tocar). Volumes: meça o RMS com `clip.GetData` (só clipes
  descomprimidos; os comprimidos, meça o WAV original). Eu não ouço o jogo: o equilíbrio é pelos números, o ouvido é do Jhonata.
  O Unity não importa FLAC (converter para WAV antes).
- **Transparência da câmera:** `CameraOccluder` (gerador: `Occluder(go)`) + `CameraOcclusionFader` na câmera; o teste é pela
  caixa (bounds + folga) contra raios câmera → pés/cabeça do alvo, sem física. Materiais `Materials/Fade/<nome>_Fade` (cópia
  transparente de cada URP Lit, regerada no build). No editor, a primeira vez que um material fica translúcido aparece
  **ciano** por um instante (o Unity compilando a variante do shader): espere alguns segundos antes de fotografar.
- **HUD (UI Toolkit, M6.10):** um `GameHud` (UIDocument + `_Project/UI/HudPanelSettings`) por cena, criado no gerador
  (`CreateGameHud`, `PrototypeSceneBuilder.Hud.cs`); cada HUD (ShipDebugHUD, FootHUD, MissionHUD, LootHUD, LandLootHUD,
  HarborHUD, HealthBarsHUD, EnemyStateLabels, toast do SaveMenuHUD) monta as peças no primeiro Update nas regiões do
  `GameHud` (TopLeft/TopRight/TopCenter/Prompts/Banners/World) e só mexe em texto quando muda (`Hud.Set` + `CachedText`).
  Peças em `HudElements.cs` (`HudBar`, `HudPrompt`, `HudBanner`, `WorldLabelPool`) e `CargoCard.cs`; visual em
  `_Project/UI/Hud.uss`. Desligar um HUD (FootHUD desliga os de navio em terra) esconde as peças dele (OnDisable).
  Janelas: `HudWindow` (pergaminho na camada `GameHud.Windows`, recebe o mouse) feita de `Pane`s que se remontam só quando a
  assinatura dos dados muda (`Win.Mix`/`Win.Of(inventário)`; obra = segundos inteiros); `HarborWindow` (casas do porto e abas
  da 05), `LootWindow` (baú e destroços), menu inicial na camada `GameHud.Menu`. Botões com `focusable = false` (senão
  Espaço/Enter "clicam" o botão focado). O Unity 6 liga o UI Toolkit ao Input System sozinho (InputForUI), sem EventSystem.
  Nenhum IMGUI sobrou no módulo UI (só F1/F3). Margens: o reset `.unity-label { margin: 0 }` fica no topo do .uss com a
  mesma força da regra do tema; `.hud Label` (classe + tipo) anulava as margens das classes. Editar o .uss com Play rodando
  vale para peças criadas depois (reimporte e reabra a janela).
  **Testar cliques sem mouse:** ache o `Button` pelo texto (`UQueryExtensions.Query<Button>(raiz)`: no `eval_file` extensão
  só com chamada estática), confira `panel.Pick(button.worldBound.center)` (prova que o clique chega nele) e chame o campo
  privado `Clickable.clicked` por reflexão.
  `capture_game_view` no modo padrão (screen) mostra o UI Toolkit; `--source camera` não. Para conferir posição sem foto:
  percorra `GameHud.Current.World.Children()` (layout em unidades do painel). Imagens de `ThirdParty/UI` entram com
  filtro bilinear (o resto de ThirdParty é paleta sem filtro). Fontes SDF dinâmicas (`FontAsset.CreateFontAsset`, 64 pt,
  atlas 1024, acentos pré-gerados); negrito/itálico ligados pelo `fontWeightTable` da Alegreya.
- **Efeitos visuais (M6.11):** `GameVfx` (objeto "VFX", `PrototypeSceneBuilder.Vfx.cs`, todas as cenas) monta 12 ParticleSystems
  em espaço de mundo (`FxSystem`: só `Emit`, nada é criado por tiro) e acompanha os navios (busca a cada 2 s; medidas do casco pelo
  colisor). `CombatEffects` (Combat) agora só avisa: `Blasted(boca, direção)`, `Splashed(ponto)`, `Impacted(ponto, normal)`; o
  Audio e o VFX ouvem. Materiais em `Materials/VFX` (refeitos a cada build): aditivo só para clarão/brilho; **fogo com alfa**
  (aditivo laranja sumia em cima do casco claro) e textura `flame_05` em pé (`Style.upright`); fumaça cinza-média (preta some no mar
  escuro visto de cima); pontos de fogo perto das bordas do convés (o meio fica embaixo das velas). Sem partículas suaves (o O1
  desligou a textura de profundidade). Texturas em `ThirdParty/VFX` entram com filtro, mipmap e 256 px.
  **Testar:** tiro de verdade (`CannonBattery.FireAtRange`) põe a câmera na mira, afastada; para ver o efeito, chame
  `CombatEffects.MuzzleBlast/Splash/Impact` direto e capture com o editor pausado (`--source camera`) para pegar o clarão
  (dura ~0,2 s). Contagem por sistema: filhos do objeto "VFX" (`ParticleSystem.particleCount`). Custo medido: ~0,1 ms de CPU, sem lixo.
  **A pé (passo 2, `GameVfx.Foot.cs`):** ouve `CharacterCombat.Attacked/Fired/Hit` de cada `FootCharacter` (busca a cada 2 s, inclui
  desligados); rastro do cutelo = `BladeTrail` (malha refeita só com rastro, lâmina medida pelo visual da arma via
  `CharacterCombat.WeaponVisual(i)`: eixo mais comprido, ponta = lado mais longe do pivô); cor da poeira pela paleta do chão (mesma
  leitura do som de passos). Personagem desligado ou dormindo (O3) e tudo a > 70 m da câmera é pulado. Para fotos de perto, uma câmera
  de teste temporária renderizando para RenderTexture → PNG no scratchpad (a câmera do jogo fica longe). `FootCommand.selectWeapon`
  pelo override não troca de arma no teste: chame `CharacterCombat.Equip(i)`. Custo com ~30 personagens: ~0,17 ms de CPU no total, sem lixo.
- **Chefe no mar (M7.1)** (`PrototypeSceneBuilder.Boss.cs`): `ShipBoss` (Combat) no navio com `EnemyShipAI`: nome/capitão para a
  `BossBarHUD` (UI, faixa do topo, só com o chefe em Chase/Broadside) e escolta. A escolta são cópias de um molde desligado
  ("ChefeEscolta/Corsario", fora do mapa), feitas no `Start` do chefe (início da cena ou reaparição, fora de combate) e só ligadas
  quando o casco passa de `escortAt` (sem Instantiate na luta); aparecem `escortDistance` atrás do chefe (lado oposto ao alvo),
  testando terra em volta. Escolta não usada some com o chefe (`OnDestroy`). O molde do respawner (cópia do galeão) aponta para o
  mesmo molde da escolta. Stats/saque em `Data/ShipStats_Galeao`, `CannonStats_Galeao`, `LootTable_Galeao` (criados a partir dos do
  brigue, preservados). **Testar:** `ShipBoss.Active[0].Health.TakeDamage(...)` até < 50% liga a escolta; o baú do chefe só sorteia o
  esquema ao abrir (`ShipWreck.HasClosedChest`). Rótulo "Corsario" vale para achar as escoltas (`EnemyShipAI` com Include).
  **Cuidado na CLI:** se `editor_play` falhar, os scripts de teste rodam na edição (o `NewGame` apagou o save.json uma vez): confira
  `EditorApplication.isPlaying` antes de qualquer script que mexe no jogo.
- **Mira da IA dos navios (04/10, pedido do Jhonata: "girando em volta, nunca me acertam"):** a bala sai perpendicular ao casco e
  herda a velocidade de quem atira. `EnemyShipAI.LeadOffset` = onde o alvo vai estar quando a bala cair (velocidade do alvo − a
  nossa, × `CannonBattery.FlightTime(dist)`, o tempo de voo de verdade com a elevação, 3 voltas); a IA vira o través para esse ponto
  (leme firme na troca de tiros: `broadsideRudderDegrees` = 6°; com 25° ficava ~12° atrás de quem gira e nunca alinhava) e só dispara
  com o ponto dentro de `atan(targetHalfLength / dist) + espalhamento` (máx. `fireArcDegrees`). Dificuldade = `leadError` (erro da
  antecipação, sorteado por disparo) + `rangeError`: chalupas 0,35/0,1, brigue 0,2/0,04, galeão 0,12/0,03. Teste (alvo girando a
  8 m/s, 70 m, 60 s): antes 12 bordadas e 0 de dano; agora chalupa ~380 de dano em velocidade constante e ~130 variando a velocidade,
  galeão ~640 e ~260 (manobrar e mudar a vela ainda salva). `FortressCannon` usa o mesmo `FlightTime`. Script de teste: fixar o navio
  do jogador num círculo (posição + `linearVelocity` por `Step()`) e curar a cada quadro somando o dano; `EnemyShipAI.AimError`/
  `AimTolerance` mostram o alinhamento.
  **De perto (05/10, "fica do lado dela e ela quase não ataca"):** o `BroadsideHeading` dobrava a proa até 35° para abrir distância
  (alvo < `preferredRange` 70 m) e o través nunca alinhava (1 bordada/min colado a 40 m). Agora só corrige a distância recarregando
  (`KeepRange`: bordo daquele lado com mais de `aimLeadTime` = 2 s de recarga); carregado, aponta o través direto. Colado a 40 m: 11
  bordadas e ~490 de dano por minuto (25 m: ~460). Teste: `side.sh` no scratchpad (jogador colado ao lado, paralelo ou parado).
- **Tripulação (M8.1)** (`Progression/ShipCrew` + `CrewSettings`, gerador em `PrototypeSceneBuilder.Upgrades.cs`): os tripulantes são o item
  `Item_Sobreviventes` no porão (o save já guarda; categoria Crew, não ocupa espaço, não desembarca). `Inventory.SetLimit(item, máx.)` = beliches
  (`MaxFit` respeita; `SetContents` não corta o que já estava salvo). `ShipCrew` reconta pelo `Inventory.Version` e dispara `Changed`;
  o `ShipUpgrades` reaplica as cópias dos stats nesse evento (não por quadro): recarga × (1 − bônus × fração), aceleração e giro × (1 + bônus ×
  fração). Baixas pelo `Health.Damaged` (dano / `damagePerCasualty`, parte quebrada sorteada); o `ShipDebugHUD` soma as baixas por ~1 s num
  aviso só. Contratar: `HarborVisitor.Recruit/RecruitBlockReason/RecruitAffordable` (paga do armazém e depois do porão).
  **Testar:** `hold.Add(crew.CrewItem, n)` e avance 1 quadro (a contagem atualiza no Update); `Health.TakeDamage` para as baixas.
- **Navio novo (M8.2)** (`Progression/ShipClassDefinition` + `PlayerShipClass`, gerador em `PrototypeSceneBuilder.Ships.cs`): uma classe =
  `Data/Ships/ShipClass_*` (stats, canhões, casco, porão, beliches, `tier`, esquema, custo, obra). O navio do jogador tem um **rig por classe**
  no mesmo objeto: a chalupa usa o "Visual"/"Cannons" que a cena já tinha; a escuna é "Visual_Escuna"/"Cannons_Escuna" (desligados). `SetClass`
  liga as peças do casco novo, troca colisor/massa/inércia/pontos de flutuação (`Buoyancy.SetFloatPoints`), velas (`SailVisual.Configure`),
  porão, beliches (`ShipCrew.SetBerths`) e os valores base dos upgrades (`ShipUpgrades.SetBase`), e dispara `ShipController.HullChanged`:
  quem guardou peças do navio busca de novo (`PlayerCannonInput`, que também confere no `OnEnable` porque fica desligado em terra;
  `ShipDebugHUD` remonta; `GameVfx` mede de novo). A obra é a mesma do estaleiro (`HarborDock.ConstructionJob.shipClass`, `Title`); o save
  guarda `shipClass` (aplicado antes dos upgrades e da vida) e a obra pelo nome do asset. Escoltas do chefe usam o mesmo modelo
  (ship-pirate-medium), por isso têm bandeira vermelha (`M_Bandeira_Corsario` nos `flag*` do molde, `PrototypeSceneBuilder.Boss.cs`). Upgrades por navio: `SetClass(classe, newShip: true)` (estaleiro, F1) zera os níveis
  (`ShipUpgrades.ResetLevels`) antes de trocar; `newShip: false` (carregar o jogo) não. `ShipUpgrades.SetBase(classe)` guarda a classe: `Total`
  multiplica pelo multiplicador do tipo (`ShipClassDefinition.UpgradeMultiplier`: casco, dano, velocidade, recarga, mira);
  `NextCost`/`NextBuildSeconds` pelo `upgradeCostMultiplier` (o `HarborVisitor` cobra e a janela mostra por eles). Balanceamento
  (chat de otimização, nível 3): casco 440 × 300, dano por segundo ~1,7× (base 1,5×), recarga 2,3 × 2,6 s, velocidade 13,65 × 13,75. A classe exige `requiredBuilding` (Estaleiro) na base.
  Save de antes da regra (escuna com níveis herdados da chalupa) carrega os níveis como estão, já rendendo os multiplicadores da escuna.
  **Testar:** esquema no armazém (`GameSave.FindItem("Esquema_Escuna")`), `HarborVisitor.BuyShip`, `HarborDock.FinishNow()`; ou F1 → Navio.
  Depois de gerar, as classes em memória ficavam apontando para ShipStats/CannonStats destruídos (o arquivo certo): o gerador agora
  descarrega as `ShipClass_*` (`ReloadShipClasses`) **antes** de reabrir a cena (depois quebra a referência da cena: a escuna virou null).
- **Economia (E1):** `MissionBoard` guarda o descanso das repetíveis (`Cooldowns`, tempo de jogo: parado no menu não anda; `RestingKey()`
  para o quadro sem alocar) e o save leva `missionCooldowns` (missionId + segundos). `PlayerStorage` (Economy, estático, zerado no
  `SubsystemRegistration`): porão (`WreckSalvager`), mochila e `knownItems` (`LandLooter`) e armazém (`HarborDock`) se registram no **Awake**
  (não no OnEnable: em terra o navio fica desligado) e saem no OnDestroy; `LootTable.IsAvailable` consulta. `LandChest`: trancado de novo
  depois de aberto = `ResetChest` (vale para qualquer baú com tranca: acampamentos, guarnição do forte, contrabandistas).
- **Mais coisas no porto (M9)** (`PrototypeSceneBuilder.Supplies.cs`; números em `Data/Ammo`, `CrewSettings_Navio` e `HarborSettings_Base`):
  munição = `AmmoDefinition` (Combat) na `CannonBattery.Ammo` (a salva leva a munição; `MaxRange` corta o alcance da carga; metralha =
  `pellets` projéteis por canhão, só o primeiro faz respingo e os acertos de balinha têm efeito com intervalo de 0,06 s). `DamageInfo`
  ganhou `sailDamage`/`crewDamage`; `ShipDebuffs` (só nos inimigos, gerador em `CreateEnemy`) soma e aplica em `ShipController.SpeedMultiplier`
  e `CannonBattery.ReloadMultiplier` (não salvo: cópia do respawner nasce limpa). `ShipAmmo` (Progression) escolhe, põe nas baterias
  (de novo no `HullChanged`) e tira uma carga no `Fired`; acabou = comum na hora (as duas baterias no mesmo quadro). `ShipCarpenter` usa o
  `Health.LastDamageTime`. Teclas no `PlayerShipExtrasInput` (Controls → Progression; 1/2 são as mesmas ações das armas a pé, R = `Repair`).
  `ItemDefinition.keepAboard`: o "descarregar tudo" (e o desembarque no porto) deixa essa quantidade a bordo (munição 9999, madeira 10).
  Mercado e ferreiro = `HarborVisitor.Buy/Sell/Craft` (paga do armazém e depois do porão; munição vai para o porão até caber). A demanda
  (`HarborDock.Demand/UseDemand`) é salva (`SaveData.marketDemand`, unidades restantes por item; ausente = cheia): sem isso, sair e
  voltar enchia de novo (+260 de ouro por recarga, achado do chat de otimização). Munição não é recomprada (fazer e revender daria lucro). Portas:
  `MarketStall` na banca verde da praça (vendedor atrás, em `serviceDoors`) e `SmithyDoor` dentro do grupo `Forja_N1` (só com a Forja
  construída). Nível 4: `AddLevelFour`/`AddUpgradeLevel` só crescem assets ainda do tamanho antigo (rodar de novo não empilha níveis);
  upgrade nível 4 = `requiredBuildingLevel` 2 do Estaleiro; Unlock do Estaleiro: valor = nível dos upgrades liberado (0 antigo = 3).
  Fortaleza: anéis de boias por nível em `BuildingVisual.exactLevel` (só o do nível atual aparece). Revisão por subagente (05/10) achou 4
  problemas (descarregar levava munição/madeira; última carga dava bordada especial de graça; metralha sem efeito de acerto; preço da
  munição igual ao de fazer), corrigidos e testados.
  Decisões do Jhonata (05/10): recarga mínima do navio do jogador 2,5 s (`ShipClassDefinition.minReloadSeconds`, no `ShipUpgrades.ReloadTime`)
  e estragos que se recuperam (`ShipDebuffs`: 10 s sem acerto daquele tipo, depois 3%/s). O teto vale para tudo (upgrades + tripulação);
  o que ele corta vira precisão (`ShipUpgrades.CappedSpreadReduction` = 1 − recarga sem teto / 2,5, máx. 40%, × a mira assistida):
  chalupa A3 + tripulação cheia −17%, A4 −26%, escuna A4 −37%; a loja mostra "recarga 2,5 s (limite) · espalhamento −X%". Baixas na
  tripulação tiram precisão junto.
  Ajustes pedidos (05/10): HUD da munição em fichas uma embaixo da outra (tecla + nome + cargas; `.hud-key.hud-ammo__key` em classe dupla
  porque o `.hud-key` vem depois no .uss; a fonte não tem "∞", a comum diz "sem limite"), aviso "Nova munição a bordo: aperte N" na
  primeira carga e "Canhões com X" ao trocar; bala de corrente com visual próprio (`AmmoDefinition.chainVisual`: duas meias-balas e a
  corrente girando 900°/s no plano do voo, pool próprio no `CannonballSystem`); porão chalupa 30 → 45 e escuna 50 → 75; carpinteiro
  descansa `carpenterCooldownSeconds` (90 s) depois de consertar algo (ligar e desligar sem consertar não conta; R no descanso avisa).
  **Testar:** cargas no porão + `ShipAmmo.Select`, `CannonBattery.FireAtRange` com os dois navios presos no lugar a cada `Step()` (soltos,
  o coice e as ondas afastam e o teste não vale); carpinteiro dentro da zona segura do porto (fora, outros inimigos afundam o navio no
  teste); portas: `HarborBuilding` de serviço `Market`/`Smithy` + `HarborBuildingVisitor.Open()`.
- **Fama do capitão (M10)** (`Progression/CaptainFame` + `FameSettings`, gerador em `PrototypeSceneBuilder.Fame.cs`; números em
  `Data/FameSettings_Capitao`, `fameReward`/`requiredRank` de cada missão e o `fame` de `EnemyShipAI`/`EnemyCamp`/`EnemyFort`/`BuriedTreasure`):
  `CaptainFame` fica no objeto "FamaDoCapitao" (sempre ligado: o navio desliga em terra) e ouve o `EventBus` (os eventos ganharam `fame`;
  `MissionTurnedInEvent` novo, publicado pelo `MissionBoard.TurnIn`). Quem não conhece a Progression usa `Core/ICaptainRank` (Services: o
  quadro filtra por `requiredRank`, `Locked()` mostra os trancados, `RewardAmount` multiplica o `bonusItem` = ouro). Bônus: `CaptainFame.Bonus(PerkEffect)`
  + evento `PerksChanged` (ShipUpgrades: dano/velocidade/casco; PlayerShipClass: porão; BaseBuildings: obras; HarborVisitor: `BuyPrice`/`SellPrice`;
  ShipCarpenter; ShipCrew). Mercado: `MarketEntry.requiredRank`; esquema = 1 unidade, "você já tem" pelo `PlayerStorage`. Save: `hasFame`,
  `captainName`, `fame`, `perks`/`perkOffer` (ids), feitos; save sem `hasFame` = soma do `fameReward` das entregues (no mínimo a fama da
  patente da missão mais difícil já entregue: nada que já foi feito fica trancado). O save do Jhonata (05/10) entra como Marujo de Respeito (75).
  Painel: `UI/FameHUD` (tecla C = `GameInput.Fame`, Esc fecha; `Core/ScreenPanel.Open` faz o input a pé não atacar com o painel aberto; os avisos
  de ação somem com ele aberto); retrato `UI/Retrato_Capitao.png` feito pelo gerador (cena de prévia, fundo preto e branco para o recorte,
  céu/mar pintado; enquadramento em `PortraitFrame/Yaw/LookUp/Shift`). Menu: Novo jogo → nome (`TextField`, 24 letras; `GameHud.TypingText`
  desliga o H) → `GameSave.NewGame(nome)`. F1: +50/+200/zerar fama.
  **Equilíbrio (revisão do chat de otimização, aprovada em 05/10):** `MissionBoard.RewardAmount(missão, item)` não multiplica o ouro das
  **entregas** (patente + "Nome que vale ouro" davam lucro comprando a carga no mercado ou com a Forja/Paiol); "Encomenda para o arsenal"
  250 de ouro e 1800 s de descanso (era 420 e 600: ~3.300–4.000 ouro/h sem sair do porto); `OnShipSunk` conta cada navio para **uma**
  missão de afundar sem alvo (a aceita primeiro; com Caça + Terror + Senhor ativas, 8 chalupas pagavam ~2.290 de ouro); as com alvo contam
  o alvo à parte. Esquema da Fortaleza na patente 3. Ritmo estimado: ~3–4 h até a patente 4; chalupas sozinhas ~240 fama/h.
  **Testar:** `CaptainFame.Current.Add(n, "x")` (sobe a patente e mostra a faixa), `Restore(nome, fama, ids, oferta, stats)` para pôr bônus,
  `FameHUD.Toggle()` + `capture_game_view` (modo screen, despausado); `SaveMenuHUD.StartNewGame(nome)` (arquiva o save: backup antes!).
  **Cena 05 ≠ 07:** a 05 é o porto com a janela em abas no navio; desembarcar e ir nas casas é só na 07. Depois de testar, deixe o editor
  na cena 07 (o Jhonata abriu a 05 em 05/10 e achou que o porto a pé tinha sumido).
- **Ajustes A1 (05/10):** Forja = `HarborDock.SmithyOrder` (`StartSmithy`/`TakeSmithy`/`CancelSmithy`, anda no Update pelo `BuildSpeed`,
  evento `SmithyReady`), `HarborVisitor.Craft` encomenda e `CollectCraft` retira (evento `Crafted` = retirou, `CraftOrdered` = encomendou),
  `Recipe.craftSeconds`, save `smithyOrder` (id = item do resultado). Ração = `Item_Racao` + `Controls/FoodEater` no personagem (R = a ação
  `Repair`, que no navio é o carpinteiro; o personagem só liga em terra), gerador `PrototypeSceneBuilder.Food.cs`. IA de terra:
  `EnemyCamp.Provoke` (`provokedSeconds`, `provokedLeash`, `EffectiveLeash`), chamado em todo dano num pirata. Mira: `CannonBattery.LockCloseness`
  (travada puxa inteira), lead com `FlightTime`, `FireSalvo(follow)` gira cada canhão pelo quanto o alvo andou desde o primeiro tiro.
  Mochilas = itens `ItemCategory.Gear` (únicos, como esquema) com `backpackCapacity` (`Equip_Mochila*`), guardados no armazém (o save leva);
  `Controls/BackpackGear` no personagem usa a maior que o `PlayerStorage` achar (lona = 15); o mercado não vende a menor para quem tem a maior.
  Revisão de equilíbrio (chat de otimização, aprovada 05/10): ração com 8 s de espera e 0,25 de espaço; mochilas 25/35.
  **Testar a mira:** a IA do alvo tem de ficar ligada (desligada sai do `EnemyShipAI.Active` e a assistência não vê); alvo se cura a cada quadro;
  ponha os navios no lugar depois de a bateria recarregar (a trava leva ~1 a 4 s: navio cruzando sai dos 12° de giro). Medido (luneta
  máx., escuna, 6 canhões): parado 7–10 de 12 balas a 40–100 m (antes 4 de 12 a 100 m); cruzando a 6 m/s a 70 m, ~4 de 6 por salva.
  Cartuchos (A2): `OnFoot/IAmmoSupply` (`CharacterCombat.SetAmmo`; sem ele a arma de fogo não gasta: IA) + `Controls/PistolAmmo` no
  personagem (`startingCartridges` só nas cenas sem save; no mundo o `GameSave.newGameKit` dá 20 cartuchos e 3 rações ao jogo novo).
  Comer (A2): clipe `Animation/Mixamo/Pirata@Drinking.fbx` (o Jhonata baixou, personagem Pirate, sem malha), importado **espelhado**
  (`MixamoImportSettings`: o gole é com a esquerda, que no Barbarossa é o gancho, do lado do papagaio, e sumia); camada "Gole" por cima de
  tudo com máscara braço direito + cabeça (`BracoDireito_Cabeca.mask`), só o trecho 2,0 → 6,1 s × 2,15; trigger `Eat`
  (`CharacterAnimatorDriver.PlayEat`). Garrafa = `bottle` do Kenney no `Hand.R` ("Garrafa", giro medido em Play: `BottleEuler`), ligada pelo
  `FoodEater` durante `eatSeconds`, que também esconde os renderers da arma na mão e chama `CharacterCombat.Interrupt`. Papagaio no ombro (05/10,
  pedido do Jhonata): no kit ele é a malha "Ernest" 100% no antebraço do gancho (ficava pendurado com as animações a pé); o gerador
  (`PrototypeSceneBuilder.Parrot.cs`) desliga essa malha e põe uma cópia fixa (`Meshes/Papagaio_Ernest`) no osso `Shoulder.L`, na
  posição da pose parada do kit + `ParrotNudge` (medido em Play). Só o capitão do jogador; o retrato da fama usa o kit original.
  **Foto de teste em Play:** depois de mexer num objeto, avance 1 quadro (`Step`) antes de renderizar (o GPU Resident Drawer só atualiza no quadro).
  Descartar (05/10, revisão do chat de otimização aprovada): janela do Armazém (e aba Carga da 05), botão "Descartar itens…" →
  −1/−10/Tudo só no que o mercado não compra (`HarborWindow.CanDiscard`: sem entrada ou `sellPrice` 0; nunca tripulação, ouro, equipamento;
  esquema só repetido no porão); "Tudo" e esquema pedem "Confirmar?" (3 s). Mochila: o "devolver" da janela de saque já descarta.
  Ao carregar, `GameSave.RemoveDuplicateSchematics` tira do porão/mochila o esquema que o armazém já tem (o save do Jhonata tinha
  Paiol e Fortaleza repetidos no porão).
  **Laser e bacamarte (A3, 05/10):** laser = `CharacterAimRig` (jogador: `reticle` + `laserGlow`; raio da boca na direção do `AimTarget` até o
  alcance da arma, pula o próprio personagem; ponto = anel pequeno e grosso, travado = anel dourado no alvo; inimigo continua com a linha
  vermelha de aviso). Cada arma de fogo tem a sua boca (`CharacterCombat.SetMuzzles`/`CurrentMuzzle`; o rig usa a da arma na mão).
  Bacamarte (`PrototypeSceneBuilder.Blunderbuss.cs`): `WeaponDefinition.pellets`/`pelletSpread` (o dano dos chumbos no mesmo alvo vira um
  golpe só; `PelletLanded` para as faíscas em leque na boca e a poeira/fumaça onde cada chumbo parou; `Fired` uma vez; cada chumbo tem
  um risco que voa da boca até onde parou em 0,25 s, `CharacterCombat.UpdatePelletTracers`, cópias do rastro criadas no 1º tiro e
  reaproveitadas, sem alocar por tiro), `twoHanded` (o rig sobe/desce pelo peito, não pelo braço, e
  usa a pose de mira de rifle medida pela linha mão direita → esquerda: `ConfigureTwoHanded`). WeaponType 2 no Animator (`AddFirearmStates`:
  parado/mirando/tiro/recarga/correndo, com reserva nas da pistola). Clipes `Pirata@Rifle Aiming Idle/Firing Rifle/Reloading/Rifle Run`
  + `Rifle Idle` (o Jhonata baixou; o resto do download ficou em `_Downloads`). Modelo `Weapon_Rifle` do Quaternius × 0,6 (0,4 sumia
  na câmera); nas costas na diagonal (coronha a ~15 cm do chão). Pistola guardada deitada na faixa (cano para trás; ~30 cm do chão). Arma trancada até comprar: `CharacterCombat.SetAvailable` + `Controls/GearWeapons` (item `Equip_Bacamarte` no
  `PlayerStorage`); trancada não saca, o "próxima arma" pula e o FootHUD esconde. Munição por arma: `PistolAmmo.ConfigureExtra`
  (`AmmoItem(arma)`; eventos `Empty`/`Packed` levam o item). **Recarga só com a arma na mão** (pedido do Jhonata: trocar bacamarte ↔ pistola
  recarregava de graça): guardar no meio da recarga guarda o que faltava (`reloadPaused`, `reloadUntil` = infinito), sacar continua de onde
  parou (`PauseReload` no início da troca, `ResumeReload` no `TakeInHand`); o painel a pé marca "(vazia)" na arma guardada sem bala
  (`CharacterCombat.IsEmpty`).
  **Lugar de guardar criado no mundo e posto num osso precisa de `MatchHandScale`:** os ossos do kit têm escala 100 e a arma muda de lugar
  mantendo a escala local; o cinto ficava com escala 1 e a pistola guardada media 1 cm (desde o M4.3).
  **Restaurar o save depois de testar:** parar o Play com o navio atracado salva sozinho (OnApplicationQuit) e a gravação pode chegar
  depois da restauração (05/10: uma mochila de teste ficou no save). Pare, espere uns segundos, restaure e confira o MD5 de novo.
  **Não edite scripts com o Jhonata em Play** (05/10: ele estava jogando, o Unity recompilou no meio): confira `isPlaying` antes de cada edição.
- **Ilhas Oeste/Norte** (`PrototypeSceneBuilder.Isles.cs`): centros em propriedades (`WestCenter`, `NorthCenter`), NavMesh de
  cada uma (`BakeNavMeshVolume`, `<cena>_NavMesh_Oeste/_Norte`). Forte: `EnemyFort` é o `IDamageable` dos colisores (filtra:
  só `DamageInfo.source` com `ShipController`) e a `Health` fica num filho sem colisor; `FortressCannon.TargetPlayer(navio)`
  mira no jogador e respeita `SafeZone`. Bala de navio contra alvo em platô: mirando na distância exata ela bate no chão
  antes (o alcance é medido no nível do mar) — por isso o pátio do forte é baixo (2,2 m). Missão com texto próprio:
  `MissionDefinition.objectiveText`/`doneText` (vazio = o do tipo).
- **Caveira** (`PrototypeSceneBuilder.Skull.cs`): tudo relativo a `SkullCenter` (= `BigIslandCenter` 52 m ao sul; a praia
  norte fica em +104). `flats` em cima de morro: a altura do platô tem de ser a que o morro já tem naquele raio; mais alto
  vira degrau e o NavMesh não sobe (aconteceu nas ruínas). Confira caminhos com `NavMesh.CalculatePath` (praia → cada posto).
- Desempenho no editor fora de foco: `smoothDeltaTime` fica preso em ~30–40 ms (o editor desacelera); para comparar, cronometre
  40 `Step()` com `Stopwatch` (porto ~29 ms, mata da Caveira ~24, sem a ilha ~19).
- `Buoyancy`: o centro de massa tem de ficar **abaixo** dos pontos de flutuação, senão o objeto capota e boia de cabeça
  para baixo (aconteceu com o barril-alvo novo).
- Inércia do navio é fixa no gerador (`automaticInertiaTensor = false`, a do casco antigo): mudar o colisor não muda o
  balanço aprovado.
- Burst está desligado neste editor (o Smart App Control do Windows bloqueia as DLLs dele). `runInBackground` ligado.
