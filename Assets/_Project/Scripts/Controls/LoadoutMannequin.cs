using System;
using Pirates.Economy;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// Vitrine do capitão na tela de equipamento (E1, 20/10; pedido do Jhonata: "mostrando o personagem e nele você coloca a espada,
    /// arma, roupa"): uma cópia do modelo do capitão (feita pelo gerador, só malhas e Animator) num pedestal longe do mapa, com câmera e
    /// luzes próprias renderizando numa RenderTexture que a tela mostra. Fica desligada (nada custa) até a tela abrir. Mostra a lâmina
    /// equipada (a mesma do <see cref="PlayerLoadout"/>) e o bacamarte nas costas se o jogador o tem; o arrasto do mouse gira o pedestal.
    /// </summary>
    public class LoadoutMannequin : MonoBehaviour
    {
        [Serializable]
        public class BladeLook
        {
            public WeaponDefinition weapon;
            public GameObject visual;
        }

        [SerializeField] GameObject model;
        [SerializeField] Transform turntable;
        [SerializeField] Camera viewCamera;
        [SerializeField] Light[] lights = Array.Empty<Light>();
        [Tooltip("A lâmina de começo (o cutelo) e as equipáveis, na mão da cópia.")]
        [SerializeField] GameObject starterBlade;
        [SerializeField] BladeLook[] blades = Array.Empty<BladeLook>();
        [Tooltip("Bacamarte nas costas da cópia: aparece se o jogador tem o item dele.")]
        [SerializeField] GameObject heavyGun;
        [SerializeField] ItemDefinition heavyGunItem;
        [SerializeField] Vector2Int resolution = new(480, 720);
        [Tooltip("Giro de começo (graus; 20 = de frente para a câmera, um pouco de lado) e giro sozinho por segundo quando ninguém arrasta.")]
        [SerializeField] float startYaw = 20f, idleSpin = 0f;

        RenderTexture texture;
        float yaw;
        bool shown;
        int shownVersion = -1;
        long shownStorage = -1;

        public static LoadoutMannequin Current { get; private set; }
        /// <summary>A imagem da vitrine (a tela I põe no fundo do quadro do personagem).</summary>
        public Texture Texture => texture;
        public bool IsShown => shown;

        public void Configure(GameObject copy, Transform pedestal, Camera cam, Light[] keyLights, GameObject starter, BladeLook[] equipable,
            GameObject gun, ItemDefinition gunItem)
        {
            model = copy;
            turntable = pedestal;
            viewCamera = cam;
            lights = keyLights ?? Array.Empty<Light>();
            starterBlade = starter;
            blades = equipable ?? Array.Empty<BladeLook>();
            heavyGun = gun;
            heavyGunItem = gunItem;
        }

        void Awake()
        {
            Current = this;
            texture = new RenderTexture(resolution.x, resolution.y, 24, RenderTextureFormat.ARGB32) { name = "VitrineDoCapitao", antiAliasing = 4 };
            if (viewCamera)
            {
                viewCamera.targetTexture = texture;
                viewCamera.enabled = false;
            }
            yaw = startYaw;
            Show(false);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (viewCamera) viewCamera.targetTexture = null;
            if (texture) texture.Release();
            Destroy(texture);
        }

        /// <summary>Liga (a tela abriu) ou desliga a vitrine: cópia, câmera e luzes.</summary>
        public void Show(bool on)
        {
            shown = on;
            if (model) model.SetActive(on);
            if (viewCamera) viewCamera.enabled = on;
            foreach (var l in lights) if (l) l.enabled = on;
            if (on)
            {
                shownVersion = -1;
                Refresh();
            }
        }

        /// <summary>Gira o pedestal (arrastar o mouse no quadro do personagem).</summary>
        public void Spin(float degrees) => yaw += degrees;

        void Update()
        {
            if (!shown) return;
            yaw += idleSpin * Time.unscaledDeltaTime;
            if (turntable) turntable.localRotation = Quaternion.Euler(0f, yaw, 0f);
            long storage = PlayerStorage.Version;
            if (PlayerLoadout.Version != shownVersion || storage != shownStorage) Refresh();
        }

        void Refresh()
        {
            shownVersion = PlayerLoadout.Version;
            shownStorage = PlayerStorage.Version;
            var item = PlayerLoadout.Active(EquipSlot.Blade);
            var weapon = item ? item.equipment as WeaponDefinition : null;
            bool found = false;
            foreach (var b in blades)
            {
                if (b == null || !b.visual) continue;
                bool on = weapon && b.weapon == weapon;
                found |= on;
                if (b.visual.activeSelf != on) b.visual.SetActive(on);
            }
            if (starterBlade) starterBlade.SetActive(!found);
            if (heavyGun) heavyGun.SetActive(heavyGunItem && PlayerStorage.Has(heavyGunItem));
        }
    }
}
