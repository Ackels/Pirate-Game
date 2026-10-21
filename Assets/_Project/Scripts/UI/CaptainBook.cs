using System;
using Pirates.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pirates.UI
{
    /// <summary>
    /// O "livro do capitão" (21/10, pedido do Jhonata: tirar missões, História e fama da tela e pôr numa tela com abas): as janelas de
    /// inventário (I), missões (J) e fama (C) viram abas de uma tela só. Cada janela continua sendo do seu HUD; aqui só se registram e
    /// trocam (fecha a aberta, abre a outra) e se monta a fileira de abas no alto de cada uma. A tecla de uma aba aberta fecha; a de outra
    /// aba troca; com outro painel na frente (fora do livro), espera.
    /// </summary>
    static class CaptainBook
    {
        public enum Tab { Inventory, Missions, Fame }

        static readonly string[] Labels = { "Inventário", "Missões", "Fama" };
        static readonly string[] Keys = { "I", "J", "C" };
        static readonly Func<bool>[] isOpen = new Func<bool>[3];
        static readonly Action<bool>[] setOpen = new Action<bool>[3];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Array.Clear(isOpen, 0, isOpen.Length);
            Array.Clear(setOpen, 0, setOpen.Length);
        }

        public static void Register(Tab tab, Func<bool> open, Action<bool> set)
        {
            isOpen[(int)tab] = open;
            setOpen[(int)tab] = set;
        }

        public static void Unregister(Tab tab)
        {
            isOpen[(int)tab] = null;
            setOpen[(int)tab] = null;
        }

        static bool Has(Tab tab) => setOpen[(int)tab] != null;

        static bool IsOpen(Tab tab) => isOpen[(int)tab] != null && isOpen[(int)tab]();

        /// <summary>A aba aberta, ou -1.</summary>
        public static int OpenTab
        {
            get
            {
                for (int i = 0; i < isOpen.Length; i++)
                    if (IsOpen((Tab)i)) return i;
                return -1;
            }
        }

        /// <summary>Tecla da aba: aberta = fecha; outra aba aberta = troca; nada na frente = abre.</summary>
        public static void Toggle(Tab tab)
        {
            if (!Has(tab)) return;
            if (IsOpen(tab))
            {
                setOpen[(int)tab](false);
                return;
            }
            int other = OpenTab;
            if (other < 0 && ScreenPanel.Open) return;   // outro painel (conversa, carta) na frente
            Open(tab);
        }

        public static void Open(Tab tab)
        {
            if (!Has(tab)) return;
            for (int i = 0; i < setOpen.Length; i++)
                if (i != (int)tab && IsOpen((Tab)i)) setOpen[i](false);
            setOpen[(int)tab](true);
        }

        /// <summary>Fileira de abas no alto da janela de <paramref name="current"/> (as que existem na cena).</summary>
        public static void AddTabs(HudWindow window, Tab current)
        {
            window.Root.AddToClassList("hud-window--book");
            var row = Hud.Row("hud-book-tabs");
            row.pickingMode = PickingMode.Position;
            for (int i = 0; i < Labels.Length; i++)
            {
                var tab = (Tab)i;
                var button = Win.Button($"{Labels[i]}  <size=75%>[{Keys[i]}]</size>", () => Open(tab), true, "hud-book-tab");
                if (tab == current) button.AddToClassList("hud-book-tab--on");
                row.Add(button);
            }
            window.Root.Insert(0, row);
        }

        /// <summary>
        /// Esconde as abas que não existem nesta cena (sem missões na 06). Chame ao abrir: a essa altura todas já se registraram.
        /// </summary>
        public static void RefreshTabs(HudWindow window)
        {
            var row = window.Root.childCount > 0 ? window.Root[0] : null;
            if (row == null || !row.ClassListContains("hud-book-tabs")) return;
            for (int i = 0; i < row.childCount && i < Labels.Length; i++) Hud.Show(row[i], Has((Tab)i));
        }
    }
}
