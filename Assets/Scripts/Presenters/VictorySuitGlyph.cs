using System;
using DG.Tweening;
using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Suit pip that flies from a foundation into a crest socket or the 3D bubble.
    /// Pooled.
    /// </summary>
    public sealed class VictorySuitGlyph : MonoBehaviour
    {
        public const float CrestDuration = 0.5f;

        private const string TweenIdPrefix = "VictoryGlyph";
        private const float SphereLocalRadius = 0.48f;

        private SpriteRenderer spriteRenderer;
        private GlyphSparkTrail sparkTrail;
        private Transform target;
        private Transform sphere;
        private Vector3 localDir = Vector3.up;
        private string tweenId;
        private Vector3 origin;
        private Vector3 startScale;
        private Vector3 landScale;
        private Vector3 hitPoint;
        private float approachDuration = 0.32f;
        private float absorbDuration = 0.55f;
        private float arcLiftFactor = 0.1f;
        private float arcSide;
        private float tweenT;
        private bool intoBubble;
        private Action onSurfaceHit;
        private Action onComplete;

        private void Awake()
        {
            EnsureBuilt();
        }

        public void Play(
            Sprite sprite,
            bool isRedSuit,
            Vector3 from,
            Transform slot,
            Vector3 worldLandScale,
            int sortingOrder,
            Action complete
        )
        {
            Begin(
                sprite,
                isRedSuit,
                from,
                worldLandScale,
                sortingOrder,
                complete,
                null,
                1.65f
            );

            intoBubble = false;
            target = slot;
            sphere = null;

            DOVirtual
                .Float(0f, 1f, CrestDuration, ApplyCrest)
                .SetEase(Ease.OutCubic)
                .SetId(tweenId)
                .OnComplete(Finish);
        }

        public void PlayIntoBubble(
            Sprite sprite,
            bool isRedSuit,
            Vector3 from,
            Transform bubbleSphere,
            Vector3 sphereLocalDir,
            Vector3 worldLandScale,
            int sortingOrder,
            float approach,
            float absorb,
            float arcLift,
            float startMul,
            float side,
            Action surfaceHit,
            Action complete
        )
        {
            approachDuration = Mathf.Max(0.08f, approach);
            absorbDuration = Mathf.Max(0.08f, absorb);
            arcLiftFactor = Mathf.Max(0f, arcLift);
            arcSide = side;

            Begin(
                sprite,
                isRedSuit,
                from,
                worldLandScale,
                sortingOrder,
                complete,
                surfaceHit,
                startMul
            );

            intoBubble = true;
            target = null;
            sphere = bubbleSphere;
            localDir = sphereLocalDir.sqrMagnitude < 0.0001f ? Vector3.up : sphereLocalDir.normalized;
            hitPoint = SurfacePoint();
            tweenT = 0f;

            var seq = DOTween.Sequence().SetId(tweenId);
            seq.Append(
                DOTween
                    .To(() => tweenT, ApplyApproach, 1f, approachDuration)
                    .SetEase(Ease.InQuart)
            );
            seq.AppendCallback(HitSurface);
            seq.Append(
                DOTween
                    .To(() => tweenT, ApplyAbsorb, 1f, absorbDuration)
                    .SetEase(Ease.OutSine)
            );
            seq.OnComplete(Finish);
        }

        public void Stop()
        {
            KillTween();
            sparkTrail?.ClearAndReset();
            onComplete = null;
            onSurfaceHit = null;
            target = null;
            sphere = null;
            intoBubble = false;
            if (spriteRenderer != null)
                spriteRenderer.enabled = false;
            gameObject.SetActive(false);
        }

        private void Begin(
            Sprite sprite,
            bool isRedSuit,
            Vector3 from,
            Vector3 worldLandScale,
            int sortingOrder,
            Action complete,
            Action surfaceHit,
            float startMul
        )
        {
            EnsureBuilt();
            KillTween();

            onComplete = complete;
            onSurfaceHit = surfaceHit;
            origin = from;
            landScale = worldLandScale.sqrMagnitude < 0.0001f ? Vector3.one : worldLandScale;
            startScale = landScale * Mathf.Max(0.5f, startMul);

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = sprite;
                spriteRenderer.color = Color.white;
                spriteRenderer.sortingOrder = sortingOrder;
                spriteRenderer.enabled = true;
            }

            transform.position = origin;
            transform.rotation = Quaternion.identity;
            transform.localScale = startScale;
            gameObject.SetActive(true);
            sparkTrail?.Play(sortingOrder - 2);
        }

        private void ApplyCrest(float t)
        {
            var dest = target != null ? target.position : origin;
            transform.position = Vector3.Lerp(origin, dest, t);
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.Lerp(startScale, landScale, t);

            if (spriteRenderer != null && t > 0.72f)
            {
                var fade = 1f - Mathf.InverseLerp(0.72f, 1f, t);
                SetAlpha(fade);
            }

            if (t > 0.9f)
                sparkTrail?.StopEmitting();
        }

        private void ApplyApproach(float t)
        {
            tweenT = t;
            var dest = SurfacePoint();
            transform.position = ArcPoint(t, dest);
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.Lerp(startScale, landScale, t);
            SetAlpha(1f);
        }

        private void HitSurface()
        {
            tweenT = 0f;
            hitPoint = SurfacePoint();
            transform.position = hitPoint;
            transform.localScale = landScale;
            sparkTrail?.StopEmitting();
            var hit = onSurfaceHit;
            onSurfaceHit = null;
            hit?.Invoke();
        }

        private void ApplyAbsorb(float t)
        {
            tweenT = t;
            var center = sphere != null ? sphere.position : hitPoint;
            transform.position = Vector3.Lerp(hitPoint, center, t);
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.Lerp(landScale, Vector3.zero, t);
            var fade = t < 0.35f ? 1f : 1f - Mathf.InverseLerp(0.35f, 1f, t);
            SetAlpha(fade);
        }

        private void Finish()
        {
            sparkTrail?.StopEmitting();
            if (intoBubble)
            {
                var center = sphere != null ? sphere.position : hitPoint;
                transform.position = center;
                transform.localScale = Vector3.zero;
                SetAlpha(0f);
            }
            else if (target != null)
            {
                transform.position = target.position;
                transform.localScale = landScale;
            }

            transform.rotation = Quaternion.identity;
            var complete = onComplete;
            onComplete = null;
            complete?.Invoke();
        }

        private Vector3 SurfacePoint()
        {
            if (sphere == null)
                return origin;

            return sphere.TransformPoint(FrontLocalDir() * SphereLocalRadius);
        }

        private Vector3 FrontLocalDir()
        {
            var cam = Camera.main;
            if (cam != null && sphere != null)
            {
                var toCam = cam.transform.position - sphere.position;
                if (toCam.sqrMagnitude > 0.0001f)
                    return sphere.InverseTransformDirection(toCam.normalized);
            }

            return localDir.sqrMagnitude < 0.0001f ? Vector3.back : localDir.normalized;
        }

        private Vector3 ArcPoint(float t, Vector3 dest)
        {
            var mid = Vector3.Lerp(origin, dest, 0.55f);
            var distance = Vector3.Distance(origin, dest);
            var lift = Vector3.up * (distance * arcLiftFactor);
            lift += Vector3.right * (distance * arcSide);
            var a = Vector3.Lerp(origin, mid + lift, t);
            var b = Vector3.Lerp(mid + lift, dest, t);
            return Vector3.Lerp(a, b, t);
        }

        private void SetAlpha(float alpha)
        {
            if (spriteRenderer == null)
                return;

            var c = spriteRenderer.color;
            c.a = Mathf.Clamp01(alpha);
            spriteRenderer.color = c;
        }

        private void EnsureBuilt()
        {
            if (string.IsNullOrEmpty(tweenId))
                tweenId = TweenIdPrefix + GetInstanceID();

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                    spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            if (sparkTrail != null)
                return;

            var sparkGo = new GameObject("Sparks", typeof(ParticleSystem));
            sparkGo.transform.SetParent(transform, false);
            sparkGo.transform.localPosition = new Vector3(0f, 0f, -0.08f);
            sparkGo.transform.localScale = Vector3.one;
            sparkTrail = sparkGo.AddComponent<GlyphSparkTrail>();
        }

        private void KillTween()
        {
            DOTween.Kill(tweenId);
        }

        private void OnDisable()
        {
            KillTween();
        }
    }
}
