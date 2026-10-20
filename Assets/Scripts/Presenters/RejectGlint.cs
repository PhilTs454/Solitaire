using DG.Tweening;
using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// One-shot foil bloom on an illegal move: card-shaped shine grows from the
    /// center to cover the face, then fades. Uses FoilUnlit when assigned.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class RejectGlint : MonoBehaviour
    {
        private const string TweenIdPrefix = "RejectGlint";
        private const string AdditiveShaderName = "Solitaire/SpriteAdditive";

        [Header("Tune in Play (select RejectGlint under the card)")]
        [SerializeField]
        private float bloomDuration = 0.22f;

        [SerializeField]
        private float startFit = 0.25f;

        [SerializeField]
        private float endFit = 1f;

        [SerializeField]
        private Color bloomColor = new Color(1f, 1f, 1f, 0.65f);

        [SerializeField]
        private SpriteRenderer spriteRenderer;

        [SerializeField]
        private Material foilMaterial;

        private static Material sharedAdditiveMaterial;
        private static bool didWarnMissingShader;

        private Transform cachedTransform;
        private SpriteRenderer cardFaceRenderer;
        private string tweenId;

        private void Awake()
        {
            cachedTransform = transform;
            tweenId = TweenIdPrefix + GetInstanceID();
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            HideImmediate();
        }

        public void SetFoilMaterial(Material material)
        {
            foilMaterial = material;
        }

        public void Play()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            CacheCardFace();
            if (cardFaceRenderer == null || !cardFaceRenderer.gameObject.activeSelf)
                return;

            ApplyLook();
            PlaceOnFace();
            SyncSorting();

            var fullScale = GetFaceMatchScale();
            var fromScale = fullScale * startFit;
            var toScale = fullScale * endFit;
            var duration = Mathf.Max(0.05f, bloomDuration);

            DOTween.Kill(tweenId);
            cachedTransform.localScale = fromScale;
            spriteRenderer.color = bloomColor;
            gameObject.SetActive(true);
            spriteRenderer.enabled = true;

            var sequence = DOTween.Sequence().SetId(tweenId);
            sequence.Append(
                cachedTransform.DOScale(toScale, duration).SetEase(Ease.OutCubic)
            );
            sequence.Join(
                spriteRenderer
                    .DOColor(WithAlpha(bloomColor, 0f), duration)
                    .SetEase(Ease.InQuad)
            );
            sequence.OnComplete(HideImmediate);
            sequence.OnKill(HideImmediate);
        }

        public void Stop()
        {
            DOTween.Kill(tweenId);
            HideImmediate();
        }

        private void ApplyLook()
        {
            if (cardFaceRenderer != null && cardFaceRenderer.sprite != null)
                spriteRenderer.sprite = cardFaceRenderer.sprite;

            var material = foilMaterial != null ? foilMaterial : GetOrCreateAdditiveMaterial();
            if (material != null)
                spriteRenderer.sharedMaterial = material;
        }

        private void PlaceOnFace()
        {
            cachedTransform.localRotation = Quaternion.identity;

            if (cardFaceRenderer == null)
            {
                cachedTransform.localPosition = new Vector3(0f, 0f, -0.02f);
                return;
            }

            var facePos = cardFaceRenderer.transform.localPosition;
            cachedTransform.localPosition = new Vector3(facePos.x, facePos.y, facePos.z - 0.02f);
        }

        private Vector3 GetFaceMatchScale()
        {
            if (
                cardFaceRenderer == null
                || cardFaceRenderer.sprite == null
                || spriteRenderer == null
                || spriteRenderer.sprite == null
            )
                return Vector3.one;

            var faceSize = cardFaceRenderer.sprite.bounds.size;
            var faceScale = cardFaceRenderer.transform.localScale;
            var targetWidth = faceSize.x * Mathf.Abs(faceScale.x);
            var targetHeight = faceSize.y * Mathf.Abs(faceScale.y);

            var glintSize = spriteRenderer.sprite.bounds.size;
            if (glintSize.x < 0.0001f || glintSize.y < 0.0001f)
                return Vector3.one;

            return new Vector3(targetWidth / glintSize.x, targetHeight / glintSize.y, 1f);
        }

        private void CacheCardFace()
        {
            if (cardFaceRenderer != null)
                return;

            var card = GetComponentInParent<CardPresenter>();
            if (card == null)
                return;

            var renderers = card.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == spriteRenderer)
                    continue;

                if (renderers[i].gameObject.name == "Front")
                {
                    cardFaceRenderer = renderers[i];
                    return;
                }
            }
        }

        private void SyncSorting()
        {
            if (cardFaceRenderer == null)
                return;

            spriteRenderer.sortingLayerID = cardFaceRenderer.sortingLayerID;
            spriteRenderer.sortingOrder = cardFaceRenderer.sortingOrder + 2;
        }

        private void HideImmediate()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = false;
                spriteRenderer.color = WithAlpha(bloomColor, 0f);
            }

            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            DOTween.Kill(tweenId);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
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
                        "[RejectGlint] Foil material missing and Solitaire/SpriteAdditive not found."
                    );
                    didWarnMissingShader = true;
                }

                return null;
            }

            sharedAdditiveMaterial = new Material(shader)
            {
                name = "RejectGlintAdditiveRuntime",
                hideFlags = HideFlags.HideAndDontSave
            };
            return sharedAdditiveMaterial;
        }
    }
}
