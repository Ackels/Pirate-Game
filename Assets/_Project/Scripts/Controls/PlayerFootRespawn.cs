using System;
using System.Collections;
using Pirates.Combat;
using Pirates.LandCombat;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// Morreu em terra: alguns segundos caído e o personagem acorda na praia de desembarque com a vida cheia
    /// (sem perder nada por enquanto). Os piratas perdem o jogador de vista e voltam aos postos.
    /// </summary>
    public class PlayerFootRespawn : MonoBehaviour
    {
        [SerializeField] PlayerModeSwitcher switcher;
        [SerializeField] float respawnDelay = 3.5f;

        Health health;
        FootHealthReactions reactions;
        float respawnAt;

        public bool IsDown => health && health.IsDead;
        public float RespawnIn => IsDown ? Mathf.Max(0f, respawnAt - Time.time) : 0f;
        public event Action Respawned;

        public void Configure(PlayerModeSwitcher modeSwitcher) => switcher = modeSwitcher;

        void Awake()
        {
            var character = switcher ? switcher.Character : null;
            if (!character) return;
            health = character.GetComponent<Health>();
            reactions = character.GetComponent<FootHealthReactions>();
        }

        void OnEnable()
        {
            if (health) health.Died += OnDied;
        }

        void OnDisable()
        {
            if (health) health.Died -= OnDied;
        }

        void OnDied() => StartCoroutine(RespawnLater());

        IEnumerator RespawnLater()
        {
            respawnAt = Time.time + respawnDelay;
            yield return new WaitForSeconds(respawnDelay);
            if (reactions) reactions.Revive();
            else health.Revive();
            var land = switcher.Landing ? switcher.Landing.LandPosition : null;
            if (land) switcher.Character.Teleport(land.position, land.rotation);
            Respawned?.Invoke();
        }
    }
}
