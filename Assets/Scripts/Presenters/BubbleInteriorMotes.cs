using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Soft additive motes inside the victory bubble (same look as PlayfieldDust, local sphere volume).
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class BubbleInteriorMotes : MonoBehaviour
    {
        private const string AdditiveShaderName = "Solitaire/SpriteAdditive";

        [SerializeField]
        private Sprite moteSprite;

        [SerializeField]
        private Color moteColor = new Color(0.62f, 0.83f, 0.96f, 0.4f);

        [SerializeField]
        private float minSize = 0.025f;

        [SerializeField]
        private float maxSize = 0.055f;

        [SerializeField]
        private float minSpeed = 0.015f;

        [SerializeField]
        private float maxSpeed = 0.045f;

        [SerializeField]
        private float minLifetime = 2.5f;

        [SerializeField]
        private float maxLifetime = 5f;

        [SerializeField]
        private float spawnRate = 6f;

        [SerializeField]
        private int maxParticles = 28;

        [SerializeField]
        private float noiseStrength = 0.12f;

        [SerializeField]
        private float sphereRadius = 0.38f;

        [SerializeField]
        private int sortingOrder = 379;

        private ParticleSystem particles;
        private ParticleSystemRenderer particlesRenderer;
        private static Material sharedMaterial;
        private static Texture2D sharedMoteTexture;
        private float baseSpawnRate;
        private bool didCaptureBaseRate;

        private void Awake()
        {
            particles = GetComponent<ParticleSystem>();
            particlesRenderer = GetComponent<ParticleSystemRenderer>();
            Build();
        }

        public void Configure(Sprite sprite)
        {
            if (sprite != null)
                moteSprite = sprite;

            if (particlesRenderer == null)
                return;

            var material = GetOrCreateMaterial();
            if (material != null)
                particlesRenderer.sharedMaterial = material;
        }

        public void Play()
        {
            if (particles == null)
                return;

            particles.Clear(true);
            particles.Play(true);
        }

        public void Stop()
        {
            if (particles == null)
                return;

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void SetCharge(float charge)
        {
            if (particles == null)
                return;

            if (!didCaptureBaseRate)
            {
                baseSpawnRate = spawnRate;
                didCaptureBaseRate = true;
            }

            var t = Mathf.Clamp01(charge);
            var emission = particles.emission;
            emission.rateOverTime = Mathf.Lerp(baseSpawnRate, baseSpawnRate * 2.4f, t);
        }

        private void Build()
        {
            var material = GetOrCreateMaterial();
            if (material != null)
                particlesRenderer.sharedMaterial = material;

            particlesRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particlesRenderer.alignment = ParticleSystemRenderSpace.View;
            particlesRenderer.sortingOrder = sortingOrder;

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.prewarm = false;
            main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f);
            main.startSpeed = 0f;

            var emission = particles.emission;
            emission.enabled = true;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = sphereRadius;
            shape.radiusThickness = 1f;

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-maxSpeed, maxSpeed);
            velocity.y = new ParticleSystem.MinMaxCurve(-maxSpeed, maxSpeed);
            velocity.z = new ParticleSystem.MinMaxCurve(-maxSpeed * 0.5f, maxSpeed * 0.5f);

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(1f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = fade;

            var noise = particles.noise;
            noise.enabled = noiseStrength > 0.001f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.octaveCount = 1;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.12f;
            noise.separateAxes = true;

            ApplyTune();
        }

        private void ApplyTune()
        {
            var main = particles.main;
            main.maxParticles = Mathf.Max(1, maxParticles);
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = moteColor;

            var emission = particles.emission;
            emission.rateOverTime = Mathf.Max(0f, spawnRate);

            var shape = particles.shape;
            shape.radius = sphereRadius;

            var velocity = particles.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(-maxSpeed, maxSpeed);
            velocity.y = new ParticleSystem.MinMaxCurve(-maxSpeed, maxSpeed);
            velocity.z = new ParticleSystem.MinMaxCurve(-maxSpeed * 0.5f, maxSpeed * 0.5f);

            var noise = particles.noise;
            noise.enabled = noiseStrength > 0.001f;
            var wander = Mathf.Max(0f, noiseStrength);
            noise.strengthX = wander;
            noise.strengthY = wander;
            noise.strengthZ = wander * 0.6f;

            particlesRenderer.sortingOrder = sortingOrder;
        }

        private Material GetOrCreateMaterial()
        {
            if (sharedMaterial != null)
            {
                if (moteSprite != null && moteSprite.texture != null)
                    sharedMaterial.mainTexture = moteSprite.texture;
                return sharedMaterial;
            }

            var shader = Shader.Find(AdditiveShaderName);
            if (shader == null)
                return null;

            var texture = moteSprite != null ? moteSprite.texture : GetOrCreateMoteTexture();
            sharedMaterial = new Material(shader)
            {
                name = "BubbleInteriorMotesRuntime",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = texture
            };
            return sharedMaterial;
        }

        private static Texture2D GetOrCreateMoteTexture()
        {
            if (sharedMoteTexture != null)
                return sharedMoteTexture;

            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "BubbleInteriorMote",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var center = (size - 1) * 0.5f;
            var radius = size * 0.35f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / radius;
                    var dy = (y - center) / radius;
                    var alpha = Mathf.Exp(-(dx * dx + dy * dy) * 3f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply(false, true);
            sharedMoteTexture = tex;
            return sharedMoteTexture;
        }
    }
}
