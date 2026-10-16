using Pirates.Combat;
using Pirates.LandCombat;
using Pirates.OnFoot;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Inimigos em terra do gerador (Protótipo 06, M4.4): acampamento pirata no miolo da ilha (paliçada com portão
    /// virado para a praia, fogueira, cabanas de palha e a bandeira pirata do Kenney; caixotes e o baú vêm do LandLoot), quatro piratas de cutelo com o mesmo corpo e
    /// animações do jogador (tingidos de vermelho) e o NavMesh da ilha, assado aqui e salvo ao lado da cena.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        // Miolo da ilha (M6.7: ~100 m ao sul da praia de desembarque); portões ao norte (para a praia) e ao sul.
        // Propriedade (não campo): campos estáticos de arquivos diferentes da classe parcial não têm ordem garantida.
        static Vector3 CampCenter => SkullCenter;
        const float CampRadius = 16f;
        static readonly Color PirateTint = new(0.62f, 0.2f, 0.16f);
        // Atiradores em azul-marinho, para reconhecer de longe.
        static readonly Color GunnerTint = new(0.22f, 0.3f, 0.48f);

        /// <summary>
        /// Visual do acampamento (estático: entra no NavMesh como obstáculo). Paliçada de estacas (fence-fortified do
        /// Survival Kit) com o portão grande ao norte e um menor ao sul, fogueira no meio, quatro cabanas de palha, duas
        /// barracas, torre de vigia, bandeira e colchonetes. Os piratas vêm depois do NavMesh.
        /// </summary>
        static Transform CreateCampSite(Material fire)
        {
            GroundAt(CampCenter, out Vector3 center);
            var camp = new GameObject("Acampamento_Pirata").transform;
            camp.SetPositionAndRotation(center, Quaternion.identity);

            // Paliçada: peças de 2 m (0,5 × 4) em volta; a estaca fica no lado -Z da peça (0,9 m para dentro).
            var wall = new GameObject("Palicada").transform;
            wall.SetParent(camp, false);
            const float scale = 4f;
            int pieces = Mathf.RoundToInt(2f * Mathf.PI * CampRadius / (0.5f * scale));
            for (int k = 0; k < pieces; k++)
            {
                float a = k * 360f / pieces;
                if (Mathf.Abs(Mathf.DeltaAngle(a, 0f)) < 15f || Mathf.Abs(Mathf.DeltaAngle(a, 180f)) < 10f) continue;
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                GroundAt(center + dir * (CampRadius + 0.225f * scale), out Vector3 ground);
                var piece = Model(Kit.Survival, "fence-fortified", wall, ground - center + Vector3.down * 0.15f, a, scale, $"Estacas_{k}");
                var box = piece.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.26f, -0.225f);
                box.size = new Vector3(0.5f, 0.55f, 0.12f);
            }

            Campfire(camp, center, "Fogueira");
            // Lanternas dos dois lados do portão norte (o grande, da praia) e uma no portão sul.
            foreach (var (deg, name) in new[] { (-19f, "Lanterna_PortaoN_O"), (19f, "Lanterna_PortaoN_L"), (169f, "Lanterna_PortaoS") })
                if (GroundAt(center + Quaternion.Euler(0f, deg, 0f) * Vector3.forward * (CampRadius + 2.2f), out var lanternGround))
                    PostLantern(camp, lanternGround, name, range: 8f);

            Tent(camp, "Cabana_NO", new Vector3(-9.5f, 0f, 6f), 30f);
            Tent(camp, "Cabana_NE", new Vector3(9.5f, 0f, 6.5f), -35f);
            Tent(camp, "Cabana_SO", new Vector3(-9f, 0f, -7f), 150f);
            Tent(camp, "Cabana_SE", new Vector3(6f, 0f, -10.5f), 200f);
            foreach (var (name, local, yaw) in new[] { ("Barraca_O", new Vector3(-12.5f, 0f, -0.5f), 90f), ("Barraca_L", new Vector3(12.5f, 0f, -1f), -90f) })
            {
                GroundAt(camp.TransformPoint(local), out Vector3 g);
                var tent = Model(Kit.Nature, "tent_detailedOpen", camp, g - center + Vector3.down * 0.05f, yaw, 4f, name);
                var box = tent.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.25f, 0f);
                box.size = new Vector3(0.87f, 0.5f, 0.67f);
                Occluder(tent);
            }
            for (int k = 0; k < 4; k++)
            {
                var local = new Vector3(k < 2 ? -10.8f : 10.8f, 0f, (k % 2 == 0 ? 2.5f : -3.5f));
                GroundAt(camp.TransformPoint(local), out Vector3 g);
                Model(Kit.Survival, "bedroll", camp, g - center + Vector3.up * 0.05f, k * 25f, SurvivalScale, $"Colchonete_{k}");
            }

            // Torre de vigia nos fundos, a leste.
            GroundAt(camp.TransformPoint(new Vector3(11.5f, 0f, -8.5f)), out Vector3 towerGround);
            var tower = Kenney("tower-watch", camp, towerGround - center, 30f, 1.6f, "TorreDeVigia");
            var towerBox = tower.AddComponent<BoxCollider>();
            towerBox.center = new Vector3(0f, 1.4f, 0f);
            towerBox.size = new Vector3(3f, 2.8f, 3f);
            Occluder(tower);

            // Bandeira pirata (Kenney) no fundo do acampamento; o mastro tem colisor.
            GroundAt(center + new Vector3(-2f, 0f, -11.5f), out Vector3 pole);
            var flag = Kenney("flag-pirate-high", camp, pole - center, 90f, 1.9f, "Bandeira");
            var mast = flag.AddComponent<CapsuleCollider>();
            mast.center = new Vector3(0f, 1.8f, 0f);
            mast.radius = 0.1f;
            mast.height = 3.6f;
            GroundAt(center + new Vector3(4f, 0f, 12f), out Vector3 logs);
            Model(Kit.Nature, "log_stack", camp, logs - center, 10f, 3.2f, "Lenha").AddComponent<BoxCollider>();

            foreach (var t in camp.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            Physics.SyncTransforms();
            return camp;
        }

        /// <summary>Cabana de palha do Kenney (3 × 3,4 m), com uma caixa de colisão em volta (piratas e jogador contornam).</summary>
        static void Tent(Transform camp, string name, Vector3 local, float yaw)
        {
            GroundAt(camp.position + local, out Vector3 g);
            var hut = Kenney("structure-roof", camp, camp.InverseTransformPoint(g), yaw, 1.2f, name);
            var box = hut.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.6f, 0f);
            box.size = new Vector3(2.6f, 3.2f, 2.6f);
            Occluder(hut);
        }

        /// <summary>
        /// NavMesh da ilha pelos colisores (domos, morros, troncos, pedras, paliçada, bonecos), só acima do nível do mar:
        /// os piratas não planejam caminho pela água. Assado no editor e salvo como asset ao lado da cena (um por cena).
        /// </summary>
        static void BakeIslandNavMesh(string scenePath)
        {
            string assetPath = System.IO.Path.ChangeExtension(scenePath, null) + "_NavMesh.asset";
            var go = new GameObject("NavMesh_Ilha");
            go.transform.position = SkullCenter;
            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            // Volume da ilha inteira, de 0,5 m abaixo do mar até acima dos morros (o das ruínas tem ~14 m).
            surface.size = new Vector3(2f * SkullRadiusX * 1.18f, 30f, 2f * SkullRadiusZ * 1.18f);
            surface.center = new Vector3(0f, 14.5f, 0f);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            if (!surface.navMeshData)
            {
                Debug.LogWarning("[Pirates] NavMesh da ilha não foi gerado: os piratas vão andar em linha reta.");
                return;
            }
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
        }

        /// <summary>
        /// Cinco piratas de cutelo (dois no portão norte, um na fogueira, um nas cabanas, um no portão sul) e três atiradores
        /// de pistola (atrás do portão, nos fundos perto da bandeira e ao pé da torre), dois de bacamarte (fogueira e portão sul) e o
        /// EnemyCamp (o da missão).
        /// </summary>
        static EnemyCamp CreatePirates(Transform campSite, FootCharacter player, Material iron, Material wood, Material brass,
            string campName, string campId)
        {
            var camp = campSite.gameObject.AddComponent<EnemyCamp>();
            var cutlass = EnemyCutlass();
            var pistol = EnemyPistol();
            // Bando amaldiçoado (M6.5): homens-tubarão de cutelo, esqueletos de pistola.
            var posts = new (string name, Vector3 local, float yaw, bool gunner, string model)[]
            {
                ("Pirata_PortaoEsq", new Vector3(-2.6f, 0f, CampRadius + 1.6f), -10f, false, KitCharacters.Mako),
                ("Pirata_PortaoDir", new Vector3(2.6f, 0f, CampRadius + 1.6f), 12f, false, KitCharacters.Sharky),
                ("Pirata_Fogueira", new Vector3(2.4f, 0f, 2.2f), 15f, false, KitCharacters.Sharky),
                // Duelista (pacote B, passo 2): no lugar do pirata das cabanas.
                ("Duelista_Cabanas", new Vector3(-6f, 0f, 3.5f), -25f, false, KitCharacters.Anne),
                ("Pirata_PortaoSul", new Vector3(0.5f, 0f, -CampRadius - 1.4f), 180f, false, KitCharacters.Sharky),
                ("Atirador_Portao", new Vector3(5f, 0f, CampRadius - 3f), 5f, true, KitCharacters.Skeleton),
                ("Atirador_Bandeira", new Vector3(-4.5f, 0f, -10f), -5f, true, KitCharacters.HeadlessSkeleton),
                ("Atirador_Torre", new Vector3(8.5f, 0f, -5.5f), 160f, true, KitCharacters.Skeleton),
                // Bacamarteiros (passo 3, 09/10): perto da fogueira e do portão sul, onde o capitão entra no corpo a corpo.
                ("Bacamarteiro_Fogueira", new Vector3(-2.6f, 0f, 1.8f), -30f, true, KitCharacters.Mako),
                ("Bacamarteiro_PortaoSul", new Vector3(3.2f, 0f, -CampRadius + 1.2f), 170f, true, KitCharacters.Sharky),
                // Brutamontes (pacote B, passo 1): no meio do pátio, entre a fogueira e o portão sul.
                ("Brutamontes_Patio", new Vector3(-0.5f, 0f, -3.5f), 175f, false, KitCharacters.Sharky),
            };
            var parent = new GameObject("Piratas").transform;
            var members = new EnemyFootAI[posts.Length];
            for (int k = 0; k < posts.Length; k++)
                members[k] = CreatePirate(parent, posts[k].name, campSite.TransformPoint(posts[k].local), posts[k].yaw,
                    PostWeapon(posts[k].name, posts[k].gunner, cutlass, pistol), player, camp, iron, wood, brass, posts[k].model);
            camp.Configure(campName, campId, members, leash: 40f, attackersAtOnce: 2);
            camp.SetKegs(campSite.GetComponentsInChildren<PowderKeg>(true));
            return camp;
        }

        static EnemyFootAI CreatePirate(Transform parent, string name, Vector3 position, float yaw, WeaponDefinition weapon,
            FootCharacter player, EnemyCamp camp, Material iron, Material wood, Material brass, string kitModel = null)
        {
            GroundAt(position, out Vector3 ground);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(ground + Vector3.up * 0.05f, Quaternion.Euler(0f, yaw, 0f));
            var controller = go.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 50f;
            var character = go.AddComponent<FootCharacter>();
            // Um pouco mais lento que o jogador (6 m/s): dá para fugir.
            character.ConfigureSpeeds(2.2f, 5.2f);
            go.AddComponent<Health>().Configure(60f);

            bool gunner = weapon.kind == WeaponKind.Firearm;
            var tint = gunner ? GunnerTint : PirateTint;
            if (!AddStarterAssetsModel(go.transform, character, tint, gunner ? "_Atirador" : "_Pirata", kitModel))
            {
                var capsule = new GameObject("Model").transform;
                capsule.SetParent(go.transform, false);
                Part(PrimitiveType.Capsule, "Corpo", capsule, new Vector3(0f, 0.9f, 0f), new Vector3(0.6f, 0.9f, 0.6f), Quaternion.identity, GetMaterial("M_Pirata", tint, 0.1f));
            }
            var model = go.transform.Find("Model");
            var combat = go.AddComponent<CharacterCombat>();
            if (weapon.twoHanded)
            {
                // Bacamarteiro: o bacamarte do jogador (duas mãos, pose de rifle), com a mesma linha vermelha de aviso.
                // A mosqueteira da Guarda Negra usa o mosquete-machadinha do kit; os bacamarteiros, o bacamarte.
                var gun = BlunderbussOn(model, iron, wood, out Transform muzzle, out Transform hand, out Transform holster, IsMusketeer(name));
                combat.Configure(new[] { weapon }, new[] { gun }, new[] { hand }, new[] { holster }, muzzle, ShotTracer(go.transform));
                combat.SetMuzzles(new[] { muzzle });
                combat.SetAimSnap(0f);
                var rig = AddAimRig(model, combat, muzzle, AimLine(go.transform, new Color(1f, 0.1f, 0.05f, 0.95f), 0.045f));
                if (rig)
                {
                    RifleAimYaw(model, out float upper, out float full);
                    rig.ConfigureTwoHanded(upper, full);
                }
            }
            else if (gunner)
            {
                var gun = PistolOn(model, iron, wood, brass, out Transform muzzle, out Transform hand, out Transform holster);
                combat.Configure(new[] { weapon }, new[] { gun }, new[] { hand }, new[] { holster }, muzzle, ShotTracer(go.transform));
                // Sem "ímã" no corpo do alvo: a bala vai onde a mira (atrasada) está, e dá para desviar.
                combat.SetAimSnap(0f);
                // Linha de mira vermelha: o aviso de que ele vai atirar.
                AddAimRig(model, combat, muzzle, AimLine(go.transform, new Color(1f, 0.1f, 0.05f, 0.95f), 0.045f));
            }
            else if (IsDuelist(name))
            {
                // Duelista: adaga sempre na mão (não troca de arma).
                var dagger = DaggerOn(model, iron, wood, out Transform hand);
                combat.Configure(new[] { weapon }, new[] { dagger }, new[] { hand }, new[] { hand }, null, null);
            }
            else if (IsBlackGuard(name))
            {
                // Guarda Negra: sabre sempre na mão.
                var sabre = SabreOn(model, iron, wood, out Transform hand);
                combat.Configure(new[] { weapon }, new[] { sabre }, new[] { hand }, new[] { hand }, null, null);
            }
            else if (weapon.animatorWeaponType == 4)
            {
                // Chefe: espadão sempre na mão.
                var sword = GreatswordOn(model, iron, wood, out Transform hand);
                combat.Configure(new[] { weapon }, new[] { sword }, new[] { hand }, new[] { hand }, null, null);
            }
            else if (weapon.animatorWeaponType == 3)
            {
                // Brutamontes: machado duplo sempre na mão (não troca de arma).
                var axe = AxeOn(model, iron, wood, out Transform hand);
                combat.Configure(new[] { weapon }, new[] { axe }, new[] { hand }, new[] { hand }, null, null);
            }
            else
            {
                var sword = CutlassOn(model, iron, wood, brass, out Transform hand, out Transform holster);
                combat.Configure(new[] { weapon }, new[] { sword }, new[] { hand }, new[] { holster }, null, null);
            }
            combat.SetTeam(1);
            go.AddComponent<FootHealthReactions>().Configure(model, 0.6f);
            var ai = go.AddComponent<EnemyFootAI>();
            ai.Configure(player.transform, camp);
            // Chega perto (só mira a 9 m: a 11 m pegavam 3 de 6 chumbos), recua só colado (2,5 m) e segura a mira 1,1–1,5 s (dá para sair
            // do leque correndo de lado).
            if (weapon.twoHanded) ai.ConfigureRanged(6f, 2.5f, 9f, new Vector2(1.1f, 1.5f), 2.5f);
            if (IsBrute(name)) MakeBrute(go, model, ai);
            if (IsDuelist(name)) MakeDuelist(go, model, ai);
            if (IsChief(name)) MakeChief(go, model, ai);
            if (IsBlackGuard(name)) MakeBlackGuard(go, model, ai, kitModel, IsMusketeer(name));
            return ai;
        }

        /// <summary>Cópia tingida de um material do Starter Assets (salva em Materials com o sufixo, feita uma vez por tom).</summary>
        static Material Tinted(Material source, Color tint, string suffix)
        {
            if (!source) return null;
            string path = $"{MaterialsFolder}/{source.name}{suffix}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat) return mat;
            mat = new Material(source) { name = source.name + suffix };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", mat.GetColor("_BaseColor") * tint);
            else mat.color *= tint;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Material GetEmissiveMaterial(string name, Color color, float intensity)
        {
            var mat = GetMaterial(name, color, 0f);
            if (mat.IsKeywordEnabled("_EMISSION") && KeepsEmission(mat)) return mat;
            if (!mat.IsKeywordEnabled("_EMISSION") && mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * intensity);
            Emissive(mat);
            return mat;
        }

        /// <summary>
        /// Liga o brilho (emissão) de um material URP Lit de um jeito que a validação da URP não desliga: com o GI em "None" o LitShader
        /// tira o _EMISSION sempre que valida o material (reimportar, abrir no Inspector), e as lanternas, janelas e os olhos do chefe
        /// apagavam (achado em 10/10). "Realtime" não muda nada no jogo (o projeto não usa GI em tempo real nem luz assada).
        /// </summary>
        static void Emissive(Material mat)
        {
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(mat);
        }

        static bool KeepsEmission(Material mat) => (mat.globalIlluminationFlags & MaterialGlobalIlluminationFlags.RealtimeEmissive) != 0;
    }
}
