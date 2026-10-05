namespace Pirates.OnFoot
{
    /// <summary>
    /// De onde a arma de fogo tira a munição (05/10: os cartuchos da mochila do capitão). Sem nenhuma (piratas da IA), a pistola
    /// atira sem gastar nada, como antes.
    /// </summary>
    public interface IAmmoSupply
    {
        /// <summary>Quantos tiros há para esta arma.</summary>
        int Count(WeaponDefinition weapon);
        /// <summary>Gasta um tiro (chamado quando a bala sai).</summary>
        void Take(WeaponDefinition weapon);
        /// <summary>Tentou atirar sem munição (clique seco).</summary>
        void OnEmpty(WeaponDefinition weapon);
    }
}
