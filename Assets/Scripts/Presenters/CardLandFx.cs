using DG.Tweening;
using Solitaire.Services;
using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Plays the ParticlePulse hierarchy on foundation land (suit burst + glow).
    /// Tableau confirms use SoftRippleConfirm instead.
    /// </summary>
    public class CardLandFx : MonoBehaviour
    {
        private const string HoldTweenIdPrefix = "ParticlePulseHold";
        private const float CaptureDelay = 0.01f;

        [SerializeField]
        private ParticleSystem rootPulse;

        [SerializeField]
        private ParticleSystem glowPulse;

        [SerializeField]
        private ParticleSystem suitBurst;

        [SerializeField]
        private ParticleSystem rankBurst;

        [Header("Born still, then explode")]
        [Tooltip("Particles appear, sit still this long, then keep the prefab's own speed.")]
        [SerializeField]
        private float holdDuration = 0.08f;

        [Header("Suit emission")]
        [Tooltip("Hearts and Diamonds. Same RGB as the picker, HDR Intensity +2.")]
        [ColorUsage(true, true)]
        [SerializeField]
        private Color redSuitEmission = new Color(
            222f / 255f * 4f,
            2f / 255f * 4f,
            44f / 255f * 4f,
            1f
        );

        [Tooltip("Spades and Clubs. Same RGB as the picker, HDR Intensity +2.")]
        [ColorUsage(true, true)]
        [SerializeField]
        private Color blackSuitEmission = new Color(
            29f / 255f * 4f,
            1f / 255f * 4f,
            73f / 255f * 4f,
            1f
        );

        private ParticleSystem[] cachedPulses;
        private ParticleSystem.Particle[] particleBuffer;
        private Vector3[][] savedVelocities;
        private int[] savedCounts;
        private string holdTweenId;
        private bool volumeCached;
        private float[] originalRateMultipliers;
        private float[] originalSimulationSpeeds;
        private ParticleSystem.Burst[][] originalBursts;

        private void Awake()
        {
            holdTweenId = HoldTweenIdPrefix + GetInstanceID();
            CacheSystems();
        }

        public void Play(Sprite suitSprite, Sprite rankSprite, bool isRedSuit)
        {
            Play(suitSprite, rankSprite, isRedSuit, LandFxVolume.Normal);
        }

        public void Play(
            Sprite suitSprite,
            Sprite rankSprite,
            bool isRedSuit,
            LandFxVolume volume
        )
        {
            CacheSystems();
            if (rootPulse == null)
                return;

            ApplyVolume(volume);

            SetChildEmission(suitBurst, true);
            SetChildEmission(rankBurst, rankSprite != null);

            var emission = isRedSuit ? redSuitEmission : blackSuitEmission;
            ApplySprite(suitBurst, suitSprite, emission);
            ApplySprite(rankBurst, rankSprite, emission);

            DOTween.Kill(holdTweenId);
            rootPulse.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            rootPulse.Play(true);
            StartHoldThenRelease();
        }

        public void Stop()
        {
            DOTween.Kill(holdTweenId);
            RestoreVolume();

            if (rootPulse == null)
                return;

            rootPulse.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetChildEmission(suitBurst, true);
            SetChildEmission(rankBurst, true);
        }

        private void StartHoldThenRelease()
        {
            if (holdDuration <= 0f)
                return;

            var sequence = DOTween.Sequence().SetId(holdTweenId);
            sequence.AppendInterval(CaptureDelay);
            sequence.AppendCallback(CaptureAndZeroVelocities);
            sequence.AppendInterval(holdDuration);
            sequence.AppendCallback(RestoreVelocities);
        }

        private void CaptureAndZeroVelocities()
        {
            if (cachedPulses == null)
                return;

            EnsureVelocityBuffers();

            for (var i = 0; i < cachedPulses.Length; i++)
            {
                var ps = cachedPulses[i];
                if (ps == null)
                {
                    savedCounts[i] = 0;
                    continue;
                }

                var count = CopyParticles(ps);
                savedCounts[i] = count;
                if (count == 0)
                    continue;

                var saved = savedVelocities[i];
                if (saved == null || saved.Length < count)
                {
                    saved = new Vector3[count];
                    savedVelocities[i] = saved;
                }

                for (var p = 0; p < count; p++)
                {
                    saved[p] = particleBuffer[p].velocity;
                    particleBuffer[p].velocity = Vector3.zero;
                }

                ps.SetParticles(particleBuffer, count);
            }
        }

        private void RestoreVelocities()
        {
            if (cachedPulses == null || savedVelocities == null)
                return;

            for (var i = 0; i < cachedPulses.Length; i++)
            {
                var ps = cachedPulses[i];
                var saved = savedVelocities[i];
                var savedCount = savedCounts != null ? savedCounts[i] : 0;
                if (ps == null || saved == null || savedCount == 0)
                    continue;

                var count = CopyParticles(ps);
                var n = Mathf.Min(count, savedCount);
                for (var p = 0; p < n; p++)
                    particleBuffer[p].velocity = saved[p];

                ps.SetParticles(particleBuffer, count);
            }
        }

        private int CopyParticles(ParticleSystem ps)
        {
            var count = ps.particleCount;
            if (count == 0)
                return 0;

            if (particleBuffer == null || particleBuffer.Length < count)
                particleBuffer = new ParticleSystem.Particle[count];

            return ps.GetParticles(particleBuffer);
        }

        private void EnsureVelocityBuffers()
        {
            if (savedVelocities == null || savedVelocities.Length != cachedPulses.Length)
                savedVelocities = new Vector3[cachedPulses.Length][];

            if (savedCounts == null || savedCounts.Length != cachedPulses.Length)
                savedCounts = new int[cachedPulses.Length];
        }

        private void CacheSystems()
        {
            if (string.IsNullOrEmpty(holdTweenId))
                holdTweenId = HoldTweenIdPrefix + GetInstanceID();

            if (rootPulse == null)
                rootPulse = GetComponent<ParticleSystem>();

            var pulses = GetComponentsInChildren<ParticleSystem>(true);
            if (cachedPulses == null || cachedPulses.Length != pulses.Length)
                volumeCached = false;
            cachedPulses = pulses;

            if (glowPulse != null && suitBurst != null)
                return;

            for (var i = 0; i < cachedPulses.Length; i++)
            {
                var ps = cachedPulses[i];
                var name = ps.gameObject.name;
                if (
                    glowPulse == null
                    && name.IndexOf("Glow", System.StringComparison.OrdinalIgnoreCase) >= 0
                )
                    glowPulse = ps;
                else if (
                    suitBurst == null
                    && name.IndexOf("Suit", System.StringComparison.OrdinalIgnoreCase) >= 0
                )
                    suitBurst = ps;
                else if (
                    rankBurst == null
                    && (
                        name.IndexOf("Rank", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf("Glyph", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf("Type", System.StringComparison.OrdinalIgnoreCase) >= 0
                    )
                )
                    rankBurst = ps;
            }
        }

        private static void SetChildEmission(ParticleSystem ps, bool enabled)
        {
            if (ps == null)
                return;

            var emission = ps.emission;
            emission.enabled = enabled;
        }

        private void CacheVolumeOriginals()
        {
            if (volumeCached || cachedPulses == null)
                return;

            volumeCached = true;
            originalRateMultipliers = new float[cachedPulses.Length];
            originalSimulationSpeeds = new float[cachedPulses.Length];
            originalBursts = new ParticleSystem.Burst[cachedPulses.Length][];

            for (var i = 0; i < cachedPulses.Length; i++)
            {
                var ps = cachedPulses[i];
                if (ps == null)
                    continue;

                var emission = ps.emission;
                originalRateMultipliers[i] = emission.rateOverTimeMultiplier;
                originalSimulationSpeeds[i] = ps.main.simulationSpeed;

                var burstCount = emission.burstCount;
                if (burstCount <= 0)
                    continue;

                var bursts = new ParticleSystem.Burst[burstCount];
                emission.GetBursts(bursts);
                originalBursts[i] = bursts;
            }
        }

        private void ApplyVolume(LandFxVolume volume)
        {
            CacheSystems();
            CacheVolumeOriginals();
            if (cachedPulses == null)
                return;

            for (var i = 0; i < cachedPulses.Length; i++)
            {
                var ps = cachedPulses[i];
                if (ps == null)
                    continue;

                var emission = ps.emission;
                emission.rateOverTimeMultiplier =
                    originalRateMultipliers[i] * volume.EmissionScale;

                var main = ps.main;
                main.simulationSpeed = originalSimulationSpeeds[i] * volume.SimulationSpeed;

                var sourceBursts = originalBursts[i];
                if (sourceBursts == null || sourceBursts.Length == 0)
                    continue;

                var bursts = new ParticleSystem.Burst[sourceBursts.Length];
                for (var b = 0; b < sourceBursts.Length; b++)
                {
                    var burst = sourceBursts[b];
                    var count = burst.count;
                    if (count.mode == ParticleSystemCurveMode.Constant)
                    {
                        burst.count = count.constant * volume.EmissionScale;
                    }
                    else if (count.mode == ParticleSystemCurveMode.TwoConstants)
                    {
                        burst.count = new ParticleSystem.MinMaxCurve(
                            count.constantMin * volume.EmissionScale,
                            count.constantMax * volume.EmissionScale
                        );
                    }

                    burst.minCount = (short)
                        Mathf.Max(0, Mathf.RoundToInt(burst.minCount * volume.EmissionScale));
                    burst.maxCount = (short)
                        Mathf.Max(0, Mathf.RoundToInt(burst.maxCount * volume.EmissionScale));
                    bursts[b] = burst;
                }

                emission.SetBursts(bursts);
            }
        }

        private void RestoreVolume()
        {
            if (!volumeCached || cachedPulses == null)
                return;

            for (var i = 0; i < cachedPulses.Length; i++)
            {
                var ps = cachedPulses[i];
                if (ps == null)
                    continue;

                var emission = ps.emission;
                emission.rateOverTimeMultiplier = originalRateMultipliers[i];

                var main = ps.main;
                main.simulationSpeed = originalSimulationSpeeds[i];

                var sourceBursts = originalBursts[i];
                if (sourceBursts != null && sourceBursts.Length > 0)
                    emission.SetBursts(sourceBursts);
            }
        }

        private void OnDisable()
        {
            DOTween.Kill(holdTweenId);
        }

        private void ApplySprite(ParticleSystem ps, Sprite sprite, Color emissionColor)
        {
            if (ps == null || sprite == null)
                return;

            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Sprites;

            if (sheet.spriteCount == 0)
                sheet.AddSprite(sprite);
            else
                sheet.SetSprite(0, sprite);

            sheet.numTilesX = 1;
            sheet.numTilesY = 1;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer == null)
                return;

            var mat = renderer.material;
            if (mat == null)
                return;

            if (sprite.texture != null)
            {
                if (mat.HasProperty("_BaseMap"))
                    mat.SetTexture("_BaseMap", sprite.texture);
                if (mat.HasProperty("_MainTex"))
                    mat.SetTexture("_MainTex", sprite.texture);
            }

            ApplyEmissionColor(mat, emissionColor);
        }

        private static void ApplyEmissionColor(Material mat, Color emissionColor)
        {
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            if (mat.HasProperty("_EmissionMap"))
                mat.SetTexture("_EmissionMap", Texture2D.whiteTexture);

            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", emissionColor);
        }
    }
}
