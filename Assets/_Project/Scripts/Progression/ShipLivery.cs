using System;
using System.Collections.Generic;
using Pirates.Core;
using UnityEngine;

namespace Pirates.Progression
{
    /// <summary>Parte do navio que se pinta (16/11, "onde gastar o ouro": visuais do navio).</summary>
    public enum LiverySlot { Hull, Trim, Sails, Flag, Emblem, SailMark, Figurehead, Lanterns }

    /// <summary>Uma cor à venda no estaleiro para uma parte do navio. <c>original</c> = as cores do kit (de graça, já vem com o navio).</summary>
    [Serializable]
    public class LiveryOption
    {
        public string id;
        public string displayName;
        public LiverySlot slot;
        public Color color = Color.white;
        public bool original;
        [Min(0)] public int price;
        [Tooltip("Patente mínima para comprar (0 = qualquer).")]
        [Min(0)] public int requiredRank;
        [Tooltip("Só no emblema: o desenho (branco com alfa, legível) posto no meio da bandeira. Vazio = bandeira lisa.")]
        public Texture2D emblem;
        [Tooltip("Só na carranca e nas lanternas: nome da peça (filho de cada grupo \"Ornamentos\" dos cascos) que esta opção liga. Vazio = nenhuma.")]
        public string ornament;
    }

    /// <summary>
    /// Pintura do navio do jogador (16/11): casco, faixa, velas e bandeira pintados com as cores compradas no estaleiro. O gerador
    /// copia as malhas do navio (os três cascos) com os triângulos do casco de fora, das velas e da bandeira levados para faixas
    /// vazias da paleta do Kenney; aqui uma cópia da paleta recebe nessas faixas a cor escolhida (com o mesmo degradê da madeira/
    /// do pano do kit) e vai num material próprio para todas as peças do navio. A cor "original" copia a faixa do kit: igual a antes.
    /// </summary>
    public class ShipLivery : MonoBehaviour, IShipLivery
    {
        [SerializeField] LiveryCatalog catalog;
        [Tooltip("Cópia legível da paleta do Kenney (a do kit não é legível).")]
        [SerializeField] Texture2D baseTexture;
        [Tooltip("Material do navio (M_Kenney): a cópia pintada parte dele.")]
        [SerializeField] Material baseMaterial;
        [Tooltip("Cascos, velas e bandeiras dos três navios (com as malhas pintáveis).")]
        [SerializeField] Renderer[] renderers = Array.Empty<Renderer>();
        [Tooltip("Grupo \"Ornamentos\" de cada casco: carrancas e lanternas montadas e desligadas; liga a escolhida.")]
        [SerializeField] Transform[] ornaments = Array.Empty<Transform>();

        // Faixas da paleta (512 px): de onde vem o degradê do kit (x, linhas 0–127) e para onde vai a pintura (mesmo x, linha de destino).
        const int BandWidth = 32, BandHeight = 128;
        static readonly (LiverySlot slot, int x, int destY)[] Bands =
        {
            (LiverySlot.Hull, 352, 384), (LiverySlot.Trim, 416, 384), (LiverySlot.Sails, 160, 384), (LiverySlot.SailMark, 224, 384),
        };
        // A bandeira: as malhas pintáveis põem a bandeira inteira (projeção de lado) num quadrado de 128 px da paleta; aqui ele recebe
        // o pano (com o degradê do pano do kit, de baixo para cima) e o emblema no meio.
        const int FlagCellX = 224, FlagCellY = 256, FlagCell = 128, FlagClothX = 160, EmblemSize = 78;
        static readonly Color EmblemLight = new(0.93f, 0.9f, 0.8f), EmblemDark = new(0.1f, 0.09f, 0.1f);

        readonly HashSet<string> owned = new();
        readonly Dictionary<LiverySlot, string> selected = new();
        Texture2D painted;
        Material material;

