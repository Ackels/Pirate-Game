using System.Collections.Generic;
using Pirates.Combat;
using Pirates.Core;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.LandCombat
{
    /// <summary>
    /// Uma sala das catacumbas com os seus monstros (C5, passo 2, 20/10): o capitão entra na caixa da sala = todos acordam; sai para longe
    /// (folga <see cref="leash"/>) ou morre = voltam ao lugar com a vida cheia. Só <see cref="attackersAtOnce"/> atacam ao mesmo tempo (os
    /// outros esperam rondando). Os mortos ficam mortos até a ruína voltar ao começo (missão da Ermida aceita), como o acampamento.
    /// </summary>
    public class CryptEncounter : MonoBehaviour
    {
        [SerializeField] string ruinId = "ermida_sao_telmo";
        [SerializeField] FootCharacter captain;
        [Tooltip("A sala no mundo: o capitão dentro acorda todos.")]
        [SerializeField] Bounds area;
        [SerializeField] float leash = 4f;
        [SerializeField] int attackersAtOnce = 2;
        [SerializeField] CryptMonster[] members = System.Array.Empty<CryptMonster>();

        readonly List<CryptMonster> attacking = new(4);
        Health captainHealth;

        public bool Active { get; private set; }
        public IReadOnlyList<CryptMonster> Members => members;
        public Bounds Area => area;

        public void Configure(FootCharacter player, Bounds room, int attackers, CryptMonster[] monsters, string ruin)
        {
            captain = player;
            area = room;
            attackersAtOnce = attackers;
            members = monsters;
            ruinId = ruin;
        }

        void Awake()
        {
            foreach (var m in members)
                if (m) m.Encounter = this;
            if (captain) captainHealth = captain.GetComponent<Health>();
        }

        void OnEnable() => EventBus.Subscribe<MissionStartedEvent>(OnMissionStarted);
        void OnDisable() => EventBus.Unsubscribe<MissionStartedEvent>(OnMissionStarted);

        void OnMissionStarted(MissionStartedEvent e)
        {
            if (!string.IsNullOrEmpty(ruinId) && e.targetId == ruinId) ResetAll();
        }

        /// <summary>Todos vivos de novo, no lugar (a ruína voltou ao começo; F1).</summary>
        public void ResetAll()
        {
            attacking.Clear();
            Active = false;
            foreach (var m in members)
                if (m) m.ResetToPost();
        }

        void Update()
        {
            if (!captain) return;
            bool alive = captain.isActiveAndEnabled && captainHealth && !captainHealth.IsDead;
            Vector3 p = captain.transform.position;
            if (!Active)
            {
                if (!alive || !area.Contains(p)) return;
                Active = true;
                foreach (var m in members)
                    if (m && !m.IsDead && !m.WaitsForSummon) m.Wake(captain);
                return;
            }
            var far = area;
            far.Expand(leash * 2f);
            if (alive && far.Contains(p)) return;
            Active = false;
            attacking.Clear();
            foreach (var m in members)
                if (m) m.Sleep();
        }

        /// <summary>Pede a vez de atacar (true = pode ir).</summary>
        public bool TryBeginAttack(CryptMonster monster)
        {
            if (attacking.Contains(monster)) return true;
            for (int i = attacking.Count - 1; i >= 0; i--)
                if (!attacking[i] || attacking[i].IsDead || !attacking[i].IsAwake) attacking.RemoveAt(i);
            if (attacking.Count >= attackersAtOnce) return false;
            attacking.Add(monster);
            return true;
        }

        public void EndAttack(CryptMonster monster) => attacking.Remove(monster);
    }
}
