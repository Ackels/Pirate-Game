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
6. **Git / GitHub (05/10):** backup em https://github.com/Ackels/Pirate-Game (**privado**: o Mixamo e o Starter Assets não permitem
   distribuir os arquivos soltos), branch `main`. Fora do git: `Library`, `Temp`, `Logs`, `UserSettings`, arquivos de IDE e `_Downloads`
   (`.gitignore`); `.gitattributes` = `* -text` (sem conversão de fim de linha) + binários. Commit e push só quando o Jhonata pedir.

## Arquitetura (resumo)

Um **assembly (.asmdef) por módulo** em `Assets/_Project/Scripts/`, sem dependências circulares:
`Core` (Services, EventBus + GameEvents, IOceanSampler, IDamageable, ICameraAimFocus, IInteraction, ILockSource) ← `Ocean`, `Ships` ← `Combat` ←
`Controls` (→ Base: atracar e desembarcar) ← `CameraSystem`; `Economy` (ItemDefinition, Inventory) ← `Loot`, `Progression` (upgrades) ← `Base` (porto);
`Ambient` (→ Core: vida do cenário só visual, barcos ancorados, gaivotas, caranguejos, moradores, fogo; ciclo de dia e noite + lanternas, publica `ITimeOfDay`);
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
  **Anti-aliasing (09/10, pedido do Jhonata: "serrilhado"): MSAA 2×** no `PC_RPAsset` (`m_MSAA: 2`; `QualitySettings` PC `antiAliasing: 2` só
  para combinar, o URP usa o do asset; Mobile continua sem). Medido no editor, mesmo quadro em RenderTexture 1366×768 (MSAA na RT igual ao
  da tela), mediana de 3 rodadas: desligado × MSAA 2× × MSAA 4× × FXAA × SMAA = mar 6,6/8,5/8,8/8,3/9,1 ms, Caveira a pé 10,1/12,1/12,9/
  12,8/13,6, vila 8,3/10,1/11,0/10,3/10,8 → **MSAA 2× ≈ +2 ms** (CPU do editor + GPU), o mais barato junto com o FXAA; na foto ampliada o
  MSAA 2× alisa as bordas sem borrar, o FXAA borra as texturas (e liga o pós-processamento da câmera). No jogo compilado o limite é a CPU
  (GPU ~2,5 ms na vila) e o VSync trava em 60: não deve mudar o FPS. Build de desenvolvimento com MSAA 2×: 0 erros, `Player.log` sem
  "Exception". "Serrilhado" no editor também vem da janela Game com "Low Resolution Aspect Ratios" e o Windows em 125% (desenha 80% dos pixels).
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
  - `Mathf.Min`/`Mathf.Max` com 3+ argumentos cai no `params float[]` e cria um array a cada chamada: em código por quadro, aninhe
    (`Mathf.Min(a, Mathf.Min(b, c))`). Achado no `WindStreaks` (07/10, ~344 B/quadro).
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
| O7 Otimização (7): jogo compilado medido em tela cheia 1366×768: ~160 FPS na vila, ~200 no mar, ~185 contra o chefe; corrigido erro do build (SSAO desativado no `PC_Renderer` fazia o URP falhar a cada quadro; agora removido); revisão da economia entregue (correções 1–3 feitas) | ✅ aprovado (05/10) |
| M6.8 Som e música (todas as cenas): módulo `Pirates.Audio` só ouvindo eventos (canhões, navio, combate a pé, passos por tipo de chão, porto, missões, saque, jogo salvo), música porto/mar/ilha/combate com fade, ambiente (ondas nas praias, gaivotas, fogo, pássaros), ouvido no alvo da câmera, volumes por canal no F1, pacotes CC0 em `ThirdParty/Audio` | ✅ aprovado |
| M6.9 Ilhas Oeste e Norte 2× (mata em todas as cenas; conteúdo na 07): Oeste = esconderijo de contrabandistas (palafitas, cais, chalupa ancorada, 4 piratas, baú do contrabando), tesouro do Zé numa gruta de pedras, segundo tesouro na praia noroeste; Norte = forte inimigo na costa sul (muralha, torre, 4 canhões do kit que atiram no seu navio), calado a tiros de canhão nas muralhas (`EnemyFort`, 150 de vida, só dano de navio), guarnição de 5 e paiol; 3 missões novas | ✅ aprovado |
| M6.10 HUD de pirata (todas as cenas): tudo em UI Toolkit (`GameHud` + Hud.uss; escala com a tela, ref. 1600×900; Kenney UI Pack + fontes Pirata One/Alegreya SDF). Passo 1: status do navio/a pé, porão/mochila, missões, avisos [F], faixas, anúncios e ganhos, rótulos/marcadores/barras no mundo, ajuda dos controles no H. Passo 2: janelas das casas do porto (e abas da cena 05), baú, destroços e menu inicial, com botões de madeira | ✅ aprovado (passos 1 e 2) |
| M6.11 Efeitos visuais, passo 1 (todas as cenas): módulo `Pirates.VFX` (`GameVfx`, só ouve o `CombatEffects` e acompanha os navios): tiro com clarão, faíscas e fumaça que fica no ar e vai com o vento; bala na água com coluna, gotas, anel e espuma; acerto com lascas de madeira 3D, faísca e poeira; rastro de espuma conforme a velocidade (e borrifo na proa); casco avariado solta fumaça (< 60%) e pega fogo (< 30%); naufrágio com espirro, fumaça escura e bolhas. Texturas do Kenney Particle Pack | ✅ aprovado |
| M6.11 Efeitos visuais, passo 2 (todas as cenas): a pé, para jogador e piratas: rastro em fita da lâmina do cutelo (só na parte rápida do golpe), clarão/faíscas/fumaça da pistola e poeira onde a bala bate, estrela e faíscas no acerto (lasquinhas em caixote), poeira dos pés na cor do chão ao correr/pular/cair, respingo na água rasa, poeira do pirata caindo morto e do caixote quebrando, baú destrancado brilhando (e brilho ao abrir), tesouro desenterrado com torrões e ouro; nada a > 70 m da câmera | ✅ aprovado |
| M6.12 Oceano, passo 1 (todas as cenas): costa no shader do mar: água turquesa até ~28 m das ilhas, raso claro perto da areia, espuma na linha d'água (vai e volta com a onda) e faixas de espuma recortadas chegando na praia; mapa por cena feito pelo gerador (altura do chão + distância do litoral), sem textura de profundidade e com a água opaca; ajustes no objeto Ocean | ✅ aprovado |
| M6.12 Oceano, passo 2 (todas as cenas): mar aberto com a crista da onda num verde-água mais claro e espuma recortada no alto das cristas (pela altura da onda, mesmo ruído da costa); reflexo do sol mantido (testado mais fosco e mais liso: pior ou igual); ajustes no objeto Ocean, "Mar aberto (visual)" | ✅ aprovado (05/10) |
| M7.1 Chefe no mar (cena 07): galeão pirata "Tormenta" do Capitão Lázaro Negro (ship-pirate-large de velas pretas, casco do brigue), mar do sudeste além da Caveira; casco 700, 6 canhões por bordo (16 de dano, recarga 4 s), 9,8 de velocidade, pontaria de elite, nunca foge; com o casco pela metade chama 2 corsários (chalupas pirata ship-pirate-medium, stats/saque das chalupas) que chegam por trás dele; barra do chefe no topo da tela; missão "O galeão Tormenta" na taverna (450 ouro, pólvora, ferro); saque grande com baú garantido (esquema); volta em 300 s ou ao aceitar a missão | ✅ aprovado (05/10) |
| M8.1 Tripulação, passo 1 (cenas 05 e 07): sobreviventes + marinheiros = tripulação nos beliches (16); bônus proporcional (cheio: recarga −20%, aceleração/giro +15%; zero = navio de antes); tiros no casco matam tripulantes (1 a cada ~40 de dano, sorteado; aviso "Baixas a bordo"); contratar na taverna (30 ouro cada; na 05, aba Carga); linha "Tripulação" no painel do navio; destroços com beliches cheios = "beliches cheios"; F1 com atalhos | ✅ aprovado ("Boa", 04/10) |
| M8.2 Navio novo (cenas 05 e 07): escuna (ship-pirate-medium, 2 mastros) construída no Estaleiro do porto com o esquema "Escuna" (baú raro, peso 3 em todas as tabelas com esquemas) **+ a construção Estaleiro na base** + 500 ouro, 80 madeira, 25 ferro, 3 min de obra (só com o navio na zona do porto); substitui a chalupa no mesmo objeto (casco 150 → 260, canhões 4 → 6 por bordo, porão 30 → 50, beliches 16 → 24, velocidade 11 → 10,5, giro 28 → 24); **upgrades voltam a 0 no navio novo**; nele casco, velocidade e recarga rendem ×1,2 (dano e mira iguais aos da chalupa; custo igual), decidido pelo Jhonata em 04/10; tripulação continua; save guarda a classe e a obra; F1 troca na hora (zera os upgrades); corsários do Tormenta com bandeira vermelha (mesmo modelo da escuna) | ✅ aprovado (04/10) |
| E1 Correções da economia (cena 07; revisão do chat de otimização, aprovadas pelo Jhonata em 04/10): (1) missão repetível descansa 10 min de jogo depois de entregue (`MissionDefinition.cooldownSeconds`, salvo; quadro mostra "volta ao quadro em X min"): acaba o ouro infinito da entrega de pólvora com o Paiol; (2) baú raro não repete esquema que está no porão, mochila **ou armazém** (`Economy/PlayerStorage`); (3) baú do acampamento esvazia e sorteia de novo quando o acampamento é repovoado (trancado de novo depois de aberto) | ✅ aprovado (04/10) |
| M9 Mais coisas no porto (cenas 05 e 07): (1) munição especial: carga de corrente (metade do dano, rasga velas: inimigo até −50% de velocidade) e de metralha (4 balinhas por canhão, 60 m, ×1,5 de dano, abate tripulação: recarga inimiga até +60%), teclas 1/2/3 no navio, uma carga por bordada, ficam a bordo ao desembarcar; (2) carpinteiro a bordo (R): conserta 2/s no mar, 1 madeira a cada 5 de casco, só 8 s sem levar tiro e com 2+ tripulantes; 10 madeiras ficam a bordo ao desembarcar; (3) mercado na praça (banca verde) com demanda (compra até 20 de cada, volta 1 a cada 45 s) e ferreiro na Forja (corrente com a Forja 1, metralha com a Forja 2); (4) nível 4 no Alojamento, Forja e Paiol, Estaleiro 2 (libera o nível 4 dos upgrades, menos a mira) e Fortaleza 2 (mais 2 canhões na muralha, zona segura +45 m) | ✅ aprovado (05/10) |
| M10 Fama do capitão (cenas 05 e 07): fama por navio afundado (chalupa 6, corsário 8, brigue 25, galeão 80), acampamento (Caveira 20, esconderijo/guarnição 15, posto 5), forte calado (40), tesouro (10) e missão entregue (10 a 120); 5 patentes (Grumete 0, Marujo de Respeito 60, Corsário 200, Capitão Temido 450, Lenda dos Sete Mares 900); cada patente: +10% de ouro nas missões (menos entregas, revisão de 05/10), contratos que pedem patente (Santa Clara/contrabandistas 1, forte 2, Tormenta 3) + 3 novos (Terror das rotas 2, Encomenda para o arsenal 3, O senhor dos mares 4), esquemas à venda no mercado (Estaleiro 1, Forja/Paiol 2, Escuna 3, Fortaleza 3 (era 4, revisão); uma vez) e 1 bônus para escolher entre 3 sorteados (9 bônus: dano, velocidade, casco, mercado, obras, ouro das missões, carpinteiro, porão, tripulação); painel na tecla C (retrato do capitão com o papagaio, nome, patente com estrelas, feitos, barra até a próxima, escada das patentes e o que cada uma libera, bônus), plaquinha no canto direito, faixa de patente nova; nome digitado no Novo jogo (save antigo = "Capitão Barbarossa", fama retroativa pelas missões já entregues) | ✅ aprovado (05/10) |
| A1 Ajustes (05/10, cenas 05 a 07): (1) Forja com tempo: encomenda (paga na hora, 1 por vez, até 10 receitas), o ferreiro trabalha mesmo com o navio longe (corrente 90 s, metralha 120 s por receita; Alojamento e bônus aceleram), fica pronta na Forja e você retira (porão, o resto no armazém); cancelar devolve; aviso e linha na faixa do porto; salva; (2) ração: comer com R a pé (+40 de vida, 8 s entre uma e outra), o capitão leva 5 do porão ao desembarcar, 0,25 de espaço, mercado 6 de ouro (não compra de volta), caixotes/barris/baú do acampamento; mochilas maiores no mercado (couro 25 por 200, patente 1; contrabandista 35 por 500, patente 2; compra única, a maior vale); (3) piratas de terra provocados por tiro/golpe: o acampamento todo vem, persegue até 85 m do centro por 15 s (renova a cada acerto) e sabe onde você está; (4) mira assistida: travada vai na distância exata (antes caía 3 a 5 m antes do casco), antecipação pelo tempo de voo real e, no máximo, cada canhão da salva segue o alvo | ✅ aprovado (05/10) |
| A2 Cartuchos da pistola (05/10, cenas 05 a 07): cada tiro gasta 1 cartucho da mochila (sem cartucho: clique seco e aviso "Sem cartuchos!"; o cutelo continua), o capitão leva 20 do porão ao desembarcar (60 ficam a bordo ao descarregar), mercado 2 de ouro (não compra de volta), Forja 20 por 1 ferro + 1 pólvora (45 s, Forja 1), barris e baú do acampamento; painel a pé mostra quantos; jogo novo traz 20 cartuchos e 3 rações; piratas da IA atiram sem gastar; animação de comer: gole numa garrafa de rum (Mixamo "Drinking" espelhado, mão direita, ~1,9 s; a arma some da mão e não ataca/atira durante, anda mais devagar) | ✅ aprovado (05/10) |
| A3 Laser e bacamarte (05/10, cenas 06 e 07): mira a pé vira laser vermelho com brilho até onde a bala bate (ponto no fim; travado = anel dourado no alvo); pistola maior (0,3 → 0,45), corrigida a pistola do cinto (1 cm) e agora deitada na faixa (em pé quase tocava o chão); bacamarte (modelo × 0,6, ~1,7 m: o capitão é cabeçudo) = arma 3 de duas mãos (animações de rifle do Mixamo), 7 chumbos × 14 em leque de 16°, 16 m, recarga 3,5 s; compra única no mercado (350, patente 1), nas costas; munição própria "Cargas de chumbo" (mercado 5, Forja 10 por 1 ferro + 1 pólvora em 60 s, barris/baú; leva 12 ao desembarcar) | ✅ aprovado (05/10) |
| M11 Dia e noite, passo 1 (todas as cenas; save na 07): relógio com dia de 14 min (6h–19h) e noite de 6 min; o sol anda de leste a oeste (hora dourada com sombras longas no fim da tarde) e de noite vira a lua azulada (mesma luz); luz ambiente, névoa, céu e reflexo do céu no mar acompanham; o dia claro continua igual ao de antes; 9 lanternas do porto (píer, taverna, praça) e a lanterna da popa do seu navio acendem com luz de verdade (os navios inimigos só brilham); ajuste pedido (05/10, "muitas casas sem iluminação"): janelas acesas em todas as casas (vila e construções da base, menos paiol/depósito de pólvora; ~3 de cada 4, cada casa acende numa hora) e nas torres de pedra (Fortaleza, moinho, forte inimigo, janelas das muralhas), luz curta na frente de cada casa, lanternas nos portões da Fortaleza, do forte inimigo e do acampamento da Caveira e no esconderijo dos contrabandistas (cais e palafitas); piratas de terra veem até ~10 m à noite (16 de dia); relógio no HUD embaixo da fama ("Dia N · HH:MM", disco de sol/lua); o save guarda dia e hora (save antigo começa às 8h); F1: amanhecer/meio-dia/pôr do sol/noite e relógio ×1/×10/×60 | ✅ aprovado (07/10) |
| M11 Clima, passo 2, parte 1 — vento (todas as cenas): direção e força mudam devagar com a hora do jogo (ruído pela hora: o save do relógio já traz o vento), em volta de um vento dominante soprando para lés-nordeste (±110°), de calmaria a forte; navios (jogador e IA) andam mais com o vento por trás/de lado e menos contra ele (vento forte: bem por trás +12%, 45° +10%, de lado +4%, meio contra −16%, contra −30%; vento fraco: 45% disso; 07/10: o pico era a 55° e o Jhonata alinhava com a seta sem ganhar o bônus); linha "Vento" no painel do navio (seta com a proa para cima, força e quanto ajuda/segura nesse rumo); fumaça do cenário e dos tiros vai com o vento; espuma das cristas do mar cresce com a força; F1: de onde vem (N/L/S/O), fraco/forte, muda sozinho; ajuste pedido (07/10, "algo bonito para saber que é o vento" + som): riscos de vento na tela à moda do Wind Waker (fitas brancas que voam com o vento perto do que a câmera olha, ondulando, algumas com laço; mais e mais rápidas com vento forte, mais fracas de noite) e som de vento (loop gerado por código, volume e tom pela força, rajadas, mais baixo a pé) | ✅ aprovado (07/10) |
| M11 Clima, passo 2, partes 2 e 3 — chuva e tempestade (todas as cenas): o tempo muda sozinho com a hora do jogo (céu fechado ~27% do tempo, chuva ~16%, tempestade ~6%: uma a cada ~2,4 dias de jogo, ~3 min reais; jogo novo começa limpo); céu fechado escurece o sol (até ×0,2) e a luz ambiente, puxa a luz para cinza-azulado, adensa a névoa (×2,4) e apaga o céu; chuva na tela (gotas esticadas inclinadas pelo vento, mais fortes na tempestade, mais fracas de noite); vento sobe com a chuva (≥ 0,4) e fica forte na tempestade (≥ 0,95); tempestade: raios (clarão azulado em toda a cena + risco do raio em zigue-zague para um lado da tela) e trovões, lanternas acesas mesmo de dia, todos os navios (jogador e IA) balançam muito mais (até ~17°) e giram 12% menos, canhões de todos (IA e fortes também) espalham +50%; sons de chuva e trovão; relógio do HUD mostra "tarde · chuva"/"· tempestade"; F1: limpo/nublado/chuva/tempestade, raio agora, muda sozinho; ajuste pedido (09/10, "gotas de lado na tela", chuva mais bonita e caindo na água): corrigido o erro que fazia as gotas voarem deitadas (a caixa nunca girava para a queda), gotas finas e mais densas caindo na vertical (inclinadas só pelo vento), anéis que se abrem e pinguinhos onde a chuva bate na água, pinguinhos no chão, no píer e no convés; som trocado por gravações CC0 do Freesound (baixadas pelo Jhonata): chuva calma em loop, chuva pesada da tempestade por cima e 5 trovões | ✅ aprovado (09/10) |
| M12 Marujos no convés, passo 1 (visual; cenas 05 e 07): a tripulação aparece como marujos no navio do jogador (moradores do kit com cores de marujo; quantos = fração dos beliches: até 4 na chalupa, 6 na escuna; eram 5 e 7, o Jhonata pediu menos para sobrar espaço (09/10)); andam pelo convés e param, às vezes conversam (sim/não com a cabeça); ao mudar a vela dois correm ao mastro e puxam o cabo; em combate (tiro dado ou levado) correm para trás dos canhões do convés principal; no disparo levam o tranco, viram, abaixam para pegar a bala nova e voltam socando até a bateria ficar pronta (ajuste pedido em 09/10, só com as animações do kit); tiro no casco faz alguns se encolherem; baixa na tripulação = um marujo cai morto e some; na escuna também ficam no tombadilho (perto do leme); navio novo, contratar e descarregar mudam quantos aparecem; longe da câmera (> 250 m, fora de combate) param | ✅ aprovado (09/10) |
| M8.3 Galeão, 3ª classe do navio (cenas 05 e 07; passo 2 do plano de 09/10): ship-pirate-large (o casco do Tormenta, medidas do brigue), casco 420, 8 canhões por bordo, porão 110, beliches 32 (8 marujos), velocidade 10, giro 20 (o mais lento); upgrades de casco e recarga rendem ×1,4 e de velocidade ×1,3 (com 1,4 empatava com a escuna no nível 4); construído no Estaleiro do porto com o esquema "Galeão" (baú do Tormenta, peso 6, ou mercado na patente 4 por 1600) + **Estaleiro nível 2** + 1200 ouro, 160 madeira, 60 ferro, 40 pólvora, 5 min de obra; dá para ir direto da chalupa; lanterna da popa no lugar de cada casco; artilheiros dos marujos ganharam o ciclo do canhão (tranco, pega a bala, soca) | ✅ aprovado (09/10) |
| M13 Inimigos novos (passo 3 de 09/10): **bacamarteiros** em terra (cenas 06 e 07: 2 no acampamento da Caveira, 1 nos contrabandistas, 1 na guarnição do forte; o bacamarte do jogador num tubarão: 6 chumbos × 7 = 42 colado, leque de 18°, até 13 m, recarga 4,5 s; chega a ~9 m, mira 1,1–1,5 s com a linha vermelha e recua só colado) e **caçadores de recompensas** no mar (cena 07: a partir da patente Corsário, a cada ~7–12 min navegando fora do porto, um navio de velas brancas (ship-medium) aparece a 230 m atrás da câmera e persegue você pelo mapa, com aviso e barra no topo; força pela patente: Corsário 280 de casco 6 × 13 e pontaria de veterano, Capitão Temido 560 6 × 15, Lenda 820 6 × 18 recarga 3,8 (acima do Tormenta); 20/35/60 de fama e saque com baú 35%; desiste depois de 90 s com você no porto ou ancorado a pé, ou longe; F1: chamar caçador) | ✅ aprovado (09/10) |
| A.1 Barris de pólvora (pacote A, passo 1; cenas 06 e 07): barril do Kenney com faixas vermelhas e tampa preta; tiro, bala de canhão ou outra explosão acendem (golpe de cutelo não), pavio 0,1–0,25 s, explosão de 5 m (80 de dano no centro → 12 na borda, empurra os personagens, quebra caixotes, acende os barris em volta: cadeia), efeito (clarão, bola de fogo, aduelas, fumaça escura) e som; 4 no acampamento da Caveira, 2 nos contrabandistas, 4 no forte (3 juntos no paiol); voltam com o acampamento repovoado | ✅ aprovado (09/10) |
| A.2 Rolamento / esquiva (pacote A, passo 2; cenas 06 e 07): **Ctrl** (gamepad RB) rola ~4,5 m em 0,8 s para onde você anda (parado: para a frente), com o "Sprinting Forward Roll" do Mixamo no corpo todo, mesmo correndo; do início até 60% do rolamento nada acerta (golpe, tiro, explosão); cancela o golpe/tiro que ia sair; 0,4 s de espera entre rolamentos; som de roupa/baque e poeira; o comando vale para a IA também (duelista, depois); ajuda do H atualizada | ✅ aprovado (09/10, "achei legal") |
| A.3 Chute e quedas (pacote A, passo 3; cenas 06 e 07): **V** (gamepad LB) = chute frontal ("Mma Kick1"): 8 de dano, empurra 0,6 m e **derruba** (o pirata cambaleia, cai sentado e levanta em ~1,5 s), 3 s entre chutes, e quem levantou fica 2 s sem cair de novo; **explosão derruba** quem está a até ~3 m do barril (arremessado de costas: piratas ~2,9 s no chão; o capitão ~1,3 s, invencível 0,5 s ao levantar, e depois de 0,8 s caído o Ctrl levanta rolando); no chão leva dano mas não cai de novo; quem morre caindo termina a queda e fica deitado (sem levantar para cair de novo); som do chute e da queda, brilho e poeira; ajuda do H | ✅ aprovado (09/10) |
| Próximos (pedido do Jhonata em 09/10: "só descemos, limpamos o acampamento e saímos", quer mais coisas a fazer, sobretudo a pé) | **Agora: A + B.** A. Combate a pé melhor (barris de pólvora que explodem, rolamento/esquiva, chute, bomba de arremesso); B. Inimigos variados em terra (brutamontes de machado duplo, duelista de adaga, esqueletos amaldiçoados à noite, chefe pirata a pé). Depois: C. Exploração (mapas do tesouro em pedaços, ruínas/caverna com alavancas e armadilhas, prisioneiros que viram tripulação, coletáveis); D. Atividades (pesca, cortar árvores/minerar, 2 marujos lutando junto a pé); F. Kraken (`Characters_Tentacle` do Quaternius, em `_Downloads`) e tubarões (`Characters_Shark`); G. Marinha / nível de procurado; H. Ilha amaldiçoada. Abordagem (E) ficou de fora. Ainda na fila: (4) detalhes do clima (bandeiras e velas com o vento, ondas maiores na tempestade) |
| Depois | Progressão (tripulação: salário/moral, papéis como carpinteiro e artilheiro); Conteúdo (missões repetíveis, ilha nova) |

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
  **Galeão (M8.3, 09/10):** 3º rig "Visual_Galeao"/"Cannons_Galeao" (`AddGalleon` em `PrototypeSceneBuilder.Ships.cs`, casco `BrigHull(BossModel)`),
  `Data/ShipStats_GaleaoJogador` e `CannonStats_GaleaoJogador` (clonados da escuna; o `ShipStats_Galeao` é o do chefe), `ShipS/ShipClass_Galeao`
  (tier 2). `ShipClassDefinition.requiredBuildingLevel` (novo, padrão 1) → `HarborVisitor.ShipBlockReason` "precisa de Estaleiro nível 2".
  Esquema `Esquema_Galeao` (épico): só no `LootTable_Galeao` (peso 6; não entra nos outros baús) e no mercado (patente 4, 1600). Lanterna da
  popa: `PlayerShipClass.Rig.lantern/lanternPosition` (o `ApplyRig` move a "Lanterna_Popa" para a popa de cada casco; antes ficava no meio da
  escuna). Marujos: 3º layout (8; convés principal 38 pontos + castelos). Testado: requisitos na ordem (Estaleiro 2 → esquema → custo), compra,
  obra, troca; 8 + 8 canhões, 32 beliches, 8 marujos.
  **Revisão (chat de otimização, 09/10):** custo igual à escuna (CPU igual, GPU +0,15 ms; rigs desligados não custam). Luta parada a 70 m, tudo
  no nível 4: escuna 124 por bordada (50/s no Tormenta, afunda em ~14 s, aguenta ~27 s), galeão 171 (68,8/s, +38%; afunda o Tormenta em ~10 s,
  aguenta ~45 s); uma bordada cala o forte. Domina tudo o que existe: **o próximo inimigo precisa estar acima do Tormenta.** Recarga no limite
  de 2,5 s já no nível 3. Caminho até o galeão no nível 4: ~4.300 ouro (+1.600 se comprar o esquema), ~2–2,5 h no fim do jogo. Esquema: 6/13 =
  46% por vitória com Estaleiro e Escuna na mão. Recém-comprado (upgrades no zero) é bem mais fraco que a escuna no máximo até ~2.900 de ouro
  em upgrades: decisão do Jhonata se mantém parte dos níveis na troca. Sandbox: voltar do galeão para um navio menor pelo F1 deixa a tripulação
  acima dos beliches (o porão não corta; no jogo não dá para construir um navio menor).
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
  **Dia e noite (M11, passo 1):** `Ambient/DayNightCycle` (objeto "CicloDiaNoite", criado no `CreateEnvironment` de todas as cenas, ajustes em
  `Data/DayNight_Ciclo`) registra `Core/ITimeOfDay` (hora, dia, `Night` 0–1, `Speed`, `SetTime`, `ResetToStart`). O dia claro fica como a cena era:
  no Awake guarda a luz ambiente (SH do céu), a névoa, o reflexo e a exposição do céu, e de noite mistura com os da noite; o céu é uma
  **cópia** do material (mexer na exposição do asset ficaria salvo). Testado em Play: `RenderSettings.ambientProbe` e `reflectionIntensity`
  mudam na hora em tempo de execução (com o reflexo do céu do dia, o mar escuro brilhava de noite). Sol: `elevation = sin(πt)^0,55 × máx.`
  (com o seno puro o fim da tarde ficava escuro e cinza: o sol caía rápido demais); a mesma `Light` vira a lua (cor, intensidade, sombra 0,6).
  Só reaplica a luz quando a hora muda ~0,003 h. Lanternas = `Ambient/NightLight` (luz de ponto sem sombra + bolinha emissiva
  `M_LanternaAcesa`; nível estático `NightLight.Level` posto pelo ciclo; só trabalha quando o nível muda): gerador `NightLantern(modelo)` e
  `ShipLantern` (popa, fora do "Visual": vale para a escuna; luz só no `PlayerShip`). O ponto laranja na água atrás do navio é o reflexo da
  lanterna no mar (smoothness 0,93), não um objeto. **Janelas acesas** (sem luz, só emissivo `M_JanelaAcesa`, `_Cull` 0): as janelas do
  Fantasy Town Kit são buracos (parede em x −0,5…−0,4 do modelo, abertura até y 0,2…0,8 e z ±0,2): `TownWindowGlow` põe uma placa no meio
  da espessura (x −0,45), maior que a abertura (o resto fica escondido dentro da parede); as do kit pirata são nichos fundos sem buraco
  (`tower-middle-windows`: ±Z, fundo a 1,44 do centro; `castle-window`: +Z, fundo a 1,10), placa 1,5 cm na frente do fundo (`PirateWindowGlow`,
  medido com raios contra a malha). `House()` faz isso sozinho (`HouseSpec.lit`, sorteio próprio para não mudar as paredes) e põe a
  `LuzDaCasa` (ponto, range 7, sem sombra) na frente da porta; o `NightLight` da casa liga tudo num nível sorteado (`Configure(..., from)`;
  a luz também só liga acima desse nível). Lanterna de poste nova: `PostLantern(pai, chão, nome)`. Peça nova com janela? Meça a abertura e
  chame um dos dois. **Custo (chat de otimização, 05/10):** vila a pé às 23h (34 luzes de ponto, 12 `LuzDaCasa`, 80 janelas na tela),
  mesmo quadro renderizado em RenderTexture 1366×768 (mediana de 15 renders × 4 rodadas, 3 repetições; o GPU Frame Time do editor sem foco
  oscila ±1,5 ms e não serve para A/B): todas as NightLight ~7,4 ms, sem `LuzDaCasa` ~7,3, nenhuma ~6,35, dia ~6,3 → luzes da noite ≈ 1 ms
  no total, casas ≤ ~0,2 ms, janelas emissivas não aparecem; no jogo compilado (limite = CPU, VSync 60) não muda o FPS. Luz nova: sem sombra
  e range ≤ 8 continua barato; pesaria sombra em luz de ponto ou range cobrindo a tela. IA de terra: `EnemyFootAI.nightSight` (0,6 × `sightRange` com `Night` = 1; o `hearRange`
  continua). Save: `hasTime`/`day`/`hour` (`GameSave.Capture`/`Apply`; `NewGame` chama `ResetToStart`). HUD: `UI/ClockHUD` (no objeto do
  GameHUD, todas as cenas; some sem ciclo), estilos `.hud-clock*`. F1: `DrawDayNight` (o `DrawTime` antigo é o da velocidade do jogo).
  **Testar:** `Services.TryGet(out ITimeOfDay t); t.SetTime(dia, hora)` + 3 `Step()` antes da foto. Visão: `EnemyFootAI.CanSee` por reflexão
  com o capitão a 8/11/14 m na frente do pirata (dia e noite).
  **Vento (M11, passo 2, parte 1):** `Ambient/WindSystem` (objeto "Vento", `CreateWind` no `CreateEnvironment` de todas as cenas, ajustes em
  `Data/Vento`) registra `Core/IWind` (`Direction`/`Degrees` = para onde sopra, `Strength` 0–1, `SailFactor(proa)`, `SetOverride` do F1). Direção e
  força = Perlin pela hora corrida do `ITimeOfDay` ((dia − 1) × 24 + hora) / `changeHours` (6): não precisa de save próprio. `ShipController.WindFactor`
  = `SailFactor` com vela para a frente (1 recolhida/de ré), × velocidade máxima, para todos os navios. Perseguição em linha reta: os dois pegam o
  mesmo vento (proporção igual; testado chalupa × escuna com vento contra e por trás). `OceanSurface` manda a espuma das cristas × `windFoamCalm…Strong`
  e começa mais embaixo com vento forte (confere a cada 0,5 s). `WindDrift` (gerador: em todo `Smoke`) põe a velocidade lateral da fumaça;
  `GameVfx.FollowWind` usa `FxSystem.SetWind` (fumaça e poeira). HUD: linha "Vento" no `ShipDebugHUD` (seta desenhada com `Painter2D`, girada por
  `style.rotate`, passos de 3°). Riscos: `Ambient/WindStreaks` (no objeto "Vento"; 14 `TrailRenderer` reaproveitados com o material
  `M_FX_Rastro`; caminho = avanço + onda de lado + laço num plano de pé, raio ≥ o avanço durante a volta para cruzar o próprio caminho;
  largura = 0,0075 × distância da câmera ao ponto olhado, 0,1–0,4 m; `Clear()` antes de reaproveitar). Som: `Audio/WindAudio` no objeto "Audio"
  (2D, canal ambiente, prioridade 200) com `Som_Vento` = `_Project/Audio/Vento_Gerado.wav`, feito pelo gerador (`WindLoopClip`: ruído com
  passa-baixa de 2 etapas em 380 Hz + passa-faixa ressonante 260–700 Hz que sobe e desce, ondulações com período que divide os 12 s, fim
  misturado com o começo; PCM; RMS −18 dBFS). Mudou a receita? Suba `WindLoopVersion` (refaz no mesmo arquivo, mesmo GUID). Eu não ouço:
  medido só por RMS/cruzamentos (energia em ~400 Hz). Se o Jhonata não gostar, trocar por uma gravação CC0 no `Som_Vento`.
  **Custo (chat de otimização, 07/10):** riscos ligados × desligados no mar com vento forte: CPU igual (≤ 0,2 ms, ruído), sem lixo por quadro
  (depois de trocar um `Mathf.Min` de 3 argumentos); o som não aparece. Sobra pequena e antiga: `GameVfx.Update` (~75 B) e `GameAudio.Update`
  (~54 B) em média, do `Scan` a cada 2 s. **Testar:** `IWind.SetOverride(graus, força)` + navio com `Teleport` e `SetSail(máx.)`, ~20 s de jogo.
  **Chuva e tempestade (M11, passo 2, partes 2 e 3):** `Ambient/WeatherSystem` (objeto "Clima", `CreateWeather` no `CreateEnvironment` de todas as
  cenas, ajustes em `Data/Clima`) registra `Core/IWeather` (`Overcast`, `Rain`, `Storm` 0–1, `Label`, evento `LightningStruck`, `SetOverride(WeatherKind?)`,
  `StrikeNow`). Tempo = Perlin pela hora corrida do `ITimeOfDay` / `changeHours` (9), esticado como o do vento; limites `cloudyFrom` 0,62, `rainFrom`
  0,80, `stormFrom` 0,95 (± `blend` 0,05): o ruído esticado fica "colado" em 1 nos 10% mais altos (p90 = 0,89, p95 = 0,97), por isso a tempestade
  usa 0,95. Medido em 100 dias: céu fechado 27%, chuva 16%, tempestade 6% (41 tempestades de ~3,6 h de jogo ≈ 3 min reais). Dia 1 limpo até as 14h
  (`clearStartHours`). Sem save próprio. Troca gradual (`transitionSeconds` 20) quando o F1 fixa ou a hora pula. `DayNightCycle.Apply` lê o
  `IWeather` (reaplica quando o céu muda 0,01): sol × `overcastSun`, sombra → `overcastShadow`, ambiente × `overcastAmbient` e tingido por
  `overcastTint`, névoa cinza e densidade × `overcastFogDensity` (guarda a da cena no Awake), céu e reflexo mais fracos, `NightLight.Level` ≥
  `stormLanterns` × tempestade (campos novos em `DayNight_Ciclo`). `WindSystem`: força ≥ `rainWindFloor` × chuva e `stormWindFloor` × tempestade.
  Chuva: `Ambient/RainEffect` (3 ParticleSystems em espaço de mundo, criados no Awake e religados no `OnEnable`: "GotasDeChuva" Stretch, máx. 2800, sem Collision: a gota morre pela vida; caixa em
  cima do `ViewFocus`, girada para a queda inclinada pelo vento (09/10: o `shownWind` começava em NaN, a comparação dava sempre falso e a caixa
  ficava sem girar: as gotas voavam deitadas para +Z); "Aneis" (HorizontalBillboard, máx. 400, 0,5–0,75 s) e "Respingos" (pinguinho com gravidade,
  máx. 300): **sem colisão das gotas** (caro com ~2000 no ar): ~380–620 pontos por segundo sorteados na tela (`ViewportPointToRay` até o nível
  do mar, ≤ 80 m); se o raio acha chão/convés/telhado antes (normal para cima, acima do mar), só o pinguinho; senão anel + pinguinho na altura
  da onda (`IOceanSampler.GetHeight`). Materiais `M_FX_Chuva` (circle_05), `M_FX_Anel` e `M_FX_Gotas` (os mesmos do `GameVfx`). Custo (chat de otimização, 09/10, tempestade
  cheia: ~2300 gotas, ~380 anéis, ~145 respingos): GPU ~+0,2 ms, CPU igual (os raios dos respingos ~0,1 ms no LateUpdate), lixo 0. Raio: `Light` direcional "Clarao" (desligada, sem sombra; liga 0,07 s + 0,13 s) e
  `LineRenderer` "Raio" (`M_FX_Raio` aditivo, 12 pontos, até 45 m de altura: mais alto passa por cima da câmera) a 18–38 m do ponto olhado, 20–55°
  para um lado (à frente cairia acima da tela). Tempestade nos navios: `ShipController.stormRoll` (4 rad/s² de balanço por ruído lento por navio:
  ~17° de pico; 2,4 dava ~10°, 5 dava ~26°) e `stormTurnLoss` (−12% de giro); `CannonBattery.StormSpreadBonus` (+50% no espalhamento final:
  depois das reduções do jogador, vale para IA e fortes). Som: `Audio/WeatherAudio` (dois loops 2D: `Som_Chuva` pela chuva, −35% na tempestade, e
  `Som_Tempestade` pela tempestade; trovão `Som_Trovao` 0,3–1,4 s depois do raio). Clipes em `ThirdParty/Audio/Clima` (gravações CC0 do Freesound
  baixadas pelo Jhonata em 09/10, originais em `_Downloads/Sons`; pasta importada em **estéreo**, Vorbis comprimido na memória): `Chuva_Loop` (Snoopy20111,
  60 s), `Tempestade_Loop` (felix.blume, 80 s sem trovão), `Trovao_1..5` (bajko ×2, Dave Welsh, netaj, Fugeni). Cortados e emendados por um script
  Python sem bibliotecas (o MP3 do jm foi decodificado pelo `UnityWebRequestMultimedia` num eval: fraco e irregular, ficou de fora; o do barco é quase
  só ronco de vento): loop = trecho + 4 s misturado no começo (potência constante); volume por RMS (chuva −21, tempestade −18 dBFS; trovões pelo
  segundo mais forte = −15) com limitador suave acima de 0,7 (< 0,01% das amostras nos loops). `Recorded(cue, clipes)` no gerador troca os clipes
  de um som que ainda apontava para os gerados (`_Project/Audio`, apagados); clipe escolhido à mão fica. HUD: `ClockHUD` põe o `Label` depois da parte do dia. F1: `DrawWeather`.
  **Custo e equilíbrio (chat de otimização, 09/10):** mesmo quadro em RenderTexture 1366×768 no mar com 1333 gotas: gotas visíveis × escondidas
  igual (8,43 × 8,43 ms); tempestade inteira × céu limpo alternando 5 vezes: igual (a 1ª rodada "+3 ms" era o notebook variando); CPU igual
  (7,8–7,9 ms); lixo 0. Na tempestade de dia acendem as ~34 luzes da noite (de propósito: ~1 ms na vila, longe no mar). O balanço de 17° não
  estraga a mira (`FireDirection` usa o través no plano; a boca sobe/desce ~0,5 m); espalhamento ×1,5 na escuna = ±3,9 → ±5,8 m no alcance,
  ~20–35% menos acertos para os dois lados: com 6% do tempo é um evento, não mexe na economia, e premia chegar perto.
  **Testar:** `IWeather.SetOverride(WeatherKind.Storm)` + ~25 s de jogo (`timeScale` 4, 3 × 90 `Step`), navio no mar aberto (`Teleport` em
  (−120, 0,2, −500): (30, −250) é em cima da vila); `StrikeNow()` + 2 `Step` antes da foto. **Saves (09/10):** apareceram `save.json` 072357ce e
  `.bak` 472a5c03 (Barbarossa, fama 267, de 05/10 17:18–17:19, jogando à noite) onde em 07/10 só havia o `.bak` df21aef4; nenhum chat copiou
  (provavelmente o Jhonata). Testei com cópia e devolvi igual.
  **Marujos no convés (M12, passo 1):** `Progression/DeckCrew` no navio do jogador (cenas 05 e 07, `AddDeckCrew` em
  `PrototypeSceneBuilder.DeckCrew.cs`, chamado depois do `AddShipClasses`). Os marujos ("Marujos/Marujo_0..5", filhos do navio: balançam
  junto; sem colisor, sem física, sem NavMesh) são `KitCharacter` do Henry/Anne com `CivilianMaterial` e o `Animation/Marujo.controller`
  (locomoção Idle/Walk 1,1/Run 3,2 pela "Speed"; "Puxar" = Punch do kit (cabo e socar a bala), "Abaixar" = Duck (coice), "Tranco" = HitReact,
  "Sim"/"Nao", "Morte" fica no chão; override `Marujo_Anne`). Cada marujo é um suporte "Marujo_i" (o DeckCrew anda e gira ele) com o "Model"
  animado dentro: o HitReact e o Duck do kit mexem na raiz do modelo e anulavam o giro feito nela. Artilheiro (`Gunner`): tranco no disparo,
  vira para dentro (+160°: giro de 180° com `Quaternion.RotateTowards` travava; o giro é por `Mathf.MoveTowardsAngle`), abaixa aos 0,8 s
  (pega a bala), volta aos 1,45 s e soca até a bateria ficar pronta. **Sono só fora de combate e a > 250 m:** mirando os canhões a câmera vai
  até o ponto de mira (> 100 m do navio) e, com 110 m, os marujos "dormiam" no meio da luta. O convés de cada casco é medido no gerador (`MeasureDeck`, grade de 0,25 m no
  espaço do navio): altura pelo `DeckProbe.DeckHeight` (de 3,05 m, abaixo da verga; sem acerto = o raio nasceu dentro do castelo, tenta de 6 m),
  ponto livre = mesma altura nas 8 vizinhas + `DeckProbe.Touches` (esfera de 0,3 m no joelho não encosta no casco: amurada, mastro, cabine; na
  cabeça não dá, as vergas passam a ~1 m do convés) + fora da caixa de cada canhão (0,35 m para os lados, de 0,25 m para dentro a 0,95 m para fora; um círculo comia o corredor entre
  os canhões); áreas < 8 células saem. Cada nível é uma área à parte (sem escadas):
  na escuna o tombadilho tem 23 pontos e quem nasce lá fica lá. Estações: atrás dos canhões do **convés principal** (= o canhão mais baixo;
  os dos castelos ficam sem artilheiro) e dos dois lados dos mastros do convés principal (`HitsNearCenter`: raio de lado a 1,2 m acerta perto
  do meio). Chalupa: 106 pontos (~6,6 m²), 4 de 8 canhões, 2 cabos; escuna: 79 (56 no principal, 23 no
  tombadilho em duas ilhas dos lados do leme), 4 de 12, 2. O passeio sorteia só na área (nível) de quem anda (`zoneCells`), a partir de 0,6 m
  (convés pequeno; antes pedia 1 m e quase nunca achava: 3 caminhadas em 18 s), e "cheio" = outro a < 0,67 m: ~1 caminhada a cada 5 s por marujo. Caminho = busca em largura na grade
  (8 vizinhos, sem cortar quina) + corte de cantos em linha reta; nada aloca por quadro. Eventos: `ShipController.SailChanged` (2 ao mastro),
  `CannonBattery.Fired`/`State` (artilheiros; combate dura 25 s depois do último tiro dado ou levado), `Health.Damaged` (tranco),
  `ShipCrew.Lost` (quem sobra no `SyncCount` cai morto; descarregar/contratar só some/aparece), `HullChanged` (casco novo: mede de novo).
  Quantos: `ceil(Fraction × maxSailors)`; a maioria nasce no convés principal. **Testar:** `hold.Add(crew.CrewItem, n)` + 100 `Step`,
  `ship.SetSail(2)`, `CannonBattery.FireAtRange(60)`, `Health.TakeDamage` (cuidado: 200 afunda a chalupa, que tem 150), `PlayerShipClass.SetClass`
  (escuna); foto com câmera de teste perto do navio (`mj_shot.cs` no scratchpad: `ship.TransformPoint(offset)`). Desligar/religar o componente
  liga as baterias de novo no `OnEnable` (achado do chat de otimização).
  **Custo (chat de otimização, 09/10):** escuna com 7 marujos, GPU +0,7 ms (74 mil triângulos com sombra, ~10,6 mil por marujo; chalupa +0,35),
  CPU do DeckCrew < 0,07 ms, Animators ~+0,1, render ~+0,4 ms no editor, lixo 0; física igual; dormem com a câmera a 130 m. No build não muda o
  FPS. Se precisar cortar: sombra desligada nos marujos ou modelo mais leve.
  **Inimigos novos (M13, 09/10):** *Bacamarteiros*: posto com nome "Bacamarteiro_..." nas listas dos acampamentos (`PostWeapon` em
  `PrototypeSceneBuilder.FootCombat.cs`) leva `Weapon_Bacamarte_Pirata` (cópia do `Weapon_Bacamarte` do jogador, mais fraca); `CreatePirate`
  monta como o do jogador (`BlunderbussOn`, `SetMuzzles`, `AddAimRig` + `ConfigureTwoHanded`) e chama `EnemyFootAI.ConfigureRanged(6, 2,5, 9,
  (1,1–1,5), 2,5)` (com 12 m de alcance ele parava a 11 m e pegavam 3 de 6 chumbos). Munição infinita (sem `IAmmoSupply`), como os de pistola.
  Medido sozinho contra o capitão parado: 1 tiro a cada ~5 s, 14–21 de dano a 8–11 m. *Caçadores*: `Combat/BountyHunters` (objeto
  "CacadoresDeRecompensa", `PrototypeSceneBuilder.Hunters.cs`): conta o tempo do navio do jogador fora de `SafeZone` (o navio ancorado com o
  capitão a pé tem zona própria: não conta) e, na patente do `ICaptainRank` ≥ `Tier.minRank`, instancia o molde "Cacador_Molde" (desligado em
  (2500, 0, 2500)) a 230 m atrás da câmera em água aberta, aplica o `Tier` (casco `Health.SetMax`, `CannonBattery.SetStats`, fama) e chama
  `EnemyShipAI.Hunt` (detecta/perde a 2400 m). `ShipBoss` no molde = barra no topo (sem escolta); `BountyHunters.Arrived` → aviso no
  `BossBarHUD`. Os 3 tiers usam 6 canhões por bordo (os canhões são montados no casco: muda dano/recarga/espalhamento). Assets:
  `ShipStats_Cacador` (vel 11, giro 25), `CannonStats_Cacador_1..3`, `LootTable_Cacador`. Testado: aparece a 230 m, chega em ~15 s, troca
  tiros a ~70 m (39–52 de dano por bordada na chalupa parada), some 90 s depois de o jogador entrar no porto, chega sozinho pelo relógio e na
  patente 4 vem com 820/18/3,8; afundado dá a fama. Nada é salvo (relógio recomeça ao carregar). **Testar:** F1 → "Chamar caçador" ou
  `BountyHunters.SpawnNow()`; `CaptainFame.Current.Add(950, "x")` para a patente 4.
  **Revisão (chat de otimização, 09/10):** duelos parados a 70 m com mira perfeita: patente 3 × escuna nv3 e patente 4 × escuna nv4/galeão nv4
  o jogador vence com folga (proporção como a do Tormenta); a patente 2 com 360 e pontaria de elite vencia a chalupa nível 1 por pouco →
  **280 de casco e pontaria de veterano** (lead 0,2, alcance 0,04, reação 0,25–0,6; `Tier.leadError/rangeError/reaction`, aplicados no
  spawn) e o primeiro caçador com **420 s** de mar (era 240). Fuga: a chalupa nv1 só abre ~0,8 m/s (fugir = ir ao porto e esperar 90 s);
  escuna nv0 leva tiros mas escapa (ele perde velocidade ao virar para a bordada); escuna nv3 foge fácil. Fora de 120 m ele só persegue.
  Custo: +~0,6 ms de CPU com ele perto (Buoyancy + IA), lixo 0; o Instantiate do spawn leva 10–13 ms (1 quadro, fora da tela, a cada
  7–12 min; se incomodar, deixar a próxima cópia pronta e desligada, como a escolta do Tormenta). Bacamarteiros: Caveira com 2 bacamarteiros
  + 2 pistoleiros provocados mata o capitão parado em ~15–20 s.
  **Barris de pólvora (pacote A, passo 1, 09/10):** `Combat/PowderKeg` é o próprio `IDamageable` do barril (sem `Health`): só acende com
  `DamageInfo.ignites` (campo novo: tiro do `CharacterCombat`, `CannonballSystem` e a própria explosão; o golpe de cutelo não acende), pavio
  sorteado em `fuseSeconds` (atraso também na cadeia: nada explode no mesmo quadro, dica do chat de otimização), `OverlapSphereNonAlloc`
  (buffer estático) + dano pela caixa do colisor (`bounds.ClosestPoint`: `ClosestPoint` não vale no chão côncavo da ilha), um dano por
  `IDamageable` e um empurrão por `Core/IPushable` (`FootCharacter.Push`: `pushVelocity` somada ao movimento e freada por `pushDrag`;
  morto não é empurrado). `CombatEffects.Explosion` → `GameVfx.OnExploded` (só `Emit` nos sistemas que já existem) e `GameAudio`
  (`Som_Explosao`: bang_06/08/10 + cannon_fire, tom mais grave). O "CombatSystems" com o `CombatEffects` agora é criado por quem precisar
  primeiro (`CombatSystemsObject()`: canhões ou barris); a cena 06 não tinha e a explosão saía muda e sem efeito. Gerador:
  `PrototypeSceneBuilder.PowderKegs.cs` (`PowderKegsAt(site, locais)`: procura chão da ilha livre num raio de 0,6 m, até 2,5 m do ponto; barris
  encostados valem), antes do NavMesh; `EnemyCamp.SetKegs` (restaurados no `ResetAll`); F1 → restaurar caixotes também restaura.
  **Parede protege** (`Sheltered`: raio do centro até o meio do colisor do alvo; bloqueia o que não é `IDamageable` ou é `EnemyFort`
  (muralha); personagens, caixotes e barris não protegem): capitão a 2,8 m atrás da paliçada 0 de dano e sem empurrão, no aberto 47 e 1,7 m.
  Só faz a conta (e o raio) para quem tem `IDamageable`/`IPushable`.
  **Revisão (chat de otimização, 09/10):** `Explode()` 0,22–0,25 ms, 0 B de lixo; cadeia de 8 a 3 m leva ~1,1–1,5 s (uma a cada 6–12
  quadros), quadros com explosão 9–14 ms no editor (mediana +2 ms = efeitos na tela; no build não muda o FPS). Dano num pirata (60) pela
  distância do barril ao centro dele: 5,5 m 0 · 5,0 m 16 · 4,6 m 22 · 4,0 m 30 · 3,0 m 43 · 2,5 m 50 · 2,0 m 57 · ≤ 1,5 m mata. Um barril
  nunca mata o capitão (máx. 80); dois juntos matam até ~2,5 m, os 3 do paiol até ~3,5 m. Tiro de pirata também acende (o pistoleiro pode
  explodir o barril ao lado do capitão). Navio ancorado a ≤ 5 m leva o dano (improvável). Cadeia de 4 em fila (1, 2 e 6,5 m) na 06:
  0,18/0,30/0,42/0,48 s; tiro de pistola de verdade a 9 m acende (o mastro da bandeira tapa o barril da torre visto de oeste).
  **Nos testes:** o `open06` pode dar timeout e o Play entrar na **07** (aconteceu: parei antes de mexer em algo, save igual). O `p06.sh`
  do scratchpad agora só dá Play com a 06 aberta, e os scripts conferem a cena. **Testar:** `PowderKeg.Active`, `keg.TakeDamage(new DamageInfo { ignites = true })` ou `Explode()`, `Restore()`.
  **Animações do Mixamo de 09/10 (o Jhonata baixou "VÁRIAS" e pediu para usar o máximo, inclusive em chefes de ilha):** 121 FBX revisados
  (binários, sem malha, esqueleto `mixamorig` do mesmo personagem "Pirate") em `Animation/Mixamo/Combate` (avulsas, `Pirata@...`),
  `Espadao` (pacote do espadão inteiro, `Espadao@...`) e `Machado` (pacote corpo a corpo com machado + desarmado, `Machado@...`); originais
  em `_Downloads/Combate`. 5 avulsas eram cópias exatas de clipes dos pacotes (curvas iguais) e foram apagadas. `MixamoImportSettings`:
  nas subpastas, **raiz fora da pose** (posição XZ e giro): vários clipes andam/giram (rolamento 5,5 m, derrubado 1,8 m e 125°, ataques
  com salto ~3 m) e o Animator sem root motion descarta isso, então tocam no lugar e o script move; as antigas da pasta raiz não mudaram.
  Ciclo (`IsCycle`): idle/walk/run/strafe/aim, menos attack/jump/turn/roll/" to ". `MixamoClip(nome)` acha pelo fim do nome do arquivo
  em todas as subpastas (nomes curtos dos pacotes, "Idle", "Run", repetem entre pastas: use o nome completo quando criar estados deles).
  Catálogo medido (duração, quanto anda/gira): `anim_measure.cs` no scratchpad. **Destino planejado:**
  capitão: rolamento ✅, chute (Mma Kick 1–3 / Kicking), bomba (Throw Object), bloquear/aparar com o cutelo (Machado Standing Block Idle /
  Block React Large), derrubado pela explosão + levantar (Knocked Down, Getting Up 1–2), atordoado (Stunned) no pirata chutado;
  **brutamontes** (pacote Machado inteiro: idle/olhando, andar nas 4 direções, correr, virar 90°, ataques downward/horizontal/backhand/
  360 alto e baixo/combos 1–3/chutes/run jump attack, reações esquerda/direita/barriga, bloqueio, provocações battlecry e chest thump,
  sacar/guardar das costas); **duelista** (Stabbing 1–3 + rolamento); **chefe de ilha com espadão** (pacote Espadão inteiro: 5 idles,
  andar/correr/de lado, giros, 5 cortes, attack, high spin, jump attack, slide attack, 2 chutes, 5 impactos, 3 bloqueios, power up = fase 2,
  casting, 2 mortes, sacar, pulo); **esqueletos amaldiçoados** (Zombie Stand Up 1–3 levantando do chão à noite; Spell Cast para um
  feiticeiro; Crouch Idle/To Standing para emboscada); desarmado (Unarmed idle/andar/correr/pular/virar): prisioneiros resgatados e marujos
  em terra (pacote D).
  **Rolamento (pacote A, passo 2):** `FootCommand.dodge` (jogador: ação `GameInput.Dodge` = Ctrl esquerdo / RB; a IA pode usar) →
  `FootCharacter`: `dodgeDistance` 4,5 m em `dodgeSeconds` 0,8 s (ritmo do clipe, freia no último quinto), `dodgeCooldown` 0,4 s,
  `DodgeInvulnerable` no trecho `dodgeInvulnerable` (0,05–0,6); rolando ignora o resto do comando (`combat.Tick(default)`), cancela o golpe
  (`Interrupt`), evento `Dodged`. `FootHealthReactions.Update` liga/desliga `Health.Invulnerable` só se foi ele que ligou (o F1 também usa).
  Animator: estado "Rolar" no Tronco (Any + trigger "Dodge", velocidade = duração do clipe / 0,8 s, `DodgeSeconds` no gerador igual ao do
  FootCharacter), tag **"CorpoInteiroAndando"**: o driver põe o corpo inteiro em 1 na hora mesmo correndo (com a mistura de 0,12 s, os
  primeiros 0,1 s saíam meio corrida). Som `Som_Rolamento` (roupa + baque), poeira na saída (`GameVfx.Foot`, `FootTrack.dodging`).
  Testado na 06: 4,6 m em 0,8 s; golpe aos 0,1 s e 0,4 s não acerta, aos 0,9 s acerta; `Invulnerable` volta a falso; fotos de lado: mergulho,
  costas, levanta agachado e sai correndo com o cutelo.
  **Revisão (chat de otimização, 09/10):** ok como está. Capitão a 3 m de um pirata no meio da Caveira (10 provocados, 24 s): parado
  ~24 de dano/s; correndo de lado ~3,3/s; rolando sem parar ~0–0,8/s, **com ou sem a invencibilidade** (o que protege é sair 4,5 m do
  alcance de 2 m do cutelo; a mira atrasada dos atiradores não acompanha). Ninguém ataca rolando, e parar para bater continua punido.
  Se ficar fácil, a alavanca é a IA (estocada do cutelo que avança ~1,5 m; atirador antecipando quem corre, só em patente alta ou
  chefe), não o rolamento. A invencibilidade vale de verdade contra tiro mirado e barril (pavio 0,1–0,25 s). Sem alocação por quadro (lido no código, não medido no Profiler).
  **Chute e quedas (pacote A, passo 3, 09/10):** `FootCommand.kick` (jogador: `GameInput.Kick` = V / LB) → `CharacterCombat.Kick`
  (`kickDamage` 8, `kickRange` 1,8, `kickAngle` 100°, `kickImpact` 0,45 s, `kickBusy` 0,9, `kickCooldown` 3, `kickPush` 3,5; eventos `Kicked`
  e `KickLanded`); o golpe leva `DamageInfo.knockdown` (enum `Core/Knockdown`: `Stumble` = chute, `Blast` = explosão; o `PowderKeg` põe
  `Blast` até `knockdownWithin` 0,6 do raio). `FootHealthReactions.OnDamaged`: com knockdown e de pé → `CharacterAnimatorDriver.PlayKnockdown`
  + `FootCharacter.KnockDown(stumbleSeconds 2,6 / blastSeconds 2,9)` (sem os clipes: só o tranco); **já no chão** = só o dano (nem tranco
  nem queda nova). `FootCharacter.IsDown`: ignora comandos, freia, o empurrão e a gravidade continuam (`MoveBody`, o mesmo do rolamento);
  morto continua "no chão" (só `SetIncapacitated(false)` zera). Animator: "Chute" (Mma Kick1 ×1,4, tag "CorpoInteiroAndando"); "Tropeco"
  (Stunned ×1,5) e "Arremessado" (Knocked Down ×1,4 a partir de 18%) → "Levantar" (Getting Up ×1,5, sai aos 72%), saídas para o levantar
  só com o bool **"Dead"** desligado (`ExitAlive`); "MortoNoChao" (fim do Knocked Down) por Any com "Die" + "Down"; as mortes normais pedem
  "Down" desligado. `PlayDeath`: no chão há menos de 1,4 s (ainda caindo) liga só o "Dead" (a queda termina deitada), senão o "Die".
  Medidos nos clipes (esqueleto do capitão por `clip.SampleAnimation` numa cópia do modelo em edição): Mma Kick1 pé esticado 0,6–0,8 s;
  Stunned senta a ~0,9 s; Knocked Down de pé até ~0,6 s e no chão a ~1,1 s; Getting Up de pé a ~1,9 s. Testado na 06: chute → pirata
  60 → 52, empurrado 0,6 m, cai, levanta e volta a lutar ~3,2 s depois; explosão a 2,6 m → capitão arremessado 2 m, 100 → 49, de pé e
  andando aos ~2,9 s; pirata colado no barril morre e fica no "Arremessado" (deitado); morto depois do chute → "MortoNoChao". **Risco a
  saber:** caído perto de um acampamento provocado, os atiradores podem matar o capitão no chão (aconteceu no teste).
  **Revisão (chat de otimização, 09/10, aplicada antes da revisão do Jhonata):** (1) chute 3 s + 2,6 s no chão travava o pirata (87% do
  tempo caído; "chute + 3 golpes" = 83 > 60 matava qualquer um sem ele agir) → queda do chute **1,5 s** ("Tropeco" ×1,6 sai aos 60% para o
  "LevantarRapido" = Getting Up ×2 a partir de 15%, sai aos 72%) e `FootHealthReactions.knockImmunity` 2 s depois de levantar (só o dano);
  (2) capitão derrubado morria no chão (barril ≥ 43 + acampamento ~24/s × 2,9 s) → `SetQuickRecovery(true)` no capitão: explosão
  `quickBlastSeconds` 1,3 s (variante 2, "ArremessadoRapido" ×1,8, sai aos 48%), `getUpInvulnerable` 0,5 s ao levantar, e
  `FootCharacter.AllowRollOutOfKnockdown(true)`: depois de `rollOutAfter` 0,8 s caído o Ctrl levanta rolando. Piratas continuam com 2,9 s
  na explosão (recompensa de armar o barril). `CharacterAnimatorDriver.KnockFallSeconds` por variante (0,8/1,5/0,47 s): morreu antes disso
  = termina a queda deitado. Testado na 06: chute → 1,52 s no chão; segundo chute aos 3,1 s só tira 8 (imune); terceiro depois da
  imunidade derruba de novo; capitão na explosão 1,28 s no chão, invencível logo depois; Ctrl aos 0,6 s não faz nada, aos 0,9 s rola 4,7 m.
  **Save (09/10 16:27):** o Jhonata jogou (testou o rolamento) e o save andou: `save.json` 6197a6e4 + `.bak` 0d0024bb (Barbarossa, fama 267,
  escuna, dia 2). É o progresso dele, não restaurar o antigo; cópia nova em `savebak26` (scratchpad, `rest26.cs`).
  **Água funda a pé (bug, 09/10):** o capitão caiu do píer num fundo de 0,9–2,5 m e ficou preso ("Água funda", todo passo desfeito, até o de
  volta). `FootCharacter.KeepOutOfDeepWater`: de pé no fundo a mais de `maxWadeDepth` + 0,15 m (andando não se chega: o limite segura em 0,90)
  = resgate para o `lastSafePosition` (como o afogamento); na água funda só o passo que desce é desfeito, e no chão volta com a altura (antes
  mantinha a nova e descia uns centímetros por quadro empurrando a beira). Testado: queda do píer em 1,1/1,5/2,2 m volta ao píer/praia; da praia
  para o mar para em 0,90 e volta andando, sem resgate. **Nos testes:** `FootCharacter.Teleport` grava o lugar seguro (para cair "de verdade",
  mova com o CharacterController desligado).
  **Save trocado por um antigo (05/10):** a cópia que fiz às 16h já era um save de 04/10 22:07 (sem fama, sem missões); nenhuma sessão do
  Claude tinha mexido nele desde as 11:19 (origem não achada; talvez o `.exe` antigo do O7, que usa a mesma pasta). Voltei o das 11:19
  (`savebak19`, o mesmo da cópia do outro chat). **Ao fazer a cópia antes de testar, confira `savedAt`, `captainName` e `fame`** (não só o
  MD5): um save errado vira a "cópia boa".
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
