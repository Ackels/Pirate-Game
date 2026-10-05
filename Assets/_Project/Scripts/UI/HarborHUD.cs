using System.Collections.Generic;
using Pirates.Base;
using Pirates.Economy;
using Pirates.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// HUD do porto-base (UI Toolkit): nome sobre o cais, faixa no alto (zona segura, obras em andamento), aviso para
    /// atracar, ganhos/anúncios (carga movida, conserto, obra pronta) e a janela do porto (<see cref="HarborWindow"/>): no
    /// navio, em abas: carga (porão ↔ armazém) + conserto, upgrades do navio, construções da base e o quadro de missões
    /// (contratos dos NPCs: aceitar, acompanhar, entregar e receber). Com o mouse.
    /// A pé (cena 07, com o HarborBuildingVisitor do personagem): placas sobre as casas de serviço, aviso na porta e a
    /// janela só com a parte daquela casa (armazém = carga; estaleiro = conserto + upgrades; mestre de obras =
    /// construções; taverna = missões).
    /// </summary>
    public class HarborHUD : MonoBehaviour
    {
        [SerializeField] HarborVisitor visitor;
        [Tooltip("Personagem a pé (opcional): as casas do porto abrem cada parte da janela.")]
        [SerializeField] HarborBuildingVisitor building;
        [SerializeField] float labelRange = 400f;
        [Tooltip("Distância (m) do personagem em que aparecem as placas das casas.")]
        [SerializeField] float buildingLabelRange = 70f;

        [Tooltip("Vida consertada pelo botão de conserto parcial.")]
        [SerializeField] float partialRepair = 30f;

        // Textos sempre na tela, refeitos só quando mudam.
        CachedText shipyardLine, worksLine, dockPrompt, buildingPrompt;
        const string SafeZoneLine = "<color=#9be37f>Zona segura do porto</color>   inimigos não atacam aqui";
        GameHud hud;
        VisualElement status;
        Label safeLabel, shipyardLabel, worksLabel;
        HudPrompt prompt;
        WorldLabelPool places;
        HarborWindow window;
        ShipUpgrades upgrades;
        BaseBuildings watchedBuildings;
        HarborDock watchedDock;
        CachedText smithyLine;
        Label smithyLabel;

        public void Bind(HarborVisitor target, HarborBuildingVisitor onFoot = null)
        {
            visitor = target;
            building = onFoot;
        }

        bool OnFoot => building && building.isActiveAndEnabled;
        bool WindowOpen => OnFoot ? building.IsOpen : visitor.IsOpen;

        void Update()
        {
            if (!visitor) return;
            WatchBuildings();
            if (status == null && !Build()) return;
            window.Update(WindowOpen, OnFoot ? building.Current : null);
            UpdateStatus();
            if (OnFoot)
            {
                if (building.IsOpen) prompt.Hide();
                else UpdateBuildingPrompt();
            }
            else if (visitor.IsOpen) prompt.Hide();
            else UpdatePrompt();
            if (OnFoot && !building.IsOpen) UpdateBuildingLabels();
            else if (!WindowOpen) UpdateWorldLabels();
            else places.HideAll();
            if (WindowOpen) hud.LowerPickups();
        }

        bool Build()
        {
            hud = GameHud.Current;
            if (!hud || !hud.EnsureBuilt()) return false;
            status = Hud.Card(paper: false);
            status.AddToClassList("hud-strip");
            safeLabel = Hud.Text(SafeZoneLine, "hud-strip__line");
            shipyardLabel = Hud.Text("", "hud-strip__line");
            worksLabel = Hud.Text("", "hud-strip__line");
            status.Add(safeLabel);
            status.Add(shipyardLabel);
            status.Add(worksLabel);
            smithyLabel = Hud.Text("", "hud-strip__line");
            status.Add(smithyLabel);
            status.style.display = DisplayStyle.None;
            hud.TopCenter.Add(status);
            prompt = new HudPrompt();
            hud.Prompts.Add(prompt);
            places = new WorldLabelPool(hud.World);
            window = new HarborWindow(hud.Windows, visitor, building, partialRepair);
            return true;
        }

        void OnEnable()
        {
            if (!visitor) return;
            visitor.Transferred += OnTransferred;
            visitor.Repaired += OnRepaired;
            upgrades = visitor.GetComponent<ShipUpgrades>();
            if (upgrades) upgrades.Upgraded += OnUpgraded;
        }

        void OnDisable()
        {
            if (status != null)
            {
                Hud.Show(status, false);
                prompt.Hide();
                places.HideAll();
                window.Update(false, null);
            }
            if (!visitor) return;
            visitor.Transferred -= OnTransferred;
            visitor.Repaired -= OnRepaired;
            if (upgrades) upgrades.Upgraded -= OnUpgraded;
            if (watchedBuildings) watchedBuildings.Built -= OnBuilt;
            watchedBuildings = null;
            if (watchedDock) watchedDock.SmithyReady -= OnSmithyReady;
            watchedDock = null;
        }

        void OnTransferred(ItemStack stack, bool toWarehouse)
        {
            string text = toWarehouse ? $"{stack.amount} {stack.item.displayName} para o armazém" : $"{stack.amount} {stack.item.displayName} para o porão";
            AddFeed(GameHud.FeedKind.Pickup, text, stack.item.color);
        }

        void OnRepaired(float hull, int wood, int gold) =>
            AddFeed(GameHud.FeedKind.Pickup, $"Casco consertado +{hull:0}  (-{wood} madeira, -{gold} ouro)", new Color(0.55f, 0.9f, 0.6f));

        void OnSmithyReady(HarborDock.SmithyOrder order) =>
            AddFeed(GameHud.FeedKind.Notice, $"Forja: <b>{order.Result.amount}× {order.Result.item.displayName.ToLowerInvariant()}</b> pronta!\n" +
                                             "<size=75%>retire na Forja do porto</size>", HudColors.Gold);

        void OnBuilt(BuildingDefinition building, int level) =>
            AddFeed(GameHud.FeedKind.Notice, $"Construção pronta: <b>{building.displayName}</b> nível {level}!", HudColors.Gold);

        /// <summary>O porto pode surgir depois deste HUD: assina os avisos de construção quando ele aparecer.</summary>
        void WatchBuildings()
        {
            var dock = HarborDock.Active.Count > 0 ? HarborDock.Active[0] : null;
            if (dock != watchedDock)
            {
                if (watchedDock) watchedDock.SmithyReady -= OnSmithyReady;
                watchedDock = dock;
                if (watchedDock) watchedDock.SmithyReady += OnSmithyReady;
            }
            var buildings = dock ? dock.Buildings : null;
            if (buildings == watchedBuildings) return;
            if (watchedBuildings) watchedBuildings.Built -= OnBuilt;
            watchedBuildings = buildings;
            if (watchedBuildings) watchedBuildings.Built += OnBuilt;
        }

        void OnUpgraded(UpgradeDefinition upgrade, int level) =>
            AddFeed(GameHud.FeedKind.Notice, $"Obra pronta: <b>{upgrade.displayName}</b> nível {level}!\n<size=75%>{upgrades.EffectText(upgrade, false)}</size>", HudColors.Gold);

        static void AddFeed(GameHud.FeedKind kind, string text, Color color)
        {
            var h = GameHud.Current;
            if (h) h.Feed(kind, text, color);
        }

        void UpdatePrompt()
        {
            if (!visitor.Nearby)
            {
                prompt.Hide();
                return;
            }
            bool tooFast = visitor.TooFast;
            long mode = tooFast ? 0 : visitor.MenuOnDock ? 1 : visitor.IsDocked ? 2 : 3;
            if (dockPrompt.Stale(visitor.Nearby, mode, tooFast ? HudDraw.Tenths(visitor.SpeedKnots) : 0))
                dockPrompt.Text = tooFast
                    ? $"Recolha as velas para atracar  <size=80%>({visitor.SpeedKnots:0.0} / {visitor.MaxSpeedKnots:0} nós)</size>"
                    : visitor.MenuOnDock ? $"Atracar em {visitor.Nearby.DisplayName}"
                    : visitor.IsDocked ? "Descer no píer   <size=80%>(solte as velas para zarpar)</size>"
                    : $"Atracar e desembarcar em {visitor.Nearby.DisplayName}";
            prompt.Show(tooFast ? null : "F", dockPrompt.Text);
        }

        // ---------- A pé: casas do porto ----------

        void UpdateBuildingPrompt()
        {
            var near = building.Nearby;
            if (!near)
            {
                prompt.Hide();
                return;
            }
            if (buildingPrompt.Stale(near, HudDraw.Flag(building.ShipDocked)))
                buildingPrompt.Text = building.ShipDocked
                    ? $"Entrar: {near.DisplayName}  <size=80%>({near.Purpose})</size>"
                    : $"{near.DisplayName}  <size=80%>(atraque o navio no píer para usar)</size>";
            prompt.Show(building.ShipDocked ? "F" : null, buildingPrompt.Text);
        }

        void UpdateBuildingLabels()
        {
            if (!places.Begin()) return;
            Vector3 me = building.transform.position;
            float range2 = buildingLabelRange * buildingLabelRange;
            for (int i = 0; i < HarborBuilding.Active.Count; i++)
            {
                var b = HarborBuilding.Active[i];
                if ((b.transform.position - me).sqrMagnitude > range2) continue;
                places.Place(b.LabelPoint, Sign(b), HudColors.Cream);
            }
            places.End();
        }

        readonly Dictionary<HarborBuilding, string> signs = new();

        /// <summary>Placa da casa: nome e, menor, para que serve (texto montado uma vez).</summary>
        string Sign(HarborBuilding b)
        {
            if (!signs.TryGetValue(b, out var text)) signs[b] = text = $"{b.DisplayName}\n<size=65%>{b.Purpose}</size>";
            return text;
        }

        /// <summary>Topo da tela, fora da janela: zona segura e obra do estaleiro.</summary>
        void UpdateStatus()
        {
            // Só a zona do porto (o navio ancorado também tem uma, para os inimigos não o atacarem com você em terra).
            bool safe = false;
            for (int i = 0; i < HarborDock.Active.Count; i++)
                if (HarborDock.Active[i].Zone && HarborDock.Active[i].Zone.Contains(visitor.transform.position)) safe = true;
            Hud.Show(safeLabel, safe);
            bool shipyard = false, works = false;
            // Na prática há um porto só; com mais de um, o cache só refaz mais vezes.
            for (int i = 0; i < HarborDock.Active.Count; i++)
            {
                var dock = HarborDock.Active[i];
                var job = dock.Job;
                if (job == null) continue;
                shipyard = true;
                bool working = dock.JobIsWorking;
                if (shipyardLine.Stale(job, job.level, working ? Mathf.CeilToInt(dock.JobSecondsLeft) : -1))
                {
                    string state = working ? FormatTime(dock.JobSecondsLeft) : "<color=#ff8a70>pausada: volte para a zona do porto</color>";
                    shipyardLine.Text = $"<color=#ffd36b>Estaleiro</color>   {job.Title}  ·  {state}";
                }
                Hud.Set(shipyardLabel, shipyardLine.Text);
            }
            for (int i = 0; i < HarborDock.Active.Count; i++)
            {
                var dock = HarborDock.Active[i];
                var job = dock.Buildings ? dock.Buildings.Job : null;
                if (job == null) continue;
                works = true;
                if (worksLine.Stale(job.building, job.level, Mathf.CeilToInt(dock.Buildings.JobSecondsLeft)))
                    worksLine.Text = $"<color=#ffd36b>Obras da base</color>   {job.building.displayName} nível {job.level}  ·  {FormatTime(dock.Buildings.JobSecondsLeft)}";
                Hud.Set(worksLabel, worksLine.Text);
            }
            // Forja (encomenda de munição): trabalhando ou pronta para retirar.
            bool forge = false;
            if (smithyLabel != null && HarborDock.Active.Count > 0)
            {
                var dock = HarborDock.Active[0];
                var order = dock.Smithy;
                if (order != null)
                {
                    forge = true;
                    if (smithyLine.Stale(order.Result.amount, order.Ready ? -1 : Mathf.CeilToInt(dock.SmithySecondsLeft)))
                        smithyLine.Text = order.Ready
                            ? $"<color=#ffd36b>Forja</color>   {order.Result.amount}× {order.Result.item.displayName.ToLowerInvariant()}  ·  <color=#9be37f>pronta: retire na Forja</color>"
                            : $"<color=#ffd36b>Forja</color>   {order.Result.amount}× {order.Result.item.displayName.ToLowerInvariant()}  ·  {FormatTime(dock.SmithySecondsLeft)}";
                    Hud.Set(smithyLabel, smithyLine.Text);
                }
                Hud.Show(smithyLabel, forge);
            }
            Hud.Show(shipyardLabel, shipyard);
            Hud.Show(worksLabel, works);
            Hud.Show(status, (safe || shipyard || works || forge) && !WindowOpen);
        }

        static string FormatTime(float seconds)
        {
            int s = Mathf.CeilToInt(seconds);
            return s >= 60 ? $"{s / 60}:{s % 60:00}" : $"{s} s";
        }

        void UpdateWorldLabels()
        {
            if (!places.Begin()) return;
            var cam = Camera.main;
            float range2 = labelRange * labelRange;
            for (int i = 0; i < HarborDock.Active.Count; i++)
            {
                var dock = HarborDock.Active[i];
                Vector3 pos = dock.transform.position + Vector3.up * 8f;
                if ((pos - cam.transform.position).sqrMagnitude > range2) continue;
                places.Place(pos, dock.DisplayName, new Color(0.7f, 0.92f, 1f));
            }
            places.End();
        }

    }
}
