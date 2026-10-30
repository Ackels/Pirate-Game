using UnityEngine;

namespace Pirates.Progression
{
    /// <summary>
    /// Onde ficam os cardumes (D2, passo 2): alguns <see cref="FishingSpot"/> ligados ao mesmo tempo em pontos de mar aberto escolhidos
    /// pelo gerador (longe das ilhas e dos covis dos monstros). Cardume vazio, ou velho demais e longe da câmera, some e reaparece noutro
    /// ponto livre, longe do navio do jogador (não nasce na frente dele). Nada é salvo: ao carregar o jogo eles se espalham de novo.
    /// </summary>
    public class FishingGrounds : MonoBehaviour
    {
        [SerializeField] FishingSpot[] spots = System.Array.Empty<FishingSpot>();
        [Tooltip("Pontos possíveis (mundo), escolhidos pelo gerador.")]
        [SerializeField] Vector3[] places = System.Array.Empty<Vector3>();
        [SerializeField] Transform playerShip;
        [Tooltip("Um cardume novo nasce a pelo menos isto do navio do jogador (m).")]
        [SerializeField] float minDistanceFromShip = 160f;
        [Tooltip("Vida de um cardume (s): depois disso, longe da câmera, muda de lugar.")]
        [SerializeField] Vector2 lifeRange = new(420f, 720f);
        [Tooltip("Espera para um cardume vazio ou velho reaparecer noutro lugar (s).")]
        [SerializeField] Vector2 respawnRange = new(40f, 90f);
        [Tooltip("Câmera mais perto que isto (m): o cardume velho não some na frente do jogador.")]
        [SerializeField] float hideDistance = 180f;

        float[] dieAt = System.Array.Empty<float>(), respawnAt = System.Array.Empty<float>();
        int[] placeOf = System.Array.Empty<int>();
        float nextCheck;
        Camera cam;

        public void Configure(FishingSpot[] fishingSpots, Vector3[] spotPlaces, Transform ship)
        {
            spots = fishingSpots;
            places = spotPlaces;
            playerShip = ship;
        }

        void Start()
        {
            int n = spots.Length;
            dieAt = new float[n];
            respawnAt = new float[n];
            placeOf = new int[n];
            for (int i = 0; i < n; i++)
            {
                placeOf[i] = -1;
                if (!spots[i]) continue;
                spots[i].gameObject.SetActive(false);
                // No começo pode nascer perto (o jogador ainda está no porto), só não em cima do navio.
                Place(i, 60f);
            }
        }

        void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 1f;
            if (!cam) cam = Camera.main;
            float now = Time.time;
            for (int i = 0; i < spots.Length; i++)
            {
                var spot = spots[i];
                if (!spot) continue;
                if (spot.gameObject.activeSelf)
                {
                    if (spot.InUse) continue;
                    bool old = now >= dieAt[i] && !CameraNear(spot.transform.position);
                    if (!spot.Empty && !old) continue;
                    spot.gameObject.SetActive(false);
                    placeOf[i] = -1;
                    respawnAt[i] = now + Random.Range(respawnRange.x, respawnRange.y);
                }
                else if (now >= respawnAt[i]) Place(i, minDistanceFromShip);
            }
        }

        /// <summary>Teste e sandbox: o cardume mais perto vem para este ponto, cheio.</summary>
        public FishingSpot MoveNearestTo(Vector3 point)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < spots.Length; i++)
            {
                if (!spots[i]) continue;
                float d = spots[i].gameObject.activeSelf ? spots[i].FlatDistance(point) : float.MaxValue * 0.5f;
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            if (best < 0) return null;
            placeOf[best] = -1;
            spots[best].Spawn(point);
            dieAt[best] = Time.time + Random.Range(lifeRange.x, lifeRange.y);
            return spots[best];
        }

        void Place(int i, float awayFromShip)
        {
            if (places.Length == 0) return;
            Vector3 ship = playerShip ? playerShip.position : Vector3.zero;
            // Sorteia um ponto livre (sem outro cardume) longe do navio; sem nenhum assim, espera e tenta de novo.
            for (int attempt = 0; attempt < 12; attempt++)
            {
                int p = Random.Range(0, places.Length);
                if (Used(p)) continue;
                Vector3 at = places[p];
                Vector3 d = at - ship;
                d.y = 0f;
                if (d.sqrMagnitude < awayFromShip * awayFromShip) continue;
                placeOf[i] = p;
                spots[i].Spawn(at);
                dieAt[i] = Time.time + Random.Range(lifeRange.x, lifeRange.y);
                return;
            }
            respawnAt[i] = Time.time + 10f;
        }

        bool Used(int place)
        {
            for (int i = 0; i < placeOf.Length; i++)
                if (placeOf[i] == place) return true;
            return false;
        }

        bool CameraNear(Vector3 point) => cam && (cam.transform.position - point).sqrMagnitude < hideDistance * hideDistance;
    }
}
