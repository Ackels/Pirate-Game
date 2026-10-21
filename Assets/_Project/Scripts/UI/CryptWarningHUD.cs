using Pirates.LandCombat;
using UnityEngine;

namespace Pirates.UI
{
    /// <summary>
    /// Aviso de golpe (C5, 20/10; Jhonata: "achei muito difícil dar parry, não dá para entender quando os inimigos estão batendo") sobre os
    /// monstros das catacumbas e, desde a revisão das animações (21/10), sobre todos os inimigos de terra. Desde 21/10 (pedido do Jhonata:
    /// "algo mais na estética pirata", menor) é um ícone (<see cref="StrikeWarningIcon"/>) em vez do "!"/"!!": <b>espadas cruzadas</b>
    /// vermelhas no preparo e, nos últimos <see cref="guardLead"/> s, douradas, maiores e pulsando = hora de erguer a guarda (com o tempo de
    /// reação, ela sobe dentro da janela de aparar, 0,3 s). Golpe que a guarda não segura (as almas do chão do Capelão, os imparáveis do
    /// Barba-Negra): <b>caveira com ossos cruzados</b> azul = saia de cima ou role.
    /// </summary>
    public class CryptWarningHUD : MonoBehaviour
    {
        [Tooltip("Segundos antes do golpe em que o aviso vira \"erga a guarda\".")]
        [SerializeField] float guardLead = 0.45f;
        [SerializeField] float range = 40f;

        static readonly Color Warning = new(0.95f, 0.22f, 0.12f), Now = new(1f, 0.84f, 0.25f);
        static readonly Color Dodge = new(0.35f, 0.7f, 1f), DodgeNow = new(0.62f, 0.95f, 1f);

        WorldIconPool icons;

        void OnDisable() => icons?.HideAll();

        void Update()
        {
            if (icons == null)
            {
                var hud = GameHud.Current;
                if (!hud || !hud.EnsureBuilt()) return;
                icons = new WorldIconPool(hud.World);
            }
            if (!icons.Begin()) return;
            var cam = Camera.main;
            var list = CryptMonster.Active;
            for (int i = 0; cam && i < list.Count; i++)
            {
                var m = list[i];
                float t = m.ImpactIn;
                if (t < 0f || m.IsDead) continue;
                Place(cam, m.transform.position + Vector3.up * m.WarningHeight, t, m.WarnUnblockable);
            }
            var foes = EnemyFootAI.Active;
            for (int i = 0; cam && i < foes.Count; i++)
            {
                var ai = foes[i];
                float t = ai.ImpactIn;
                if (t < 0f) continue;
                Place(cam, ai.transform.position + Vector3.up * (2.35f * ai.transform.lossyScale.y), t, ai.ImpactUnblockable);
            }
            icons.End();
        }

        void Place(Camera cam, Vector3 p, float impactIn, bool unblockable)
        {
            if ((p - cam.transform.position).sqrMagnitude > range * range) return;
            bool now = impactIn <= guardLead;
            if (unblockable) icons.Place(p, StrikeWarningIcon.Kind.Skull, now ? DodgeNow : Dodge, now);
            else icons.Place(p, StrikeWarningIcon.Kind.Swords, now ? Now : Warning, now);
        }
    }
}
