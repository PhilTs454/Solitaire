using DG.Tweening;
using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Slides a HUD widget to the nearest screen edge and fades it out.
    /// Added at runtime to direct children of the game HUD canvases.
    /// </summary>
    public sealed class HudTuck : MonoBehaviour
    {
        public const float Duration = 0.3f;
        private const float EdgePadding = 64f;
        private const string TweenIdPrefix = "HudTuck";

        private RectTransform rect;
        private CanvasGroup canvasGroup;
        private Vector2 restAnchored;
        private float restAlpha = 1f;
        private bool captured;
        private bool tucked;
        private string tweenId;

        private void Awake()
        {
            rect = transform as RectTransform;
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            tweenId = TweenIdPrefix + GetInstanceID();
        }

        public void Tuck(float delay)
        {
            EnsureReady();
            if (rect == null || tucked)
                return;

            if (!captured)
                CaptureRest();

            tucked = true;
            KillTween();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            var target = restAnchored + EdgeDelta();
            var sequence = DOTween.Sequence().SetId(tweenId).SetUpdate(true);
            if (delay > 0f)
                sequence.AppendInterval(delay);
            sequence.Append(rect.DOAnchorPos(target, Duration).SetEase(Ease.InCubic));
            sequence.Join(canvasGroup.DOFade(0f, Duration));
        }

        public void Restore(bool instant)
        {
            EnsureReady();
            if (rect == null || !captured)
                return;

            tucked = false;
            KillTween();
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;

            if (instant)
            {
                rect.anchoredPosition = restAnchored;
                canvasGroup.alpha = restAlpha;
                return;
            }

            rect.DOAnchorPos(restAnchored, Duration).SetEase(Ease.OutCubic).SetId(tweenId);
            canvasGroup.DOFade(restAlpha, Duration).SetId(tweenId);
        }

        private void CaptureRest()
        {
            restAnchored = rect.anchoredPosition;
            restAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
            captured = true;
        }

        private Vector2 EdgeDelta()
        {
            var size = rect.rect.size;
            var edge = NearestEdge();
            switch (edge)
            {
                case RectTransform.Edge.Left:
                    return new Vector2(-(size.x + EdgePadding), 0f);
                case RectTransform.Edge.Right:
                    return new Vector2(size.x + EdgePadding, 0f);
                case RectTransform.Edge.Bottom:
                    return new Vector2(0f, -(size.y + EdgePadding));
                default:
                    return new Vector2(0f, size.y + EdgePadding);
            }
        }

        private RectTransform.Edge NearestEdge()
        {
            var canvas = GetComponentInParent<Canvas>();
            var cam =
                canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;
            var screen = RectTransformUtility.WorldToScreenPoint(cam, rect.position);

            var left = screen.x;
            var right = Screen.width - screen.x;
            var bottom = screen.y;
            var top = Screen.height - screen.y;

            var min = left;
            var edge = RectTransform.Edge.Left;
            if (right < min)
            {
                min = right;
                edge = RectTransform.Edge.Right;
            }

            if (bottom < min)
            {
                min = bottom;
                edge = RectTransform.Edge.Bottom;
            }

            if (top < min)
                edge = RectTransform.Edge.Top;

            return edge;
        }

        private void EnsureReady()
        {
            if (rect == null)
                rect = transform as RectTransform;
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            if (string.IsNullOrEmpty(tweenId))
                tweenId = TweenIdPrefix + GetInstanceID();
        }

        private void KillTween()
        {
            DOTween.Kill(tweenId);
        }

        private void OnDestroy()
        {
            KillTween();
        }
    }
}
