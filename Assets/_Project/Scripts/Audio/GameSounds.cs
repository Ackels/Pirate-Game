using System;

namespace Pirates.Audio
{
    /// <summary>Todos os sons do jogo, num lugar só (o gerador preenche com os assets de Data/Audio).</summary>
    [Serializable]
    public class GameSounds
    {
        // Mar e navio
        public SoundCue cannonFire, cannonSplash, cannonImpact, shipSinking, sails, anchor, shipCreak, harborBell, seaWaves;
        // A pé
        public SoundCue stepSand, stepGrass, stepDirt, stepRock, stepWood, stepWater, jump;
        public SoundCue swordSwing, swordHit, pistolShot, pistolReload, drawSword, drawPistol, pain, death, bodyFall;
        public SoundCue crateBreak, dummyHit, chestOpen, dig, pickup, backpackFull;
        // Porto e interface
        public SoundCue doorOpen, doorClose, cargoMoved, repair, purchase, buildDone, missionAccepted, missionDone,
            missionPaid, gameSaved;
        // Ambiente
        public SoundCue fire, jungleBirds, seagulls, shoreWaves, wind;
        // Música
        public SoundCue musicPort, musicSea, musicIsland, musicCombat;
    }
}
