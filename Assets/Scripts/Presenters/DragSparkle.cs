using DG.Tweening;
using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Occasional gentle motes that drift from a dragged card, giving it a magical "picked up" feel.
    /// Similar to StockPressSparkle but more subtle and continuous while dragging.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class DragSparkle : MonoBehaviour
    {
        private const string AdditiveShaderName = "Solitaire/SpriteAdditive";
        private const string EmitTweenIdPrefix = "DragSparkleEmit";

        [Header("Tune in Play (select DragSparkle under the card)")]
        [Tooltip("Seconds between mote emissions while dragging")]
        [SerializeField]
        private float emitInterval = 0.8f;

        [Tooltip("Random variance in emit timing (+/- this amount)")]
        [SerializeField]
        private float emitVariance = 0.3f;

        [SerializeField]
        private int minCount = 1;

        [SerializeField]
        private int maxCount = 2;

        [Tooltip("World size. Slightly smaller than stock sparkles for subtlety")]
        [SerializeField]
        private float minSize = 0.08f;

        [SerializeField]
        private float maxSize = 0.12f;

        [SerializeField]
        private float minSpeed = 0.15f;

        [SerializeField]
        private float maxSpeed = 0.25f;

        [SerializeField]
        private float minLifetime = 0.8f;

        [SerializeField]
        private float maxLifetime = 1.2f;

        [Tooltip("How far from card edges motes can spawn (0.5 = half card width/height)")]
        [SerializeField]
        private float spawnRadius = 0.4f;

        [SerializeField]
        private Color moteColor = new Color(0.9f, 0.95f, 1f, 0.6f);

        [Tooltip("Downward drift strength")]
        [SerializeField]
        private float downwardDrift = 0.08f;

        private static Texture2D sharedMoteTexture;
        private static Material sharedMaterial;
        private static bool didWarnMissingShader;

        private ParticleSystem particles;
        private ParticleSystemRenderer particlesRenderer;
        private SpriteRenderer cardRenderer;
        private Transform cardTransform;
        private string emitTweenId;
        private bool didBuild;
        private bool isDragging;

        private void Awake()
        {
            particles = GetComponent<ParticleSystem>();
            particlesRenderer = GetComponent<ParticleSystemRenderer>();
            emitTweenId = EmitTweenIdPrefix + GetInstanceID();
        }

        public void Bind(SpriteRenderer cardFace)
        {
            cardRenderer = cardFace;
            cardTransform = cardFace != null ? cardFace.transform.parent : transform.parent;
            EnsureBuilt();
        }

        public void StartDragSparkling()
        {
            if (isDragging) return;
            
            isDragging = true;
            EnsureBuilt();
            if (particles == null) return;

            ApplyTune();
            SyncSorting();
            ScheduleNextEmit();
        }

        public void StopDragSparkling()
        {
            isDragging = false;
            DOTween.Kill(emitTweenId);
        }

        private void ScheduleNextEmit()
        {
            if (!isDragging) return;

            var delay = emitInterval + Random.Range(-emitVariance, emitVariance);
            delay = Mathf.Max(0.1f, delay);

            DOTween.Kill(emitTweenId);
            DOVirtual.DelayedCall(delay, () =>
            {
                if (isDragging)
                {
                    EmitMotes();
                    ScheduleNextEmit();
                }
            }).SetId(emitTweenId);
        }

        private void EmitMotes()
        {
            if (particles == null) return;

            var count = Random.Range(minCount, maxCount + 1);
            var halfSize = GetCardHalfSize();
            
            var origin = cardRenderer != null
                ? cardRenderer.transform
                : cardTransform != null
                    ? cardTransform
                    : transform;

            for (var i = 0; i < count; i++)
            {
                // Spawn around card perimeter
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var distance = Random.Range(halfSize.magnitude * 0.3f, halfSize.magnitude * spawnRadius);
                var local = new Vector3(
                    Mathf.Cos(angle) * distance,
                    Mathf.Sin(angle) * distance,
                    -0.03f
                );

                // Gentle outward drift with slight downward tendency
                var outward = new Vector3(
                    Mathf.Cos(angle) * Random.Range(minSpeed, maxSpeed) * 0.5f,
                    Mathf.Sin(angle) * Random.Range(minSpeed, maxSpeed) * 0.3f - downwardDrift,
                    0f
                );

                var emit = new ParticleSystem.EmitParams
                {
                    position = origin.position + origin.TransformDirection(local),
                    velocity = origin.TransformDirection(outward),
                    startSize = Random.Range(minSize, maxSize),
                    startLifetime = Random.Range(minLifetime, maxLifetime),
                    startColor = moteColor
                };
                particles.Emit(emit, 1);
            }
        }

        private Vector2 GetCardHalfSize()
        {
            if (cardRenderer == null || cardRenderer.sprite == null)
                return new Vector2(0.5f, 0.75f);

            var size = cardRenderer.sprite.bounds.size;
            var scale = cardRenderer.transform.lossyScale;
            return new Vector2(
                size.x * Mathf.Abs(scale.x) * 0.5f,
                size.y * Mathf.Abs(scale.y) * 0.5f
            );
        }

        private void EnsureBuilt()
        {
            if (didBuild) return;

            if (particles == null)
                particles = GetComponent<ParticleSystem>();
            if (particlesRenderer == null)
                particlesRenderer = GetComponent<ParticleSystemRenderer>();

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var material = GetOrCreateMaterial();
            if (material != null)
                particlesRenderer.sharedMaterial = material;

            particlesRenderer.renderMode = ParticleSystemRenderMode.Billboard;

            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 2f;
            main.maxParticles = 12;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.2f; // Gentle downward fall
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());

            var shape = particles.shape;
            shape.enabled = false;

            // Gentle fade out
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
                    new GradientAlphaKey(0f, 0f),      // Start invisible
                    new GradientAlphaKey(1f, 0.1f),    // Quick fade in
                    new GradientAlphaKey(1f, 0.4f),    // Stay visible while falling
                    new GradientAlphaKey(0f, 1f)       // Fade out
                }
            );
            colorOverLifetime.color = fade;

            // Subtle size animation
            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                AnimationCurve.EaseInOut(0f, 1f, 0.8f, 0.3f)
            );

            didBuild = true;
            ApplyTune();
        }

        private void ApplyTune()
        {
            if (particles == null) return;

            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = moteColor;
        }

        private void SyncSorting()
        {
            if (particlesRenderer == null) return;

            if (cardRenderer != null)
            {
                particlesRenderer.sortingLayerID = cardRenderer.sortingLayerID;
                particlesRenderer.sortingOrder = cardRenderer.sortingOrder + 15;
                return;
            }

            particlesRenderer.sortingOrder = 150;
        }

        private void OnDisable()
        {
            StopDragSparkling();
        }

        private static Material GetOrCreateMaterial()
        {
            if (sharedMaterial != null)
                return sharedMaterial;

            var shader = Shader.Find(AdditiveShaderName);
            if (shader == null)
            {
                if (!didWarnMissingShader)
                {
                    Debug.LogWarning("[DragSparkle] Solitaire/SpriteAdditive not found.");
                    didWarnMissingShader = true;
                }
                return null;
            }

            sharedMaterial = new Material(shader)
            {
                name = "DragSparkleRuntime",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = GetOrCreateMoteTexture()
            };
            return sharedMaterial;
        }

        private static Texture2D GetOrCreateMoteTexture()
        {
            if (sharedMoteTexture != null)
                return sharedMoteTexture;

            const int size = 24;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "DragMote",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var center = (size - 1) * 0.5f;
            var radius = size * 0.4f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / radius;
                    var dy = (y - center) / radius;
                    // Softer falloff than stock sparkles for subtlety
                    var alpha = Mathf.Exp(-(dx * dx + dy * dy) * 2.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply(false, true);
            sharedMoteTexture = tex;
            return sharedMoteTexture;
        }
    }
}