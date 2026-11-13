using System.Collections.Generic;
using Pirates.Combat;
using Pirates.LandCombat;
using Pirates.OnFoot;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Lázaro Negro, chefe final a pé (pacote H, passo 3, 06/11; HISTORIA.md, capítulo 6): o capitão rival do prólogo (o modelo de capitão
    /// do kit, casaco roxo, barba grisalha, gancho e punhos de prata) 45% maior, olhos de prata que brilham, ectoplasma verde da maldição e
    /// o espadão de prata. Luta como o Barba-Negra (<see cref="LandBoss"/>), mais forte; na fase 2 a névoa chama mortos (<see cref="LazarusCurse"/>).
    /// Fica escondido no templo do alto até o cerco do altar ser quebrado. O templo: piso de pedra das ruínas no alto, um arco gótico com a
    /// bandeira ao norte, colunas, velas e caveiras em volta do altar.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string LazarusBounty = "lazaro_negro";
        const float LazarusHealth = 1000f, LazarusScale = 1.45f, LazarusDamage = 1.15f;
        static Vector2 LazarusPost => CursedSummit + new Vector2(0f, 6.5f);
        static readonly Color LazarusEyes = new(0.75f, 0.92f, 1f);

        /// <summary>O templo do alto (antes do NavMesh): piso, arco com a bandeira, colunas, velas e caveiras.</summary>
        static void CursedTemple(Transform island)
        {
            var root = new GameObject("Templo").transform;
            root.SetParent(island, false);
            root.localPosition = new Vector3(CursedSummit.x, CursedSummitHeight + 0.005f, CursedSummit.y);
            // Piso (o alto é plano: platô na altura do topo): placas de 2 m até 8,5 m do altar, umas faltando.
            var rng = new System.Random(66);
            for (int ix = -4; ix <= 4; ix++)
            for (int iz = -4; iz <= 4; iz++)
            {
                var p = new Vector2(ix * 2f, iz * 2f);
                if (p.magnitude > 8.5f || rng.NextDouble() < 0.12) continue;
                string model = rng.NextDouble() < 0.25 ? "Floor_Diamond" : "Floor_Standard";
                var tile = RuinPiece(root, model, new Vector3(p.x, -0.03f, p.y), 90f * rng.Next(4), $"Piso_{ix}_{iz}");
                RuinsRecolor(tile, "Piso_");
            }
            // Fundo do templo, ao norte: o arco gótico com a bandeira de Lázaro e duas colunas.
            var arch = RuinPiece(root, "Arch_Gothic_RoundColumn", new Vector3(0f, 0f, 11.5f), 180f, "Arco_Templo", 1.6f);
            SolidProp(arch.gameObject);
            Occluder(arch.gameObject);
            var flag = RuinPiece(root, "Flag_GothicArch", new Vector3(0f, 0f, 11.45f), 180f, "Bandeira_Templo", 1.6f);
            RuinsRecolor(flag, "Lazaro_");
            foreach (float x in new[] { -5.2f, 5.2f })
            {
                var col = RuinPiece(root, "Column_Round", new Vector3(x, 0f, 10.6f), 0f, "Coluna_Templo", 1.4f);
                SolidProp(col.gameObject);
                Occluder(col.gameObject);
            }
            // Velas e caveiras em volta do altar.
            foreach (var (x, z, yaw) in new[] { (-2.6f, 1.2f, 20f), (2.7f, 0.8f, -30f), (-2.2f, -2f, 60f), (2.4f, -1.8f, 110f) })
                RuinPiece(root, "Candles_2", new Vector3(x, 0f, z), yaw, "Velas", 0.7f);
            foreach (var (x, z, yaw) in new[] { (-1.6f, 2.6f, 30f), (1.8f, 2.4f, -40f), (3.4f, -3.2f, 200f), (-3.8f, -3.0f, 150f), (0.6f, -3.6f, 180f) })
                RuinPiece(root, "Skull", new Vector3(x, 0f, z), yaw, "Caveira", 1.4f);
            foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
        }

        /// <summary>Lázaro (escondido) no templo, com os mortos que a névoa chama na fase 2 e o <see cref="LazarusCurse"/>.</summary>
        static LazarusCurse AddLazarus(Transform island, Transform siegeRoot, AltarSiege siege, FootCharacter player, LandMaterials mats,
            WeaponDefinition cutlass, WeaponDefinition pistol)
        {
            var site = new GameObject("TemploDeLazaro").transform;
            site.SetParent(island, true);
            site.position = SkullGround(island, CursedSummit);
            var camp = site.gameObject.AddComponent<EnemyCamp>();
            var post = SkullGround(island, LazarusPost);
            if (NavMesh.SamplePosition(post, out var hit, 1.5f, NavMesh.AllAreas)) post = hit.position;

            pendingChief = new ChiefSpec { name = "Lázaro Negro", title = "o capitão amaldiçoado da prata", health = LazarusHealth, scale = LazarusScale };
            var ai = CreatePirate(site, "Chefe_Lazaro", post, 180f, LazarusSword(), player, camp, mats.iron, mats.wood, mats.brass, KitCharacters.Captain);
            pendingChief = null;
            ai.ConfigureBounty(LazarusBounty);
            var go = ai.gameObject;
            var model = go.transform.Find("Model");

            // Roupa do Lázaro do prólogo (casaco roxo, barba grisalha, prata), olhos de prata acesos.
            var outfit = LazarusMaterial();
            var body = new List<Renderer>();
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = outfit;
                r.sharedMaterials = slots;
                body.Add(r);
            }
            // Espadão de prata.
            var silver = SilverBlade();
            var blade = new List<Renderer>();
            var sword = go.GetComponent<CharacterCombat>().WeaponVisual(0);
            if (sword)
                foreach (var r in sword.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var slots = r.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = silver;
                    r.sharedMaterials = slots;
                    blade.Add(r);
                }
            // Aura de prata (fase 2) no lugar das brasas vermelhas, e o ectoplasma verde da maldição sempre.
            var aura = go.transform.Find("Aura")?.GetComponent<ParticleSystem>();
            if (aura)
            {
                var main = aura.main;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.95f, 1f, 0.9f), new Color(0.35f, 0.9f, 0.6f, 0.8f));
            }
            GhostWisps(go.transform, FxMaterial("M_FX_Brilho", "circle_05", additive: true));
            var boss = go.GetComponent<LandBoss>();
            boss.Configure("Lázaro Negro", "o capitão amaldiçoado da prata", ChiefMoves(), new[] { 0, 1 }, body.ToArray(), blade.ToArray(),
                SilverBladeGlow(), aura);
            boss.SetWaitForGuards(false, 0f);
            boss.SetEyeColor(LazarusEyes * 1.6f);

            camp.Configure("Templo de Lázaro", "templo_lazaro", new[] { ai }, leash: 24f, attackersAtOnce: 1);
            camp.SetFame(150);
            camp.SetHiddenFromHud(true);

            // Os mortos que a névoa chama (fase 2): em volta do altar, como uma onda do cerco, sem publicar.
            var called = WaveGroup(siegeRoot, island, 3, player, mats, cutlass, pistol,
                new[] { "Pirata_Lazaro_1", "Pirata_Lazaro_2", "Atirador_Lazaro", "Pirata_Lazaro_3" });
            called.gameObject.name = "MortosDeLazaro";

            var clips = GraveyardRiseClips();
            var rise = clips.Length > 0 ? clips[0] : new CursedGraveyard.RiseClip { startSeconds = 0.9f, seconds = 1.6f };
            var curse = site.gameObject.AddComponent<LazarusCurse>();
            curse.Configure(siege, ai, called, 0, rise.startSeconds, rise.seconds);
            return curse;
        }

        /// <summary>Espadão de Lázaro: o do Barba-Negra com os golpes 15% mais fortes (refeito a cada geração).</summary>
        static WeaponDefinition LazarusSword()
        {
            var w = LoadOrCreateAsset<WeaponDefinition>(WeaponsFolder + "/Weapon_Espadao_Lazaro.asset", _ => { });
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(ChiefSwordAsset()), w);
            w.name = "Weapon_Espadao_Lazaro";
            w.displayName = "Espadão de prata";
            w.damage *= LazarusDamage;
            if (w.variantStrikes != null)
                for (int i = 0; i < w.variantStrikes.Length; i++)
                {
                    var s = w.variantStrikes[i];
                    s.damage *= LazarusDamage;
                    w.variantStrikes[i] = s;
                }
            EditorUtility.SetDirty(w);
            return SteeredByHands(WithSupportHand(w, GreatswordSupport));
        }

        /// <summary>Roupa do Lázaro do prólogo (paleta 2 do capitão) com os olhos de prata acesos (a máscara dos olhos do Barba-Negra).</summary>
        static Material LazarusMaterial()
        {
            ChiefMaterial();   // garante a máscara dos olhos (Textures/Civis/Chefe_Olhos.png)
            var source = CivilianMaterial(KitCharacters.Captain, 2);
            string path = $"{MaterialsFolder}/M_Lazaro.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(source) { name = "M_Lazaro" };
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.CopyPropertiesFromMaterial(source);
            mat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/Civis/Chefe_Olhos.png"));
            mat.SetColor("_EmissionColor", LazarusEyes * 1.4f);
            Emissive(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Lâmina de prata: o atlas do kit clareado, lisa e com um brilho frio fraco.</summary>
        static Material SilverBlade()
        {
            var mat = GetTexturedMaterial("M_Espadao_Prata", QuaterniusAtlas, 0.75f);
            mat.SetColor("_BaseColor", new Color(1.35f, 1.4f, 1.5f));
            mat.SetFloat("_Metallic", 0.6f);
            mat.SetColor("_EmissionColor", new Color(0.12f, 0.16f, 0.2f));
            Emissive(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Lâmina de prata acesa (aviso do golpe imparável e fase 2): brilho branco-azulado.</summary>
        static Material SilverBladeGlow()
        {
            var mat = GetTexturedMaterial("M_Espadao_Prata_Furia", QuaterniusAtlas, 0.4f);
            mat.SetColor("_EmissionColor", new Color(0.6f, 0.85f, 1f) * 2.4f);
            Emissive(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
