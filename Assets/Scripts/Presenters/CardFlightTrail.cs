using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Additive ribbon behind a flying victory card. Pooled — do not add per-card in the prefab.
    /// </summary>
    [RequireComponent(typeof(TrailRenderer))]
    public sealed class CardFlightTrail : MonoBehaviour
    {
        private const string AdditiveShaderName = "Solitaire/SpriteAdditive";
        private const float TrailTime = 0.42f;

        private static readonly Color WarmTint = new Color(1f, 0.42f, 0.28f, 1f);
        private static readonly Color CoolTint = new Color(0.45f, 0.38f, 1f, 1f);

        private static Material sharedMaterial;
        private static Texture2D sharedRibbonTexture;
        private static bool didWarnMissingShader;

        private TrailRenderer trail;
        private bool didBuild;

        public float FadeSeconds => TrailTime;

        private void Awake()
        {
            EnsureBuilt();
        }

        public void Play(bool isRedSuit, int sortingOrder)
        {
            EnsureBuilt();
            if (trail == null)
                return;

            var tint = isRedSuit ? WarmTint : CoolTint;
            trail.colorGradient = BuildGradient(tint);
            trail.sortingOrder = sortingOrder;
            trail.Clear();
            trail.emitting = true;
        }

        public void StopEmitting()
        {
            if (trail != null)
                trail.emitting = false;
        }

        public void ClearAndReset()
        {
            if (trail == null)
                return;

            trail.emitting = false;
            trail.Clear();
        }

        private void EnsureBuilt()
        {
            if (didBuild)
                return;

            didBuild = true;
            trail = GetComponent<TrailRenderer>();
            if (trail == null)
                trail = gameObject.AddComponent<TrailRenderer>();

            var mat = GetOrCreateMaterial();
            if (mat != null)
                trail.material = mat;

            trail.time = TrailTime;
            trail.minVertexDistance = 0.03f;
            trail.widthMultiplier = 1f;
            trail.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.2f),
                new Keyframe(0.35f, 0.1f),
                new Keyframe(1f, 0.02f)
            );
            trail.numCornerVertices = 1;
            trail.numCapVertices = 2;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.allowOcclusionWhenDynamic = false;
            trail.autodestruct = false;
            trail.emitting = false;
            trail.Clear();
        }

        private static Gradient BuildGradient(Color tint)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(tint, 0f),
                    new GradientColorKey(Color.Lerp(tint, Color.white, 0.35f), 0.25f),
                    new GradientColorKey(tint, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.4f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            return gradient;
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
                        "[CardFlightTrail] Shader Solitaire/SpriteAdditive not found."
                    );
                    didWarnMissingShader = true;
                }

                return null;
            }

            sharedMaterial = new Material(shader)
            {
                name = "CardFlightTrailRuntime",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = GetOrCreateRibbonTexture()
            };
            return sharedMaterial;
        }

        private static Texture2D GetOrCreateRibbonTexture()
        {
            if (sharedRibbonTexture != null)
                return sharedRibbonTexture;

            const int width = 16;
            const int height = 32;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "FlightRibbon",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var center = (height - 1) * 0.5f;
            var radius = height * 0.42f;
            for (var y = 0; y < height; y++)
            {
                var dy = (y - center) / radius;
                var alpha = Mathf.Exp(-(dy * dy) * 2.4f);
                var color = new Color(1f, 1f, 1f, alpha);
                for (var x = 0; x < width; x++)
                    tex.SetPixel(x, y, color);
            }

            tex.Apply(false, true);
            sharedRibbonTexture = tex;
            return sharedRibbonTexture;
        }
    }
}
