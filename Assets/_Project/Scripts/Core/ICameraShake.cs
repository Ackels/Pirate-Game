namespace Pirates.Core
{
    /// <summary>Tremida da câmera (acerto, aparar, levar golpe), sem quem pede conhecer a câmera. Nos Services.</summary>
    public interface ICameraShake
    {
        /// <summary>Treme até <paramref name="strength"/> m e some em <paramref name="seconds"/> (tempo real: vale durante a parada de impacto).</summary>
        void Shake(float strength, float seconds);
    }
}
