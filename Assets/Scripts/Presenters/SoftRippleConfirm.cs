using System;
using DG.Tweening;
using Solitaire.Helpers;
using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Candy neon ring on a valid tableau drop: expands a little and fades.
    /// Additive blend adds light. Magenta tint keeps the ring readable on a pale table
    /// (white additive just borrowed the old dark-magenta background).
    /// A faster inner ring (SoftRippleCore) adds punch on the same frame.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SoftRippleConfirm : MonoBehaviour
    {
        private const string TweenIdPrefix = "SoftRipple";
        private const int CardSortingLayerId = 34509423;
        private const string AdditiveShaderName = "Solitaire/SpriteAdditive";

        [Header("Outer ring")]
        [SerializeField]
        private SpriteRenderer spriteRenderer;

        [Tooltip("Optional. If empty, uses the sprite passed from GameInstaller / Configure.")]
        [SerializeField]
        private Sprite rippleSprite;

        [Tooltip("Optional additive material. If empty, uses Solitaire/SpriteAdditive.")]
        [SerializeField]
        private Material rippleMaterial;

        [Tooltip("Outer size vs the card's shorter side when the ring appears. 0 = grow from nothing.")]
        [SerializeField]
        private float startFit = 0.03f;

        [Tooltip("Outer size vs the card's shorter side at the end of the pop.")]
        [SerializeField]
        private float endFit = 1.2f;

        [SerializeField]
        private float duration = 0.25f;

        [Tooltip("How long the glow takes to fade out. Can be longer than Duration.")]
        [SerializeField]
        private float fadeDuration = 0.3f;

        [Tooltip("Wait this fraction of the scale tween before fading, so the glow stays bright.")]
        [SerializeField]
        [Range(0f, 0.9f)]
        private float fadeHold = 0.3f;

        [Tooltip("Additive tint. White borrows the table color; magenta keeps the candy look on a light table. Values can go above 1.")]
        [ColorUsage(true, true)]
        [SerializeField]
        private Color rippleColor = new Color(3.4f, 0.5f, 2.6f, 1f);

        [Header("Stars")]
        [Tooltip("Particle tint. Applied at runtime so the Stars prefab stays unchanged. Values can go above 1.")]
        [ColorUsage(true, true)]
        [SerializeField]
        private Color starsColor = new Color(3.8f, 1.4f, 3.0f, 1f);

        [Header("Inner ring (punch)")]
        [Tooltip("Inner size vs the card's shorter side when it appears.")]
        [SerializeField]
        private float coreStartFit = 0.05f;

        [Tooltip("Inner size vs the card's shorter side at the end of the pop.")]
        [SerializeField]
        private float coreEndFit = 0.9f;

        [SerializeField]
        private float coreDuration = 0.15f;

        [SerializeField]
        private float coreFadeDuration = 0.15f;

        private static Material sharedAdditiveMaterial;
        private static bool didWarnMissingShader;

        private Transform cachedTransform;
        private Transform coreTransform;
        private SpriteRenderer coreRenderer;
        private SpriteRenderer cardFaceRenderer;
        private string tweenId;
        private bool didSetup;
        private float playStartScale = 1f;
        private float playEndScale = 1f;
        private float playCoreStartScale = 1f;
        private float playCoreEndScale = 1f;
        private float playAlpha = 1f;
        private ParticleSystem starsPulse;
        private ParticleSystemRenderer starsRenderer;
        private Transform starsTransform;
        private MaterialPropertyBlock starsPropertyBlock;
        private Gradient starsFadeGradient;

        private void Awake()
        {
            SetupIfNeeded();
        }

        /// <summary>
        /// Call right after AddComponent when spawning from code.
        /// </summary>
        public void Configure(
            Sprite sprite,
            Material additiveMaterial = null,
            ParticleSystem stars = null,
            Color? color = null
        )
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (sprite != null)
            {
                rippleSprite = sprite;
                if (spriteRenderer != null)
                    spriteRenderer.sprite = sprite;
            }

            if (additiveMaterial != null)
                rippleMaterial = additiveMaterial;

            if (stars != null)
            {
                starsPulse = stars;
                starsTransform = stars.transform;
                starsRenderer = stars.GetComponent<ParticleSystemRenderer>();
            }

            if (color.HasValue)
                rippleColor = color.Value;

            SetupIfNeeded();
            ApplyLook();
            ApplyStarsLook();
        }

        private void SetupIfNeeded()
        {
            if (didSetup)
                return;

            cachedTransform = transform;
            tweenId = TweenIdPrefix + GetInstanceID();

            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            Debug.Assert(spriteRenderer != null, "[SoftRippleConfirm] Missing SpriteRenderer.");

            EnsureCore();
            CacheCardFace();
            ApplyLook();

            didSetup = true;
            HideImmediate();
        }

        private void EnsureCore()
        {
            if (coreRenderer != null)
                return;

            // Child of this ripple so pooled hosts move as one unit.
            var go = new GameObject("SoftRippleCore");
            go.transform.SetParent(cachedTransform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            coreRenderer = go.AddComponent<SpriteRenderer>();
            coreTransform = go.transform;
        }

        /// <summary>
        /// Face to size/place against. Call before Play when using a shared pooled ripple.
        /// </summary>
        public void SetCardFace(SpriteRenderer faceRenderer)
        {
            cardFaceRenderer = faceRenderer;
        }

        public void Play(int sortingOrder = 500, Action onComplete = null)
        {
            SetupIfNeeded();
            DOTween.Kill(tweenId);

            if (cardFaceRenderer == null)
                CacheCardFace();

            playAlpha = 1f;
            SyncSorting(sortingOrder);
            ApplyPlayTransform();
            PlayBurst(onComplete);
        }

        public void PlayAtWorld(
            float fit,
            int sortingOrder = 500,
            Action onComplete = null,
            float alpha = 1f
        )
        {
            SetupIfNeeded();
            DOTween.Kill(tweenId);

            playAlpha = Mathf.Clamp01(alpha);
            cardFaceRenderer = null;
            playStartScale = fit * startFit;
            playEndScale = fit * endFit;
            playCoreStartScale = fit * coreStartFit;
            playCoreEndScale = fit * coreEndFit;

            PlaceOnFace(cachedTransform);
            PlaceOnFace(coreTransform);
            cachedTransform.localScale = Vector3.one * playStartScale;
            if (coreTransform != null)
                coreTransform.localScale = Vector3.one * playCoreStartScale;

            SyncSorting(sortingOrder);
            PlayBurst(onComplete);
        }

        private void PlayBurst(Action onComplete)
        {
            PlayStars();

            gameObject.SetActive(true);
            if (coreTransform != null)
                coreTransform.gameObject.SetActive(true);

            var sequence = DOTween.Sequence().SetId(tweenId);
            PlayLayer(
                sequence,
                cachedTransform,
                spriteRenderer,
                playStartScale,
                playEndScale,
                duration,
                fadeDuration
            );
            PlayLayer(
                sequence,
                coreTransform,
                coreRenderer,
                playCoreStartScale,
                playCoreEndScale,
                coreDuration,
                coreFadeDuration
            );
            sequence.OnComplete(() =>
            {
                HideImmediate();
                onComplete?.Invoke();
            });
        }

        private void ApplyLook()
        {
            ApplyLookTo(spriteRenderer);
            ApplyLookTo(coreRenderer);
        }

        /// <summary>
        /// Paints the burst magenta on this card's Stars instance only.
        /// The prefab stays lime/white; we override color here so ice-blue tables still show sparkles.
        /// </summary>
        private void ApplyStarsLook()
        {
            if (starsPulse == null)
                return;

            var main = starsPulse.main;
            main.startColor = new ParticleSystem.MinMaxGradient(starsColor);

            var colorOverLifetime = starsPulse.colorOverLifetime;
            if (colorOverLifetime.enabled)
            {
                if (starsFadeGradient == null)
                    starsFadeGradient = new Gradient();

                // White keys so startColor owns the hue. Stronger alpha so they don't vanish on a pale table.
                starsFadeGradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(Color.white, 0f),
                        new GradientColorKey(Color.white, 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(0.7f, 1f)
                    }
                );
                colorOverLifetime.color = starsFadeGradient;
            }

            if (starsRenderer == null)
                return;

            if (starsPropertyBlock == null)
                starsPropertyBlock = new MaterialPropertyBlock();

            starsRenderer.GetPropertyBlock(starsPropertyBlock);
            starsPropertyBlock.SetColor("_BaseColor", starsColor);
            starsPropertyBlock.SetColor("_Color", starsColor);
            starsRenderer.SetPropertyBlock(starsPropertyBlock);
        }

        private void ApplyLookTo(SpriteRenderer renderer)
        {
            if (renderer == null)
                return;

            var sprite = rippleSprite != null ? rippleSprite : SoftRingSprite.Get();
            renderer.sprite = sprite;

            var material = rippleMaterial != null ? rippleMaterial : GetOrCreateAdditiveMaterial();
            if (material != null)
                renderer.sharedMaterial = material;

            renderer.sortingLayerID = CardSortingLayerId;
            renderer.color = rippleColor;
        }

        private void PlayLayer(
            Sequence sequence,
            Transform layerTransform,
            SpriteRenderer renderer,
            float startScale,
            float endScale,
            float scaleTime,
            float fadeTime
        )
        {
            if (layerTransform == null || renderer == null)
                return;

            var startColor = rippleColor;
            startColor.a = playAlpha;
            var endColor = startColor;
            endColor.a = 0f;

            layerTransform.localScale = Vector3.one * startScale;
            renderer.color = startColor;
            renderer.enabled = true;

            sequence.Join(layerTransform.DOScale(endScale, scaleTime).SetEase(Ease.OutCubic));

            var hold = Mathf.Clamp01(fadeHold) * scaleTime;
            sequence.Insert(
                hold,
                renderer.DOColor(endColor, fadeTime).SetEase(Ease.InQuad)
            );
        }

        private void PlayStars()
        {
            if (starsPulse == null)
                return;

            ApplyStarsLook();
            PlaceStarsOnFace();
            SyncStarsSorting();

            if (starsTransform != null)
                starsTransform.gameObject.SetActive(true);

            starsPulse.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            starsPulse.Play(true);
        }

        private void StopStars()
        {
            if (starsPulse == null)
                return;

            starsPulse.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void Stop()
        {
            DOTween.Kill(tweenId);
            StopStars();
            HideImmediate();
        }

        private void ApplyPlayTransform()
        {
            var fit = GetFaceFit();
            playStartScale = fit * startFit;
            playEndScale = fit * endFit;
            playCoreStartScale = fit * coreStartFit;
            playCoreEndScale = fit * coreEndFit;

            PlaceOnFace(cachedTransform);
            PlaceOnFace(coreTransform);
            cachedTransform.localScale = Vector3.one * playStartScale;
            if (coreTransform != null)
                coreTransform.localScale = Vector3.one * playCoreStartScale;
        }

        private float GetFaceFit()
        {
            if (
                cardFaceRenderer == null
                || cardFaceRenderer.sprite == null
                || spriteRenderer == null
                || spriteRenderer.sprite == null
            )
            {
                return 1f;
            }

            var faceSize = cardFaceRenderer.sprite.bounds.size;
            var faceScale = cardFaceRenderer.transform.localScale;
            var target = Mathf.Min(
                faceSize.x * Mathf.Abs(faceScale.x),
                faceSize.y * Mathf.Abs(faceScale.y)
            );

            var ringSize = spriteRenderer.sprite.bounds.size;
            var ringMax = Mathf.Max(ringSize.x, ringSize.y);
            if (ringMax < 0.0001f || target < 0.0001f)
                return 1f;

            return target / ringMax;
        }

        private void PlaceOnFace(Transform layerTransform)
        {
            if (layerTransform == null)
                return;

            layerTransform.localRotation = Quaternion.identity;

            if (cardFaceRenderer == null)
            {
                layerTransform.localPosition = new Vector3(0f, 0f, -0.05f);
                return;
            }

            var facePos = cardFaceRenderer.transform.localPosition;
            layerTransform.localPosition = new Vector3(facePos.x, facePos.y, facePos.z - 0.05f);
        }

        private void PlaceStarsOnFace()
        {
            if (starsTransform == null)
                return;

            // Keep the prefab's -90 X rotation so the burst stays in the card plane.
            if (cardFaceRenderer == null)
            {
                starsTransform.localPosition = new Vector3(0f, 0f, -0.05f);
                return;
            }

            var facePos = cardFaceRenderer.transform.localPosition;
            starsTransform.localPosition = new Vector3(facePos.x, facePos.y, facePos.z - 0.05f);
        }

        private void CacheCardFace()
        {
            if (cardFaceRenderer != null)
                return;

            var card = GetComponentInParent<CardPresenter>();
            if (card == null)
                return;

            SpriteRenderer front = null;
            SpriteRenderer back = null;
            var renderers = card.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == spriteRenderer || renderer == coreRenderer)
                    continue;

                if (renderer.gameObject.name == "Front")
                    front = renderer;
                else if (renderer.gameObject.name == "Back")
                    back = renderer;
            }

            cardFaceRenderer = front != null ? front : back;
        }

        private void SyncSorting(int sortingOrder)
        {
            ApplySorting(spriteRenderer, sortingOrder);
            ApplySorting(coreRenderer, sortingOrder);
            SyncStarsSorting();
        }

        private void SyncStarsSorting()
        {
            if (starsRenderer == null)
                return;

            if (cardFaceRenderer == null)
            {
                starsRenderer.sortingLayerID = CardSortingLayerId;
                starsRenderer.sortingOrder = 2;
                return;
            }

            // Above Front and rank/suit so sparkles read, still on this card's layer.
            starsRenderer.sortingLayerID = cardFaceRenderer.sortingLayerID;
            starsRenderer.sortingOrder = cardFaceRenderer.sortingOrder + 2;
        }

        private void ApplySorting(SpriteRenderer renderer, int sortingOrder)
        {
            if (renderer == null)
                return;

            renderer.sortingLayerID = CardSortingLayerId;
            renderer.sortingOrder = sortingOrder;

            if (cardFaceRenderer == null)
                return;

            // Same as Front so Type / Suit1 / Suit2 (Front + 1) stay readable on top.
            renderer.sortingLayerID = cardFaceRenderer.sortingLayerID;
            renderer.sortingOrder = cardFaceRenderer.sortingOrder;
        }

        private void HideImmediate()
        {
            HideLayer(spriteRenderer, cachedTransform, playStartScale);
            HideLayer(coreRenderer, coreTransform, playCoreStartScale);

            gameObject.SetActive(false);
            if (coreTransform != null)
                coreTransform.gameObject.SetActive(false);
        }

        private static void HideLayer(SpriteRenderer renderer, Transform layerTransform, float startScale)
        {
            if (renderer != null)
                renderer.enabled = false;

            if (layerTransform != null)
                layerTransform.localScale = Vector3.one * startScale;
        }

        private void OnDisable()
        {
            DOTween.Kill(tweenId);
        }

        private void OnDestroy()
        {
            // Core is a child — destroyed with this object. Only clean up orphans.
            if (
                coreTransform != null
                && coreTransform.parent != cachedTransform
                && coreTransform.parent != transform
            )
            {
                Destroy(coreTransform.gameObject);
            }
        }

        private static Material GetOrCreateAdditiveMaterial()
        {
            if (sharedAdditiveMaterial != null)
                return sharedAdditiveMaterial;

            var shader = Shader.Find(AdditiveShaderName);
            if (shader == null)
            {
                if (!didWarnMissingShader)
                {
                    Debug.LogWarning(
                        "[SoftRippleConfirm] Shader Solitaire/SpriteAdditive not found — ring will not glow."
                    );
                    didWarnMissingShader = true;
                }

                return null;
            }

            sharedAdditiveMaterial = new Material(shader)
            {
                name = "SoftRippleAdditiveRuntime",
                hideFlags = HideFlags.HideAndDontSave
            };
            return sharedAdditiveMaterial;
        }

    }
}
