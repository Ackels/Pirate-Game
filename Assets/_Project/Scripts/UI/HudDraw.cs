using Pirates.Economy;
using UnityEngine;

namespace Pirates.UI
{
    /// <summary>
    /// Texto de HUD refeito só quando os valores que ele mostra mudam (cada $"..." por quadro virava lixo de memória).
    /// Uso, num campo da classe (não variável local nem propriedade): <c>if (vida.Stale(...)) vida.Text = $"...";</c>
    /// e passe <c>vida.Text</c> para <c>Hud.Set</c>. Enum e bool entram como número (<c>(long)estado</c>, <c>HudDraw.Flag(x)</c>),
    /// senão viram object.
    /// </summary>
    struct CachedText
    {
        object o;
        long a, b, c;
        bool valid;

        public string Text { get; set; }

        public bool Stale(long a, long b = 0, long c = 0) => Stale(null, a, b, c);

        public bool Stale(object o, long a = 0, long b = 0, long c = 0)
        {
            if (valid && ReferenceEquals(o, this.o) && a == this.a && b == this.b && c == this.c) return false;
            this.o = o;
            this.a = a;
            this.b = b;
            this.c = c;
            valid = true;
            return true;
        }
    }

    /// <summary>Chaves do <see cref="CachedText"/> e textos pequenos compartilhados pelos HUDs.</summary>
    static class HudDraw
    {
        /// <summary>Chaves do <see cref="CachedText"/>: o número como o texto mostra ("0.0" = décimos, "0" = inteiro).</summary>
        public static long Tenths(float value) => (long)Mathf.Round(value * 10f);
        public static long Units(float value) => (long)Mathf.Round(value);
        public static long Flag(bool value) => value ? 1 : 0;

        public static string SpaceText(ItemDefinition item) =>
            item.spacePerUnit <= 0f ? "sem espaço" : $"{item.spacePerUnit:0.##} esp. cada";
    }
}
