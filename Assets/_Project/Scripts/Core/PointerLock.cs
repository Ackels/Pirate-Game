using UnityEngine;

namespace Pirates.Core
{
    /// <summary>
    /// Cursor parado enquanto o mouse gira a câmera (04/11, pedido do Jhonata: o cursor andando pela tela ao girar incomodava). A câmera
    /// trava e esconde o cursor do sistema; quem mira pelo cursor (a pé, canhões, roda dos marujos) lê a posição por aqui e continua
    /// mirando onde o cursor estava, não no meio da tela (onde o cursor travado fica).
    /// </summary>
    public static class PointerLock
    {
        static Vector2 frozenAt;

        /// <summary>A câmera está girando pelo mouse (cursor escondido e travado).</summary>
        public static bool Frozen { get; private set; }

        public static void Freeze(Vector2 screenPosition)
        {
            frozenAt = screenPosition;
            Frozen = true;
        }

        public static void Release() => Frozen = false;

        /// <summary>A posição do cursor para mirar: a de antes de girar, enquanto gira; senão a de agora.</summary>
        public static Vector2 Position(Vector2 live) => Frozen ? frozenAt : live;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Frozen = false;
    }
}
