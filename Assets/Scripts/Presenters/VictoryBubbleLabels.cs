using System;
using System.Collections.Generic;
using DG.Tweening;
using Solitaire.Helpers;
using TMPro;
using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// World-space Baron Neue copy glued to the victory bubble:
    /// VICTORY spelled letter-by-letter above, juicy score below.
    /// </summary>
    public sealed class VictoryBubbleLabels : MonoBehaviour
    {
        private const string DefaultTitleWord = "DECK COMPLETE!";
        private const string TitleTweenId = "VictoryBubbleTitle";
        private const string ScoreTweenId = "VictoryBubbleScore";
        private const string DimTweenId = "VictoryBubbleDim";
        private const string HintTweenId = "VictoryBubbleHint";
        private const string ContinueHint = "tap anywhere to collect";
        private const int CardSortingLayerId = 34509423;
        private const float LetterStartScale = 0.42f;
        private const float LetterPeakScale = 1.3f;
        private const float LetterPeakTime = 0.056f;
        private const float LetterSettleTime = 0.077f;
        private const float LetterStartAlpha = 0.5f;
        private const float TitleArcHeightOverWidth = 0.08f;

        private Transform sphere;
        private Transform titleRoot;
        private Transform scoreRoot;
        private TextMeshPro hint;
        private SpriteRenderer dimmer;
        private ScorePopupTune tune;
        private TMP_FontAsset font;
        private int sortingOrder;
        private string titleWord = DefaultTitleWord;
        private float titleSize = 8.8f;
        private float scoreSize = 5.4f;
        private float titleClearance = 0.7f;
        private float scoreClearance = 0.55f;
        private bool follow;
        private readonly List<TextMeshPro> letters = new List<TextMeshPro>(8);
        private readonly List<TextMeshPro> scoreLetters = new List<TextMeshPro>(10);

        public void Configure(
            Transform bubbleSphere,
            TMP_FontAsset fontAsset,
            ScorePopupTune popupTune,
            int order,
            float titleFontSize,
            float scoreFontSize,
            float titleGap,
            float scoreGap,
            string bannerWord
        )
        {
            sphere = bubbleSphere;
            font = fontAsset;
            tune = popupTune != null ? popupTune : new ScorePopupTune();
            sortingOrder = order;
            titleWord = string.IsNullOrWhiteSpace(bannerWord) ? DefaultTitleWord : bannerWord;
            titleSize = Mathf.Max(0.4f, titleFontSize);
            scoreSize = Mathf.Max(0.4f, scoreFontSize);
            titleClearance = Mathf.Max(0.05f, titleGap);
            scoreClearance = Mathf.Max(0.05f, scoreGap);

            titleRoot = EnsureRoot("VictoryTitle");
            scoreRoot = EnsureRoot("VictoryScore");
            hint = EnsureLabel("VictoryHint", ContinueHint, 1.15f, sortingOrder + 48);
            ApplyHintLook(hint);
            EnsureDimmer();
            if (
                !titleRoot.gameObject.activeSelf
                && !scoreRoot.gameObject.activeSelf
            )
                HideImmediate();
        }

        public void ShowTitle(Action onComplete = null)
        {
            follow = true;
            Place();
            BuildLetters();
            AnimateLetters(onComplete);
        }

        public void PlayScore(int value, Action onComplete = null)
        {
            follow = true;
            Place();
            DOTween.Kill(ScoreTweenId);
            BuildScoreLetters(Mathf.Max(0, value).ToString("N0"), hidden: true);
            AnimateGlyphs(scoreLetters, ScoreTweenId, onComplete);
        }

        public void SnapScore(int value)
        {
            follow = true;
            Place();
            DOTween.Kill(ScoreTweenId);
            BuildScoreLetters(Mathf.Max(0, value).ToString("N0"), hidden: false);
        }

        public void ShowContinueHint()
        {
            follow = true;
            Place();
            if (hint == null)
                return;

            DOTween.Kill(HintTweenId);
            hint.text = ContinueHint;
            hint.alpha = 0f;
            hint.transform.localScale = Vector3.one * 0.86f;
            hint.gameObject.SetActive(true);
            var fade = DOTween.Sequence().SetId(HintTweenId);
            fade.Join(hint.DOFade(0.92f, 0.28f).SetEase(Ease.OutQuad));
            fade.Join(hint.transform.DOScale(1f, 0.28f).SetEase(Ease.OutQuad));
        }

        public void ShowBackgroundDim()
        {
            follow = true;
            EnsureDimmer();
            if (dimmer == null)
                return;

            dimmer.gameObject.SetActive(true);
            var color = dimmer.color;
            color.a = 0f;
            dimmer.color = color;
            DOTween.Kill(DimTweenId);
            dimmer.DOFade(0.21f, 0.28f).SetEase(Ease.OutQuad).SetId(DimTweenId);
        }

        public void Hide()
        {
            follow = false;
            DOTween.Kill(TitleTweenId);
            DOTween.Kill(ScoreTweenId);
            DOTween.Kill(DimTweenId);
            DOTween.Kill(HintTweenId);
            HideImmediate();
        }

        private void LateUpdate()
        {
            if (follow)
                Place();
        }

        private void AnimateLetters(Action onComplete)
        {
            AnimateGlyphs(letters, TitleTweenId, onComplete);
        }

        private void AnimateGlyphs(List<TextMeshPro> glyphs, string tweenId, Action onComplete)
        {
            DOTween.Kill(tweenId);
            if (glyphs == null || glyphs.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            var seq = DOTween.Sequence().SetId(tweenId);
            for (var i = 0; i < glyphs.Count; i++)
            {
                var letter = glyphs[i];
                var startAt = i * LetterPeakTime;
                seq.InsertCallback(startAt, () => PlayLetter(letter, tweenId));
            }

            var lastDone = (glyphs.Count - 1) * LetterPeakTime + LetterPeakTime + LetterSettleTime;
            seq.InsertCallback(lastDone, () => onComplete?.Invoke());
        }

        private static void PlayLetter(TextMeshPro letter, string tweenId)
        {
            if (letter == null)
                return;

            letter.alpha = LetterStartAlpha;
            letter.transform.localScale = Vector3.one * LetterStartScale;
            letter.gameObject.SetActive(true);

            var restRot = letter.transform.localRotation;
            letter.transform.localRotation =
                restRot * Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-10f, 10f));

            var pop = DOTween.Sequence();
            pop.Append(letter.transform.DOScale(LetterPeakScale, LetterPeakTime).SetEase(Ease.OutCubic));
            pop.Join(letter.DOFade(1f, LetterPeakTime).SetEase(Ease.OutQuad));
            pop.Join(letter.transform.DOLocalRotateQuaternion(restRot, LetterPeakTime).SetEase(Ease.OutCubic));
            pop.Append(
                letter.transform.DOScale(1f, LetterSettleTime).SetEase(Ease.OutBack, 2.6f)
            );
            pop.SetId(tweenId);
        }

        private void BuildLetters()
        {
            for (var i = 0; i < letters.Count; i++)
            {
                if (letters[i] != null)
                    letters[i].gameObject.SetActive(false);
            }

            letters.Clear();
            titleRoot.gameObject.SetActive(true);

            var widths = new float[titleWord.Length];
            var total = 0f;
            var tracking = titleSize * 0.018f;
            for (var i = 0; i < titleWord.Length; i++)
            {
                var glyph = titleWord[i];
                var letter = EnsureLetter(i, glyph.ToString());
                letter.ForceMeshUpdate();
                widths[i] = glyph == ' '
                    ? titleSize * 0.055f
                    : Mathf.Max(0.05f, letter.GetPreferredValues().x);
                total += widths[i];
                if (i < titleWord.Length - 1)
                    total += tracking;
                letters.Add(letter);
            }

            var xs = new float[letters.Count];
            var x = -total * 0.5f;
            for (var i = 0; i < letters.Count; i++)
            {
                x += widths[i] * 0.5f;
                xs[i] = x;
                x += widths[i] * 0.5f + tracking;
            }

            var half = Mathf.Max(0.01f, total * 0.5f);
            var arcHeight = Mathf.Max(0.06f, total * TitleArcHeightOverWidth);
            var radius = (half * half) / (2f * arcHeight) + arcHeight * 0.5f;
            var circleY = arcHeight - radius;

            for (var i = 0; i < letters.Count; i++)
            {
                var lx = xs[i];
                var ly = circleY + Mathf.Sqrt(Mathf.Max(0f, radius * radius - lx * lx));
                letters[i].transform.localPosition = new Vector3(lx, ly, 0f);

                var radial = new Vector3(lx, ly - circleY, 0f);
                letters[i].transform.localRotation =
                    radial.sqrMagnitude < 0.0001f
                        ? Quaternion.identity
                        : Quaternion.FromToRotation(Vector3.up, radial.normalized);

                letters[i].transform.localScale = Vector3.one * LetterStartScale;
                letters[i].alpha = 0f;
                letters[i].gameObject.SetActive(false);
            }
        }

        private void BuildScoreLetters(string text, bool hidden)
        {
            for (var i = 0; i < scoreLetters.Count; i++)
            {
                if (scoreLetters[i] != null)
                    scoreLetters[i].gameObject.SetActive(false);
            }

            scoreLetters.Clear();
            if (scoreRoot == null || string.IsNullOrEmpty(text))
                return;

            scoreRoot.gameObject.SetActive(true);

            var widths = new float[text.Length];
            var total = 0f;
            var tracking = scoreSize * 0.018f;
            for (var i = 0; i < text.Length; i++)
            {
                var letter = EnsureScoreLetter(i, text[i].ToString());
                letter.ForceMeshUpdate();
                widths[i] = Mathf.Max(0.04f, letter.GetPreferredValues().x);
                total += widths[i];
                if (i < text.Length - 1)
                    total += tracking;
                scoreLetters.Add(letter);
            }

            var x = -total * 0.5f;
            for (var i = 0; i < scoreLetters.Count; i++)
            {
                x += widths[i] * 0.5f;
                scoreLetters[i].transform.localPosition = new Vector3(x, 0f, 0f);
                scoreLetters[i].transform.localRotation = Quaternion.identity;
                if (hidden)
                {
                    scoreLetters[i].transform.localScale = Vector3.one * LetterStartScale;
                    scoreLetters[i].alpha = 0f;
                    scoreLetters[i].gameObject.SetActive(false);
                }
                else
                {
                    scoreLetters[i].transform.localScale = Vector3.one;
                    scoreLetters[i].alpha = 1f;
                    scoreLetters[i].gameObject.SetActive(true);
                }

                x += widths[i] * 0.5f + tracking;
            }
        }

        private void Place()
        {
            if (sphere == null)
                return;

            var parent = transform.parent;
            if (parent != null)
            {
                var s = parent.lossyScale;
                transform.localScale = new Vector3(
                    1f / Mathf.Max(0.001f, s.x),
                    1f / Mathf.Max(0.001f, s.y),
                    1f / Mathf.Max(0.001f, s.z)
                );
            }

            transform.position = sphere.position;
            transform.rotation = Quaternion.identity;
            var radius = 0.5f * sphere.lossyScale.y;
            if (titleRoot != null)
                titleRoot.localPosition = new Vector3(0f, radius + titleClearance, 0f);
            if (scoreRoot != null)
                scoreRoot.localPosition = new Vector3(0f, -(radius + scoreClearance), 0f);

            PlaceHint();
            FitDimmer();
        }

        private void PlaceHint()
        {
            if (hint == null)
                return;

            var cam = Camera.main;
            if (cam == null)
                return;

            var planeZ = sphere != null ? sphere.position.z : hint.transform.position.z;
            var dist = Mathf.Abs(cam.transform.position.z - planeZ);
            if (dist < 0.01f)
                dist = 1f;

            var world = cam.ScreenToWorldPoint(
                new Vector3(Screen.width * 0.5f, Screen.height * 0.12f, dist)
            );
            world.z = planeZ;
            hint.transform.position = world;
            hint.transform.rotation = Quaternion.identity;
        }

        private void FitDimmer()
        {
            if (dimmer == null)
                return;

            var cam = Camera.main;
            if (cam == null)
                return;

            var z = cam.transform.position.z + 0.12f;
            dimmer.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, z);
            var height = cam.orthographic ? cam.orthographicSize * 2.6f : 20f;
            var width = height * cam.aspect;
            dimmer.transform.localScale = new Vector3(width, height, 1f);
        }

        private Transform EnsureRoot(string name)
        {
            var child = transform.Find(name);
            if (child == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(transform, false);
                child = go.transform;
            }

            var leftover = child.GetComponent<TextMeshPro>();
            if (leftover != null)
                leftover.enabled = false;

            return child;
        }

        private TextMeshPro EnsureLetter(int index, string glyph)
        {
            var name = "Letter_" + index;
            var child = titleRoot.Find(name);
            var tmp = child != null ? child.GetComponent<TextMeshPro>() : null;
            if (tmp == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(titleRoot, false);
                tmp = go.AddComponent<TextMeshPro>();
            }

            SetupTmp(tmp, glyph, titleSize, sortingOrder + 50);
            ApplyLook(tmp, bonus: true);
            return tmp;
        }

        private TextMeshPro EnsureScoreLetter(int index, string glyph)
        {
            var name = "ScoreLetter_" + index;
            var child = scoreRoot.Find(name);
            var tmp = child != null ? child.GetComponent<TextMeshPro>() : null;
            if (tmp == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(scoreRoot, false);
                tmp = go.AddComponent<TextMeshPro>();
            }

            SetupTmp(tmp, glyph, scoreSize, sortingOrder + 51);
            ApplyLook(tmp, bonus: false);
            return tmp;
        }

        private TextMeshPro EnsureLabel(string name, string text, float fontSize, int order)
        {
            var child = transform.Find(name);
            var tmp = child != null ? child.GetComponent<TextMeshPro>() : null;
            if (tmp == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(transform, false);
                tmp = go.AddComponent<TextMeshPro>();
            }

            SetupTmp(tmp, text, fontSize, order);
            return tmp;
        }

        private void SetupTmp(TextMeshPro tmp, string text, float fontSize, int order)
        {
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.enableAutoSizing = false;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            tmp.richText = false;
            tmp.extraPadding = true;
            tmp.rectTransform.sizeDelta = new Vector2(8f, 6.4f);
            if (font != null)
                tmp.font = font;

            var renderer = tmp.GetComponent<MeshRenderer>();
            if (renderer == null)
                return;

            renderer.sortingLayerID = CardSortingLayerId;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.receiveShadows = false;
        }

        private void EnsureDimmer()
        {
            if (dimmer != null)
                return;

            var child = transform.Find("BackgroundDim");
            var go = child != null ? child.gameObject : new GameObject("BackgroundDim");
            go.transform.SetParent(transform, false);
            dimmer = go.GetComponent<SpriteRenderer>();
            if (dimmer == null)
                dimmer = go.AddComponent<SpriteRenderer>();

            dimmer.sprite = VictoryProcSprites.White();
            dimmer.color = new Color(0.07f, 0.03f, 0.14f, 0f);
            dimmer.sortingLayerName = "Default";
            dimmer.sortingOrder = 80;
            dimmer.gameObject.SetActive(false);
        }

        private void ApplyLook(TextMeshPro tmp, bool bonus)
        {
            if (tmp == null || tune == null)
                return;

            var top = bonus ? tune.bonusTop : tune.goldTop;
            var bottom = bonus ? tune.bonusBottom : tune.goldBottom;
            var shade = bonus ? tune.bonusShade : tune.goldShade;

            tmp.color = Color.white;
            tmp.enableVertexGradient = true;
            tmp.colorGradient = new VertexGradient(top, top, bottom, bottom);
            tmp.outlineWidth = 0f;

            var mat = tmp.fontMaterial;
            if (mat == null)
                return;

            mat.DisableKeyword("OUTLINE_ON");
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetFloat("_OutlineWidth", 0f);
            mat.SetFloat("_FaceDilate", Mathf.Clamp(tune.faceDilate, 0f, 0.6f));
            mat.SetColor("_UnderlayColor", shade);
            mat.SetFloat("_UnderlayOffsetX", Mathf.Clamp(tune.shadeOffsetX, -1f, 1f));
            mat.SetFloat("_UnderlayOffsetY", Mathf.Clamp(tune.shadeOffsetY, -1f, 1f));
            mat.SetFloat("_UnderlayDilate", Mathf.Clamp01(tune.shadeDilate));
            mat.SetFloat("_UnderlaySoftness", Mathf.Clamp01(tune.shadeSoftness));
            tmp.UpdateMeshPadding();
        }

        private void ApplyHintLook(TextMeshPro tmp)
        {
            if (tmp == null)
                return;

            tmp.color = Color.white;
            tmp.enableVertexGradient = false;
            tmp.outlineWidth = 0f;
            tmp.fontStyle = FontStyles.Normal;
            tmp.alpha = 0f;
            tmp.rectTransform.sizeDelta = new Vector2(14f, 2.4f);

            var mat = tmp.fontMaterial;
            if (mat == null)
                return;

            mat.DisableKeyword("OUTLINE_ON");
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetFloat("_OutlineWidth", 0f);
            mat.SetFloat("_FaceDilate", 0.08f);
            mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.55f));
            mat.SetFloat("_UnderlayOffsetX", 0f);
            mat.SetFloat("_UnderlayOffsetY", -0.12f);
            mat.SetFloat("_UnderlayDilate", 0.12f);
            mat.SetFloat("_UnderlaySoftness", 0.35f);
            tmp.UpdateMeshPadding();
        }

        private void HideImmediate()
        {
            if (titleRoot != null)
                titleRoot.gameObject.SetActive(false);

            if (scoreRoot != null)
                scoreRoot.gameObject.SetActive(false);

            if (hint != null)
            {
                hint.alpha = 0f;
                hint.gameObject.SetActive(false);
            }

            if (dimmer != null)
            {
                var color = dimmer.color;
                color.a = 0f;
                dimmer.color = color;
                dimmer.gameObject.SetActive(false);
            }
        }
    }
}
