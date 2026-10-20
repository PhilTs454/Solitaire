using DG.Tweening;
using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Soft card-shaped glow under a lonely foundation Ace.
    /// Same sprite as the face, slightly larger; an opaque occluder hides the middle
    /// so the glow only peeks around the edge (does not wash the Ace).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class AceHomeGlow : MonoBehaviour
    {
        private const string TweenIdPrefix = "AceHomeGlow";
        private const int CardSortingLayerId = 34509423;
        private const string AdditiveShaderName = "Solitaire/SpriteAdditive";

        [Header("Tune in Play (select AceHomeGlow under the Ace)")]
        [Tooltip("Size vs the card. 1.1 = original-ish halo.")]
        [SerializeField]
        private float outerScale = 1.1f;

        [SerializeField]
        private Color glowColor = new Color(1f, 0.82f, 1f, 0.45f);

        [SerializeField]
        private float fadeDuration = 0.2f;

        [SerializeField]
        private SpriteRenderer spriteRenderer;

        [SerializeField]
        private Material glowMaterial;

        private static Material sharedAdditiveMaterial;
        private static Material sharedOccluderMaterial;
        private static bool didWarnMissingShader;

        private Transform cachedTransform;
        private Transform occluderTransform;
        private SpriteRenderer occluderRenderer;
        private SpriteRenderer cardFaceRenderer;
        private string tweenId;
        private bool isShowing;

        private void Awake()
        {
            cachedTransform = transform;
            tweenId = TweenIdPrefix + GetInstanceID();
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            EnsureOccluder();
            HideImmediate();
        }

        public void SetGlowMaterial(Material material)
        {
            glowMaterial = material;
        }

        public void Play()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            CacheCardFace();
            EnsureOccluder();
            ApplyLook();
            ApplySize();
            SyncSorting();

            DOTween.Kill(tweenId);
            isShowing = true;
            gameObject.SetActive(true);
            spriteRenderer.enabled = true;
            if (occluderRenderer != null)
            {
                occluderRenderer.enabled = true;
                occluderTransform.gameObject.SetActive(true);
            }

            var start = glowColor;
            start.a = 0f;
            spriteRenderer.color = start;
            spriteRenderer.DOColor(glowColor, fadeDuration).SetEase(Ease.OutQuad).SetId(tweenId);
        }

        public void Stop(bool fadeOut = true)
        {
            DOTween.Kill(tweenId);

            if (!fadeOut || !isShowing || !gameObject.activeInHierarchy || spriteRenderer == null)
            {
                HideImmediate();
                return;
            }

            var fade = glowColor;
            fade.a = 0f;
            spriteRenderer
                .DOColor(fade, fadeDuration)
                .SetEase(Ease.InQuad)
                .SetId(tweenId)
                .OnComplete(HideImmediate);
        }

        private void LateUpdate()
        {
            if (!isShowing)
                return;

            ApplyLook();
            ApplySize();
            SyncSorting();

            if (spriteRenderer != null && !DOTween.IsTweening(tweenId))
                spriteRenderer.color = glowColor;
        }

        private void EnsureOccluder()
        {
            if (occluderRenderer != null)
                return;

            var go = new GameObject("GlowOccluder");
            go.transform.SetParent(cachedTransform != null ? cachedTransform : transform, false);
            occluderTransform = go.transform;
            occluderRenderer = go.AddComponent<SpriteRenderer>();
            occluderRenderer.sharedMaterial = GetOrCreateOccluderMaterial();
            occluderRenderer.color = Color.white;
        }

        private void ApplyLook()
        {
            if (spriteRenderer == null)
                return;

            var faceSprite = cardFaceRenderer != null ? cardFaceRenderer.sprite : null;
            if (faceSprite != null)
                spriteRenderer.sprite = faceSprite;

            var material = glowMaterial != null ? glowMaterial : GetOrCreateAdditiveMaterial();
            if (material != null)
                spriteRenderer.sharedMaterial = material;

            if (occluderRenderer != null && faceSprite != null)
            {
                occluderRenderer.sprite = faceSprite;
                occluderRenderer.sharedMaterial = GetOrCreateOccluderMaterial();
                occluderRenderer.color = Color.white;
            }
        }

        private void ApplySize()
        {
            if (cachedTransform == null)
                cachedTransform = transform;

            cachedTransform.localRotation = Quaternion.identity;

            if (
                cardFaceRenderer == null
                || cardFaceRenderer.sprite == null
                || spriteRenderer == null
                || spriteRenderer.sprite == null
            )
            {
                cachedTransform.localPosition = new Vector3(0f, 0f, 0.02f);
                cachedTransform.localScale = Vector3.one * outerScale;
                return;
            }

            var faceSize = cardFaceRenderer.sprite.bounds.size;
            var faceScale = cardFaceRenderer.transform.localScale;
            var targetWidth = faceSize.x * Mathf.Abs(faceScale.x);
            var targetHeight = faceSize.y * Mathf.Abs(faceScale.y);

            var glowSize = spriteRenderer.sprite.bounds.size;
            if (glowSize.x < 0.0001f || glowSize.y < 0.0001f)
            {
                cachedTransform.localScale = Vector3.one * outerScale;
                return;
            }

            var facePos = cardFaceRenderer.transform.localPosition;
            cachedTransform.localPosition = new Vector3(facePos.x, facePos.y, facePos.z + 0.03f);
            cachedTransform.localScale = new Vector3(
                (targetWidth / glowSize.x) * outerScale,
                (targetHeight / glowSize.y) * outerScale,
                1f
            );

            if (occluderTransform == null)
                return;

            // Occluder is a child, so undo the parent's outerScale and match the face exactly.
            occluderTransform.localRotation = Quaternion.identity;
            occluderTransform.localPosition = Vector3.zero;
            occluderTransform.localScale = new Vector3(
                1f / outerScale,
                1f / outerScale,
                1f
            );
        }

        private void CacheCardFace()
        {
            var card = GetComponentInParent<CardPresenter>();
            if (card == null)
                return;

            SpriteRenderer front = null;
            SpriteRenderer back = null;
            var renderers = card.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == spriteRenderer || renderer == occluderRenderer)
                    continue;

                if (renderer.gameObject.name == "Front")
                    front = renderer;
                else if (renderer.gameObject.name == "Back")
                    back = renderer;
            }

            cardFaceRenderer = front != null ? front : back;
        }

        private void SyncSorting()
        {
            if (spriteRenderer == null)
                return;

            spriteRenderer.sortingLayerID = CardSortingLayerId;
            spriteRenderer.sortingOrder = 0;

            if (cardFaceRenderer == null)
                return;

            var layer = cardFaceRenderer.sortingLayerID;
            var faceOrder = cardFaceRenderer.sortingOrder;

            // Glow under occluder under Ace face.
            spriteRenderer.sortingLayerID = layer;
            spriteRenderer.sortingOrder = faceOrder - 2;

            if (occluderRenderer == null)
                return;

            occluderRenderer.sortingLayerID = layer;
            occluderRenderer.sortingOrder = faceOrder - 1;
        }

        private void HideImmediate()
        {
            isShowing = false;
            DOTween.Kill(tweenId);

            if (spriteRenderer != null)
            {
                var c = glowColor;
                c.a = 0f;
                spriteRenderer.color = c;
                spriteRenderer.enabled = false;
            }

            if (occluderRenderer != null)
                occluderRenderer.enabled = false;

            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            DOTween.Kill(tweenId);
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
                        "[AceHomeGlow] Shader Solitaire/SpriteAdditive not found — home glow will not add."
                    );
                    didWarnMissingShader = true;
                }

                return null;
            }

            sharedAdditiveMaterial = new Material(shader)
            {
                name = "AceHomeGlowAdditiveRuntime",
                hideFlags = HideFlags.HideAndDontSave
            };
            return sharedAdditiveMaterial;
        }

        private static Material GetOrCreateOccluderMaterial()
        {
            if (sharedOccluderMaterial != null)
                return sharedOccluderMaterial;

            var shader = Shader.Find("Solitaire/SpriteOpaque");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return null;

            sharedOccluderMaterial = new Material(shader)
            {
                name = "AceHomeGlowOccluderRuntime",
                hideFlags = HideFlags.HideAndDontSave
            };
            return sharedOccluderMaterial;
        }
    }
}
