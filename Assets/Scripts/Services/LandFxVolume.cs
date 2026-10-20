namespace Solitaire.Services
{
    /// <summary>
    /// Foundation land intensity. Harvest is a quiet setup beat, not the climax.
    /// </summary>
    public readonly struct LandFxVolume
    {
        public float EmissionScale { get; }
        public float BurstScale { get; }
        public float SimulationSpeed { get; }
        public float SfxVolume { get; }
        public bool PunchCard { get; }
        public bool AceRipple { get; }

        public static LandFxVolume Normal { get; } =
            new LandFxVolume(1f, 1f, 1f, 0.55f, true, true);

        public static LandFxVolume Harvest { get; } =
            new LandFxVolume(0.35f, 0.6f, 1.25f, 0.165f, false, false);

        public LandFxVolume(
            float emissionScale,
            float burstScale,
            float simulationSpeed,
            float sfxVolume,
            bool punchCard,
            bool aceRipple
        )
        {
            EmissionScale = emissionScale;
            BurstScale = burstScale;
            SimulationSpeed = simulationSpeed;
            SfxVolume = sfxVolume;
            PunchCard = punchCard;
            AceRipple = aceRipple;
        }
    }
}