        public static ShipLivery Current { get; private set; }
        public LiveryCatalog Catalog => catalog;
        /// <summary>Muda a cada compra ou troca (a janela do estaleiro remonta por ele).</summary>
        public int Version { get; private set; }
        public event Action Changed;

        public void Configure(LiveryCatalog liveryCatalog, Texture2D palette, Material material, Renderer[] parts, Transform[] ornamentGroups)
        {
            ornaments = ornamentGroups ?? Array.Empty<Transform>();
            catalog = liveryCatalog;
            baseTexture = palette;
            baseMaterial = material;
            renderers = parts;
        }

        void Awake()
        {
            Current = this;
            Services.Register<IShipLivery>(this);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            Services.Unregister<IShipLivery>(this);
            if (painted) Destroy(painted);
            if (material) Destroy(material);
        }

        void Start() => Apply();

        // ---------- Consulta ----------

        public bool Owns(LiveryOption option) => option != null && (option.original || option.price <= 0 || owned.Contains(option.id));

        public LiveryOption Selected(LiverySlot slot)
        {
            if (catalog && selected.TryGetValue(slot, out var id))
            {
                var o = catalog.Find(id);
                if (o != null && o.slot == slot && Owns(o)) return o;
            }
            return catalog ? catalog.Original(slot) : null;
        }

        public bool IsSelected(LiverySlot slot, LiveryOption option) => option != null && Selected(slot) == option;

        // ---------- Compra e troca ----------

        /// <summary>Marca como comprada (quem cobra é o estaleiro) e já pinta.</summary>
        public void Grant(LiveryOption option)
        {
            if (option == null) return;
            owned.Add(option.id);
            Select(option);
        }

        public void Select(LiveryOption option)
        {
            if (option == null || !Owns(option)) return;
            selected[option.slot] = option.id;
            Apply();
        }

        // ---------- Save ----------

        public string[] Owned
        {
            get
            {
                var list = new string[owned.Count];
                owned.CopyTo(list);
                return list;
            }
        }

        public string[] SelectedIds
        {
            get
            {
                var list = new List<string>();
                foreach (var (slot, id) in selected) list.Add($"{slot}={id}");
                return list.ToArray();
            }
        }

        public void Restore(bool has, string[] ownedIds, string[] selectedIds)
        {
            owned.Clear();
            selected.Clear();
            if (has)
            {
                if (ownedIds != null) foreach (var id in ownedIds) if (!string.IsNullOrEmpty(id)) owned.Add(id);
                if (selectedIds != null)
                    foreach (var pair in selectedIds)
                    {
                        int eq = pair?.IndexOf('=') ?? -1;
                        if (eq <= 0 || !Enum.TryParse(pair.Substring(0, eq), out LiverySlot slot)) continue;
                        selected[slot] = pair.Substring(eq + 1);
                    }
            }
            Apply();
        }

        // ---------- Pintura ----------

        /// <summary>Refaz a paleta pintada e põe o material em todas as peças.</summary>
        public void Apply()
        {
            Version++;
            ApplyOrnaments();
            if (!baseTexture || !baseMaterial || !catalog)
            {
                Changed?.Invoke();
                return;
            }
            if (!painted)
            {
                painted = new Texture2D(baseTexture.width, baseTexture.height, TextureFormat.RGBA32, false)
                {
                    name = "Navio_Pintado", filterMode = baseTexture.filterMode, wrapMode = baseTexture.wrapMode,
                };
                painted.SetPixels32(baseTexture.GetPixels32());
            }
            foreach (var (slot, x, destY) in Bands) PaintBand(x, destY, Selected(slot));
            PaintFlag(Selected(LiverySlot.Flag), Selected(LiverySlot.Emblem));
            painted.Apply(false);
            if (!material)
            {
                material = new Material(baseMaterial) { name = "M_Navio_Pintado" };
                material.SetTexture("_BaseMap", painted);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", painted);
            }
            foreach (var r in renderers)
                if (r) r.sharedMaterial = material;
            Changed?.Invoke();
        }

