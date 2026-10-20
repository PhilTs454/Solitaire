using DG.Tweening;
using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Soft breathing outline for an Ace sitting alone on a foundation.
    /// Put this on a child of Card.prefab so you can edit sprite/material/size in the Inspector (not only at runtime).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public class AceFoundationRim : MonoBehaviour
    {
        private const string TweenIdPrefix = "AceFoundationRim";
        private const int CardSortingLayerId = 34509423;

        [Header("Refs")]
        [SerializeField]
        private SpriteRenderer spriteRenderer;

        [Header("Look (set these on the Card prefab)")]
        [Tooltip("Your Gemini/Photoshop rim sprite. If empty, a procedural fallback is used at runtime only.")]
        [SerializeField]
        private Sprite rimSprite;

        [Tooltip("Optional. Leave empty to keep the SpriteRenderer material you assign in the Inspector.")]
        [SerializeField]
        private Material rimMaterial;

        [Header("Breathe (Play mode)")]
        [SerializeField]
        private float breatheDuration = 1.4f;

        [SerializeField]
        private float minAlpha = 0.22f;

        [SerializeField]
        private float maxAlpha = 0.55f;

        [Header("Size")]
        [Tooltip("If on, size follows the card Front, then applies sizeX/Y + padding.")]
        [SerializeField]
        private bool fitToCardFace = true;

        [Tooltip("Width multiplier (or local scale X if Fit To Card Face is off).")]
        [SerializeField]
        private float sizeX = 1.03f;

        [Tooltip("Height multiplier (or local scale Y if Fit To Card Face is off).")]
        [SerializeField]
        private float sizeY = 1.03f;

        [SerializeField]
        private float paddingX = 0f;

        [SerializeField]
        private float paddingY = 0f;

        [Header("Editor")]
        [Tooltip("Show the rim in Scene/Prefab view so you can tweak size without Play.")]
        [SerializeField]
        private bool previewInEditor = true;

        private static Sprite sharedRimSprite;
        private Transform cachedTransform;
        private string tweenId;
        private bool isPlayingBreath;

        private void OnEnable()
        {
            CacheRefs();
            ApplyLook();

            if (!Application.isPlaying)
            {
                RefreshEditorPreview();
                return;
            }

            // Runtime: stay hidden until Play() is called
            if (!isPlayingBreath)
                HideImmediate();
        }

        private void OnValidate()
        {
            CacheRefs();
            ApplyLook();

            if (!Application.isPlaying)
                RefreshEditorPreview();
            else if (isPlayingBreath)
                ApplySize();
        }

        private void Awake()
        {
            CacheRefs();
            if (string.IsNullOrEmpty(tweenId))
                tweenId = TweenIdPrefix + GetInstanceID();

            ApplyLook();

            if (Application.isPlaying && !isPlayingBreath)
                HideImmediate();
        }

        public void Play(int sortingOrder)
        {
            if (!Application.isPlaying)
                return;

            DOTween.Kill(tweenId);

            // Always pure white — only alpha breathes
            isPlayingBreath = true;

            ApplyLook();
            SyncSorting(sortingOrder);
            ApplySize();

            var low = Color.white;
            low.a = minAlpha;
            var high = Color.white;
            high.a = maxAlpha;

            spriteRenderer.color = low;
            spriteRenderer.enabled = true;
            gameObject.SetActive(true);

            spriteRenderer
                .DOColor(high, breatheDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetId(tweenId);
        }

        public void Stop(bool fadeOut = true)
        {
            if (!Application.isPlaying)
            {
                HideImmediate();
                return;
            }

            DOTween.Kill(tweenId);
            isPlayingBreath = false;

            if (!fadeOut || !gameObject.activeInHierarchy || spriteRenderer == null)
            {
                HideImmediate();
                return;
            }

            var fade = Color.white;
            fade.a = 0f;
            spriteRenderer
                .DOColor(fade, 0.2f)
                .SetEase(Ease.OutQuad)
                .SetId(tweenId)
                .OnComplete(HideImmediate);
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
                return;

            if (isPlayingBreath && spriteRenderer != null && spriteRenderer.enabled)
                ApplySize();
        }

        private void CacheRefs()
        {
            cachedTransform = transform;
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (string.IsNullOrEmpty(tweenId))
                tweenId = TweenIdPrefix + GetInstanceID();
        }

        private void ApplyLook()
        {
            if (spriteRenderer == null)
                return;

            // Prefer Inspector / prefab sprite
            if (rimSprite != null)
                spriteRenderer.sprite = rimSprite;
            else if (Application.isPlaying && spriteRenderer.sprite == null)
                spriteRenderer.sprite = GetOrCreateRimSprite();

            // Prefer explicit material; otherwise leave whatever is on the SpriteRenderer
            if (rimMaterial != null)
                spriteRenderer.sharedMaterial = rimMaterial;

            // Never tint RGB — sprite stays white (alpha controlled separately)
            var c = spriteRenderer.color;
            spriteRenderer.color = new Color(1f, 1f, 1f, c.a);
        }

        private void RefreshEditorPreview()
        {
            if (spriteRenderer == null)
                return;

            if (previewInEditor)
            {
                ApplySize();
                spriteRenderer.enabled = true;

                // Keep RGB white in the editor preview
                var c = Color.white;
                c.a = maxAlpha;
                spriteRenderer.color = c;
            }
            else
            {
                spriteRenderer.enabled = false;
            }
        }

        private void ApplySize()
        {
            if (cachedTransform == null)
                cachedTransform = transform;

            cachedTransform.localRotation = Quaternion.identity;

            if (!fitToCardFace)
            {
                cachedTransform.localPosition = new Vector3(0f, 0f, -0.02f);
                cachedTransform.localScale = new Vector3(sizeX, sizeY, 1f);
                return;
            }

            var face = FindCardFaceRenderer();
            if (face == null || face.sprite == null || spriteRenderer == null || spriteRenderer.sprite == null)
            {
                cachedTransform.localPosition = new Vector3(0f, 0f, -0.02f);
                cachedTransform.localScale = new Vector3(sizeX, sizeY, 1f);
                return;
            }

            var spriteSize = face.sprite.bounds.size;
            var faceScale = face.transform.localScale;
            var targetWidth = spriteSize.x * Mathf.Abs(faceScale.x) + paddingX;
            var targetHeight = spriteSize.y * Mathf.Abs(faceScale.y) + paddingY;

            var rimSize = spriteRenderer.sprite.bounds.size;
            if (rimSize.x < 0.0001f || rimSize.y < 0.0001f)
            {
                cachedTransform.localScale = new Vector3(sizeX, sizeY, 1f);
                return;
            }

            var facePos = face.transform.localPosition;
            cachedTransform.localPosition = new Vector3(facePos.x, facePos.y, facePos.z - 0.02f);
            cachedTransform.localScale = new Vector3(
                (targetWidth / rimSize.x) * sizeX,
                (targetHeight / rimSize.y) * sizeY,
                1f
            );
        }

        private SpriteRenderer FindCardFaceRenderer()
        {
            var card = GetComponentInParent<CardPresenter>();
            if (card == null)
                return null;

            SpriteRenderer front = null;
            SpriteRenderer back = null;

            foreach (var renderer in card.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer == spriteRenderer)
                    continue;

                if (renderer.gameObject.name == "Front")
                    front = renderer;
                else if (renderer.gameObject.name == "Back")
                    back = renderer;
            }

            if (front != null && front.gameObject.activeInHierarchy)
                return front;
            if (back != null && back.gameObject.activeInHierarchy)
                return back;
            return front != null ? front : back;
        }

        private void SyncSorting(int sortingOrder)
        {
            if (spriteRenderer == null)
                return;

            spriteRenderer.sortingLayerID = CardSortingLayerId;
            spriteRenderer.sortingOrder = sortingOrder;

            var face = FindCardFaceRenderer();
            if (face == null)
                return;

            spriteRenderer.sortingLayerID = face.sortingLayerID;
            spriteRenderer.sortingOrder = face.sortingOrder + 15;
        }

        private void HideImmediate()
        {
            isPlayingBreath = false;

            if (spriteRenderer != null)
            {
                var c = Color.white;
                c.a = minAlpha;
                spriteRenderer.color = c;
                spriteRenderer.enabled = false;
            }
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
                DOTween.Kill(tweenId);
        }

        private static Sprite GetOrCreateRimSprite()
        {
            if (sharedRimSprite != null)
                return sharedRimSprite;

            const int width = 128;
            const int height = 192;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "AceFoundationRim",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            const float stroke = 0.045f;
            const float radius = 0.12f;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var u = x / (width - 1f);
                    var v = y / (height - 1f);

                    var dist = SignedDistanceRoundedRect(u, v, radius);
                    var absDist = Mathf.Abs(dist);

                    float alpha;
                    if (absDist > stroke)
                        alpha = 0f;
                    else
                    {
                        var t = 1f - absDist / stroke;
                        alpha = t * t;
                    }

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply(false, false);

            sharedRimSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            sharedRimSprite.name = "AceFoundationRimSprite";
            return sharedRimSprite;
        }

        private static float SignedDistanceRoundedRect(float u, float v, float radius)
        {
            var px = Mathf.Abs(u - 0.5f);
            var py = Mathf.Abs(v - 0.5f);
            var halfW = 0.5f - radius;
            var halfH = 0.5f - radius;

            var dx = Mathf.Max(px - halfW, 0f);
            var dy = Mathf.Max(py - halfH, 0f);
            var outside = Mathf.Sqrt(dx * dx + dy * dy) - radius;

            var insideX = halfW - px;
            var insideY = halfH - py;
            var inside = Mathf.Min(insideX, insideY);

            if (px > halfW || py > halfH)
                return outside;
            return -Mathf.Min(inside, radius);
        }
    }
}
