using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// World-space arrow that points at the card the player should use for a hint.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class HintArrowPresenter : MonoBehaviour
    {
        private const string TweenId = "HintArrow";

        [Tooltip("World-space X nudge from the card center (positive = right).")]
        [SerializeField]
        private float horizontalOffset = 0f;

        [Tooltip("World-space Y distance above the card.")]
        [SerializeField]
        private float heightOffset = 1.1f;

        [SerializeField]
        private float dropDistance = 0.35f;

        [SerializeField]
        private float bobDistance = 0.12f;

        [SerializeField]
        private float bobDuration = 0.2f;

        [SerializeField]
        private float introDuration = 0.22f;

        [SerializeField]
        private float outroDuration = 0.15f;

        [Tooltip("Extra Z rotation in degrees if your arrow art does not point down by default.")]
        [SerializeField]
        private float rotationOffsetZ = 180f;

        private SpriteRenderer spriteRenderer;
        private Transform cachedTransform;
        private Vector3 baseScale;
        private Color baseColor = Color.white;

        private void Awake()
        {
            cachedTransform = transform;
            spriteRenderer = GetComponent<SpriteRenderer>();
            baseScale = cachedTransform.localScale;

            if (spriteRenderer != null)
                baseColor = spriteRenderer.color;

            HideImmediate();
        }

        public void ShowAbove(Vector3 worldPosition)
        {
            DOTween.Kill(TweenId);

            var restPosition =
                worldPosition + new Vector3(horizontalOffset, heightOffset, 0f);
            var startPosition = restPosition + Vector3.up * dropDistance;

            cachedTransform.position = startPosition;
            cachedTransform.rotation = Quaternion.Euler(0f, 0f, rotationOffsetZ);
            cachedTransform.localScale = baseScale * 0.3f;
            SetAlpha(0f);

            if (spriteRenderer != null)
                spriteRenderer.enabled = true;

            gameObject.SetActive(true);

            var intro = DOTween.Sequence().SetId(TweenId);
            intro.Join(
                cachedTransform.DOMove(restPosition, introDuration).SetEase(Ease.OutCubic)
            );
            intro.Join(
                cachedTransform.DOScale(baseScale, introDuration).SetEase(Ease.OutBack)
            );
            intro.Join(DOTween.To(GetAlpha, SetAlpha, baseColor.a, introDuration).SetEase(Ease.OutQuad));
            intro.OnComplete(StartBob);
        }

        public async UniTask HideAsync()
        {
            if (!gameObject.activeSelf)
                return;

            DOTween.Kill(TweenId);

            var outro = DOTween.Sequence().SetId(TweenId);
            outro.Join(
                cachedTransform
                    .DOScale(baseScale * 0.3f, outroDuration)
                    .SetEase(Ease.InBack)
            );
            outro.Join(DOTween.To(GetAlpha, SetAlpha, 0f, outroDuration).SetEase(Ease.InQuad));

            await outro.AsyncWaitForCompletion();
            HideImmediate();
        }

        public void Hide()
        {
            HideAsync().Forget();
        }

        private void StartBob()
        {
            if (!gameObject.activeSelf)
                return;

            cachedTransform
                .DOMoveY(cachedTransform.position.y + bobDistance, bobDuration)
                .SetId(TweenId)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        private void HideImmediate()
        {
            DOTween.Kill(TweenId);

            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = false;
                SetAlpha(baseColor.a);
            }

            if (cachedTransform != null)
                cachedTransform.localScale = baseScale;

            gameObject.SetActive(false);
        }

        private float GetAlpha()
        {
            return spriteRenderer != null ? spriteRenderer.color.a : 1f;
        }

        private void SetAlpha(float alpha)
        {
            if (spriteRenderer == null)
                return;

            var color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }

        private void OnDisable()
        {
            DOTween.Kill(TweenId);
        }
    }
}
