using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Gummy hover/press on a UI button. Uses pointer events (works with the New Input System UI module).
    /// </summary>
    public class BlobButton : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        private const string TweenIdPrefix = "BlobButton";

        [SerializeField]
        private float hoverScale = 1.06f;

        [Tooltip("X scale of the quick squash on hover.")]
        [SerializeField]
        private float squashX = 1.1f;

        [SerializeField]
        private float squashY = 0.94f;

        [SerializeField]
        private float pressScale = 0.94f;

        [SerializeField]
        private float popDuration = 0.08f;

        [SerializeField]
        private float settleDuration = 0.14f;

        [Header("Appear (deal-end pop)")]
        [Tooltip("Wide squash when the button fades in. Same idea as a stock press.")]
        [SerializeField]
        private float appearSquashX = 1.08f;

        [Tooltip("Short squash when the button fades in.")]
        [SerializeField]
        private float appearSquashY = 0.86f;

        [SerializeField]
        private float appearPopDuration = 0.07f;

        [SerializeField]
        private float appearSettleDuration = 0.16f;

        private RectTransform rect;
        private Button button;
        private Vector3 restScale = Vector3.one;
        private bool hovered;
        private bool pressed;

        private string TweenId => TweenIdPrefix + GetInstanceID();

        private void Awake()
        {
            rect = transform as RectTransform;
            button = GetComponent<Button>();
            restScale = rect != null ? rect.localScale : Vector3.one;
        }

        private void OnDisable()
        {
            hovered = false;
            pressed = false;
            DOTween.Kill(TweenId);
            if (rect != null)
                rect.localScale = restScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!CanAnimate())
                return;

            hovered = true;
            if (pressed)
                return;

            PlayHoverPop();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            if (pressed || rect == null)
                return;

            TweenTo(restScale, settleDuration, Ease.OutQuad);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!CanAnimate() || rect == null)
                return;

            pressed = true;
            TweenTo(restScale * pressScale, popDuration, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
            if (rect == null)
                return;

            if (hovered && CanAnimate())
            {
                TweenTo(restScale * hoverScale, settleDuration, Ease.OutBack);
                return;
            }

            TweenTo(restScale, settleDuration, Ease.OutQuad);
        }

        /// <summary>
        /// Squash wide/short, then spring back to rest. Used when a dim button becomes live.
        /// </summary>
        public void PlayAppearPop(float delay = 0f)
        {
            if (rect == null)
                return;

            DOTween.Kill(TweenId);
            rect.localScale = restScale;
            var squash = new Vector3(restScale.x * appearSquashX, restScale.y * appearSquashY, 1f);
            var pop = DOTween.Sequence().SetId(TweenId).SetTarget(rect);
            if (delay > 0f)
                pop.AppendInterval(delay);
            pop.Append(
                rect.DOScale(squash, Mathf.Max(0.04f, appearPopDuration)).SetEase(Ease.OutQuad)
            );
            pop.Append(
                rect.DOScale(restScale, Mathf.Max(0.06f, appearSettleDuration)).SetEase(Ease.OutBack)
            );
        }

        private void PlayHoverPop()
        {
            if (rect == null)
                return;

            DOTween.Kill(TweenId);
            var squash = new Vector3(restScale.x * squashX, restScale.y * squashY, 1f);
            var hover = restScale * hoverScale;
            var pop = DOTween.Sequence().SetId(TweenId).SetTarget(rect);
            pop.Append(rect.DOScale(squash, popDuration).SetEase(Ease.OutQuad));
            pop.Append(rect.DOScale(hover, settleDuration).SetEase(Ease.OutBack));
        }

        private void TweenTo(Vector3 scale, float duration, Ease ease)
        {
            DOTween.Kill(TweenId);
            rect.DOScale(scale, Mathf.Max(0.04f, duration)).SetEase(ease).SetId(TweenId).SetTarget(rect);
        }

        private bool CanAnimate()
        {
            return button == null || button.interactable;
        }
    }
}
