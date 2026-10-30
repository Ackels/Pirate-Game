using System;
using System.Collections.Generic;
using Pirates.Core;
using Pirates.Economy;
using UnityEngine;

namespace Pirates.Progression
{
    public enum FishTime { Any, Day, Night }
    public enum FishWeather { Any, Rain, Storm }

    /// <summary>
    /// Quando e onde um peixe morde (D2, 30/10): hora (dia/noite), tempo (chuva/tempestade), regiões (ids do <see cref="FishJournal"/>;
    /// vazio = qualquer água) e "uma vez só" (os lendários: pescado, sai do sorteio para sempre). Vale para a vara e para a rede.
    /// </summary>
    [Serializable]
    public struct FishWhen
    {
        public FishTime time;
        public FishWeather weather;
        [Tooltip("Ids das regiões (vazio = qualquer água).")]
        public string[] regions;
        [Tooltip("Lendário: depois de pescado não morde mais.")]
        public bool once;
    }

    /// <summary>
    /// Diário do pescador (D2, 30/10; pedido do Jhonata: "mais tipos de peixes e dar mais propósitos para os peixes"): cada espécie que o
    /// capitão pesca (vara ou rede) entra no diário com quantas já vieram e o maior tamanho (cada peixe sai com um tamanho sorteado). Uma
    /// espécie nova dá fama (o lendário, mais). Prêmios: <see cref="rodPrizeAt"/> espécies = vara de ouro (fisga mais fácil), todas as
    /// comuns a épicas = rede reforçada (cabe mais), os lendários todos = ouro e fama. Também diz em que região do mapa um ponto fica e se
    /// um peixe morde ali agora (<see cref="Allows"/>). O save guarda tudo (<see cref="IFishJournal"/>).
    /// </summary>
    public class FishJournal : MonoBehaviour, IFishJournal
    {
        /// <summary>Uma espécie do diário: tamanho (m) sorteado entre o mínimo e o máximo, e a dica de onde/quando ela morde.</summary>
        [Serializable]
        public class Species
        {
            public ItemDefinition item;
            public Vector2 size = new(0.2f, 0.4f);
            [TextArea] public string where;
            public bool legendary;
        }

        /// <summary>Região do mapa (ilha ou mar): um círculo no chão.</summary>
        [Serializable]
        public class Region
        {
            public string id, name;
            public Vector3 center;
            public float radius = 150f;
        }

        public struct Result
        {
            public float size;
            public bool isNew, isRecord;
        }

        [SerializeField] Species[] species = Array.Empty<Species>();
        [SerializeField] Region[] regions = Array.Empty<Region>();
        [Tooltip("Porão do navio do jogador: onde os prêmios do diário chegam.")]
        [SerializeField] Inventory hold;

        [Header("Prêmios")]
        [SerializeField] ItemDefinition goldRod;
        [SerializeField] ItemDefinition strongNet;
        [SerializeField] ItemDefinition gold;
        [Tooltip("Espécies diferentes para ganhar a vara de ouro.")]
        [SerializeField] int rodPrizeAt = 10;
        [SerializeField] int legendGold = 600, legendFame = 150;
        [Tooltip("Fama por espécie nova no diário (lendário: a outra).")]
        [SerializeField] int newSpeciesFame = 3, newLegendFame = 20;

        [Header("Hora e tempo")]
        [Tooltip("Noite a partir disto (ITimeOfDay.Night 0–1).")]
        [SerializeField] float nightFrom = 0.5f;
        [Tooltip("Chuva/tempestade a partir disto (IWeather 0–1).")]
        [SerializeField] float weatherFrom = 0.5f;

        public const int PrizeRod = 1, PrizeNet = 2, PrizeLegends = 4;

        struct Entry
        {
            public int count;
            public float best;
        }

        readonly Dictionary<ItemDefinition, Entry> entries = new();
        int prizes;

