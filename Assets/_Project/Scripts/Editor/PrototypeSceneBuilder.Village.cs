using UnityEngine;
using Random = System.Random;

namespace Pirates.EditorTools
{
    /// <summary>
    /// Casas da vila montadas com as peças modulares do Fantasy Town Kit (Kenney, CC0): cada peça ocupa uma célula de 1 m
    /// no kit (parede no lado -X da célula, 1 m de altura); o gerador escala tudo por <c>cell</c> (3 m por andar).
    /// Andar térreo de pedra ou madeira, andar de cima de madeira, porta na frente (+Z local), janelas sorteadas,
    /// telhado de duas águas com a cumeeira ao longo de Z. Um BoxCollider só cobre a casa inteira. O filho "Porta" marca
    /// a entrada (na fachada, virado para fora).
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        static readonly string[] StoneWindows = { "wall-window-shutters", "wall-window-glass", "wall-window-small", "wall-window-round", "wall" };
        static readonly string[] WoodWindows = { "wall-wood-window-shutters", "wall-wood-window-glass", "wall-wood-window-small", "wall-wood-window-round", "wall-wood" };

        sealed class HouseSpec
        {
            public string name;
            /// <summary>Células em X (largura, 1 ou 2) e Z (fundo).</summary>
            public int width = 2, depth = 2, floors = 1;
            public bool woodGround;
            public float cell = 3f;
            public bool chimney = true;
            /// <summary>Telhado alto (peças "roof-high", vermelhas) em vez do baixo (verde).</summary>
            public bool highRoof;
            /// <summary>Peça da porta (null: porta comum de pedra ou madeira).</summary>
            public string door;
            /// <summary>Estandarte na fachada (ex.: "banner-red"), no andar de cima.</summary>
            public string banner;
            public int seed;
        }

        /// <summary>Casa no ponto do chão <paramref name="ground"/>, com a frente (porta) virada para <paramref name="yaw"/>.</summary>
        static Transform House(Transform parent, HouseSpec h, Vector3 ground, float yaw)
        {
            var rng = new Random(h.seed);
            var root = new GameObject(h.name).transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(ground, Quaternion.Euler(0f, yaw, 0f));
            float c = h.cell;
            Vector3 Cell(int i, int j, int floor) =>
                new((i - (h.width - 1) * 0.5f) * c, floor * c, (j - (h.depth - 1) * 0.5f) * c);

            // Fundação de pedra: some com o desnível do chão embaixo da casa.
            Part(PrimitiveType.Cube, "Fundacao", root, new Vector3(0f, -0.45f, 0f),
                new Vector3(h.width * c + 0.3f, 1f, h.depth * c + 0.3f), Quaternion.identity, KenneyColors.Stone);

            int doorCell = rng.Next(h.width);
            for (int f = 0; f < h.floors; f++)
            {
                bool wood = f > 0 || h.woodGround;
                var windows = wood ? WoodWindows : StoneWindows;
                for (int i = 0; i < h.width; i++)
                for (int j = 0; j < h.depth; j++)
                {
                    // Lados da célula que dão para fora: -X (giro 0), +Z (90), +X (180), -Z (270).
                    if (i == 0) Wall(i, j, f, 0f);
                    if (j == h.depth - 1) Wall(i, j, f, 90f);
                    if (i == h.width - 1) Wall(i, j, f, 180f);
                    if (j == 0) Wall(i, j, f, 270f);
                }

                void Wall(int i, int j, int floor, float side)
                {
                    bool front = side == 90f && floor == 0 && i == doorCell;
                    string piece = front ? h.door ?? (wood ? "wall-wood-door" : "wall-door") : windows[rng.Next(windows.Length)];
                    Model(Kit.Town, piece, root, Cell(i, j, floor), side, c, $"Parede_{floor}_{i}_{j}_{side:0}");
                }
            }

            // Telhado: uma água por coluna (largura 2) ou a peça de duas águas (largura 1).
            for (int j = 0; j < h.depth; j++)
            {
                string high = h.highRoof ? "-high" : "";
                if (h.width == 1)
                    Model(Kit.Town, $"roof{high}-gable", root, Cell(0, j, h.floors), 90f, c, $"Telhado_{j}");
                else
                {
                    Model(Kit.Town, $"roof{high}", root, Cell(0, j, h.floors), 180f, c, $"Telhado_E_{j}");
                    Model(Kit.Town, $"roof{high}", root, Cell(1, j, h.floors), 0f, c, $"Telhado_D_{j}");
                }
            }
            if (h.banner != null)
            {
                // O estandarte do kit fica do lado de dentro da célula: virado e empurrado para fora da fachada.
                int i = h.width > 1 ? 1 - doorCell : 0;
                Model(Kit.Town, h.banner, root, Cell(i, h.depth - 1, h.floors - 1) + new Vector3(0f, 0f, 0.93f * c), 270f, c, "Estandarte");
            }
            if (h.chimney)
                Model(Kit.Town, "chimney", root, Cell(h.width - 1, h.depth - 1, h.floors) + new Vector3(0f, -0.2f * c, 0f), 180f, c, "Chamine");

            // Marcador da porta (na fachada, virado para fora): casas de serviço do porto põem a entrada aqui.
            var door = new GameObject("Porta").transform;
            door.SetParent(root, false);
            door.localPosition = Cell(doorCell, h.depth - 1, 0) + new Vector3(0f, 0f, 0.5f * c);

            var box = root.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, h.floors * c * 0.5f, 0f);
            box.size = new Vector3(h.width * c, h.floors * c, h.depth * c);
            foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            Occluder(root.gameObject);
            return root;
        }
    }
}
