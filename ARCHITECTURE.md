# Pirate RPG — Escopo e Arquitetura

Unity 6.6 (6000.6.3f1) · URP · C# · Input System · single-player

## 1. Core game loop

```
        ┌──────────────────────────────────────────────┐
        ▼                                              │
  [BASE / PORTO] ── aceita missão / compra upgrade ──► [NAVEGAR]
        ▲                                              │
        │                                  explora / encontra inimigo
  volta com recursos                                   ▼
        │                              [COMBATE NAVAL] ou [DESEMBARQUE]
        └──── recursos, ouro, loot ◄──── [COMBATE EM TERRA / EXPLORAÇÃO]
```

A progressão vem de upgrades do barco: **casco (vida)**, **canhões (dano, alcance, quantidade)**,
**tamanho (classe: Chalupa → Brigue → Fragata)** e **tripulação (recarga, abordagem, velocidade das velas)**.

## 2. Escopo priorizado

| # | Marco | Entrega | Status |
|---|-------|---------|--------|
| M0 | Arquitetura | Módulos, asmdefs, padrões | ✅ |
| **M1** | **Navegação** | Barco com física de flutuação, velas por níveis, leme, oceano com ondas, câmera elevada, HUD de debug | ✅ |
| M2 | Combate naval | Canhões laterais (bombordo/estibordo), mira em arco, vida do casco, 1 inimigo com IA (perseguir/flanquear/atirar), naufrágio, loot flutuante (ouro, madeira, pólvora, ferro, sobreviventes, baú raro com esquemas) | ✅ |
| M3 | Recursos + Base | Inventário completo (ouro, madeira, pólvora, ferro, comida, tripulação), porto-base com construções liberadas por esquemas, loja de upgrades, stats via modificadores | ✅ |
| M4 | Ilhas + Terra | Desembarque, personagem a pé (câmera elevada), combate corpo a corpo + pistola, baús, inimigos em terra | ✅ |
| M5 | Missões | NPCs no porto, missões data-driven (afundar X, entregar Y, explorar Z), recompensas | ✅ M5.1 (quadro no porto; desde o M6.6, na taverna, a pé) |
| M6 | Persistência + polish | Save/load, áudio, VFX, shader de oceano, arte low-poly (Kenney/Quaternius, CC0) | ⏳ M6.1 save/load ✅; M6.2 navios e objetos ✅; M6.3 ilhas geradas ✅; M6.4 porto vira vila ✅; M6.5 personagens do Quaternius ✅; M6.6 porto a pé (casas de serviço) ✅; M6.7 Caveira 2× + câmera translúcida |

**MVP = M1 a M5 como vertical slice:** 1 porto-base, 3 ilhas, 2 tipos de inimigo naval (chalupa e brigue ✅), 1 tipo em terra,
5 missões e 3 níveis de upgrade por categoria.

**Fora do MVP:** vento dinâmico, clima, comércio entre portos, facções/reputação, moral da tripulação, mapa-múndi procedural.

## 3. Arquitetura

### Princípios
- **Módulos isolados por Assembly Definition**: compilação mais rápida e dependências explícitas (sem ciclos).
- **Dados em ScriptableObjects**: stats de navio, ondas, itens, upgrades e missões. Game design ajustável sem mexer em código.
- **Composição**: um navio é uma soma de componentes (`Buoyancy`, `ShipController`, depois `Hull`, `CannonBattery`, `Crew`, `Cargo`).
- **Interfaces nas bordas**: `IShipInput` (jogador **ou** IA pilotam o mesmo navio), `IOceanSampler` (qualquer objeto flutua), `IDamageable` (M2).
- **Service registry mínimo** (`Services`) para sistemas globais; **EventBus** (M2+) para eventos entre módulos (ex.: `ShipSunk` → Missões, Recursos).

### Módulos e dependências

