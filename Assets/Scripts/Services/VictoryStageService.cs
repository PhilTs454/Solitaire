using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Solitaire.Helpers;
using Solitaire.Models;
using Solitaire.Presenters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Solitaire.Services
{
    /// <summary>
    /// Runtime overlay for the victory result frame. No scene art required.
    /// </summary>
    public sealed class VictoryStageService : IVictoryStage, IDisposable
    {
        private const string RootName = "VictoryStageCanvas";
        private const string TweenId = "VictoryStage";
        private const float CountDuration = 0.45f;

        [Inject]
        private readonly VictoryPalette palette;

        [Inject]
        private readonly IAudioService audioService;

        [Inject(Optional = true, Id = "ScorePopupFont")]
        private readonly TMP_FontAsset font;

        [Inject(Optional = true)]
        private readonly VictoryBubblePresenter bubble;

        private Canvas canvas;
        private Image veil;
        private Image flash;
        private Image blast;
        private RectTransform starRoot;
        private Image star;
        private Image glow;
        private RectTransform leftCurtain;
        private RectTransform rightCurtain;
        private TextMeshProUGUI title;
        private TextMeshProUGUI scoreCaption;
        private TextMeshProUGUI scoreLabel;
        private RectTransform buttonRow;
        private Button newMatchButton;
        private Button homeButton;
        private Button skipButton;

        private bool skipRequested;
        private bool ctaReady;
        private bool buttonClicked;
        private int scoreTarget;
        private Vector3 cameraRest;
        private bool cameraRestStored;
        private Tweener scoreTween;

        public bool IsVisible => canvas != null && canvas.gameObject.activeSelf;

        public async UniTask PlayAsync(
            int score,
            Func<bool> stillActive,
            Action onNewMatch,
            Action onHome
        )
        {
            EnsureBuilt();
            HideInternal(killOnly: true);

            scoreTarget = Mathf.Max(0, score);
            skipRequested = false;
            ctaReady = false;
            buttonClicked = false;
            BindButtons(onNewMatch, onHome);
            bubble?.ArmContinue(() =>
            {
                if (buttonClicked)
                    return;
                buttonClicked = true;
                Hide();
                onNewMatch?.Invoke();
            });

            ApplyPalette();
            ResetToIntro();
            canvas.gameObject.SetActive(true);

            VictorySequenceAudio.Play(audioService, Audio.SfxStreak, 0.7f);

            var veilColor = palette.Veil;
            veilColor.a = 0f;
            veil.color = veilColor;
            veil.raycastTarget = false;

            ShowPayoff();

            var elapsed = 0f;
            while (elapsed < 0.12f && !skipRequested && IsActive(stillActive))
            {
                elapsed += Time.unscaledDeltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (!IsActive(stillActive))
            {
                Hide();
                return;
            }

            if (skipRequested)
                SnapToCta();
            else
            {
                ShowContinue();
                ctaReady = true;
            }

            while (!buttonClicked && IsActive(stillActive) && IsVisible)
                await UniTask.Yield(PlayerLoopTiming.Update);

            if (!buttonClicked)
                Hide();
        }

        public void Hide()
        {
            HideInternal(killOnly: false);
        }

        public void Dispose()
        {
            Hide();
            if (canvas != null)
            {
                UnityEngine.Object.Destroy(canvas.gameObject);
                canvas = null;
            }
        }

        private void SnapToCta()
        {
            DOTween.Kill(TweenId);
            RestoreCamera();
            VictorySequenceAudio.Play(audioService, Audio.SfxStreak, 0.7f);

            var veilColor = palette.Veil;
            veilColor.a = 0f;
            veil.color = veilColor;
            veil.raycastTarget = false;

            flash.color = new Color(1f, 1f, 1f, 0f);

            ShowPayoff(snapScore: true);
            ShowContinue();
            ctaReady = true;
        }

        private void ShowPayoff(bool snapScore = false)
        {
            if (bubble != null && bubble.isActiveAndEnabled)
            {
                bubble.SetPayoffScore(scoreTarget);
                if (!bubble.HasPayoffStarted)
                    bubble.BeginPayoff();
                else if (snapScore)
                    bubble.SnapScore(scoreTarget);
                return;
            }

            ShowCopy();
            if (snapScore && scoreLabel != null)
                scoreLabel.text = scoreTarget.ToString();
            else
                CountScore();
        }

        private void ShowCopy()
        {
            title.gameObject.SetActive(true);
            scoreCaption.gameObject.SetActive(true);
            scoreLabel.gameObject.SetActive(true);
            title.alpha = 1f;
            scoreCaption.alpha = 1f;
            var titleRt = title.rectTransform;
            titleRt.localScale = Vector3.one * 0.7f;
            titleRt.DOScale(1f, 0.28f).SetEase(Ease.OutBack).SetId(TweenId);
        }

        private void CountScore()
        {
            if (scoreLabel == null)
                return;

            var current = 0;
            scoreLabel.text = "0";
            scoreTween?.Kill();
            scoreTween = DOTween
                .To(() => current, x =>
                {
                    current = x;
                    scoreLabel.text = current.ToString();
                }, scoreTarget, CountDuration)
                .SetEase(Ease.OutQuad)
                .SetId(TweenId);
        }

        private void ShowContinue()
        {
            if (bubble != null && bubble.isActiveAndEnabled)
            {
                bubble.EnableContinue();
                if (buttonRow != null)
                    buttonRow.gameObject.SetActive(false);
                return;
            }

            ShowButtons();
        }

        private void ShowButtons()
        {
            buttonRow.gameObject.SetActive(true);
            buttonRow.localScale = Vector3.one * 0.85f;
            buttonRow.DOScale(1f, 0.22f).SetEase(Ease.OutBack).SetId(TweenId);
        }

        private void RestoreCamera()
        {
            if (!cameraRestStored)
                return;

            var cam = Camera.main;
            if (cam != null)
                cam.transform.localPosition = cameraRest;
            cameraRestStored = false;
        }

        private void BindButtons(Action onNewMatch, Action onHome)
        {
            newMatchButton.onClick.RemoveAllListeners();
            homeButton.onClick.RemoveAllListeners();
            skipButton.onClick.RemoveAllListeners();

            newMatchButton.onClick.AddListener(() =>
            {
                if (buttonClicked)
                    return;
                buttonClicked = true;
                Hide();
                onNewMatch?.Invoke();
            });

            homeButton.onClick.AddListener(() =>
            {
                if (buttonClicked)
                    return;
                buttonClicked = true;
                Hide();
                onHome?.Invoke();
            });

            skipButton.onClick.AddListener(() =>
            {
                if (!ctaReady)
                    skipRequested = true;
            });
        }

        private void ResetToIntro()
        {
            var veilColor = palette.Veil;
            veilColor.a = 0f;
            veil.color = veilColor;
            veil.raycastTarget = false;

            flash.color = new Color(1f, 1f, 1f, 0f);
            blast.gameObject.SetActive(false);
            starRoot.gameObject.SetActive(false);
            leftCurtain.gameObject.SetActive(false);
            rightCurtain.gameObject.SetActive(false);

            title.gameObject.SetActive(false);
            scoreCaption.gameObject.SetActive(false);
            scoreLabel.gameObject.SetActive(false);
            scoreLabel.text = "0";
            buttonRow.gameObject.SetActive(false);
        }

        private void ApplyPalette()
        {
            veil.color = new Color(palette.Veil.r, palette.Veil.g, palette.Veil.b, veil.color.a);
            star.color = palette.Star;
            glow.color = palette.StarGlow;
            title.color = palette.Title;
            scoreCaption.color = palette.Score;
            scoreLabel.color = palette.Score;

            var curtains = leftCurtain.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < curtains.Length; i++)
            {
                var c = palette.Curtain;
                if (i > 0)
                    c = Color.Lerp(c, Color.black, 0.18f);
                curtains[i].color = c;
            }

            var rightImages = rightCurtain.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < rightImages.Length; i++)
            {
                var c = palette.Curtain;
                if (i > 0)
                    c = Color.Lerp(c, Color.black, 0.18f);
                rightImages[i].color = c;
            }

            SetButtonColor(newMatchButton, palette.Button);
            SetButtonColor(homeButton, palette.Button);
        }

        private static void SetButtonColor(Button button, Color color)
        {
            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = color;
        }

        private void HideInternal(bool killOnly)
        {
            DOTween.Kill(TweenId);
            scoreTween?.Kill();
            scoreTween = null;
            RestoreCamera();
            skipRequested = false;
            ctaReady = false;

            if (!killOnly && canvas != null)
                canvas.gameObject.SetActive(false);
        }

        private static bool IsActive(Func<bool> stillActive)
        {
            return stillActive == null || stillActive();
        }

        private void EnsureBuilt()
        {
            if (canvas != null)
                return;

            var root = new GameObject(RootName);
            canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            root.AddComponent<GraphicRaycaster>();
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var white = VictoryProcSprites.White();

            veil = CreateImage("Veil", root.transform, palette.Veil, white);
            Stretch(veil.rectTransform);
            skipButton = veil.gameObject.AddComponent<Button>();
            skipButton.transition = Selectable.Transition.None;

            flash = CreateImage("Flash", root.transform, new Color(1f, 1f, 1f, 0f), white);
            Stretch(flash.rectTransform);
            flash.raycastTarget = false;

            blast = CreateImage("Blast", root.transform, Color.white, SoftRingSprite.Get());
            Center(blast.rectTransform, 520f, 520f);
            blast.raycastTarget = false;
            blast.gameObject.SetActive(false);

            starRoot = CreateRect("StarRoot", root.transform);
            Center(starRoot, 360f, 360f);
            glow = CreateImage("Glow", starRoot, palette.StarGlow, VictoryProcSprites.Glow());
            Center(glow.rectTransform, 360f, 360f);
            glow.raycastTarget = false;
            BuildRays(starRoot);
            star = CreateImage("Star", starRoot, palette.Star, VictoryProcSprites.Star());
            Center(star.rectTransform, 240f, 240f);
            star.raycastTarget = false;

            leftCurtain = BuildCurtain("CurtainLeft", root.transform, true);
            rightCurtain = BuildCurtain("CurtainRight", root.transform, false);

            title = CreateLabel("Title", root.transform, "YOU DID IT!", 78f, palette.Title);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0.5f, 0.82f);
            titleRt.anchorMax = new Vector2(0.5f, 0.82f);
            titleRt.sizeDelta = new Vector2(900f, 140f);
            titleRt.anchoredPosition = Vector2.zero;

            scoreCaption = CreateLabel(
                "ScoreCaption",
                root.transform,
                "SCORE THIS RUN",
                28f,
                palette.Score
            );
            var capRt = scoreCaption.rectTransform;
            capRt.anchorMin = new Vector2(0.5f, 0.74f);
            capRt.anchorMax = new Vector2(0.5f, 0.74f);
            capRt.sizeDelta = new Vector2(700f, 48f);
            capRt.anchoredPosition = Vector2.zero;

            scoreLabel = CreateLabel("Score", root.transform, "0", 64f, palette.Score);
            var scoreRt = scoreLabel.rectTransform;
            scoreRt.anchorMin = new Vector2(0.5f, 0.68f);
            scoreRt.anchorMax = new Vector2(0.5f, 0.68f);
            scoreRt.sizeDelta = new Vector2(700f, 90f);
            scoreRt.anchoredPosition = Vector2.zero;

            buttonRow = CreateRect("Buttons", root.transform);
            buttonRow.anchorMin = new Vector2(0.5f, 0.22f);
            buttonRow.anchorMax = new Vector2(0.5f, 0.22f);
            buttonRow.sizeDelta = new Vector2(640f, 90f);
            buttonRow.anchoredPosition = Vector2.zero;
            var rowLayout = buttonRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 28f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = true;

            newMatchButton = CreateTextButton("NewMatch", buttonRow, "NEW MATCH");
            homeButton = CreateTextButton("Home", buttonRow, "HOME");
        }

        private void BuildRays(RectTransform parent)
        {
            for (var i = 0; i < 6; i++)
            {
                var ray = CreateImage("Ray" + i, parent, palette.Ray, VictoryProcSprites.White());
                var rt = ray.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.08f);
                rt.sizeDelta = new Vector2(18f, 300f);
                rt.anchoredPosition = Vector2.zero;
                rt.localEulerAngles = new Vector3(0f, 0f, i * 60f);
                ray.raycastTarget = false;
            }
        }

        private RectTransform BuildCurtain(string name, Transform parent, bool left)
        {
            var rt = CreateRect(name, parent);
            if (left)
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 0.5f);
            }
            else
            {
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 0.5f);
            }

            rt.sizeDelta = new Vector2(220f, 0f);
            rt.anchoredPosition = Vector2.zero;
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = VictoryProcSprites.White();
            image.color = palette.Curtain;
            image.raycastTarget = false;

            for (var i = 0; i < 3; i++)
            {
                var fold = CreateImage("Fold" + i, rt, Color.Lerp(palette.Curtain, Color.black, 0.2f), VictoryProcSprites.White());
                var foldRt = fold.rectTransform;
                foldRt.anchorMin = new Vector2(0f, 0f);
                foldRt.anchorMax = new Vector2(0f, 1f);
                foldRt.pivot = new Vector2(0.5f, 0.5f);
                foldRt.sizeDelta = new Vector2(18f, 0f);
                foldRt.anchoredPosition = new Vector2(40f + i * 55f, 0f);
                fold.raycastTarget = false;
            }

            return rt;
        }

        private Button CreateTextButton(string name, Transform parent, string label)
        {
            var rt = CreateRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = VictoryProcSprites.White();
            image.color = palette.Button;
            var button = rt.gameObject.AddComponent<Button>();
            rt.gameObject.AddComponent<BlobButton>();
            var layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 84f;
            layout.flexibleWidth = 1f;

            var text = CreateLabel(name + "Label", rt, label, 28f, palette.ButtonLabel);
            Stretch(text.rectTransform);
            text.raycastTarget = false;
            return button;
        }

        private TextMeshProUGUI CreateLabel(
            string name,
            Transform parent,
            string text,
            float size,
            Color color
        )
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
                tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Image CreateImage(string name, Transform parent, Color color, Sprite sprite)
        {
            var rt = CreateRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return image;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rt, float width, float height)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
