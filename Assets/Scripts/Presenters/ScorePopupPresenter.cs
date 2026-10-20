using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// One gold (or muted) score label. One swoop: tiny → fast 45° burst
    /// (speed and scale lerp) → slower float up. Tune on GameInstaller.
    /// </summary>
    public class ScorePopupPresenter : MonoBehaviour
    {
        private const string TweenIdPrefix = "ScorePopup";
        private const int CardSortingLayerId = 34509423;

        private TextMeshPro tmp;
        private MeshRenderer meshRenderer;
        private Material fontMat;
        private ScorePopupTune tune;
        private string tweenId;
        private bool playing;
        private Action<int> onCommitted;
        private int pendingDelta;
        private bool didCommit;

        public bool IsPlaying => playing;

        public void Configure(TMP_FontAsset font, ScorePopupTune popupTune)
        {
            if (tmp == null)
                tmp = GetComponent<TextMeshPro>();

            Debug.Assert(tmp != null, "[ScorePopupPresenter] Missing TextMeshPro.");

            tweenId = TweenIdPrefix + GetInstanceID();
            tune = popupTune != null ? popupTune : new ScorePopupTune();

            tmp.raycastTarget = false;
            tmp.richText = false;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.extraPadding = true;
            tmp.rectTransform.sizeDelta = new Vector2(8f, 3f);
            ApplyFontSize();

            if (font != null)
                tmp.font = font;

            fontMat = tmp.fontMaterial;

            meshRenderer = tmp.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sortingLayerID = CardSortingLayerId;
                meshRenderer.sortingOrder = 600;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                meshRenderer.receiveShadows = false;
            }
        }

        public void Play(
            int delta,
            Vector3 worldPosition,
            float delaySeconds,
            ScorePopupStyle style = ScorePopupStyle.Normal,
            Action<int> committed = null,
            bool homeToTarget = false,
            Vector3 homeWorldPosition = default,
            float homeDuration = 0f,
            bool commitOnPeak = true
        )
        {
            if (tmp == null)
                tmp = GetComponent<TextMeshPro>();

            if (tmp == null || delta == 0)
            {
                committed?.Invoke(delta);
                return;
            }

            if (tune == null)
                tune = new ScorePopupTune();

            Stop();
            playing = true;
            pendingDelta = delta;
            didCommit = false;
            onCommitted = committed;
            gameObject.SetActive(true);

            var positive = delta > 0;
            var isBonus = positive && style == ScorePopupStyle.Bonus;

            tmp.text = BuildText(delta, positive, isBonus);
            tmp.lineSpacing = isBonus ? tune.bonusLineSpacing : 0f;
            tmp.rectTransform.sizeDelta = isBonus ? new Vector2(10f, 5f) : new Vector2(8f, 3f);
            ApplyFontSize(isBonus);
            ApplyLook(positive, isBonus);

            // Homing pops are already placed (pill + offset). Card pops use the tune nudge.
            var start = homeToTarget
                ? worldPosition
                : worldPosition + new Vector3(tune.offsetX, tune.offsetY, 0f);
            start.z = worldPosition.z - 0.02f;
            transform.position = start;

            var scaleFrom = Mathf.Max(0.01f, tune.scaleStart);
            var scaleTo = Mathf.Max(scaleFrom, tune.scaleFloat);
            transform.localScale = new Vector3(scaleFrom, scaleFrom, 1f);
            tmp.alpha = 0f;

            var delay = Mathf.Max(0f, delaySeconds);
            var burstTime = Mathf.Max(0.04f, tune.burstDuration);
            var floatTime = Mathf.Max(0.08f, tune.floatDuration)
                * (isBonus ? Mathf.Max(0.1f, tune.bonusHoldScale) : 1f);
            var angleRad = tune.burstAngle * Mathf.Deg2Rad;
            var burstDir = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f).normalized;
            var burstDist = Mathf.Max(0f, tune.burstDistance);
            var v0 = Mathf.Max(0.01f, tune.burstSpeedStart);
            var v1 = Mathf.Max(0.01f, tune.burstSpeedEnd);
            var floatV0 = Mathf.Max(0.01f, tune.floatSpeedStart);
            var floatV1 = Mathf.Max(0.01f, tune.floatSpeedEnd);
            var rise = tune.rise * (isBonus ? Mathf.Max(0.1f, tune.bonusRiseScale) : 1f);
            var driftRight = Mathf.Max(0f, tune.floatRight);
            var visibleLife = burstTime + floatTime;
            var upOnly = Mathf.Clamp01(tune.upOnlyTail);
            var xStopT = 1f - Mathf.Clamp01(upOnly * visibleLife / floatTime);
            xStopT = Mathf.Clamp(xStopT, 0.05f, 0.95f);

            var sequence = DOTween.Sequence().SetId(tweenId);
            if (delay > 0f)
                sequence.AppendInterval(delay);

            sequence.AppendCallback(() =>
            {
                if (tmp != null)
                    tmp.alpha = 1f;
                transform.position = start;
                transform.localScale = new Vector3(scaleFrom, scaleFrom, 1f);
            });

            // Fast 45° burst: time is linear, speed and scale lerp on t.
            sequence.Append(
                DOTween
                    .To(
                        () => 0f,
                        t =>
                        {
                            var traveled = LerpTravel01(t, v0, v1);
                            transform.position = start + burstDir * (burstDist * traveled);
                            var scale = Mathf.Lerp(scaleFrom, scaleTo, t);
                            transform.localScale = new Vector3(scale, scale, 1f);
                        },
                        1f,
                        burstTime
                    )
                    .SetEase(Ease.Linear)
            );

            if (commitOnPeak)
                sequence.AppendCallback(CommitPending);

            var burstEnd = start + burstDir * burstDist;

            if (homeToTarget)
            {
                var home = homeWorldPosition;
                home.z = burstEnd.z;
                var travel = Mathf.Max(0.08f, homeDuration > 0f ? homeDuration : floatTime);
                var scaleLand = scaleTo * 0.4f;

                // Fast into the HUD so it reads as "sucked into the score".
                sequence.Append(
                    DOTween
                        .To(
                            () => 0f,
                            t =>
                            {
                                transform.position = Vector3.LerpUnclamped(burstEnd, home, t);
                                var scale = Mathf.Lerp(scaleTo, scaleLand, t);
                                transform.localScale = new Vector3(scale, scale, 1f);
                            },
                            1f,
                            travel
                        )
                        .SetEase(Ease.InCubic)
                );

                sequence.Insert(
                    delay + burstTime + travel * 0.4f,
                    DOTween
                        .To(() => tmp.alpha, a => tmp.alpha = a, 0f, travel * 0.6f)
                        .SetEase(Ease.InQuad)
                );
            }
            else
            {
                // Slow float: right + up, then the last tail of its life is up only.
                sequence.Append(
                    DOTween
                        .To(
                            () => 0f,
                            t =>
                            {
                                var traveled = LerpTravel01(t, floatV0, floatV1);
                                var y = traveled * rise;
                                float x;
                                if (t < xStopT)
                                {
                                    var xLocal = xStopT > 0.0001f ? t / xStopT : 1f;
                                    x = LerpTravel01(xLocal, floatV0, floatV1) * driftRight;
                                }
                                else
                                {
                                    x = driftRight;
                                }

                                transform.position = burstEnd + new Vector3(x, y, 0f);
                                transform.localScale = new Vector3(scaleTo, scaleTo, 1f);
                            },
                            1f,
                            floatTime
                        )
                        .SetEase(Ease.Linear)
                );

                sequence.Insert(
                    delay + burstTime + floatTime * 0.45f,
                    DOTween
                        .To(() => tmp.alpha, a => tmp.alpha = a, 0f, floatTime * 0.55f)
                        .SetEase(Ease.InQuad)
                );
            }

            sequence.OnComplete(Finish);
        }

        public void Stop()
        {
            CommitPending();
            DOTween.Kill(tweenId);
            Finish();
        }

        /// <summary>Tells the HUD this +N landed. Safe to call twice.</summary>
        private void CommitPending()
        {
            if (didCommit)
                return;

            didCommit = true;
            var delta = pendingDelta;
            var callback = onCommitted;
            pendingDelta = 0;
            onCommitted = null;
            callback?.Invoke(delta);
        }

        /// <summary>
        /// How far along a path at time t, if speed lerps from v0 to v1.
        /// Fast start + slow end means most of the distance happens early.
        /// </summary>
        private static float LerpTravel01(float t, float v0, float v1)
        {
            var denom = (v0 + v1) * 0.5f;
            if (denom < 0.0001f)
                return t;

            return (v0 * t + (v1 - v0) * t * t * 0.5f) / denom;
        }

        /// <summary>Bonus pops get a small word stacked above the number.</summary>
        private string BuildText(int delta, bool positive, bool isBonus)
        {
            if (isBonus && !string.IsNullOrEmpty(tune.bonusLabel))
                return tune.bonusLabel + "\n+" + delta;

            return positive ? "+" + delta : delta.ToString();
        }

        private void ApplyFontSize(bool isBonus = false)
        {
            if (tmp == null || tune == null)
                return;

            var size = Mathf.Max(0.1f, tune.fontSize);
            if (isBonus)
                size *= Mathf.Max(0.1f, tune.bonusFontScale);

            tmp.fontSize = size;
        }

        private void ApplyLook(bool positive, bool isBonus = false)
        {
            var top = isBonus ? tune.bonusTop : positive ? tune.goldTop : tune.muteTop;
            var bottom = isBonus ? tune.bonusBottom : positive ? tune.goldBottom : tune.muteBottom;
            var shade = isBonus ? tune.bonusShade : positive ? tune.goldShade : tune.muteShade;

            tmp.color = Color.white;
            tmp.enableVertexGradient = true;
            tmp.colorGradient = new VertexGradient(top, top, bottom, bottom);
            tmp.outlineWidth = 0f;

            if (fontMat == null)
                fontMat = tmp.fontMaterial;

            if (fontMat == null)
                return;

            fontMat.DisableKeyword("OUTLINE_ON");
            fontMat.EnableKeyword("UNDERLAY_ON");
            fontMat.SetFloat("_OutlineWidth", 0f);
            fontMat.SetFloat("_FaceDilate", Mathf.Clamp(tune.faceDilate, 0f, 0.6f));
            fontMat.SetColor("_UnderlayColor", shade);
            fontMat.SetFloat("_UnderlayOffsetX", Mathf.Clamp(tune.shadeOffsetX, -1f, 1f));
            fontMat.SetFloat("_UnderlayOffsetY", Mathf.Clamp(tune.shadeOffsetY, -1f, 1f));
            fontMat.SetFloat("_UnderlayDilate", Mathf.Clamp01(tune.shadeDilate));
            fontMat.SetFloat("_UnderlaySoftness", Mathf.Clamp01(tune.shadeSoftness));
            tmp.UpdateMeshPadding();
        }

        private void Finish()
        {
            playing = false;
            if (tmp != null)
                tmp.alpha = 0f;

            if (this == null)
                return;

            transform.localScale = Vector3.one;
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (!playing)
                return;

            CommitPending();
            DOTween.Kill(tweenId);
            playing = false;
        }
    }
}
