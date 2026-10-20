using System.Collections.Generic;
using DG.Tweening;
using Solitaire.Models;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Solitaire.Presenters
{
    public class HomePresenter : OrientationAwarePresenter
    {
        private const string SuitIdleTweenId = "HomeSuitIdle";
        private const string HomeCardTweenId = "HomeCardDeal";
        private const string HomeCardFloatId = "HomeCardFloat";
        private const string HomeFanFogName = "HomeFanFog";
        private const string HomeFanFogId = "HomeFanFog";
        private const string AdditiveShaderName = "Solitaire/SpriteAdditive";

        [SerializeField]
        private Button _buttonNewMatch;

        [SerializeField]
        private Button _buttonContinue;

        [SerializeField]
        private Button _buttonOptions;

        [SerializeField]
        private Button _buttonLeaderboard;

        [SerializeField]
        private RectTransform _rectCards;

        [SerializeField]
        private RectTransform _rectSuitsCenter;

        [SerializeField]
        private RectTransform _rectSuitsLeft;

        [SerializeField]
        private RectTransform _rectSuitsRight;

        [Header("Suit idle (Home)")]
        [SerializeField]
        private float suitIdleScale = 1.05f;

        [SerializeField]
        private float suitIdleHalfDuration = 1.25f;

        [SerializeField]
        private float suitIdleStagger = 0.22f;

        [Tooltip("Wait before the first suit pops in.")]
        [SerializeField]
        private float suitPopDelay = 0.4f;

        [SerializeField]
        private float suitPopDuration = 0.1f;

        [Tooltip("Gap between each suit pop. Smaller = they overlap more.")]
        [SerializeField]
        private float suitPopStagger = 0.08f;

        [Header("Card deal (Home)")]
        [SerializeField]
        private float cardDealDuration = 0.42f;

        [SerializeField]
        private float cardDealStagger = 0.09f;

        [SerializeField]
        private float cardDealJump = 52f;

        [Header("Card float (Home)")]
        [SerializeField]
        private float cardFloatHeight = 10f;

        [SerializeField]
        private float cardFloatHalfDuration = 1.35f;

        [SerializeField]
        private float cardFloatStagger = 0.18f;

        [Header("Float motes")]
        [Tooltip("Sprite for the falling motes. Empty = soft procedural dot.")]
        [SerializeField]
        private Sprite moteSprite;

        [SerializeField]
        private Color moteColor = new Color(1f, 1f, 0.95f, 0.7f);

        [SerializeField]
        private float moteSize = 14f;

        [Tooltip("Raise spawn above the card bottom (canvas pixels).")]
        [SerializeField]
        private float moteSpawnYOffset = 40f;

        [SerializeField]
        private float moteFallDistance = 28f;

        [SerializeField]
        private float moteCooldown = 0.28f;

        [SerializeField]
        private float moteLifetime = 0.45f;

        [SerializeField]
        private float moteStartScale = 0.7f;

        [Header("Land shine")]
        [Tooltip("White specular tick. Keep alpha around 0.5–0.8.")]
        [SerializeField]
        private Color shineColor = new Color(1f, 1f, 1f, 0.72f);

        [Tooltip("How long the mirror-pass takes. Short = a tick, not a wash.")]
        [SerializeField]
        private float shineDuration = 0.16f;

        [Tooltip("0 = as the card lands. Negative = earlier (during the hop). Positive = after land.")]
        [SerializeField]
        private float shineDelay = -0.08f;

        [SerializeField]
        private float shineAngle = -28f;

        [Tooltip("Width of the highlight bar in pixels. 8–14 feels like a reflection.")]
        [SerializeField]
        private float shineWidth = 10f;

        [Header("Fan fog")]
        [Tooltip("Optional. Empty = soft procedural oval. You can drop in particeGlow2 later.")]
        [SerializeField]
        private Sprite fogSprite;

        [SerializeField]
        private Color fogColor = new Color(1f, 0.92f, 1f, 0.28f);

        [SerializeField]
        private Vector2 fogSize = new Vector2(420f, 96f);

        [Tooltip("Local Y under Cards. Card art sits around y=325, so ~220 sits under the fan.")]
        [SerializeField]
        private float fogYOffset = 220f;

        [Tooltip("How long the fog takes to form from nothing. Try 0.8–1.2.")]
        [SerializeField]
        private float fogFadeDuration = 0.9f;

        [Tooltip("Scale when the fog is born. Grows to 1 over Fog Fade Duration.")]
        [SerializeField]
        private float fogStartScale = 0.18f;

        [SerializeField]
        private float fogBreatheHalfDuration = 1.7f;

        [Tooltip("How far each fog layer slides, in pixels. Small = subtle.")]
        [SerializeField]
        private float fogDrift = 18f;

        [Tooltip("Seconds for one slide. Bigger = slower, more like mist.")]
        [SerializeField]
        private float fogDriftDuration = 6.5f;

        [SerializeField]
        private float fogDebrisSize = 6f;

        [SerializeField]
        private float fogDebrisCooldown = 0.7f;

        [Tooltip("0 = straight shelf. 0.6 = smile under the fan. 1 = strong arch.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float fogArch = 0.65f;

        [Inject]
        private readonly Game _game;

        [Inject]
        private readonly GamePopup _gamePopup;

        [Inject]
        private readonly GameState _gameState;
        private RectTransform _rectLeaderboard;

        private RectTransform _rectOptions;
        private Sequence _sequenceCards;
        private Sequence _sequenceSuitsCenter;
        private Sequence _sequenceSuitsLeft;
        private Sequence _sequenceSuitsRight;
        private CardRestPose[] _cardRests;
        private float[] _lastCardY;
        private float[] _moteCooldownLeft;
        private bool _cardFloatActive;
        private RectTransform _fanFog;
        private RectTransform _fanFogBack;
        private RectTransform _fanFogSmall;
        private RectTransform _fanFogFront;
        private Image _fanFogBackImage;
        private Image _fanFogSmallImage;
        private Image _fanFogFrontImage;
        private CanvasGroup _fanFogGroup;
        private float _fogDebrisCooldownLeft;
        private readonly List<RectTransform> _motePool = new List<RectTransform>(16);
        private readonly List<RectTransform> _fogDebrisPool = new List<RectTransform>(8);
        private readonly Vector3[] _worldCorners = new Vector3[4];
        private static Sprite sharedMoteSprite;
        private static Sprite sharedShineSprite;
        private static Sprite sharedFogSprite;
        private static float bakedFogArch = -1f;
        private static Material sharedUiAdditiveMaterial;

        private struct CardRestPose
        {
            public RectTransform rect;
            public float restZ;
            public Vector3 restScale;
        }

        private void Awake()
        {
            _rectOptions = _buttonOptions.GetComponent<RectTransform>();
            _rectLeaderboard = _buttonLeaderboard.GetComponent<RectTransform>();
            AddBlob(_buttonNewMatch);
            AddBlob(_buttonContinue);
            AddBlob(_buttonOptions);
            AddBlob(_buttonLeaderboard);
            CaptureCardRests();
        }

        private static void AddBlob(Button button)
        {
            if (button == null || button.GetComponent<BlobButton>() != null)
                return;

            button.gameObject.AddComponent<BlobButton>();
        }

        protected override void Start()
        {
            base.Start();

            _game.NewMatchCommand.BindTo(_buttonNewMatch).AddTo(this);
            _game.ContinueCommand.BindTo(_buttonContinue).AddTo(this);
            _gamePopup.OptionsCommand.BindTo(_buttonOptions).AddTo(this);
            _gamePopup.LeaderboardCommand.BindTo(_buttonLeaderboard).AddTo(this);

            // Play animation sequence on state change
            _gameState
                .State.Where(state => state == Game.State.Home)
                .Subscribe(_ =>
                    PlayAnimationSequence(_orientation.State.Value == Orientation.Landscape)
                )
                .AddTo(this);

            // Stop soft idle when leaving Home (canvas may just disable)
            _gameState
                .State.Where(state => state != Game.State.Home)
                .Subscribe(_ =>
                {
                    StopAllSuitIdle();
                    StopCardFloat();
                })
                .AddTo(this);
        }

        private void OnDisable()
        {
            StopAllSuitIdle();
            StopCardFloat();
            DOTween.Kill(HomeCardTweenId);
            DOTween.Kill(HomeFanFogId);
        }

        [ContextMenu("Restart Home Animation")]
        public void RestartHomeAnimation()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[HomePresenter] Enter Play Mode, then press Restart to replay the card deal."
                );
                return;
            }

            var isLandscape =
                _orientation != null && _orientation.State.Value == Orientation.Landscape;
            PlayAnimationSequence(isLandscape);
        }

        protected override void OnOrientationChanged(bool isLandscape)
        {
            _rectCards.anchoredPosition = isLandscape ? new Vector2(0, -120) : Vector2.zero;

            if (isLandscape)
            {
                _rectOptions.SetInsetAndSizeFromParentEdge(
                    RectTransform.Edge.Left,
                    50,
                    _rectOptions.sizeDelta.x
                );
                _rectLeaderboard.SetInsetAndSizeFromParentEdge(
                    RectTransform.Edge.Right,
                    50,
                    _rectLeaderboard.sizeDelta.x
                );
            }
            else
            {
                _rectOptions.anchorMin = new Vector2(0.5f, 0f);
                _rectOptions.anchorMax = new Vector2(0.5f, 0f);
                _rectOptions.anchoredPosition = new Vector2(-70f, _rectOptions.sizeDelta.y);

                _rectLeaderboard.anchorMin = new Vector2(0.5f, 0f);
                _rectLeaderboard.anchorMax = new Vector2(0.5f, 0f);
                _rectLeaderboard.anchoredPosition = new Vector2(70f, _rectLeaderboard.sizeDelta.y);
            }

            PlayAnimationSequence(isLandscape);
        }

        private void PlayAnimationSequence(bool isLandscape)
        {
            StopAllSuitIdle();
            StopCardFloat();
            AnimateCards(ref _sequenceCards);

            if (isLandscape)
            {
                AnimateSuits(ref _sequenceSuitsLeft, _rectSuitsLeft, false);
                AnimateSuits(ref _sequenceSuitsRight, _rectSuitsRight, true);
            }
            else
            {
                AnimateSuits(ref _sequenceSuitsCenter, _rectSuitsCenter, false);
            }
        }

        private void CaptureCardRests()
        {
            if (_cardRests != null || _rectCards == null)
                return;

            var pivots = new List<RectTransform>();
            for (var i = 0; i < _rectCards.childCount; i++)
            {
                var rect = _rectCards.GetChild(i) as RectTransform;
                if (rect == null || !IsHomeCardPivot(rect))
                    continue;

                pivots.Add(rect);
            }

            _cardRests = new CardRestPose[pivots.Count];
            for (var i = 0; i < pivots.Count; i++)
            {
                var rect = pivots[i];
                _cardRests[i] = new CardRestPose
                {
                    rect = rect,
                    restZ = SignedZ(rect.localEulerAngles.z),
                    restScale = rect.localScale
                };
            }
        }

        private void StackCardsForDeal()
        {
            if (_cardRests == null)
                return;

            for (var i = 0; i < _cardRests.Length; i++)
            {
                var pose = _cardRests[i];
                if (pose.rect == null)
                    continue;

                DOTween.Kill(pose.rect);
                DestroyLeftoverSparkle(pose.rect);
                KillCardShine(pose.rect);
                pose.rect.anchoredPosition = Vector2.zero;
                pose.rect.localEulerAngles = Vector3.zero;
                pose.rect.localScale = pose.restScale * 0.92f;
            }
        }

        private void AnimateCards(ref Sequence sequence)
        {
            CaptureCardRests();
            if (_cardRests == null || _cardRests.Length == 0)
                return;

            if (sequence != null)
            {
                sequence.Kill();
                sequence = null;
            }

            DOTween.Kill(HomeCardTweenId);
            StackCardsForDeal();

            var duration = Mathf.Max(0.15f, cardDealDuration);
            var stagger = Mathf.Max(0f, cardDealStagger);
            sequence = DOTween.Sequence().SetAutoKill(false).SetId(HomeCardTweenId);

            for (var i = 0; i < _cardRests.Length; i++)
            {
                var pose = _cardRests[i];
                if (pose.rect == null)
                    continue;

                var rect = pose.rect;
                var restZ = pose.restZ;
                var restScale = pose.restScale;
                var delay = (_cardRests.Length - 1 - i) * stagger;

                var deal = DOTween.Sequence();
                deal.Append(
                    rect.DOJumpAnchorPos(Vector2.zero, cardDealJump, 1, duration)
                        .SetEase(Ease.OutQuad)
                );
                deal.Join(
                    rect.DOLocalRotate(new Vector3(0f, 0f, restZ), duration, RotateMode.Fast)
                        .SetEase(Ease.OutBack)
                );
                deal.Join(rect.DOScale(restScale, duration).SetEase(Ease.OutBack));
                deal.InsertCallback(Mathf.Max(0f, duration + shineDelay), () => PlayCardShine(rect));
                deal.AppendCallback(() => PlayLandKick(rect, restScale));

                sequence.Insert(delay, deal);
            }

            sequence.InsertCallback(duration * 0.4f, StartFanFog);
            sequence.OnComplete(StartCardFloat);
        }

        private void PlayLandKick(RectTransform rect, Vector3 restScale)
        {
            if (rect == null)
                return;

            var squash = new Vector3(restScale.x * 1.08f, restScale.y * 0.92f, 1f);
            rect.DOScale(squash, 0.07f)
                .SetEase(Ease.OutQuad)
                .SetLoops(2, LoopType.Yoyo)
                .OnComplete(() => rect.localScale = restScale);
        }

        private void PlayCardShine(RectTransform pivot)
        {
            var bar = EnsureCardShine(pivot);
            if (bar == null)
                return;

            var image = bar.GetComponent<Image>();
            var host = bar.parent as RectTransform;
            var cardWidth = host != null ? host.rect.width : 109f;
            var cardHeight = host != null ? host.rect.height : 164f;
            var duration = Mathf.Max(0.08f, shineDuration);
            var travel = cardWidth * 0.5f;
            var clear = shineColor;
            clear.a = 0f;

            DOTween.Kill(bar);
            bar.gameObject.SetActive(true);
            bar.localEulerAngles = new Vector3(0f, 0f, shineAngle);
            bar.sizeDelta = new Vector2(Mathf.Max(4f, shineWidth), cardHeight);
            image.color = clear;
            bar.anchoredPosition = new Vector2(travel, 0f);

            var sweep = DOTween.Sequence().SetTarget(bar);
            sweep.Append(bar.DOAnchorPosX(-travel, duration).SetEase(Ease.Linear));
            sweep.Insert(0f, image.DOColor(shineColor, duration * 0.2f).SetEase(Ease.OutQuad));
            sweep.Insert(duration * 0.35f, image.DOColor(clear, duration * 0.65f).SetEase(Ease.InQuad));
            sweep.OnComplete(() => bar.gameObject.SetActive(false));
        }

        private RectTransform EnsureCardShine(RectTransform pivot)
        {
            var visual = FindCardVisual(pivot);
            if (visual == null)
                return null;

            var host = visual.Find("HomeCardShine") as RectTransform;
            if (host != null && host.GetComponent<Mask>() == null)
            {
                Destroy(host.gameObject);
                host = null;
            }

            if (host == null)
            {
                var hostGo = new GameObject(
                    "HomeCardShine",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Mask)
                );
                host = hostGo.GetComponent<RectTransform>();
                host.SetParent(visual, false);
                host.anchorMin = Vector2.zero;
                host.anchorMax = Vector2.one;
                host.offsetMin = Vector2.zero;
                host.offsetMax = Vector2.zero;
                host.SetAsLastSibling();

                var cardImage = visual.GetComponent<Image>();
                var maskImage = hostGo.GetComponent<Image>();
                maskImage.sprite = cardImage != null ? cardImage.sprite : null;
                maskImage.type = cardImage != null ? cardImage.type : Image.Type.Simple;
                maskImage.color = Color.white;
                maskImage.raycastTarget = false;
                maskImage.preserveAspect = cardImage != null && cardImage.preserveAspect;

                var mask = hostGo.GetComponent<Mask>();
                mask.showMaskGraphic = false;
            }

            var bar = host.Find("ShineBar") as RectTransform;
            if (bar != null)
                return bar;

            var barGo = new GameObject(
                "ShineBar",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            bar = barGo.GetComponent<RectTransform>();
            bar.SetParent(host, false);
            bar.anchorMin = new Vector2(0.5f, 0.5f);
            bar.anchorMax = new Vector2(0.5f, 0.5f);
            bar.pivot = new Vector2(0.5f, 0.5f);
            bar.anchoredPosition = Vector2.zero;

            var image = barGo.GetComponent<Image>();
            image.sprite = GetOrCreateShineSprite();
            image.raycastTarget = false;
            image.maskable = true;
            image.color = shineColor;
            bar.gameObject.SetActive(false);
            return bar;
        }

        private static void KillCardShine(RectTransform pivot)
        {
            var visual = FindCardVisual(pivot);
            if (visual == null)
                return;

            var host = visual.Find("HomeCardShine");
            if (host == null)
                return;

            var bar = host.Find("ShineBar");
            if (bar != null)
            {
                DOTween.Kill(bar);
                bar.gameObject.SetActive(false);
            }
        }

        private static Sprite GetOrCreateShineSprite()
        {
            const int version = 2;
            if (sharedShineSprite != null && sharedShineSprite.name == "HomeCardShineSpriteV" + version)
                return sharedShineSprite;

            const int width = 32;
            const int height = 128;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "HomeCardShine",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var centerX = (width - 1) * 0.5f;
            var softRadius = width * 0.18f;
            var hotRadius = width * 0.07f;
            for (var y = 0; y < height; y++)
            {
                var yNorm = y / (height - 1f);
                var endFade = 1f;
                if (yNorm < 0.08f)
                    endFade = yNorm / 0.08f;
                else if (yNorm > 0.92f)
                    endFade = (1f - yNorm) / 0.08f;

                for (var x = 0; x < width; x++)
                {
                    var dx = x - centerX;
                    var soft = Mathf.Exp(-(dx * dx) / (softRadius * softRadius));
                    var hot = Mathf.Exp(-(dx * dx) / (hotRadius * hotRadius));
                    var alpha = Mathf.Clamp01(soft * 0.45f + hot * 0.7f) * endFade;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply(false, false);
            sharedShineSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            sharedShineSprite.name = "HomeCardShineSpriteV" + version;
            return sharedShineSprite;
        }

        private void Update()
        {
            if (!_cardFloatActive || _cardRests == null)
                return;

            var dt = Time.unscaledDeltaTime;
            for (var i = 0; i < _cardRests.Length; i++)
            {
                var rect = _cardRests[i].rect;
                if (rect == null)
                    continue;

                if (_moteCooldownLeft[i] > 0f)
                    _moteCooldownLeft[i] -= dt;

                var y = rect.anchoredPosition.y;
                var rising = y > _lastCardY[i] + 0.15f;
                _lastCardY[i] = y;

                if (rising && _moteCooldownLeft[i] <= 0f)
                {
                    EmitMoteFromCardBottom(rect);
                    _moteCooldownLeft[i] = moteCooldown;
                }
            }

            TickFogDebris(dt);
        }

        private void StartCardFloat()
        {
            if (_cardRests == null)
                return;

            if (_gameState != null && _gameState.State.Value != Game.State.Home)
                return;

            var count = _cardRests.Length;
            if (_lastCardY == null || _lastCardY.Length != count)
            {
                _lastCardY = new float[count];
                _moteCooldownLeft = new float[count];
            }

            var height = Mathf.Max(2f, cardFloatHeight);
            var half = Mathf.Max(0.4f, cardFloatHalfDuration);
            var stagger = Mathf.Max(0f, cardFloatStagger);

            for (var i = 0; i < count; i++)
            {
                var rect = _cardRests[i].rect;
                if (rect == null)
                    continue;

                _lastCardY[i] = rect.anchoredPosition.y;
                _moteCooldownLeft[i] = i * 0.05f;

                DOTween.Kill(HomeCardFloatId + rect.GetInstanceID());
                rect.anchoredPosition = Vector2.zero;
                rect.DOAnchorPosY(height, half)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetDelay(i * stagger)
                    .SetId(HomeCardFloatId + rect.GetInstanceID());
            }

            _cardFloatActive = true;
        }

        private void StopCardFloat()
        {
            _cardFloatActive = false;
            StopFanFog();
            if (_cardRests == null)
                return;

            for (var i = 0; i < _cardRests.Length; i++)
            {
                var rect = _cardRests[i].rect;
                if (rect == null)
                    continue;

                DOTween.Kill(HomeCardFloatId + rect.GetInstanceID());
                rect.anchoredPosition = Vector2.zero;
            }

            HideAllMotes();
        }

        private void StartFanFog()
        {
            if (_rectCards == null)
                return;

            if (_gameState != null && _gameState.State.Value != Game.State.Home)
                return;

            EnsureFanFog();
            if (_fanFog == null || _fanFogGroup == null)
                return;

            DOTween.Kill(HomeFanFogId);
            _fanFog.anchoredPosition = new Vector2(0f, fogYOffset);
            _fanFog.sizeDelta = fogSize;
            _fanFog.gameObject.SetActive(true);
            _fogDebrisCooldownLeft = 0.2f;

            var sprite = ResolveFogSprite();
            var peak = Mathf.Max(0.02f, fogColor.a);
            var smallPeak = peak * 0.55f;
            var frontPeak = peak * 0.42f;
            ApplyFogLayer(_fanFogBack, _fanFogBackImage, sprite, FogLayerSize(1.12f), 0f);
            ApplyFogLayer(_fanFogSmall, _fanFogSmallImage, sprite, FogLayerSize(0.78f), 0f);
            ApplyFogLayer(_fanFogFront, _fanFogFrontImage, sprite, FogLayerSize(1.22f), 0f);

            _fanFogGroup.alpha = 1f;
            var startScale = Mathf.Clamp(fogStartScale, 0.05f, 0.95f);
            _fanFog.localScale = new Vector3(startScale, startScale, 1f);
            var fade = Mathf.Max(0.2f, fogFadeDuration);
            var half = Mathf.Max(0.4f, fogBreatheHalfDuration);
            var drift = Mathf.Max(2f, fogDrift);
            var driftTime = Mathf.Max(2f, fogDriftDuration);
            _fogDebrisCooldownLeft = fade;

            var form = DOTween.Sequence().SetId(HomeFanFogId).SetTarget(_fanFog);
            form.Append(_fanFog.DOScale(Vector3.one, fade).SetEase(Ease.OutCubic));
            if (_fanFogBackImage != null)
                form.Join(_fanFogBackImage.DOFade(peak, fade).SetEase(Ease.OutQuad));
            if (_fanFogSmallImage != null)
                form.Join(_fanFogSmallImage.DOFade(smallPeak, fade).SetEase(Ease.OutQuad));
            if (_fanFogFrontImage != null)
                form.Join(_fanFogFrontImage.DOFade(frontPeak, fade).SetEase(Ease.OutQuad));
            form.Append(
                _fanFogGroup.DOFade(0.72f, half).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
            );

            if (_fanFogBack != null)
            {
                _fanFogBack.anchoredPosition = Vector2.zero;
                _fanFogBack
                    .DOAnchorPosX(drift, driftTime)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetId(HomeFanFogId)
                    .SetTarget(_fanFogBack);
            }

            if (_fanFogSmall != null)
            {
                _fanFogSmall.anchoredPosition = Vector2.zero;
                _fanFogSmall
                    .DOAnchorPos(new Vector2(drift * 0.35f, -drift * 0.85f), driftTime * 0.58f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetId(HomeFanFogId)
                    .SetTarget(_fanFogSmall);
            }

            if (_fanFogFront != null)
            {
                _fanFogFront.anchoredPosition = Vector2.zero;
                _fanFogFront
                    .DOAnchorPos(new Vector2(-drift * 1.25f, drift * 0.35f), driftTime * 0.78f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetId(HomeFanFogId)
                    .SetTarget(_fanFogFront);
            }
        }

        private void StopFanFog()
        {
            DOTween.Kill(HomeFanFogId);
            HideAllFogDebris();
            if (_fanFog == null)
                return;

            _fanFog.localScale = Vector3.one;
            _fanFog.gameObject.SetActive(false);
        }

        private void EnsureFanFog()
        {
            if (_fanFog != null && _fanFogSmall == null)
            {
                Destroy(_fanFog.gameObject);
                _fanFog = null;
                _fanFogBack = null;
                _fanFogBackImage = null;
                _fanFogSmall = null;
                _fanFogSmallImage = null;
                _fanFogFront = null;
                _fanFogFrontImage = null;
                _fanFogGroup = null;
            }

            if (_fanFog == null && _rectCards != null)
            {
                var leftover = _rectCards.Find(HomeFanFogName);
                if (leftover != null)
                    Destroy(leftover.gameObject);
            }

            if (_fanFog != null || _rectCards == null)
                return;

            var go = new GameObject(HomeFanFogName, typeof(RectTransform), typeof(CanvasGroup));
            _fanFog = go.GetComponent<RectTransform>();
            _fanFog.SetParent(_rectCards, false);
            _fanFog.SetAsFirstSibling();
            _fanFog.anchorMin = new Vector2(0.5f, 0.5f);
            _fanFog.anchorMax = new Vector2(0.5f, 0.5f);
            _fanFog.pivot = new Vector2(0.5f, 0.5f);
            _fanFog.anchoredPosition = new Vector2(0f, fogYOffset);
            _fanFog.sizeDelta = fogSize;
            _fanFog.localScale = Vector3.one;
            _fanFog.localRotation = Quaternion.identity;

            _fanFogGroup = go.GetComponent<CanvasGroup>();
            _fanFogGroup.blocksRaycasts = false;
            _fanFogGroup.interactable = false;

            _fanFogBack = CreateFogLayer("FogBack");
            _fanFogBackImage = _fanFogBack.GetComponent<Image>();
            _fanFogSmall = CreateFogLayer("FogSmall");
            _fanFogSmallImage = _fanFogSmall.GetComponent<Image>();
            _fanFogFront = CreateFogLayer("FogFront");
            _fanFogFrontImage = _fanFogFront.GetComponent<Image>();

            go.SetActive(false);
        }

        private RectTransform CreateFogLayer(string name)
        {
            var go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(_fanFog, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = FogLayerSize(1f);

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = false;
            var additive = GetOrCreateUiAdditiveMaterial();
            if (additive != null)
                image.material = additive;

            return rect;
        }

        private Vector2 FogLayerSize(float mul)
        {
            if (fogSprite != null)
                return fogSize * mul;

            var arch = Mathf.Clamp01(fogArch);
            var width = fogSize.x * mul;
            var height = (Mathf.Max(96f, fogSize.y) + arch * 88f) * mul;
            return new Vector2(width, height);
        }

        private Sprite ResolveFogSprite()
        {
            return fogSprite != null ? fogSprite : GetOrCreateFogSprite(Mathf.Clamp01(fogArch));
        }

        private void ApplyFogLayer(
            RectTransform rect,
            Image image,
            Sprite sprite,
            Vector2 size,
            float alpha
        )
        {
            if (rect == null || image == null)
                return;

            rect.sizeDelta = size;
            image.sprite = sprite;
            image.color = new Color(fogColor.r, fogColor.g, fogColor.b, alpha);
        }

        private void TickFogDebris(float dt)
        {
            if (_fanFog == null || !_fanFog.gameObject.activeSelf)
                return;

            if (_fogDebrisCooldownLeft > 0f)
            {
                _fogDebrisCooldownLeft -= dt;
                return;
            }

            if (CountActiveFogDebris() >= 3)
                return;

            EmitFogDebris();
            _fogDebrisCooldownLeft = Mathf.Max(0.25f, fogDebrisCooldown);
        }

        private int CountActiveFogDebris()
        {
            var count = 0;
            for (var i = 0; i < _fogDebrisPool.Count; i++)
            {
                var debris = _fogDebrisPool[i];
                if (debris != null && debris.gameObject.activeSelf)
                    count++;
            }

            return count;
        }

        private void EmitFogDebris()
        {
            if (_fanFog == null)
                return;

            var debris = GetFogDebris();
            debris.SetParent(_fanFog, false);
            debris.SetAsLastSibling();

            var halfW = fogSize.x * 0.32f;
            var halfH = fogSize.y * 0.16f;
            var start = new Vector2(
                Random.Range(-halfW, halfW),
                Random.Range(-halfH, halfH * 0.35f)
            );
            var end = start + new Vector2(Random.Range(-16f, 16f), Random.Range(14f, 28f));
            var life = Random.Range(1.3f, 2.1f);
            var size = Mathf.Max(3f, fogDebrisSize);

            debris.anchoredPosition = start;
            debris.sizeDelta = new Vector2(size, size);
            debris.localScale = Vector3.one * 0.35f;
            debris.gameObject.SetActive(true);

            var image = debris.GetComponent<Image>();
            image.sprite = ResolveMoteSprite();
            image.color = new Color(fogColor.r, fogColor.g, fogColor.b, 0f);

            DOTween.Kill(debris);
            var twinkle = DOTween.Sequence().SetTarget(debris);
            twinkle.Append(debris.DOAnchorPos(end, life).SetEase(Ease.Linear));
            twinkle.Join(debris.DOScale(0.8f, life).SetEase(Ease.OutQuad));
            twinkle.Join(
                DOTween.Sequence()
                    .Append(image.DOFade(0.55f, life * 0.22f).SetEase(Ease.OutQuad))
                    .Append(image.DOFade(0f, life * 0.78f).SetEase(Ease.InQuad))
            );
            twinkle.OnComplete(() => debris.gameObject.SetActive(false));
        }

        private RectTransform GetFogDebris()
        {
            for (var i = 0; i < _fogDebrisPool.Count; i++)
            {
                var pooled = _fogDebrisPool[i];
                if (pooled != null && !pooled.gameObject.activeSelf)
                    return pooled;
            }

            var go = new GameObject(
                "HomeFogDebris",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(6f, 6f);

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            var additive = GetOrCreateUiAdditiveMaterial();
            if (additive != null)
                image.material = additive;

            go.SetActive(false);
            _fogDebrisPool.Add(rect);
            return rect;
        }

        private void HideAllFogDebris()
        {
            for (var i = 0; i < _fogDebrisPool.Count; i++)
            {
                var debris = _fogDebrisPool[i];
                if (debris == null)
                    continue;

                DOTween.Kill(debris);
                debris.gameObject.SetActive(false);
            }
        }

        private static bool IsHomeCardPivot(Transform child)
        {
            if (child == null)
                return false;

            var n = child.name;
            return n != HomeFanFogName && n != "HomeFloatMote";
        }

        private void EmitMoteFromCardBottom(RectTransform pivot)
        {
            if (_rectCards == null || pivot == null)
                return;

            var visual = FindCardVisual(pivot);
            visual.GetWorldCorners(_worldCorners);
            var worldBottom = (_worldCorners[0] + _worldCorners[3]) * 0.5f;
            var screen = RectTransformUtility.WorldToScreenPoint(null, worldBottom);
            if (
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rectCards,
                    screen,
                    null,
                    out var local
                )
            )
                return;

            var mote = GetMote();
            mote.SetParent(_rectCards, false);
            mote.SetAsLastSibling();
            mote.anchoredPosition = local + new Vector2(0f, moteSpawnYOffset);
            mote.localRotation = Quaternion.identity;
            mote.localScale = Vector3.one * Mathf.Max(0.1f, moteStartScale);
            mote.sizeDelta = new Vector2(moteSize, moteSize);
            mote.gameObject.SetActive(true);

            var image = mote.GetComponent<Image>();
            image.sprite = ResolveMoteSprite();
            image.color = moteColor;

            var life = Mathf.Max(0.1f, moteLifetime);
            var startY = mote.anchoredPosition.y;
            DOTween.Kill(mote);
            var fall = DOTween.Sequence().SetTarget(mote);
            fall.Append(
                mote.DOAnchorPosY(startY - moteFallDistance, life).SetEase(Ease.InQuad)
            );
            fall.Join(mote.DOScale(0.25f, life).SetEase(Ease.InQuad));
            fall.Join(image.DOFade(0f, life).SetEase(Ease.InQuad));
            fall.OnComplete(() => mote.gameObject.SetActive(false));
        }

        private RectTransform GetMote()
        {
            for (var i = 0; i < _motePool.Count; i++)
            {
                var pooled = _motePool[i];
                if (pooled != null && !pooled.gameObject.activeSelf)
                    return pooled;
            }

            var go = new GameObject(
                "HomeFloatMote",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(14f, 14f);

            var image = go.GetComponent<Image>();
            image.sprite = ResolveMoteSprite();
            image.raycastTarget = false;
            image.preserveAspect = true;
            go.SetActive(false);
            _motePool.Add(rect);
            return rect;
        }

        private Sprite ResolveMoteSprite()
        {
            return moteSprite != null ? moteSprite : GetOrCreateMoteSprite();
        }

        private void HideAllMotes()
        {
            for (var i = 0; i < _motePool.Count; i++)
            {
                var mote = _motePool[i];
                if (mote == null)
                    continue;

                DOTween.Kill(mote);
                mote.gameObject.SetActive(false);
            }
        }

        private static RectTransform FindCardVisual(RectTransform pivot)
        {
            if (pivot == null)
                return pivot;

            for (var i = 0; i < pivot.childCount; i++)
            {
                var child = pivot.GetChild(i) as RectTransform;
                if (child != null
                    && child.name != "HomeLandSparkle"
                    && child.name != "HomeFloatMote"
                    && child.name != "HomeCardShine")
                    return child;
            }

            return pivot;
        }

        private static Sprite GetOrCreateMoteSprite()
        {
            if (sharedMoteSprite != null)
                return sharedMoteSprite;

            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HomeFloatMote",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var center = (size - 1) * 0.5f;
            var radius = size * 0.35f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / radius;
                    var dy = (y - center) / radius;
                    var alpha = Mathf.Exp(-(dx * dx + dy * dy) * 3f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply(false, false);
            sharedMoteSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f
            );
            sharedMoteSprite.name = "HomeFloatMoteSprite";
            return sharedMoteSprite;
        }

        private static Sprite GetOrCreateFogSprite(float arch)
        {
            const int version = 3;
            if (
                sharedFogSprite != null
                && sharedFogSprite.name == "HomeFanFogSpriteV" + version
                && Mathf.Abs(bakedFogArch - arch) < 0.02f
            )
                return sharedFogSprite;

            bakedFogArch = arch;
            const int width = 512;
            const int height = 192;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "HomeFanFog",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var sideNy = -0.28f;
            var midNy = Mathf.Lerp(-0.08f, 0.46f, arch);
            for (var y = 0; y < height; y++)
            {
                var ny = y / (height - 1f) * 2f - 1f;
                for (var x = 0; x < width; x++)
                {
                    var nx = x / (width - 1f) * 2f - 1f;
                    var lift = 1f - nx * nx;
                    var arcNy = Mathf.Lerp(sideNy, midNy, lift);
                    var dy = ny - arcNy;
                    var endT = Mathf.InverseLerp(0.7f, 1.05f, Mathf.Abs(nx));
                    var endFade = 1f - Mathf.SmoothStep(0f, 1f, endT);
                    var blob = Mathf.Exp(-(nx * nx) / 0.7f - (dy * dy) / 0.05f);
                    var line = Mathf.Exp(-(nx * nx) / 0.55f - (dy * dy) / 0.005f);
                    var alpha = Mathf.Clamp01((blob * 0.72f + line * 0.5f) * endFade);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply(false, false);
            sharedFogSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            sharedFogSprite.name = "HomeFanFogSpriteV" + version;
            return sharedFogSprite;
        }

        private static Material GetOrCreateUiAdditiveMaterial()
        {
            if (sharedUiAdditiveMaterial != null)
                return sharedUiAdditiveMaterial;

            var shader = Shader.Find(AdditiveShaderName);
            if (shader == null)
            {
                Debug.LogWarning(
                    "[HomePresenter] Shader Solitaire/SpriteAdditive not found — fan fog will use default UI blend."
                );
                return null;
            }

            sharedUiAdditiveMaterial = new Material(shader)
            {
                name = "HomeFanFogAdditive",
                color = Color.white
            };
            return sharedUiAdditiveMaterial;
        }

        private static void DestroyLeftoverSparkle(RectTransform pivot)
        {
            if (pivot == null)
                return;

            var leftover = pivot.Find("HomeLandSparkle");
            if (leftover != null)
                Object.Destroy(leftover.gameObject);

            for (var i = 0; i < pivot.childCount; i++)
            {
                var child = pivot.GetChild(i);
                var nested = child.Find("HomeLandSparkle");
                if (nested != null)
                    Object.Destroy(nested.gameObject);
            }
        }

        private static float SignedZ(float z)
        {
            while (z > 180f)
                z -= 360f;
            while (z < -180f)
                z += 360f;
            return z;
        }

        private void AnimateSuits(ref Sequence sequence, RectTransform rectSuits, bool isReverse)
        {
            if (rectSuits == null)
                return;

            StopSuitIdle(rectSuits);

            if (sequence != null)
            {
                sequence.Kill();
                sequence = null;
            }

            sequence = DOTween.Sequence().SetAutoKill(false);

            var delay = Mathf.Max(0f, suitPopDelay);
            var pop = Mathf.Max(0.05f, suitPopDuration);
            var stagger = Mathf.Max(0f, suitPopStagger);
            var index = 0;

                for (
                    var i = isReverse ? rectSuits.childCount - 1 : 0;
                    isReverse ? i >= 0 : i < rectSuits.childCount;
                    i += isReverse ? -1 : 1
                )
                {
                    var rect = rectSuits.GetChild(i);
                rect.localScale = Vector3.zero;

                var at = delay + index * stagger;
                sequence.Insert(at, rect.DOScale(Vector3.one, pop).SetEase(Ease.InCubic));
                sequence.Insert(
                    at + pop,
                    rect.DOPunchScale(Vector3.one * 0.5f, pop).SetEase(Ease.OutCubic)
                );
                index++;
            }

            sequence.OnComplete(() => StartSuitIdle(rectSuits));
        }

        private void StartSuitIdle(RectTransform rectSuits)
        {
            if (rectSuits == null || _gameState.State.Value != Game.State.Home)
                return;

            for (var i = 0; i < rectSuits.childCount; i++)
            {
                var child = rectSuits.GetChild(i);
                var id = SuitIdleTweenId + child.GetInstanceID();
                DOTween.Kill(id);

                child.localScale = Vector3.one;
                child
                    .DOScale(suitIdleScale, suitIdleHalfDuration)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetDelay(i * suitIdleStagger)
                    .SetId(id);
            }
        }

        private void StopSuitIdle(RectTransform rectSuits)
        {
            if (rectSuits == null)
                return;

            for (var i = 0; i < rectSuits.childCount; i++)
            {
                var child = rectSuits.GetChild(i);
                DOTween.Kill(SuitIdleTweenId + child.GetInstanceID());
            }
        }

        private void StopAllSuitIdle()
        {
            StopSuitIdle(_rectSuitsCenter);
            StopSuitIdle(_rectSuitsLeft);
            StopSuitIdle(_rectSuitsRight);
        }
    }
}