```
Pirates.Core          (sem dependências)   Services, EventBus + GameEvents (navio afundado, acampamento derrotado, tesouro achado,
                                           missão aceita), IOceanSampler, IDamageable, IInteraction (F), ILockSource
 ├─ Pirates.Ocean     → Core               WaveSettings, OceanSurface (ondas e costa na GPU: Shaders/Ocean.shader)
 ├─ Pirates.Ships     → Core               ShipStats, ShipController, Buoyancy, IShipInput
 │   └─ Pirates.Controls → Ships, Combat, Economy, OnFoot, LandCombat, Base, InputSystem   GameInput, PlayerShipInput, PlayerInteractInput (F → IInteraction),
 │                                          PlayerModeSwitcher, BoatBoarding, BackpackUnloader, PlayerFootRespawn,
 │                                          HarborShoreLeave (F no cais: atracar e descer no píer, M6.6)
 │       └─ Pirates.CameraSystem → Controls     ElevatedCameraRig, CameraOcclusionFader + CameraOccluder (o que tapa o alvo
 │                                              fica translúcido, M6.7)
 ├─ Pirates.UI        → Ships, Combat, Loot, Base, Missions, Save…   HUD em UI Toolkit (GameHud + Hud.uss, M6.10): painéis,
 │                                          rótulos no mundo, janelas (HarborWindow, LootWindow) e menu inicial (SaveMenuHUD)
 ├─ [M2] Pirates.Combat     → Core, Ships   Cannon, Projectile, Health, EnemyShipAI; FortressCannon (Fortaleza do
 │                                          jogador ou forte inimigo), EnemyFort (forte que se cala a tiros, M6.9)
 ├─ Pirates.Economy   → Core               ItemDefinition, Inventory com limite de espaço (M2.4); [M3] UpgradeDefinition
 ├─ Pirates.Loot      → Combat, Economy    LootTable, LootDropper, ShipWreck, WreckSalvager (M2.4); LandChest, LootSack,
 │                                          BreakableLoot, LandLooter (mochila e saque a pé, M4.2)
 ├─ Pirates.Base      → Combat, Economy, Progression   HarborSettings, HarborDock (armazém + estaleiro), BaseBuildings, HarborVisitor (M3.1–3.3);
 │                                          HarborBuilding (porta de casa de serviço) + HarborBuildingVisitor (personagem a pé, M6.6)
 ├─ Pirates.Progression → Combat, Economy  UpgradeDefinition, ShipUpgrades (cópias em memória dos stats), BuildingDefinition (M3.2/3.3), CrewSettings + ShipCrew (tripulação nos beliches: bônus de recarga/manobra, baixas por dano; M8.1), ShipClassDefinition + PlayerShipClass (classes do navio do jogador, um casco por classe no mesmo objeto; M8.2), ShipAmmo (munição especial) e ShipCarpenter (conserto no mar) (M9), FameSettings + CaptainFame (fama, patentes e bônus do capitão; M10)
 ├─ Pirates.OnFoot    → Core               FootCharacter (CharacterController; jogador e IA), CharacterAnimatorDriver, LandingPoint, IFootInput (M4.1);
 │                                          WeaponDefinition, CharacterCombat (dano via IDamageable), CharacterAimRig
 │                                          (mira procedural da pistola, depois da animação) (M4.3)
 ├─ Pirates.LandCombat → Core, Combat, OnFoot   EnemyFootAI (IFootInput + NavMesh), EnemyCamp, FootHealthReactions (M4.4)
 ├─ Pirates.Missions  → Core, Economy    MissionDefinition (Data/Missions), MissionBoard (quadro do porto, progresso pelo
 │                                          EventBus), WorldMarker (destinos no HUD) (M5.1)
 ├─ Pirates.Save      → Core, Economy, Combat, Ships, Progression, Base, Missions   SaveData (DTOs em JSON), SaveFile (um
 │                                          arquivo, troca atômica + .bak), GameSave (captura/aplica, salva ao atracar/desatracar) (M6.1)
 ├─ Pirates.Audio     → Core, Ships, Combat, OnFoot, LandCombat, Controls, CameraSystem, Base, Loot, Missions, Save…
 │                                          SoundCue (Data/Audio), SoundMix (volumes por canal), SoundPlayer (vozes), GameAudio
 │                                          (ouve eventos), ShipAudio, CharacterAudio (passos pelo chão, armas, voz), MusicDirector
 │                                          (porto/mar/ilha/combate), AmbientEmitter, ListenerAnchor (ouvido no alvo) (M6.8)
 ├─ Pirates.VFX       → Core, Ships, Combat, OnFoot, Loot  GameVfx: efeitos do combate naval, rastro, avaria e naufrágio; a pé: cutelo, pistola, poeira, saque (M6.11)
 ├─ Pirates.Ambient   → Core               vida do cenário, só visual: MooredBob (barco ancorado na onda), CirclingBird
 │                                          (gaivota), BeachCrawler (caranguejo), Townsperson (morador: anda no NavMesh
 │                                          entre paradas ou fica parado gesticulando), FlickerLight (fogo), Spinner (moinho) (M6.4/6.5)
 ├─ Pirates.Sandbox   → todos os de jogo   SandboxPanel (F1, só editor/dev build)
 └─ Pirates.Editor    (só Editor)           geradores de cena de protótipo
```

`Ocean` e `Ships` **não se conhecem**: o navio só pede um `IOceanSampler`. Trocar o oceano CPU por um
shader de GPU no M6 não mexe no código do navio.

### Estados do jogo (M3+)
`GameStateMachine`: `Sailing` ↔ `Docked (Porto)` ↔ `OnFoot` ↔ `Paused`. Cada estado liga/desliga os
mapas de input e as câmeras correspondentes.

### Estrutura de pastas
```
Assets/_Project/
  Scripts/<Módulo>/      código + .asmdef por módulo
  Data/                  ScriptableObjects (gerados pelo menu de protótipo)
  Materials/  Scenes/  Prefabs/ (M2+)
```

## 4. Visual "realista": abordagem
- URP com sombras suaves, fog exponencial e skybox (já no M1).
- Visual **low-poly estilizado** com assets CC0 (Kenney Pirate Kit; Quaternius para personagens depois) em
  `Assets/ThirdParty/`. Os modelos entram sem mudar a lógica: o visual é filho do GameObject com a física, que continua
  com os colisores e a flutuação dos protótipos. O que ainda não foi trocado segue em primitivas com as cores da paleta.
- Oceano: no M1 era uma malha deformada na CPU (a altura bate exatamente com a física). Hoje as mesmas ondas sobem o vértice no shader `Pirates/Ocean` (URP Lit + `OceanWaves.hlsl`, O4), e a costa (turquesa, raso, espuma) vem de um mapa por cena feito pelo gerador (`OceanShore.hlsl`, M6.12).