        public static FishJournal Current { get; private set; }
        public int Version { get; private set; }
        public IReadOnlyList<Species> AllSpecies => species;
        public ItemDefinition GoldRod => goldRod;
        public ItemDefinition StrongNet => strongNet;
        public int RodPrizeAt => rodPrizeAt;
        public int LegendGold => legendGold;
        public int LegendFame => legendFame;
        public int Prizes => prizes;
        /// <summary>Espécies diferentes já pescadas.</summary>
        public int Found => entries.Count;
        public int CommonTotal { get; private set; }
        public int LegendTotal { get; private set; }

        /// <summary>Pescou uma espécie (vara ou rede): tamanho sorteado, se é nova, se é recorde.</summary>
        public static event Action<ItemDefinition, Result> Logged;
        /// <summary>Prêmio do diário recebido (texto para a faixa).</summary>
        public static event Action<string, string> PrizeWon;

        void Awake()
        {
            Current = this;
            CommonTotal = LegendTotal = 0;
            foreach (var s in species)
            {
                if (s == null || !s.item) continue;
                if (s.legendary) LegendTotal++;
                else CommonTotal++;
            }
        }

        void OnEnable() => Services.Register<IFishJournal>(this);
        void OnDisable() => Services.Unregister<IFishJournal>(this);

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public void Configure(Species[] list, Region[] areas, Inventory playerHold, ItemDefinition rodPrize, ItemDefinition netPrize, ItemDefinition goldItem)
        {
            species = list;
            regions = areas;
            hold = playerHold;
            goldRod = rodPrize;
            strongNet = netPrize;
            gold = goldItem;
        }

        // ---------- Onde e quando ----------

        /// <summary>Região do ponto (a mais perto entre as que o contêm), ou null no mar aberto.</summary>
        public Region RegionAt(Vector3 point)
        {
            Region found = null;
            float best = float.MaxValue;
            foreach (var r in regions)
            {
                if (r == null) continue;
                float dx = point.x - r.center.x, dz = point.z - r.center.z;
                float d2 = dx * dx + dz * dz;
                if (d2 <= r.radius * r.radius && d2 < best)
                {
                    best = d2;
                    found = r;
                }
            }
            return found;
        }

        /// <summary>O peixe com essas regras morde em <paramref name="point"/> agora?</summary>
        public static bool Allows(in FishWhen when, ItemDefinition item, Vector3 point)
        {
            var journal = Current;
            if (when.once && journal && journal.Count(item) > 0) return false;
            if (when.time != FishTime.Any)
            {
                bool night = Services.TryGet(out ITimeOfDay time) && time.Night >= (journal ? journal.nightFrom : 0.5f);
                if (night != (when.time == FishTime.Night)) return false;
            }
            if (when.weather != FishWeather.Any)
            {
                float from = journal ? journal.weatherFrom : 0.5f;
                if (!Services.TryGet(out IWeather weather)) return false;
                bool ok = when.weather == FishWeather.Storm ? weather.Storm >= from : Mathf.Max(weather.Rain, weather.Storm) >= from;
                if (!ok) return false;
            }
            if (when.regions != null && when.regions.Length > 0)
            {
                var region = journal ? journal.RegionAt(point) : null;
                if (region == null || Array.IndexOf(when.regions, region.id) < 0) return false;
            }
            return true;
        }

        // ---------- Diário ----------

        public int Count(ItemDefinition item) => item && entries.TryGetValue(item, out var e) ? e.count : 0;
        public float Best(ItemDefinition item) => item && entries.TryGetValue(item, out var e) ? e.best : 0f;

        public Species Find(ItemDefinition item)
        {
            foreach (var s in species) if (s != null && s.item == item) return s;
            return null;
        }

        public int FoundLegends
        {
            get
            {
                int n = 0;
                foreach (var s in species) if (s != null && s.legendary && Count(s.item) > 0) n++;
                return n;
            }
        }

        public int FoundCommon => Found - FoundLegends;

