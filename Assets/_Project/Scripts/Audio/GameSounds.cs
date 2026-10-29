using System;

namespace Pirates.Audio
{
    /// <summary>Todos os sons do jogo, num lugar só (o gerador preenche com os assets de Data/Audio).</summary>
    [Serializable]
    public class GameSounds
    {
        // Mar e navio
        public SoundCue cannonFire, cannonSplash, cannonImpact, explosion, shipSinking, sails, anchor, shipCreak, harborBell, seaWaves;
        // A pé
        public SoundCue stepSand, stepGrass, stepDirt, stepRock, stepWood, stepWater, jump, dodge;
        public SoundCue kick, block, parry, swordSwing, swordHit, pistolShot, pistolReload, drawSword, drawPistol, pain, death, bodyFall;
        public SoundCue crateBreak, dummyHit, chestOpen, dig, pickup, backpackFull;
        // Coleta (D1): machadada no tronco.
        public SoundCue chop;
        // Catacumbas (C5): gemido das almas penadas e guincho dos morcegos (feitos pelo gerador).
        public SoundCue ghostMoan, batScreech;
        // Porto e interface
        public SoundCue doorOpen, doorClose, cargoMoved, repair, purchase, buildDone, missionAccepted, missionDone,
            missionPaid, gameSaved;
        // Ambiente
        public SoundCue fire, jungleBirds, seagulls, shoreWaves, wind, rain, storm, thunder, crypt;
        // Música
        public SoundCue musicTitle, musicPort, musicSea, musicIsland, musicCombat;
    }
}
