namespace Pirates.Core
{
    /// <summary>
    /// O que um prato da taverna dá por alguns minutos (D2, 30/10). Poison = veneno de baiacu na lâmina (dano por segundo por alguns
    /// segundos a cada golpe, enquanto durar); Smoke = bomba de fumaça (usada na hora, não dura).
    /// </summary>
    public enum MealEffect { None, Stamina, Reload, HullRegen, Stealth, Damage, Poison, Smoke }

    /// <summary>
    /// Pratos comidos (bônus que duram alguns minutos), para quem usa o bônus sem conhecer o módulo das refeições (IA de terra: furtividade).
    /// </summary>
    public interface IMealBuffs
    {
        /// <summary>Quanto o bônus vale agora (0 = sem prato daquele tipo).</summary>
        float Amount(MealEffect effect);
    }
}