        /// <summary>Um peixe pescado (vara ou rede): sorteia o tamanho, anota e paga o que o diário der. Item fora do diário = nada.</summary>
        public Result Log(ItemDefinition item)
        {
            var s = Find(item);
            if (s == null) return default;
            // Peixe grande é mais raro que o pequeno.
            float size = Mathf.Lerp(s.size.x, s.size.y, Mathf.Pow(UnityEngine.Random.value, 1.6f));
            entries.TryGetValue(item, out var e);
            var result = new Result { size = size, isNew = e.count == 0, isRecord = e.count > 0 && size > e.best };
            e.count++;
            e.best = Mathf.Max(e.best, size);
            entries[item] = e;
            Version++;
            if (result.isNew && CaptainFame.Current)
                CaptainFame.Current.Add(s.legendary ? newLegendFame : newSpeciesFame, s.legendary ? "peixe lendário no diário (P)" : "espécie nova no diário (P)");
            Logged?.Invoke(item, result);
            CheckPrizes();
            return result;
        }

        void CheckPrizes()
        {
            if ((prizes & PrizeRod) == 0 && Found >= rodPrizeAt)
            {
                prizes |= PrizeRod;
                Give(goldRod, 1);
                PrizeWon?.Invoke("Diário do pescador: " + (goldRod ? goldRod.displayName : "vara de ouro") + "!", $"{rodPrizeAt} espécies no diário");
            }
            if ((prizes & PrizeNet) == 0 && CommonTotal > 0 && FoundCommon >= CommonTotal)
            {
                prizes |= PrizeNet;
                Give(strongNet, 1);
                PrizeWon?.Invoke("Diário do pescador: " + (strongNet ? strongNet.displayName : "rede reforçada") + "!", "todos os peixes do mar");
            }
            if ((prizes & PrizeLegends) == 0 && LegendTotal > 0 && FoundLegends >= LegendTotal)
            {
                prizes |= PrizeLegends;
                Give(gold, legendGold);
                if (CaptainFame.Current) CaptainFame.Current.Add(legendFame, "todos os peixes lendários");
                PrizeWon?.Invoke("Rei dos pescadores!", $"os {LegendTotal} lendários no diário · +{legendGold} de ouro");
            }
        }

        void Give(ItemDefinition item, int amount)
        {
            if (!item || amount <= 0) return;
            // Equipamento que o jogador já tem (F1, save antigo) não vem de novo.
            if (item.category == ItemCategory.Gear && PlayerStorage.Count(item) > 0) return;
            if (hold) hold.Add(item, amount);
        }

        // ---------- Save ----------

        public void Capture(out string[] ids, out int[] counts, out float[] best, out int prizeBits)
        {
            ids = new string[entries.Count];
            counts = new int[entries.Count];
            best = new float[entries.Count];
            int i = 0;
            foreach (var pair in entries)
            {
                ids[i] = pair.Key.name;
                counts[i] = pair.Value.count;
                best[i] = pair.Value.best;
                i++;
            }
            prizeBits = prizes;
        }

        public void Restore(string[] ids, int[] counts, float[] best, int prizeBits)
        {
            entries.Clear();
            prizes = prizeBits;
            if (ids != null)
                for (int i = 0; i < ids.Length; i++)
                {
                    ItemDefinition item = null;
                    foreach (var s in species)
                        if (s != null && s.item && s.item.name == ids[i]) item = s.item;
                    if (!item) continue;
                    entries[item] = new Entry
                    {
                        count = counts != null && i < counts.Length ? Mathf.Max(1, counts[i]) : 1,
                        best = best != null && i < best.Length ? best[i] : 0f,
                    };
                }
            Version++;
        }

        // ---------- Testes / F1 ----------

        /// <summary>Teste: anota a espécie sem pescar (como se tivesse vindo na vara).</summary>
        public Result LogForTest(ItemDefinition item) => Log(item);

        /// <summary>Teste: esvazia o diário (e os prêmios).</summary>
        public void ClearAll() => Restore(null, null, null, 0);

        /// <summary>"38 cm", "6,5 cm", "1,42 m".</summary>
        public static string FormatSize(float meters)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (meters >= 1f) return meters.ToString("0.00", inv).Replace('.', ',') + " m";
            float cm = meters * 100f;
            return (cm >= 10f ? cm.ToString("0", inv) : cm.ToString("0.0", inv).Replace('.', ',')) + " cm";
        }
    }
}
