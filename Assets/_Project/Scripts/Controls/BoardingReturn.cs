using Pirates.Core;
using Pirates.OnFoot;
using UnityEngine;

namespace Pirates.Controls
{
    /// <summary>
    /// A pé no palco da abordagem (04/11): F perto do ponto de chegada, no convés do seu navio, volta ao navio
    /// (<see cref="ShipBoarding.Return"/>). Uma ação, não uma janela.
    /// </summary>
    public class BoardingReturn : MonoBehaviour, IInteraction
    {
        [SerializeField] ShipBoarding boarding;
        [SerializeField] float reach = 2.6f;

        FootCharacter character;

        public ShipBoarding Boarding => boarding;
        public bool IsOpen => false;
        public bool CanOpen => boarding && boarding.CanReturn(transform.position, reach) && !(character && (character.IsDown || character.IsDodging));

        public void Configure(ShipBoarding shipBoarding) => boarding = shipBoarding;

        void Awake() => character = GetComponent<FootCharacter>();

        public bool Open() => CanOpen && boarding.Return();

        public void Close() { }
    }
}
