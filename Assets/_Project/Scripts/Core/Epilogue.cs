namespace Pirates.Core
{
    /// <summary>
    /// Atalho para tocar o epílogo de quem não conhece a UI (o modo sandbox, F1): o EpilogueDirector põe aqui o que toca.
    /// </summary>
    public static class Epilogue
    {
        public static System.Action Play;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Play = null;
    }
}
