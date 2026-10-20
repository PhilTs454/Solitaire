using DG.Tweening;
using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Tiny motes that fly out the top and bottom edges when the stock spring pops back.
    /// Same soft-dot look as the Home float motes.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class StockPressSparkle : MonoBehaviour
    {
        private const string AdditiveShaderName = "Solitaire/SpriteAdditive";
        private const string DelayTweenIdPrefix = "StockPressSparkleDelay";

        [Header("Tune in Play (select StockPressSparkle under the card)")]
        [SerializeField]
        private int minCount = 2;

        [SerializeField]
        private int maxCount = 3;

        [Tooltip("World size. Home motes are ~14px on a 109px card.")]
        [SerializeField]
        private float minSize = 0.1f;

        [SerializeField]
        private float maxSize = 0.16f;

        [SerializeField]
        private float minSpeed = 0.28f;

        [SerializeField]
        private float maxSpeed = 0.55f;

        [SerializeField]
        private float minLifetime = 0.35f;

        [SerializeField]
        private float maxLifetime = 0.5f;

        [Tooltip("How far along the short edge the motes can spawn.")]
        [SerializeField]
        private float edgeSpread = 0.35f;

        [SerializeField]
        private Color moteColor = new Color(1f, 1f, 0.95f, 0.85f);

        private static Texture2D sharedMoteTexture;
        private static Material sharedMaterial;
        private static bool didWarnMissingShader;

        private ParticleSystem particles;
        private ParticleSystemRenderer particlesRenderer;
        private SpriteRenderer backRenderer;
        private Transform cardTransform;
        private string delayTweenId;
        private bool didBuild;

        private void Awake()
        {
            particles = GetComponent<ParticleSystem>();
            particlesRenderer = GetComponent<ParticleSystemRenderer>();
            delayTweenId = DelayTweenIdPrefix + GetInstanceID();
        }

        public void Bind(SpriteRenderer back)
        {
            backRenderer = back;
            cardTransform = back != null ? back.transform.parent : transform.parent;
            EnsureBuilt();
        }

        public void PlayAfterPop(float delaySeconds)
        {
            EnsureBuilt();
            DOTween.Kill(delayTweenId);
            DOVirtual.DelayedCall(Mathf.Max(0f, delaySeconds), Play).SetId(delayTweenId);
        }

        public void Play()
        {
            EnsureBuilt();
            if (particles == null)
                return;

            ApplyTune();
            SyncSorting();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var total = Random.Range(Mathf.Max(1, minCount), Mathf.Max(minCount, maxCount) + 1);
            var topCount = Mathf.Max(1, (total + 1) / 2);
            var bottomCount = Mathf.Max(0, total - topCount);

            EmitAlongEdge(1f, topCount);
            EmitAlongEdge(-1f, bottomCount);
        }

        public void Stop()
        {
            DOTween.Kill(delayTweenId);
            if (particles == null)
                return;

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void EmitAlongEdge(float upSign, int count)
        {
            if (count <= 0 || particles == null)
                return;

            var halfSize = GetCardHalfSize();
            // Use the bound sprite (not its parent) so a scaled pile slot still
            // sparks at its own edges. TransformDirection ignores scale; halfSize
            // already includes lossyScale.
            var origin = backRenderer != null
                ? backRenderer.transform
                : cardTransform != null
                    ? cardTransform
                    : transform;
            var halfWidth = halfSize.x * Mathf.Clamp01(edgeSpread);

            for (var i = 0; i < count; i++)
            {
                var local = new Vector3(
                    Random.Range(-halfWidth, halfWidth),
                    upSign * halfSize.y,
                    -0.05f
                );
                var sideways = origin.TransformDirection(Vector3.right) * Random.Range(-0.1f, 0.1f);
                var outward = origin.TransformDirection(Vector3.up) * upSign
                    * Random.Range(minSpeed, maxSpeed);

                var emit = new ParticleSystem.EmitParams
                {
                    position = origin.position + origin.TransformDirection(local),
                    velocity = outward + sideways,
                    startSize = Random.Range(minSize, maxSize),
                    startLifetime = Random.Range(minLifetime, maxLifetime),
                    startColor = moteColor
                };
                particles.Emit(emit, 1);
            }
        }

        private Vector2 GetCardHalfSize()
        {
            if (backRenderer == null || backRenderer.sprite == null)
                return new Vector2(0.5f, 0.75f);

            var size = backRenderer.sprite.bounds.size;
            var scale = backRenderer.transform.lossyScale;
            return new Vector2(
                size.x * Mathf.Abs(scale.x) * 0.5f,
                size.y * Mathf.Abs(scale.y) * 0.5f
            );
        }

        private void EnsureBuilt()
        {
            if (didBuild)
                return;

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
            main.duration = 0.55f;
            main.maxParticles = 8;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());

            var shape = particles.shape;
            shape.enabled = false;

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
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.35f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = fade;

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f)
            );

            didBuild = true;
            ApplyTune();
        }

        private void ApplyTune()
        {
            if (particles == null)
                return;

            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = moteColor;
        }

        private void SyncSorting()
        {
            if (particlesRenderer == null)
                return;

            if (backRenderer != null)
            {
                particlesRenderer.sortingLayerID = backRenderer.sortingLayerID;
                particlesRenderer.sortingOrder = backRenderer.sortingOrder + 20;
                return;
            }

            particlesRenderer.sortingOrder = 200;
        }

        private void OnDisable()
        {
            DOTween.Kill(delayTweenId);
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
                    Debug.LogWarning(
                        "[StockPressSparkle] Solitaire/SpriteAdditive not found."
                    );
                    didWarnMissingShader = true;
                }

                return null;
            }

            sharedMaterial = new Material(shader)
            {
                name = "StockPressSparkleRuntime",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = GetOrCreateMoteTexture()
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
                name = "StockPressMote",
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
