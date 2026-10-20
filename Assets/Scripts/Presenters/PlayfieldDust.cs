using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Quiet full-screen motes behind the cards. Add this to an empty in the Game scene.
    /// The Particle System is built in Awake so you only tune the fields below.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class PlayfieldDust : MonoBehaviour
    {
        private const string AdditiveShaderName = "Solitaire/SpriteAdditive";
        private const string SortingLayerName = "Default";

        [Header("Look")]
        [Tooltip("Optional. Soft star or dot. Empty = a procedural glow speckle.")]
        [SerializeField]
        private Sprite moteSprite;

        [Tooltip("Keep RGB at or below 1. Start around 0.25 so they read on a bright sky.")]
        [SerializeField]
        private Color moteColor = new Color(1f, 1f, 0.95f, 0.28f);

        [SerializeField]
        private float minSize = 0.12f;

        [SerializeField]
        private float maxSize = 0.2f;

        [Header("Motion")]
        [SerializeField]
        private float minSpeed = 0.02f;

        [SerializeField]
        private float maxSpeed = 0.06f;

        [SerializeField]
        private float minLifetime = 12f;

        [SerializeField]
        private float maxLifetime = 18f;

        [Tooltip("New motes per second.")]
        [SerializeField]
        private float spawnRate = 10f;

        [SerializeField]
        private int maxParticles = 70;

        [Tooltip("Tiny wander. 0 = straight drift only.")]
        [SerializeField]
        private float noiseStrength = 0.08f;

        [Header("Coverage")]
        [Tooltip("Grows the camera box so edges stay filled. 1.15 = 15% extra.")]
        [SerializeField]
        private float boxPadding = 1.15f;

        [Tooltip("Above table art (Default −15…2), still under the Card layer.")]
        [SerializeField]
        private int sortingOrder = 3;

        [Tooltip("How far in front of the camera the dust sits. Camera is at Z −1.")]
        [SerializeField]
        private float zInFrontOfCamera = 0.5f;

        private ParticleSystem particles;
        private ParticleSystemRenderer particlesRenderer;
        private static Material sharedMaterial;
        private static Texture2D sharedMoteTexture;
        private static bool didWarnMissingShader;
        private static bool didWarnMissingCamera;

        private void Awake()
        {
            particles = GetComponent<ParticleSystem>();
            particlesRenderer = GetComponent<ParticleSystemRenderer>();
            Build();
            Play();
        }

        /// <summary>Play Mode: ⋮ on this component → Debug/Make Visible Now.</summary>
        [ContextMenu("Debug/Make Visible Now")]
        public void DebugMakeVisible()
        {
            moteColor = new Color(1f, 1f, 0.95f, 0.4f);
            minSize = 0.16f;
            maxSize = 0.28f;
            spawnRate = 2f;
            maxParticles = 24;
            sortingOrder = 3;
            if (!Application.isPlaying)
                return;

            if (particles == null)
                particles = GetComponent<ParticleSystem>();
            if (particlesRenderer == null)
                particlesRenderer = GetComponent<ParticleSystemRenderer>();
            ApplyTune();
            SnapToCamera();
            FitBoxToCamera();
            particles.Play(true);
        }

        private void OnValidate()
        {
            if (!Application.isPlaying || particles == null)
                return;

            ApplyTune();
            FitBoxToCamera();
        }

        private void Build()
        {
            Debug.Assert(particles != null, "[PlayfieldDust] ParticleSystem missing.");

            SnapToCamera();

            var material = GetOrCreateMaterial();
            if (material != null)
            {
                particlesRenderer.sharedMaterial = material;
                var block = new MaterialPropertyBlock();
                particlesRenderer.GetPropertyBlock(block);
                if (material.mainTexture != null)
                    block.SetTexture("_MainTex", material.mainTexture);
                particlesRenderer.SetPropertyBlock(block);
            }

            particlesRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particlesRenderer.alignment = ParticleSystemRenderSpace.View;
            particlesRenderer.sortingLayerName = SortingLayerName;
            particlesRenderer.sortingOrder = sortingOrder;

            // Duration can only be set while the system is stopped.
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.playOnAwake = true;
            main.loop = true;
            main.prewarm = true;
            main.duration = 12f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startRotation = 0f;
            main.startSpeed = 0f;

            var emission = particles.emission;
            emission.enabled = true;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;
            shape.randomDirectionAmount = 0f;
            shape.sphericalDirectionAmount = 0f;

            // Stay on the XY plane so specks don't fly into the camera (Z −1) or behind the table.
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-maxSpeed, maxSpeed);
            velocity.y = new ParticleSystem.MinMaxCurve(-minSpeed, maxSpeed);
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
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.2f),
                    new GradientAlphaKey(1f, 0.75f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = fade;

            var noise = particles.noise;
            noise.enabled = noiseStrength > 0.001f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.octaveCount = 1;
            noise.frequency = 0.2f;
            noise.scrollSpeed = 0.08f;
            noise.separateAxes = true;

            ApplyTune();
            FitBoxToCamera();
        }

        private void ApplyTune()
        {
            var main = particles.main;
            main.maxParticles = Mathf.Max(1, maxParticles);
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = moteColor;

            var emission = particles.emission;
            emission.rateOverTime = Mathf.Max(0f, spawnRate);
            var startCount = (short)Mathf.Clamp(maxParticles / 2, 4, maxParticles);
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, startCount) });

            var velocity = particles.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(-maxSpeed, maxSpeed);
            velocity.y = new ParticleSystem.MinMaxCurve(-minSpeed, maxSpeed);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var noise = particles.noise;
            noise.enabled = noiseStrength > 0.001f;
            var wander = Mathf.Max(0f, noiseStrength);
            noise.strengthX = wander;
            noise.strengthY = wander;
            noise.strengthZ = 0f;

            particlesRenderer.sortingOrder = sortingOrder;
        }

        private void Play()
        {
            particles.Play(true);
            Debug.Log(
                $"[PlayfieldDust] Playing at {transform.position} (camera-relative Z). "
                    + "If you still see nothing, the GameObject may be inactive."
            );
        }

        private void SnapToCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                if (!didWarnMissingCamera)
                {
                    Debug.LogWarning(
                        "[PlayfieldDust] No Main Camera. Place this object at the table center."
                    );
                    didWarnMissingCamera = true;
                }

                return;
            }

            // Camera lives at Z −1. Table sprites sit at Z 0. Dust must be between them
            // or the opaque sky hides every particle (they do not use Card sorting).
            var p = cam.transform.position;
            var z = p.z + Mathf.Max(0.1f, zInFrontOfCamera);
            transform.position = new Vector3(p.x, p.y, z);
        }

        private void FitBoxToCamera()
        {
            var cam = Camera.main;
            var shape = particles.shape;
            if (cam == null || !cam.orthographic)
            {
                shape.scale = new Vector3(18f, 18f, 0.1f);
                return;
            }

            var pad = Mathf.Max(1f, boxPadding);
            var height = cam.orthographicSize * 2f * pad;
            var width = height * cam.aspect;
            shape.scale = new Vector3(width, height, 0.1f);
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
            {
                if (!didWarnMissingShader)
                {
                    Debug.LogWarning(
                        "[PlayfieldDust] Solitaire/SpriteAdditive not found. Dust may look flat."
                    );
                    didWarnMissingShader = true;
                }

                return null;
            }

            Texture texture = moteSprite != null ? moteSprite.texture : GetOrCreateMoteTexture();
            sharedMaterial = new Material(shader)
            {
                name = "PlayfieldDustRuntime",
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
                name = "PlayfieldDustMote",
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
