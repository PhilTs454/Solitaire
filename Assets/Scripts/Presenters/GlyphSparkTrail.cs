using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Sparse additive motes behind a flying victory glyph.
    /// Same ParticleSystem path as BubbleInteriorMotes: procedural speckle if no sprite.
    /// World space so they linger as the pip flies on.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class GlyphSparkTrail : MonoBehaviour
    {
        private const string AdditiveShaderName = "Solitaire/SpriteAdditive";
        private const int CardSortingLayerId = 34509423;
        private const float MinLife = 0.35f;
        private const float MaxLife = 0.55f;
        private const float Drift = 0.03f;

        private static readonly Color MoteColor = new Color(0.85f, 0.55f, 0.95f, 0.7f);

        private static Material sharedMaterial;
        private static Texture2D sharedMoteTexture;
        private static Sprite sharedSprite;
        private static float sharedSize = 0.12f;
        private static int sharedCount = 16;

        private ParticleSystem particles;
        private ParticleSystemRenderer particlesRenderer;
        private bool didBuild;

        public static void SetMoteSprite(Sprite sprite)
        {
            if (sprite != null)
                sharedSprite = sprite;
        }

        public static void SetTune(float size, int count)
        {
            sharedSize = Mathf.Max(0.02f, size);
            sharedCount = Mathf.Clamp(count, 1, 64);
        }

        public void Play(int sortingOrder)
        {
            EnsureBuilt();
            if (particles == null)
                return;

            PullTowardCamera();
            particlesRenderer.sortingLayerID = CardSortingLayerId;
            particlesRenderer.sortingOrder = sortingOrder;
            ApplyMaterial();
            ApplyTune();

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play(true);
        }

        public void StopEmitting()
        {
            if (particles != null)
                particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public void ClearAndReset()
        {
            if (particles == null)
                return;

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void Awake()
        {
            EnsureBuilt();
        }

        private void EnsureBuilt()
        {
            if (didBuild)
                return;

            didBuild = true;
            particles = GetComponent<ParticleSystem>();
            particlesRenderer = GetComponent<ParticleSystemRenderer>();
            if (particles == null)
                particles = gameObject.AddComponent<ParticleSystem>();
            if (particlesRenderer == null)
                particlesRenderer = GetComponent<ParticleSystemRenderer>();

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            particlesRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particlesRenderer.alignment = ParticleSystemRenderSpace.View;
            particlesRenderer.sortingLayerID = CardSortingLayerId;
            ApplyMaterial();

            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.prewarm = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.gravityModifier = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(MinLife, MaxLife);
            main.startSpeed = 0f;
            main.startColor = MoteColor;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f);
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            var emission = particles.emission;
            emission.enabled = true;
            emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            shape.radiusThickness = 1f;

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-Drift, Drift);
            velocity.y = new ParticleSystem.MinMaxCurve(-Drift * 1.4f, Drift * 0.4f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

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
                    new GradientAlphaKey(0.2f, 0f),
                    new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(0.7f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = fade;

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                AnimationCurve.EaseInOut(0f, 1f, 1f, 0.25f)
            );

            ApplyTune();
        }

        private void ApplyTune()
        {
            if (particles == null)
                return;

            var size = sharedSize;
            var count = sharedCount;
            var main = particles.main;
            main.maxParticles = count;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.78f, size * 1.15f);

            var emission = particles.emission;
            var life = (MinLife + MaxLife) * 0.5f;
            emission.rateOverTime = count / Mathf.Max(0.15f, life);
            emission.rateOverDistance = Mathf.Max(0.4f, count * 0.08f);
        }

        private void PullTowardCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                transform.localPosition = new Vector3(0f, 0f, -0.08f);
                return;
            }

            var parent = transform.parent;
            var origin = parent != null ? parent.position : transform.position;
            var toward = cam.transform.position - origin;
            if (toward.sqrMagnitude < 0.0001f)
                return;

            var world = origin + toward.normalized * 0.08f;
            transform.position = world;
        }

        private void ApplyMaterial()
        {
            var material = GetOrCreateMaterial();
            if (material != null)
                particlesRenderer.sharedMaterial = material;
        }

        private static Material GetOrCreateMaterial()
        {
            if (sharedMaterial != null)
            {
                if (sharedSprite != null && sharedSprite.texture != null)
                    sharedMaterial.mainTexture = sharedSprite.texture;
                return sharedMaterial;
            }

            var shader = Shader.Find(AdditiveShaderName);
            if (shader == null)
                return null;

            Texture texture =
                sharedSprite != null && sharedSprite.texture != null
                    ? sharedSprite.texture
                    : GetOrCreateMoteTexture();
            sharedMaterial = new Material(shader)
            {
                name = "GlyphSparkTrailRuntime",
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
                name = "GlyphSparkMote",
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
