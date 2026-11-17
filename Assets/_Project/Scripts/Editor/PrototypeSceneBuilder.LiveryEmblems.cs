using System;
using Pirates.Progression;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Pintura do navio, passo 2 (16/11): cor da bandeira, emblema da bandeira e cor da caveira das velas. Os emblemas são desenhados aqui
    /// por formas (círculos, cápsulas, caixas, polígonos: distância com sinal, borda suavizada em 1 px) e salvos em PNG branco com alfa,
    /// legíveis, em <c>Textures/Emblemas</c>; o ShipLivery os pinta no meio da bandeira na cor que contrasta com o pano.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string EmblemFolder = Root + "/Textures/Emblemas";
        const string EmblemStyle = "emblema-v1";
        const int EmblemPixels = 128;

        /// <summary>Cores novas (bandeira, emblema, caveira das velas) no catálogo, só as que faltam: o resto do asset é do Jhonata.</summary>
        static void AddLiveryExtras(LiveryCatalog catalog, Texture2D palette)
        {
            LiveryOption O(string id, string name, LiverySlot slot, Color color, int price, int rank = 0, bool original = false, Texture2D emblem = null, string ornament = null) => new()
            {
                id = id, displayName = name, slot = slot, color = color, price = price, requiredRank = rank, original = original, emblem = emblem,
                ornament = ornament,
            };
            var dark = new Color(0.16f, 0.15f, 0.17f);
            var extras = new[]
            {
                O("bandeira_vermelha", "Vermelho sangue", LiverySlot.Flag, new Color(0.6f, 0.11f, 0.09f), 300),
                O("bandeira_azul", "Azul-marinho", LiverySlot.Flag, new Color(0.13f, 0.2f, 0.42f), 300),
                O("bandeira_verde", "Verde-garrafa", LiverySlot.Flag, new Color(0.1f, 0.33f, 0.22f), 300),
                O("bandeira_branca", "Branca", LiverySlot.Flag, new Color(0.9f, 0.88f, 0.8f), 400),
                O("bandeira_dourada", "Dourada", LiverySlot.Flag, new Color(0.88f, 0.64f, 0.18f), 1000, 3),
                O("bandeira_roxa", "Roxa de Lázaro", LiverySlot.Flag, new Color(0.32f, 0.14f, 0.42f), 1200, 4),

                O("emblema_nenhum", "Lisa", LiverySlot.Emblem, dark, 0, original: true),
                O("emblema_caveira", "Caveira e ossos", LiverySlot.Emblem, dark, 250, emblem: Emblem("Caveira", SkullAndBones)),
                O("emblema_espadas", "Espadas cruzadas", LiverySlot.Emblem, dark, 400, emblem: Emblem("Espadas", CrossedSwords)),
                O("emblema_ancora", "Âncora", LiverySlot.Emblem, dark, 400, emblem: Emblem("Ancora", Anchor)),
                O("emblema_coroa", "Coroa de Prata", LiverySlot.Emblem, dark, 2000, 4, emblem: Emblem("Coroa", Crown)),

                O("marca_original", "Caveira clara", LiverySlot.SailMark, PaletteBand(palette, 224), 0, original: true),
                O("marca_preta", "Preta", LiverySlot.SailMark, new Color(0.12f, 0.11f, 0.12f), 300),
                O("marca_branca", "Branca como osso", LiverySlot.SailMark, new Color(0.94f, 0.91f, 0.82f), 300),
                O("marca_vermelha", "Vermelha", LiverySlot.SailMark, new Color(0.66f, 0.13f, 0.1f), 400),
                O("marca_dourada", "Dourada", LiverySlot.SailMark, new Color(0.92f, 0.7f, 0.22f), 900, 2),

                O("carranca_nenhuma", "Sem carranca", LiverySlot.Figurehead, new Color(0.55f, 0.35f, 0.24f), 0, original: true),
                O("carranca_dama", "Dama da proa", LiverySlot.Figurehead, FigureheadPainted, 900, ornament: "Carranca_Dama"),
                O("carranca_caveira", "Caveira dourada", LiverySlot.Figurehead, FigureheadGold, 1400, 2, ornament: "Carranca_Caveira"),
                O("carranca_dourada", "Dama dourada", LiverySlot.Figurehead, FigureheadGold, 2500, 3, ornament: "Carranca_DamaDourada"),

                O("lanternas_popa", "Só a da popa", LiverySlot.Lanterns, new Color(0.3f, 0.26f, 0.22f), 0, original: true),
                O("lanternas_proa", "Lanternas na proa", LiverySlot.Lanterns, new Color(1f, 0.72f, 0.4f), 350, ornament: "Lanternas_Proa"),
                O("lanternas_todas", "Proa e popa", LiverySlot.Lanterns, new Color(1f, 0.62f, 0.3f), 700, ornament: "Lanternas_Todas"),
                O("lanternas_verdes", "Fogo-fátuo verde", LiverySlot.Lanterns, GhostLantern, 1500, 4, ornament: "Lanternas_Verdes"),
            };
            var list = new System.Collections.Generic.List<LiveryOption>(catalog.options);
            bool changed = false;
            foreach (var o in extras)
            {
                var have = catalog.Find(o.id);
                if (have == null) { list.Add(o); changed = true; }
                else if (o.emblem && !have.emblem) { have.emblem = o.emblem; changed = true; }
                else if (!string.IsNullOrEmpty(o.ornament) && string.IsNullOrEmpty(have.ornament)) { have.ornament = o.ornament; changed = true; }
            }
            if (!changed) return;
            catalog.options = list.ToArray();
            EditorUtility.SetDirty(catalog);
        }

        /// <summary>Desenha (só se faltar ou o estilo mudou) e devolve o emblema legível.</summary>
        static Texture2D Emblem(string name, Func<Vector2, float> shape)
        {
            EnsureFolder(Root + "/Textures", "Emblemas");
            string path = $"{EmblemFolder}/Emblema_{name}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer && importer.userData == EmblemStyle) return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var tex = new Texture2D(EmblemPixels, EmblemPixels, TextureFormat.RGBA32, false);
            float px = 2f / EmblemPixels;
            for (int y = 0; y < EmblemPixels; y++)
                for (int x = 0; x < EmblemPixels; x++)
                {
                    var p = new Vector2((x + 0.5f) * px - 1f, (y + 0.5f) * px - 1f);
                    float a = Mathf.Clamp01(0.5f - shape(p) / px);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.userData = EmblemStyle;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------- Formas (distância com sinal: negativo = dentro) ----------

        static float SdCircle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        static float SdSegment(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            var pa = p - a;
            var ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        }

        static float SdBox(Vector2 p, Vector2 c, Vector2 half, float round)
        {
            var d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + new Vector2(round, round);
            return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - round;
        }

        static float SdPolygon(Vector2 p, Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                var e = v[j] - v[i];
                var w = p - v[i];
                var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        /// <summary>Osso: cápsula com duas bolinhas em cada ponta.</summary>
        static float SdBone(Vector2 p, Vector2 a, Vector2 b)
        {
            var dir = (b - a).normalized;
            var n = new Vector2(-dir.y, dir.x);
            float d = SdSegment(p, a, b, 0.06f);
            foreach (var end in new[] { a - dir * 0.02f, b + dir * 0.02f })
                d = Mathf.Min(d, Mathf.Min(SdCircle(p, end + n * 0.065f, 0.075f), SdCircle(p, end - n * 0.065f, 0.075f)));
            return d;
        }

        // ---------- Emblemas ----------

        static float SkullAndBones(Vector2 p)
        {
            p.y -= 0.12f;
            float skull = Mathf.Min(SdCircle(p, new Vector2(0f, 0.25f), 0.42f), SdBox(p, new Vector2(0f, -0.12f), new Vector2(0.24f, 0.17f), 0.07f));
            float holes = Mathf.Min(SdCircle(p, new Vector2(-0.16f, 0.2f), 0.115f), SdCircle(p, new Vector2(0.16f, 0.2f), 0.115f));
            holes = Mathf.Min(holes, SdPolygon(p, new[] { new Vector2(0f, 0.06f), new Vector2(-0.06f, -0.06f), new Vector2(0.06f, -0.06f) }));
            for (int t = -1; t <= 1; t++) holes = Mathf.Min(holes, SdBox(p, new Vector2(t * 0.085f, -0.24f), new Vector2(0.014f, 0.08f), 0f));
            skull = Mathf.Max(skull, -holes);
            float bones = Mathf.Min(SdBone(p, new Vector2(-0.7f, -0.86f), new Vector2(0.7f, -0.4f)), SdBone(p, new Vector2(0.7f, -0.86f), new Vector2(-0.7f, -0.4f)));
            bones = Mathf.Max(bones, -(Mathf.Max(skull, -holes) - 0.05f));
            return Mathf.Min(skull, bones);
        }

        static float CrossedSwords(Vector2 p)
        {
            float d = 1f;
            foreach (float s in new[] { -1f, 1f })
            {
                var bottom = new Vector2(-0.58f * s, -0.7f);
                var tip = new Vector2(0.6f * s, 0.78f);
                var dir = (tip - bottom).normalized;
                var n = new Vector2(-dir.y, dir.x);
                var guard = bottom + dir * 0.32f;
                // lâmina: mais larga perto da guarda, afinando até a ponta
                float blade = SdPolygon(p, new[] { guard + n * 0.075f, tip - dir * 0.12f + n * 0.05f, tip, tip - dir * 0.12f - n * 0.03f, guard - n * 0.055f });
                float cross = SdSegment(p, guard - n * 0.18f, guard + n * 0.18f, 0.045f);
                float grip = SdSegment(p, bottom + dir * 0.05f, guard, 0.042f);
                float pommel = SdCircle(p, bottom, 0.075f);
                d = Mathf.Min(d, Mathf.Min(Mathf.Min(blade, cross), Mathf.Min(grip, pommel)));
            }
            return d;
        }

        static float Anchor(Vector2 p)
        {
            float ring = Mathf.Abs(SdCircle(p, new Vector2(0f, 0.7f), 0.13f)) - 0.045f;
            float shank = SdBox(p, new Vector2(0f, 0.02f), new Vector2(0.055f, 0.6f), 0.02f);
            float bar = SdBox(p, new Vector2(0f, 0.38f), new Vector2(0.32f, 0.05f), 0.03f);
            float arc = Mathf.Max(Mathf.Abs(SdCircle(p, new Vector2(0f, -0.05f), 0.6f)) - 0.06f, p.y + 0.3f);
            float flukes = 1f;
            foreach (float s in new[] { -1f, 1f })
                flukes = Mathf.Min(flukes, SdPolygon(p, new[] { new Vector2(s * 0.44f, -0.36f), new Vector2(s * 0.74f, -0.1f), new Vector2(s * 0.66f, -0.42f) }));
            return Mathf.Min(Mathf.Min(ring, shank), Mathf.Min(Mathf.Min(bar, arc), flukes));
        }

        static float Crown(Vector2 p)
        {
            p.y += 0.04f;
            float body = SdPolygon(p, new[]
            {
                new Vector2(-0.65f, -0.36f), new Vector2(-0.72f, 0.36f), new Vector2(-0.36f, 0.05f), new Vector2(0f, 0.56f),
                new Vector2(0.36f, 0.05f), new Vector2(0.72f, 0.36f), new Vector2(0.65f, -0.36f),
            });
            float band = SdBox(p, new Vector2(0f, -0.48f), new Vector2(0.7f, 0.11f), 0.03f);
            float jewels = Mathf.Min(SdCircle(p, new Vector2(-0.72f, 0.44f), 0.09f), Mathf.Min(SdCircle(p, new Vector2(0f, 0.66f), 0.1f), SdCircle(p, new Vector2(0.72f, 0.44f), 0.09f)));
            float crown = Mathf.Min(Mathf.Min(body, band), jewels);
            float gaps = Mathf.Abs(p.y + 0.36f) - 0.022f;
            gaps = Mathf.Max(gaps, Mathf.Abs(p.x) - 0.66f);
            float stones = Mathf.Min(SdCircle(p, new Vector2(-0.36f, -0.48f), 0.055f), Mathf.Min(SdCircle(p, new Vector2(0f, -0.48f), 0.06f), SdCircle(p, new Vector2(0.36f, -0.48f), 0.055f)));
            return Mathf.Max(crown, -Mathf.Min(gaps, stones));
        }
    }
}
