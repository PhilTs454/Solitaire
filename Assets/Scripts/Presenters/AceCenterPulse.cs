using DG.Tweening;
using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Soft candy idle: gently pulses the card center glyph (Suit2) while an Ace sits alone on foundation.
    /// </summary>
    public class AceCenterPulse : MonoBehaviour
    {
        private const string TweenIdPrefix = "AceCenterPulse";

        [SerializeField]
        private float breatheDuration = 1.15f;

        [SerializeField]
        private float scaleAmplitude = 0.08f;

        [SerializeField]
        private float minAlpha = 0.72f;

        [SerializeField]
        private float maxAlpha = 1f;

        private SpriteRenderer target;
        private Transform cachedTransform;
        private Vector3 restScale;
        private Color restColor = Color.white;
        private string tweenId;
        private bool isPlaying;

        public void Play(SpriteRenderer centerGlyph)
        {
            Stop(fadeOut: false);

            if (centerGlyph == null)
                return;

            target = centerGlyph;
            cachedTransform = centerGlyph.transform;
            restScale = cachedTransform.localScale;
            restColor = centerGlyph.color;
            tweenId = TweenIdPrefix + GetInstanceID();
            isPlaying = true;

            var sequence = DOTween.Sequence().SetId(tweenId).SetLoops(-1, LoopType.Yoyo);
            sequence.Join(
                cachedTransform
                    .DOScale(restScale * (1f + scaleAmplitude), breatheDuration)
                    .SetEase(Ease.InOutSine)
            );
            sequence.Join(
                DOTween
                    .To(GetAlpha, SetAlpha, maxAlpha, breatheDuration)
                    .From(minAlpha)
                    .SetEase(Ease.InOutSine)
            );
        }

        public void Stop(bool fadeOut = true)
        {
            if (!string.IsNullOrEmpty(tweenId))
                DOTween.Kill(tweenId);

            isPlaying = false;

            if (cachedTransform != null)
                cachedTransform.localScale = restScale;

            if (target != null)
            {
                if (fadeOut)
                {
                    var c = restColor;
                    c.a = restColor.a;
                    target.color = c;
                }
                else
                {
                    target.color = restColor;
                }
            }

            target = null;
            cachedTransform = null;
        }

        private float GetAlpha()
        {
            return target != null ? target.color.a : 1f;
        }

        private void SetAlpha(float alpha)
        {
            if (target == null)
                return;

            var color = target.color;
            color.a = alpha;
            target.color = color;
        }

        private void OnDisable()
        {
            if (isPlaying)
                Stop(fadeOut: false);
        }
    }
}