        /// <summary>Liga em cada casco só a carranca e as lanternas escolhidas.</summary>
        void ApplyOrnaments()
        {
            if (!catalog) return;
            string head = Selected(LiverySlot.Figurehead)?.ornament, lamps = Selected(LiverySlot.Lanterns)?.ornament;
            foreach (var group in ornaments)
            {
                if (!group) continue;
                for (int i = 0; i < group.childCount; i++)
                {
                    var child = group.GetChild(i);
                    bool on = (!string.IsNullOrEmpty(head) && child.name == head) || (!string.IsNullOrEmpty(lamps) && child.name == lamps);
                    if (child.gameObject.activeSelf != on) child.gameObject.SetActive(on);
                }
            }
        }

        /// <summary>
        /// Copia a faixa do kit (linhas 0–127) para a de destino; com cor, troca pela cor escolhida com o mesmo claro/escuro de cada linha
        /// (o degradê que o kit usa para sombrear a madeira e o pano).
        /// </summary>
        void PaintBand(int x, int destY, LiveryOption option)
        {
            var src = baseTexture.GetPixels(x, 0, BandWidth, BandHeight);
            if (option != null && !option.original)
            {
                float mean = 0f;
                foreach (var c in src) mean += Luma(c);
                mean = Mathf.Max(0.01f, mean / src.Length);
                for (int i = 0; i < src.Length; i++)
                {
                    float shade = Mathf.Clamp(Luma(src[i]) / mean, 0.65f, 1.35f);
                    var c = option.color * shade;
                    c.a = 1f;
                    src[i] = c;
                }
            }
            painted.SetPixels(x, destY, BandWidth, BandHeight, src);
        }

        /// <summary>Quadrado da bandeira: pano na cor escolhida (original = a média do pano do kit) e o emblema clarinho (ou escuro em pano claro).</summary>
        void PaintFlag(LiveryOption cloth, LiveryOption emblem)
        {
            var band = baseTexture.GetPixels(FlagClothX, 0, BandWidth, BandHeight);
            var rows = new float[BandHeight];
            float mean = 0f;
            for (int y = 0; y < BandHeight; y++)
            {
                float sum = 0f;
                for (int x = 0; x < BandWidth; x++) sum += Luma(band[y * BandWidth + x]);
                rows[y] = sum / BandWidth;
                mean += rows[y];
            }
            mean = Mathf.Max(0.01f, mean / BandHeight);
            Color baseColor;
            if (cloth != null && !cloth.original) baseColor = cloth.color;
            else
            {
                baseColor = Color.clear;
                foreach (var c in band) baseColor += c;
                baseColor /= band.Length;
            }
            baseColor.a = 1f;
            var mark = Luma(baseColor) < 0.5f ? EmblemLight : EmblemDark;
            var art = emblem != null ? emblem.emblem : null;
            bool readable = art && art.isReadable;
            int from = (FlagCell - EmblemSize) / 2;
            var cell = new Color[FlagCell * FlagCell];
            for (int y = 0; y < FlagCell; y++)
            {
                float shade = Mathf.Clamp(rows[y * BandHeight / FlagCell] / mean, 0.75f, 1.25f);
                for (int x = 0; x < FlagCell; x++)
                {
                    var c = baseColor;
                    int ex = x - from, ey = y - from;
                    if (readable && ex >= 0 && ey >= 0 && ex < EmblemSize && ey < EmblemSize)
                    {
                        var e = art.GetPixel(ex * art.width / EmblemSize, ey * art.height / EmblemSize);
                        c = Color.Lerp(c, mark, e.a);
                    }
                    c *= shade;
                    c.a = 1f;
                    cell[y * FlagCell + x] = c;
                }
            }
            painted.SetPixels(FlagCellX, FlagCellY, FlagCell, FlagCell, cell);
        }

        static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
    }
}
