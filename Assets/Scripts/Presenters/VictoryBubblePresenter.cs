using System;
using System.Collections.Generic;
using DG.Tweening;
using Solitaire.Models;
using Solitaire.Services;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Zenject;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Bubbly prefab: NewBubble on Sphere. Runtime drives charge, impacts, and gloss
    /// on the body / gloss layers.
    /// </summary>
    public sealed class VictoryBubblePresenter : MonoBehaviour
    {
        [Header("Glyph Burst")]
        [Tooltip("How many pips spray from Points into the bubble (coin-to-chest style).")]
        [SerializeField]
        [Range(4, 32)]
        private int burstCount = 16;

        [Tooltip("Seconds between each pip launch.")]
        [SerializeField]
        [Range(0.02f, 0.2f)]
        private float stagger = 0.045f;

        [Tooltip("Extra wait after the entrance punch before the first pip.")]
        [SerializeField]
        [Range(0f, 0.6f)]
        private float spawnHold = 0.14f;

        [Header("Bubble Juice")]
        [Tooltip("Breath between the last gulp and the finale. Clamped to 0.35s.")]
        [SerializeField]
        [Range(0.15f, 1.2f)]
        private float fullPayoffDelay = 0.3f;

        [Tooltip("Sphere scale added at full charge (0.08 = +8%).")]
        [SerializeField]
        [Range(0f, 0.15f)]
        private float chargeScaleBoost = 0.08f;

        [Tooltip("Shader impact peak strength on each pip hit.")]
        [SerializeField]
        [Range(0f, 2f)]
        private float impactStrengthPeak = 1f;

        [SerializeField]
        [Range(0.1f, 0.45f)]
        private float spawnPunchDuration = 0.24f;

        [Tooltip("Start scale relative to rest.")]
        [SerializeField]
        [Range(0.55f, 0.95f)]
        private float spawnStartScale = 0.82f;

        [Tooltip("Brief peak scale on spawn before settling to rest (1.08 = +8%).")]
        [SerializeField]
        [Range(1f, 1.2f)]
        private float spawnPeakScale = 1.08f;

        [SerializeField]
        [Range(0f, 1f)]
        private float spawnStreakChargeVolume = 0.62f;

        [Tooltip("Viewport Y when the bubble appears (0.5 = center). Lower = further down.")]
        [SerializeField]
        [Range(0.35f, 0.58f)]
        private float bubbleViewportY = 0.47f;

        [Tooltip("If on, pips spawn from the Points TMP (top-right). If off, foundations.")]
        [SerializeField]
        private bool fromPointsHud = true;

        [Tooltip("World-unit scatter around the Points label.")]
        [SerializeField]
        [Range(0f, 1.5f)]
        private float startJitter = 0.4f;

        [Tooltip("Seconds to slam from Points onto the glass.")]
        [SerializeField]
        [Range(0.12f, 0.8f)]
        private float approachDuration = 0.32f;

        [Tooltip("Seconds to drift inside and vanish after the hit.")]
        [SerializeField]
        [Range(0.15f, 1.2f)]
        private float absorbDuration = 0.55f;

        [Tooltip("Arc height as a fraction of travel distance.")]
        [SerializeField]
        [Range(0f, 0.5f)]
        private float arcLift = 0.12f;

        [Tooltip("Pip size at launch, relative to land size.")]
        [SerializeField]
        [Range(0.8f, 2.5f)]
        private float startScaleMul = 1.65f;

        [Tooltip("Land size as a fraction of bubble diameter.")]
        [SerializeField]
        [Range(0.06f, 0.3f)]
        private float pipSizeOnDiameter = 0.16f;

        [Header("Glyph Sparks")]
        [Tooltip("World size of each sparkle in the pip wake.")]
        [SerializeField]
        [Range(0.04f, 0.4f)]
        private float sparkSize = 0.12f;

        [Tooltip("How many sparkles hang behind each flying pip.")]
        [SerializeField]
        [Range(2, 40)]
        private int sparkCount = 16;

        [Header("Victory Copy")]
        [Tooltip("World TMP size for the banner above the bubble. Stay under the score.")]
        [SerializeField]
        [Range(1.5f, 16f)]
        private float titleWorldSize = 2.8f;

        [Tooltip("World TMP size for the score under the bubble.")]
        [SerializeField]
        [Range(1.5f, 9f)]
        private float victoryScoreSize = 5.4f;

        [Tooltip("Gap from the glass to VICTORY!, in world units.")]
        [SerializeField]
        [Range(0.1f, 1.8f)]
        private float victoryTitleGap = 0.7f;

        [Tooltip("Gap from the glass to the score, in world units.")]
        [SerializeField]
        [Range(0.1f, 1.4f)]
        private float victoryScoreGap = 0.55f;

        [Header("Finale")]
        [Tooltip("Banner spelled above the final score after the burst.")]
        [SerializeField]
        private string bannerWord = "DECK COMPLETE!";

        [Tooltip("Hold at maximum size / white rim before the squeeze.")]
        [SerializeField]
        [Range(0.05f, 0.4f)]
        private float fullChargeDuration = 0.12f;

        [Tooltip("Squash down / widen out: the pressure frame.")]
        [SerializeField]
        [Range(0.03f, 0.2f)]
        private float compressionDuration = 0.06f;

        [Tooltip("Nearly still, unstable wobble before release.")]
        [SerializeField]
        [Range(0.05f, 0.3f)]
        private float tensionHold = 0.1f;

        [Tooltip("Scale outward, then the sphere is gone.")]
        [SerializeField]
        [Range(0.05f, 0.25f)]
        private float burstDuration = 0.08f;

        [Tooltip("Beat between the burst and the score slam.")]
        [SerializeField]
        [Range(0f, 0.3f)]
        private float scoreSlamDelay = 0.04f;

        [Tooltip("Counter size while glyphs are collected, relative to its rest size.")]
        [SerializeField]
        [Range(0.3f, 1f)]
        private float scoreCollectScale = 0.85f;

        [Tooltip("Where the slam settles, relative to rest. Auto-clamped to fit the screen.")]
        [SerializeField]
        [Range(1f, 6f)]
        private float scoreSlamScale = 3f;

        [Tooltip("Quiet after the slam before 'tap anywhere' is live.")]
        [SerializeField]
        [Range(0.4f, 2.5f)]
        private float continueHintDelay = 1.15f;

        [Tooltip("Debug only: wait for a tap on the glass instead of popping automatically.")]
        [SerializeField]
        private bool requireTapToPop;

        [Header("Hit Mini Bubbles")]
        [Tooltip("Outside mini-bubble size as a fraction of the big bubble diameter.")]
        [SerializeField]
        [Range(0.02f, 0.22f)]
        private float miniSizeOnDiameter = 0.06f;

        [Tooltip("World-unit X/Y shift applied after picking a surface spawn.")]
        [SerializeField]
        private Vector2 miniSpawnOffset = Vector2.zero;

        [Tooltip("How far across the visible glass they can spawn. 0 is front-center, 1 is the full front.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float miniSurfaceSpread = 0.78f;

        [Tooltip("Average seconds each mini bubble lives.")]
        [SerializeField]
        [Range(0.2f, 3f)]
        private float miniLifetime = 0.9f;

        [Tooltip("Random lifetime spread around the average (0 = fixed).")]
        [SerializeField]
        [Range(0f, 0.6f)]
        private float miniLifetimeSpread = 0.28f;

        [Tooltip("Random size variation (0.2 ≈ 80%–120% of base size).")]
        [SerializeField]
        [Range(0f, 0.35f)]
        private float miniSizeVariation = 0.18f;

        [Tooltip("Max random delay before a mini bubble appears.")]
        [SerializeField]
        [Range(0f, 0.15f)]
        private float miniSpawnDelay = 0f;

        [Tooltip("World units per second upward.")]
        [SerializeField]
        [Range(0f, 6f)]
        private float miniRiseSpeed = 1.2f;

        [Tooltip("World units per second sideways drift.")]
        [SerializeField]
        [Range(0f, 4f)]
        private float miniMoveSpeed = 0.25f;

        [Tooltip("Side-to-side wobble as a fraction of bubble diameter.")]
        [SerializeField]
        [Range(0f, 0.25f)]
        private float miniWobble = 0.07f;

        [Tooltip("Gentle breathe pulse on scale (0 = off).")]
        [SerializeField]
        [Range(0f, 0.12f)]
        private float miniPulse = 0.05f;

        [Tooltip("Slow Z spin in degrees per second.")]
        [SerializeField]
        [Range(0f, 90f)]
        private float miniSpin = 22f;

        [Tooltip("Grow slightly while rising (fraction of start size).")]
        [SerializeField]
        [Range(0f, 0.15f)]
        private float miniGrowth = 0.06f;

        public float SpawnHold => spawnPunchDuration + spawnHold;
        public float Stagger => stagger;
        public int BurstCount => Mathf.Clamp(burstCount, 4, 32);
        public float FlightDuration => approachDuration + absorbDuration;
        public float StartJitter => startJitter;
        public float ApproachDuration => approachDuration;
        public float AbsorbDuration => absorbDuration;
        public float ArcLift => arcLift;
        public float StartScaleMul => startScaleMul;
        public bool FromPointsHud => fromPointsHud;
        public float SparkSize => sparkSize;
        public int SparkCount => sparkCount;

        private const string SpawnTweenId = "VictoryBubbleSpawn";
        private const string ImpactTweenId = "VictoryBubbleImpact";
        private const string ChargeTweenId = "VictoryBubbleCharge";
        private const string InteriorSpinId = "VictoryBubbleInteriorSpin";
        private const string FullBeatTweenId = "VictoryBubbleFullBeat";
        private const string ImpactShaderTweenId = "VictoryBubbleImpactShader";
        private const string SpawnFlashTweenId = "VictoryBubbleSpawnFlash";
        private const string BubbleScoreTweenId = "VictoryBubbleScoreCounter";
        private const string ScoreIdleTweenId = "VictoryBubbleScoreIdle";
        private const string ScoreGoldTweenId = "VictoryBubbleScoreGold";
        private const string BubbleExplodeTweenId = "VictoryBubbleExplode";
        private const string FinaleTweenId = "VictoryBubbleFinale";
        private const string FinaleShapeTweenId = "VictoryBubbleFinaleShape";
        private const string BurstRippleDelayId = "VictoryBubbleBurstRipple";
        private const string CameraPunchTweenId = "VictoryBubbleCameraPunch";
        private const string GlossShaderName = "Solitaire/VictoryBubbleGloss";
        private const float MinPipWorldSize = 0.26f;
        private const int CardSortingLayerId = 34509423;

        private static readonly int ChargeId = Shader.PropertyToID("_Charge");
        private static readonly int ImpactPointId = Shader.PropertyToID("_ImpactPoint");
        private static readonly int ImpactStrengthId = Shader.PropertyToID("_ImpactStrength");
        private static readonly int ImpactTimeId = Shader.PropertyToID("_ImpactTime");
        private static readonly int GlossFadeId = Shader.PropertyToID("_Fade");
        private static readonly int GlossBroadStrengthId = Shader.PropertyToID("_BroadStrength");
        private static readonly int GlossRimStrengthId = Shader.PropertyToID("_RimBoostStrength");
        private static readonly int GlossFilmStrengthId = Shader.PropertyToID("_FilmStrength");
        private static readonly int GlossSharpStrengthId = Shader.PropertyToID("_SharpStrength");
        private static readonly int BubbleAlphaId = Shader.PropertyToID("_BubbleAlpha");
        private const float RuptureDuration = 0.06f;

        [SerializeField]
        private MeshRenderer meshRenderer;

        [SerializeField]
        private Transform absorbTarget;

        [SerializeField]
        private int sortingOrder = 380;

        [SerializeField]
        private Sprite interiorMoteSprite;

        [Tooltip("Pngtree sun rays used after the bubble pops.")]
        [SerializeField]
        private Sprite sunRaysSprite;

        [Tooltip("MAT_FantasyWarrior_ParticleFX_Sparkle_Additive.")]
        [SerializeField]
        private Material sunRaysMaterial;

        [Tooltip("Unity Default-Particle. Used by pop flash/ring/droplets and ScoreIdleGlow.")]
        [SerializeField]
        private Material defaultParticleMaterial;

        [Tooltip("World TMP under CrestShield/Bubbly named Score. Counts up during glyph absorbs.")]
        [SerializeField]
        private TextMeshPro bubbleScore;

        [Tooltip("Bubble counter when the run score is 0 (preview / force win).")]
        [SerializeField]
        private int defaultVictoryScore = 500;

        private readonly Dictionary<Card.Suits, AbsorbSlot> absorbSlots =
            new Dictionary<Card.Suits, AbsorbSlot>(4);

        private MaterialPropertyBlock chargeBlock;
        private MaterialPropertyBlock glossBlock;
        private MeshRenderer glossRenderer;
        private MeshRenderer interiorRenderer;
        private float chargeValue;
        private float impactShaderStrength;
        private float glossFlashMul = 1f;
        private float glossBroadBase = 1.15f;
        private float glossRimBase = 0.72f;
        private float glossFilmBase = 0.34f;
        private float glossSharpBase = 0.85f;
        private bool bodyHasImpact;
        private Vector3 restScale = Vector3.one;
        private Vector3 sphereRestScale = Vector3.one;
        private bool allowAutoHide = true;
        private bool didCacheSlots;
        private SortingGroup sortingGroup;
        private Transform sphereTransform;
        private Transform interiorShell;
        private BubbleInteriorMotes interiorMotes;
        private VictoryMiniBubbleFx miniBubbles;
        private VictoryBurstFx burstFx;
        private VictoryScoreShine scoreShine;
        private VictoryBubbleLabels labels;
        private int bubbleScoreDisplayed;
        private bool awaitingBubbleTap;
        private bool bubbleShellHidden;
        private bool glyphScoreActive;
        private Tweener bubbleScoreTween;
        private Vector3 bubbleScoreRestScale = Vector3.one;
        private bool didCacheScoreScale;
        private float glossFade = 1f;
        private float bodyFade = 1f;
        private float bodyAlphaRest = 1f;
        private bool bodyHasAlpha;
        private bool finaleShaping;
        private bool finaleComplete;

        [Inject(Optional = true)]
        private ICardLandFxService landFx;

        [Inject(Optional = true)]
        private IAudioService audioService;

        [Inject(Optional = true, Id = "ScorePopupFont")]
        private TMP_FontAsset scoreFont;

        [Inject(Optional = true)]
        private ScorePopupTune scoreTune;

        [Inject(Optional = true)]
        private IPointsService pointsService;

        public Transform AbsorbTarget => absorbTarget;

        public Transform Sphere =>
            sphereTransform != null ? sphereTransform : transform;

        public Sprite InteriorMoteSprite => interiorMoteSprite;

        private void Awake()
        {
            restScale = transform.localScale;
            if (restScale.sqrMagnitude < 0.0001f)
                restScale = Vector3.one;

            sphereTransform = transform.Find("Sphere");
            if (sphereTransform == null)
                sphereTransform = transform;

            sphereRestScale = sphereTransform.localScale;
            if (sphereRestScale.sqrMagnitude < 0.0001f)
                sphereRestScale = Vector3.one;

            interiorShell = sphereTransform.Find("InteriorShell");

            if (meshRenderer == null)
                meshRenderer = sphereTransform.GetComponent<MeshRenderer>();
            if (UsesGlossShader(meshRenderer))
                meshRenderer = FindBodyRenderer();

            sortingGroup = GetComponent<SortingGroup>();
            if (sortingGroup == null)
                sortingGroup = gameObject.AddComponent<SortingGroup>();

            sortingGroup.sortingOrder = sortingOrder;
            if (meshRenderer != null)
                meshRenderer.sortingOrder = sortingOrder;

            chargeBlock = new MaterialPropertyBlock();
            glossBlock = new MaterialPropertyBlock();
            CacheShaderBaselines();
            ClearGlossPropertyBlocks();
            EnsureAbsorbTarget();
            EnsureInteriorMotes();
            miniBubbles = new VictoryMiniBubbleFx();
            burstFx = new VictoryBurstFx();
            scoreShine = new VictoryScoreShine();
            EnsureLabels();
            EnsureBubbleScore();
            ApplyCharge(0f);
        }

        private void Start()
        {
            if (allowAutoHide && gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            miniBubbles?.Dispose();
            miniBubbles = null;
            burstFx?.Dispose();
            burstFx = null;
            scoreShine?.Dispose();
            scoreShine = null;
        }

        public void Prepare()
        {
            allowAutoHide = false;
            CacheAbsorbSlots();
            HideLayoutPreviews();
            PlaceAtViewportCenter();
            chargeValue = 0f;
            glossFade = 1f;
            bodyFade = 1f;
            finaleShaping = false;
            finaleComplete = false;
            burstFx?.StopAll();
            scoreShine?.StopAll();
            DOTween.Kill(ScoreIdleTweenId);
            DOTween.Kill(ScoreGoldTweenId);
            ClearGlossPropertyBlocks();
            ApplyCharge(0f);

            if (sphereTransform != null)
            {
                sphereTransform.localScale = sphereRestScale;
                sphereTransform.localRotation = Quaternion.identity;
            }

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            PlaySpawnPunch();
            PlayInteriorDrift();
            interiorMotes?.Play();
            miniBubbles?.Warmup(Sphere, meshRenderer);
            burstFx?.Warmup(Sphere, defaultParticleMaterial);
            scoreShine?.Warmup(sunRaysSprite, sunRaysMaterial, defaultParticleMaterial);
            HideVictoryCopyForPrepare();
            HideBubbleScore();
            SetBubbleShellVisible(true);
        }

        public void Hide()
        {
            DOTween.Kill(SpawnTweenId);
            DOTween.Kill(ImpactTweenId);
            DOTween.Kill(ChargeTweenId);
            DOTween.Kill(InteriorSpinId);
            DOTween.Kill(FullBeatTweenId);
            DOTween.Kill(ImpactShaderTweenId);
            DOTween.Kill(SpawnFlashTweenId);
            DOTween.Kill(BubbleScoreTweenId);
            DOTween.Kill(BubbleExplodeTweenId);
            DOTween.Kill(FinaleTweenId);
            DOTween.Kill(FinaleShapeTweenId);
            DOTween.Kill(BurstRippleDelayId);
            DOTween.Kill(CameraPunchTweenId);
            DOTween.Kill(ScoreIdleTweenId);
            DOTween.Kill(ScoreGoldTweenId);
            interiorMotes?.Stop();
            miniBubbles?.StopAll();
            burstFx?.StopAll();
            scoreShine?.StopAll();
            HideVictoryCopy();
            SetBubbleShellVisible(true);
            chargeValue = 0f;
            glossFade = 1f;
            bodyFade = 1f;
            finaleShaping = false;
            finaleComplete = false;
            ClearGlossPropertyBlocks();
            ApplyCharge(0f);
            transform.localScale = restScale;
            transform.localRotation = Quaternion.identity;
            if (sphereTransform != null)
            {
                sphereTransform.localScale = sphereRestScale;
                sphereTransform.localRotation = Quaternion.identity;
            }
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        public bool HasPayoffStarted => payoffStarted;

        private const string ContinueHintTweenId = "VictoryBubbleContinueHint";

        private int pendingScore;
        private bool payoffStarted;
        private bool continueArmed;
        private bool scorePresented;
        private Action continueAction;

        public void ArmPayoff(int score)
        {
            SetPayoffScore(score);
        }

        public void SetPayoffScore(int score)
        {
            pendingScore = ResolveFinalScore(score);
        }

        /// <summary>
        /// Show the in-bubble counter when pip flight starts; ticks up until glyphs finish.
        /// Stays small here: the slam after the burst is the payoff, not this.
        /// </summary>
        public void BeginGlyphScoreCount(int totalGulps)
        {
            pendingScore = ResolveFinalScore(pendingScore);
            if (pendingScore <= 0 || totalGulps <= 0)
                return;

            EnsureBubbleScore();
            glyphScoreActive = true;
            DOTween.Kill(BubbleScoreTweenId);
            SetBubbleScoreDisplay(0);
            if (bubbleScore != null)
            {
                bubbleScore.alpha = 1f;
                bubbleScore.transform.localScale = bubbleScoreRestScale * scoreCollectScale;
                bubbleScore.gameObject.SetActive(true);
            }

            var duration = Mathf.Max(
                0.35f,
                (totalGulps - 1) * Stagger + FlightDuration
            );
            bubbleScoreTween = DOTween
                .To(
                    () => bubbleScoreDisplayed,
                    SetBubbleScoreDisplay,
                    pendingScore,
                    duration
                )
                .SetEase(Ease.OutQuad)
                .SetId(BubbleScoreTweenId);
        }

        public void CompleteGlyphScoreCount()
        {
            if (!glyphScoreActive)
                return;

            DOTween.Kill(BubbleScoreTweenId);
            SetBubbleScoreDisplay(pendingScore);
        }

        public void ArmContinue(Action onContinue)
        {
            continueAction = onContinue;
            continueArmed = false;
        }

        public void EnableContinue()
        {
            continueArmed = false;
            TryScheduleContinueHint();
        }

        /// <summary>
        /// Every glyph is inside and counted. Runs the automatic climax unless the
        /// debug tap-to-pop flag is on.
        /// </summary>
        public void NotifyAllGlyphsConsumed()
        {
            if (requireTapToPop)
            {
                ArmBubbleTap();
                return;
            }

            BeginPayoff();
        }

        public bool RequiresTapToPop => requireTapToPop;

        /// <summary>
        /// Rest scale for the slammed score, clamped so long totals still fit the screen.
        /// </summary>
        private Vector3 GetSlamScale()
        {
            var target = bubbleScoreRestScale * Mathf.Max(1f, scoreSlamScale);
            var cam = Camera.main;
            if (bubbleScore == null || cam == null)
                return target;

            bubbleScore.ForceMeshUpdate();
            var textWidth = bubbleScore.GetRenderedValues(false).x;
            var parentWidth = Mathf.Max(0.001f, transform.lossyScale.x);
            if (textWidth <= 0.001f)
                return target;

            var depth = Mathf.Abs(cam.transform.position.z - bubbleScore.transform.position.z);
            var worldHeight = cam.orthographic
                ? cam.orthographicSize * 2f
                : 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var maxScale = worldHeight * cam.aspect * 0.8f / (textWidth * parentWidth);
            var fitted = Mathf.Min(target.x, maxScale);
            return Vector3.one * Mathf.Max(bubbleScoreRestScale.x * 0.6f, fitted);
        }

        public bool FinaleComplete => finaleComplete;

        public void BeginPayoff()
        {
            if (payoffStarted)
                return;

            payoffStarted = true;
            EnsureLabels();
            PlayFinale();
        }

        public void ShowVictoryTitle()
        {
            BeginPayoff();
        }

        public void PlayScore(int score)
        {
            pendingScore = Mathf.Max(0, score);
            EnsureBubbleScore();
            TweenBubbleScoreTo(pendingScore, 0.42f, OnScorePresented);
        }

        public void SnapScore(int score)
        {
            pendingScore = Mathf.Max(0, score);
            EnsureBubbleScore();
            SetBubbleScoreDisplay(pendingScore);
            if (bubbleScore != null)
            {
                DOTween.Kill(BubbleScoreTweenId);
                bubbleScore.transform.localScale = GetSlamScale();
                bubbleScore.alpha = 1f;
                bubbleScore.gameObject.SetActive(true);
            }

            finaleComplete = true;
            if (bubbleScore != null)
            {
                scoreShine?.PlayBurst(
                    bubbleScore.transform.position,
                    MeasureScoreWidth(),
                    sortingOrder + 49
                );
            }

            BeginScoreIdle(GetSlamScale());
            OnScorePresented();
        }

        private void OnScorePresented()
        {
            scorePresented = true;
            TryScheduleContinueHint();
        }

        private void TryScheduleContinueHint()
        {
            if (continueAction == null || !scorePresented)
                return;

            DOTween.Kill(ContinueHintTweenId);
            DOTween
                .Sequence()
                .SetId(ContinueHintTweenId)
                .AppendInterval(Mathf.Max(0.4f, continueHintDelay))
                .OnComplete(() =>
                {
                    if (continueAction == null)
                        return;
                    labels?.ShowContinueHint();
                    continueArmed = true;
                });
        }

        private void HideVictoryCopy()
        {
            DOTween.Kill(ContinueHintTweenId);
            labels?.Hide();
            payoffStarted = false;
            pendingScore = 0;
            continueArmed = false;
            scorePresented = false;
            continueAction = null;
            awaitingBubbleTap = false;
            bubbleShellHidden = false;
            glyphScoreActive = false;
            finaleComplete = false;
            finaleShaping = false;
            DOTween.Kill(BubbleScoreTweenId);
            DOTween.Kill(BubbleExplodeTweenId);
            DOTween.Kill(FinaleTweenId);
            DOTween.Kill(FinaleShapeTweenId);
            HideBubbleScore();
        }

        private void HideVictoryCopyForPrepare()
        {
            DOTween.Kill(ContinueHintTweenId);
            payoffStarted = false;
            continueArmed = false;
            scorePresented = false;
            labels?.Hide();
        }

        private void Update()
        {
            if (awaitingBubbleTap && PressedThisFrame() && HitBubble(PressScreenPos()))
            {
                ExplodeBubbleFromTap();
                return;
            }

            if (!continueArmed || continueAction == null)
                return;
            if (!PressedThisFrame())
                return;

            var act = continueAction;
            continueArmed = false;
            act.Invoke();
        }

        private static bool PressedThisFrame()
        {
            if (Input.touchCount > 0)
                return Input.GetTouch(0).phase == TouchPhase.Began;
            return Input.GetMouseButtonDown(0);
        }

        private static Vector3 PressScreenPos()
        {
            if (Input.touchCount > 0)
                return Input.GetTouch(0).position;
            return Input.mousePosition;
        }

        private bool HitBubble(Vector3 screenPos)
        {
            var cam = Camera.main;
            var target = Sphere;
            if (cam == null || target == null)
                return false;

            var center = cam.WorldToScreenPoint(target.position);
            if (center.z < 0f)
                return false;

            var radius = 0.5f * target.lossyScale.y;
            var edge = cam.WorldToScreenPoint(target.position + cam.transform.up * radius);
            var pixelRadius = Vector2.Distance(center, edge);
            return Vector2.Distance(screenPos, center) <= pixelRadius * 1.08f;
        }

        private void EnsureLabels()
        {
            if (labels != null)
            {
                labels.Configure(
                    Sphere,
                    scoreFont,
                    scoreTune,
                    sortingOrder,
                    titleWorldSize,
                    victoryScoreSize,
                    victoryTitleGap,
                    victoryScoreGap,
                    bannerWord
                );
                return;
            }

            var existing = transform.Find("Labels");
            var root = existing != null ? existing.gameObject : new GameObject("Labels");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            labels = root.GetComponent<VictoryBubbleLabels>();
            if (labels == null)
                labels = root.AddComponent<VictoryBubbleLabels>();

            labels.Configure(
                Sphere,
                scoreFont,
                scoreTune,
                sortingOrder,
                titleWorldSize,
                victoryScoreSize,
                victoryTitleGap,
                victoryScoreGap,
                bannerWord
            );
        }

        public bool TryGetAbsorbSlot(
            Card.Suits suit,
            out Transform slot,
            out Vector3 lossyScale
        )
        {
            if (!TryGetAbsorbFlight(suit, null, out var sphere, out _, out var pipScale))
            {
                slot = null;
                lossyScale = Vector3.one;
                return false;
            }

            slot = sphere;
            lossyScale = pipScale;
            return true;
        }

        public bool TryGetAbsorbFlight(
            Card.Suits suit,
            Sprite pipSprite,
            out Transform sphere,
            out Vector3 sphereLocalDir,
            out Vector3 pipScale
        )
        {
            sphere = Sphere;
            if (sphere == null)
            {
                sphereLocalDir = Vector3.back;
                pipScale = Vector3.one;
                return false;
            }

            sphereLocalDir = GetFrontLocalDir(sphere);
            pipScale = GetPipScale(pipSprite);
            return true;
        }

        private static Vector3 GetFrontLocalDir(Transform sphere)
        {
            var cam = Camera.main;
            if (cam == null)
                return Vector3.back;

            var toCam = cam.transform.position - sphere.position;
            if (toCam.sqrMagnitude < 0.0001f)
                return Vector3.back;

            return sphere.InverseTransformDirection(toCam.normalized);
        }

        public Vector3 GetLandScale()
        {
            return GetPipScale(null);
        }

        public Vector3 GetPipScale(Sprite sprite)
        {
            var diameter =
                sphereTransform != null ? sphereTransform.lossyScale.x : transform.lossyScale.x;
            var desired = Mathf.Max(MinPipWorldSize, diameter * pipSizeOnDiameter);
            var spriteWidth = 1f;
            if (sprite != null)
                spriteWidth = Mathf.Max(0.01f, sprite.bounds.size.x);

            var s = desired / spriteWidth;
            return new Vector3(s, s, s);
        }

        private float GetRippleFit()
        {
            var diameter =
                sphereTransform != null ? sphereTransform.lossyScale.x : transform.lossyScale.x;
            return Mathf.Max(0.85f, diameter * 0.28f);
        }

        /// <summary>
        /// The pip touched the glass: ripple and local squash only. No charge, no score.
        /// </summary>
        public void OnGlyphSurfaceHit(Vector3 glyphWorldPosition)
        {
            if (finaleShaping)
                return;

            ApplyShaderImpact(glyphWorldPosition);
            landFx?.PlayWorld(glyphWorldPosition, GetRippleFit(), sortingOrder + 40, 0.6f);

            if (!DOTween.IsTweening(ImpactTweenId))
                PlayGummySquash(false, glyphWorldPosition);
        }

        /// <summary>
        /// The pip finished drifting inside: charge, score and gulp land together here.
        /// </summary>
        public void OnGlyphConsumed(Vector3 glyphWorldPosition, int consumedCount, int totalCount)
        {
            if (finaleShaping)
                return;

            var total = Mathf.Max(1, totalCount);
            var consumed = Mathf.Clamp(consumedCount, 0, total);
            var targetCharge = Mathf.Clamp01(consumed / (float)total);
            AnimateCharge(targetCharge);

            var last = consumed >= total;
            if (last)
                PlayGummySquash(true, glyphWorldPosition);

            if (glyphScoreActive)
                NudgeGlyphScoreFromAbsorb(consumed, total);

            miniBubbles?.Play(
                Sphere,
                meshRenderer,
                sortingOrder + 6,
                new VictoryMiniBubbleFx.Tune(
                    miniSizeOnDiameter,
                    miniSpawnOffset,
                    miniSurfaceSpread,
                    miniLifetime,
                    miniLifetimeSpread,
                    miniSizeVariation,
                    miniSpawnDelay,
                    miniRiseSpeed,
                    miniMoveSpeed,
                    miniWobble,
                    miniPulse,
                    miniSpin,
                    miniGrowth
                ),
                consumed,
                last
            );
        }

        private void EnsureAbsorbTarget()
        {
            if (absorbTarget != null)
                return;

            absorbTarget = sphereTransform != null ? sphereTransform : transform;
        }

        private void EnsureInteriorMotes()
        {
            if (interiorMotes != null)
                return;

            var parent = interiorShell != null ? interiorShell : sphereTransform;
            if (parent == null)
                return;

            var existing = parent.Find("InteriorMotes");
            if (existing != null)
            {
                interiorMotes = existing.GetComponent<BubbleInteriorMotes>();
                if (interiorMotes != null)
                    return;
            }

            var go = new GameObject("InteriorMotes");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.AddComponent<ParticleSystem>();
            interiorMotes = go.AddComponent<BubbleInteriorMotes>();
            if (interiorMoteSprite != null)
                interiorMotes.Configure(interiorMoteSprite);
        }

        private void CacheAbsorbSlots()
        {
            if (didCacheSlots)
                return;

            didCacheSlots = true;
            absorbSlots.Clear();

            var root = FindGlyphLayoutRoot();
            if (root == null)
                return;

            TryAddSlot(root, "Heart", Card.Suits.Heart);
            TryAddSlot(root, "Diamond", Card.Suits.Diamond);
            TryAddSlot(root, "Club", Card.Suits.Club);
            TryAddSlot(root, "Spade", Card.Suits.Spade);
        }

        private Transform FindGlyphLayoutRoot()
        {
            Transform fallback = null;
            var bestCount = -1;
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name != "GylphPos" && child.name != "GlyphPos")
                    continue;

                var count = 0;
                if (child.Find("Heart") != null)
                    count++;
                if (child.Find("Diamond") != null)
                    count++;
                if (child.Find("Club") != null)
                    count++;
                if (child.Find("Spade") != null)
                    count++;

                if (count > bestCount)
                {
                    bestCount = count;
                    fallback = child;
                }

                if (count >= 4)
                    return child;
            }

            return fallback;
        }

        private void TryAddSlot(Transform root, string childName, Card.Suits suit)
        {
            var child = root.Find(childName);
            if (child == null)
                return;

            absorbSlots[suit] = new AbsorbSlot { Slot = child };
        }

        private void HideLayoutPreviews()
        {
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name != "GylphPos" && child.name != "GlyphPos")
                    continue;

                var renderers = child.GetComponentsInChildren<SpriteRenderer>(true);
                for (var r = 0; r < renderers.Length; r++)
                    renderers[r].enabled = false;
            }
        }

        private void PlaceAtViewportCenter()
        {
            var camera = Camera.main;
            if (camera == null)
                return;

            var z = Mathf.Abs(camera.transform.position.z);
            var center = camera.ViewportToWorldPoint(new Vector3(0.5f, bubbleViewportY, z));
            center.z = transform.position.z;
            transform.position = center;
        }

        private void AnimateCharge(float target)
        {
            DOTween.Kill(ChargeTweenId);
            DOTween
                .To(
                    () => chargeValue,
                    value =>
                    {
                        chargeValue = value;
                        ApplyChargeVisuals(chargeValue);
                    },
                    target,
                    0.18f
                )
                .SetEase(Ease.OutQuad)
                .SetId(ChargeTweenId);
        }

        private void ApplyCharge(float charge)
        {
            chargeValue = charge;
            ApplyChargeVisuals(charge);
        }

        private void ApplyChargeVisuals(float charge)
        {
            var clamped = Mathf.Clamp01(charge);
            ApplyBodyCharge(clamped);
            ApplyGlossCharge(clamped, glossFlashMul);
            ApplyChargeScale(clamped);
            interiorMotes?.SetCharge(clamped);
        }

        private void ApplyBodyCharge(float charge)
        {
            if (meshRenderer == null || UsesGlossShader(meshRenderer))
                return;

            meshRenderer.GetPropertyBlock(chargeBlock, 0);
            chargeBlock.SetFloat(ChargeId, charge);
            if (bodyHasAlpha)
                chargeBlock.SetFloat(BubbleAlphaId, bodyAlphaRest * Mathf.Clamp01(bodyFade));
            if (bodyHasImpact)
                chargeBlock.SetFloat(ImpactStrengthId, impactShaderStrength);
            meshRenderer.SetPropertyBlock(chargeBlock, 0);
        }

        private void ApplyShaderImpact(Vector3 hitWorld)
        {
            if (meshRenderer == null || UsesGlossShader(meshRenderer) || !bodyHasImpact)
                return;

            var sphere = Sphere;
            if (sphere == null)
                return;

            var local = sphere.InverseTransformPoint(hitWorld);
            meshRenderer.GetPropertyBlock(chargeBlock, 0);
            chargeBlock.SetVector(ImpactPointId, local);
            chargeBlock.SetFloat(ImpactTimeId, Time.time);
            impactShaderStrength = impactStrengthPeak;
            chargeBlock.SetFloat(ImpactStrengthId, impactShaderStrength);
            chargeBlock.SetFloat(ChargeId, chargeValue);
            meshRenderer.SetPropertyBlock(chargeBlock, 0);

            DOTween.Kill(ImpactShaderTweenId);
            DOTween
                .To(
                    () => impactShaderStrength,
                    value =>
                    {
                        impactShaderStrength = value;
                        ApplyBodyCharge(Mathf.Clamp01(chargeValue));
                    },
                    0f,
                    0.34f
                )
                .SetEase(Ease.OutQuad)
                .SetId(ImpactShaderTweenId);
        }

        private void ApplyChargeScale(float charge)
        {
            if (sphereTransform == null || finaleShaping || DOTween.IsTweening(ImpactTweenId))
                return;

            var mul = 1f + charge * chargeScaleBoost;
            sphereTransform.localScale = sphereRestScale * mul;
        }

        private void ApplyGlossCharge(float charge, float flashMul)
        {
            if (glossRenderer == null)
                return;

            var chargeMul = 0.82f + charge * 0.38f;
            glossRenderer.GetPropertyBlock(glossBlock, 0);
            glossBlock.SetFloat(GlossFadeId, Mathf.Clamp01(glossFade));
            glossBlock.SetFloat(GlossBroadStrengthId, glossBroadBase * chargeMul * flashMul);
            glossBlock.SetFloat(GlossRimStrengthId, glossRimBase * chargeMul * flashMul);
            glossBlock.SetFloat(GlossFilmStrengthId, glossFilmBase * (0.75f + charge * 0.55f) * flashMul);
            glossBlock.SetFloat(GlossSharpStrengthId, glossSharpBase * (0.9f + charge * 0.25f) * flashMul);
            glossRenderer.SetPropertyBlock(glossBlock, 0);

            if (interiorRenderer == null)
                return;

            interiorRenderer.GetPropertyBlock(glossBlock, 0);
            glossBlock.SetFloat(GlossFadeId, Mathf.Clamp01(glossFade));
            interiorRenderer.SetPropertyBlock(glossBlock, 0);
        }

        private void CacheShaderBaselines()
        {
            if (sphereTransform != null)
            {
                var glossTransform = sphereTransform.Find("GlossLayer");
                if (glossTransform != null)
                    glossRenderer = glossTransform.GetComponent<MeshRenderer>();

                if (interiorShell != null)
                    interiorRenderer = interiorShell.GetComponent<MeshRenderer>();
            }

            if (glossRenderer != null && glossRenderer.sharedMaterial != null)
            {
                var mat = glossRenderer.sharedMaterial;
                if (mat.HasProperty(GlossBroadStrengthId))
                    glossBroadBase = mat.GetFloat(GlossBroadStrengthId);
                if (mat.HasProperty(GlossRimStrengthId))
                    glossRimBase = mat.GetFloat(GlossRimStrengthId);
                if (mat.HasProperty(GlossFilmStrengthId))
                    glossFilmBase = mat.GetFloat(GlossFilmStrengthId);
                if (mat.HasProperty(GlossSharpStrengthId))
                    glossSharpBase = mat.GetFloat(GlossSharpStrengthId);
            }

            if (meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                var mat = meshRenderer.sharedMaterial;
                bodyHasImpact =
                    mat.HasProperty(ImpactPointId)
                    && mat.HasProperty(ImpactStrengthId)
                    && mat.HasProperty(ImpactTimeId);
                bodyHasAlpha = mat.HasProperty(BubbleAlphaId);
                if (bodyHasAlpha)
                    bodyAlphaRest = mat.GetFloat(BubbleAlphaId);
            }
        }

        private void ArmBubbleTap()
        {
            TweenBubbleScoreTo(pendingScore, 0.18f);
            awaitingBubbleTap = true;
        }

        private void ExplodeBubbleFromTap()
        {
            awaitingBubbleTap = false;
            DOTween.Kill(FullBeatTweenId);
            BeginPayoff();
        }

        /// <summary>
        /// The single peak: charge, squeeze, hold, burst, and the score lands where
        /// the bubble was. Nothing here waits on input.
        /// </summary>
        private void PlayFinale()
        {
            finaleComplete = false;
            finaleShaping = true;
            pendingScore = ResolveFinalScore(pendingScore);
            CompleteGlyphScoreCount();

            DOTween.Kill(FinaleTweenId);
            DOTween.Kill(FullBeatTweenId);
            DOTween.Kill(BubbleExplodeTweenId);

            var breath = Mathf.Clamp(fullPayoffDelay, 0.05f, 0.35f);
            var seq = DOTween.Sequence().SetId(FinaleTweenId);
            seq.AppendInterval(breath);
            seq.AppendCallback(PlayFullCharge);
            seq.AppendInterval(fullChargeDuration);
            seq.AppendCallback(PlayCompression);
            seq.AppendInterval(compressionDuration);
            seq.AppendCallback(PlayTensionHold);
            seq.AppendInterval(tensionHold);
            seq.AppendCallback(BurstBubble);
            seq.AppendInterval(burstDuration + scoreSlamDelay);
            seq.AppendCallback(SlamFinalScore);
            seq.AppendInterval(0.34f);
            seq.OnComplete(() =>
            {
                finaleShaping = false;
                finaleComplete = true;
                OnScorePresented();
            });
        }

        private void PlayFullCharge()
        {
            DOTween.Kill(ChargeTweenId);
            ApplyCharge(1f);
            PlayFullBubbleFlash();

            if (sphereTransform == null)
                return;

            DOTween.Kill(ImpactTweenId);
            DOTween.Kill(FinaleShapeTweenId);
            sphereTransform.localRotation = Quaternion.identity;
            sphereTransform
                .DOScale(GetChargeRestScale(true) * 1.06f, fullChargeDuration)
                .SetEase(Ease.OutQuad)
                .SetId(FinaleShapeTweenId);
        }

        private void PlayCompression()
        {
            VictorySequenceAudio.Play(audioService, Audio.SfxStreakCharge, 0.85f);
            if (sphereTransform == null)
                return;

            var rest = GetChargeRestScale(true);
            var squeezed = new Vector3(rest.x * 1.16f, rest.y * 0.82f, rest.z);
            DOTween.Kill(FinaleShapeTweenId);
            sphereTransform
                .DOScale(squeezed, compressionDuration)
                .SetEase(Ease.InQuad)
                .SetId(FinaleShapeTweenId);
        }

        private void PlayTensionHold()
        {
            if (sphereTransform == null)
                return;

            DOTween.Kill(FinaleShapeTweenId);
            sphereTransform
                .DOShakeScale(tensionHold, new Vector3(0.035f, 0.035f, 0f), 16, 0f, false)
                .SetId(FinaleShapeTweenId);
        }

        private void BurstBubble()
        {
            VictorySequenceAudio.Play(audioService, Audio.SfxStreak, 0.95f);
            var burstPos = Sphere != null ? Sphere.position : transform.position;
            var diameter = Sphere != null ? Sphere.lossyScale.x : 1f;

            labels?.ShowBackgroundDim();
            miniBubbles?.StopAll();
            burstFx?.Play(burstPos, diameter, sortingOrder, Sphere);
            PlayDelayedLandRipple(burstPos);
            PlayCameraPunch();
            interiorMotes?.Stop();

            if (bubbleScore != null)
                bubbleScore.alpha = 0f;

            if (sphereTransform == null)
                return;

            DOTween.Kill(FinaleShapeTweenId);
            var rest = GetChargeRestScale(true);
            var popScale = new Vector3(rest.x * 1.7f, rest.y * 1.3f, rest.z);
            var seq = DOTween.Sequence().SetId(BubbleExplodeTweenId);
            seq.Append(
                sphereTransform.DOScale(popScale, RuptureDuration).SetEase(Ease.OutQuad)
            );
            seq.Join(
                DOTween
                    .To(
                        () => glossFade,
                        value =>
                        {
                            glossFade = value;
                            bodyFade = value;
                            ApplyGlossCharge(Mathf.Clamp01(chargeValue), glossFlashMul);
                            ApplyBodyCharge(Mathf.Clamp01(chargeValue));
                        },
                        0f,
                        RuptureDuration
                    )
                    .SetEase(Ease.InQuad)
            );
            seq.OnComplete(() => SetBubbleShellVisible(false));
        }

        private void PlayDelayedLandRipple(Vector3 burstPos)
        {
            DOTween.Kill(BurstRippleDelayId);
            DOTween
                .Sequence()
                .SetId(BurstRippleDelayId)
                .AppendInterval(0.04f)
                .OnComplete(() =>
                    landFx?.PlayWorld(burstPos, GetRippleFit() * 1.15f, sortingOrder + 44, 0.55f)
                );
        }

        private void SlamFinalScore()
        {
            EnsureBubbleScore();
            DOTween.Kill(BubbleScoreTweenId);
            SetBubbleScoreDisplay(pendingScore);
            labels?.ShowTitle();

            if (bubbleScore == null)
                return;

            var slam = GetSlamScale();
            var scoreTransform = bubbleScore.transform;
            bubbleScore.gameObject.SetActive(true);
            bubbleScore.alpha = 0f;
            scoreTransform.localScale = slam * 0.55f;

            var seq = DOTween.Sequence().SetId(BubbleScoreTweenId);
            seq.Append(scoreTransform.DOScale(slam * 1.22f, 0.08f).SetEase(Ease.OutQuad));
            seq.Join(bubbleScore.DOFade(1f, 0.08f).SetEase(Ease.OutQuad));
            seq.AppendCallback(() =>
            {
                VictorySequenceAudio.Play(audioService, Audio.SfxStreak, 1f);
                scoreShine?.PlayBurst(
                    scoreTransform.position,
                    MeasureScoreWidth(),
                    sortingOrder + 49
                );
            });
            seq.Append(scoreTransform.DOScale(slam * 0.95f, 0.12f).SetEase(Ease.OutQuad));
            seq.Append(scoreTransform.DOScale(slam, 0.12f).SetEase(Ease.OutQuad));
            seq.OnComplete(() => BeginScoreIdle(slam));
        }

        private void BeginScoreIdle(Vector3 slam)
        {
            if (bubbleScore == null)
                return;

            DOTween.Kill(ScoreIdleTweenId);
            DOTween.Kill(ScoreGoldTweenId);
            ApplyBubbleScoreLook();

            var scoreTransform = bubbleScore.transform;
            scoreTransform
                .DOScale(slam * 1.04f, 1.15f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetId(ScoreIdleTweenId);

            var tune = scoreTune != null ? scoreTune : new ScorePopupTune();
            var restTop = tune.goldTop;
            var hotTop = Color.Lerp(restTop, Color.white, 0.45f);
            var restBottom = tune.goldBottom;
            var hotBottom = Color.Lerp(restBottom, tune.bonusTop, 0.4f);
            var pulse = 0f;
            DOTween
                .To(
                    () => pulse,
                    value =>
                    {
                        pulse = value;
                        if (bubbleScore == null)
                            return;
                        var top = Color.Lerp(restTop, hotTop, value);
                        var bottom = Color.Lerp(restBottom, hotBottom, value);
                        bubbleScore.colorGradient = new VertexGradient(top, top, bottom, bottom);
                    },
                    1f,
                    1.15f
                )
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetId(ScoreGoldTweenId);

            scoreShine?.PlayIdle(scoreTransform.position, MeasureScoreWidth(), sortingOrder + 49);
        }

        private float MeasureScoreWidth()
        {
            if (bubbleScore == null)
                return 1.2f;

            bubbleScore.ForceMeshUpdate();
            var size = bubbleScore.GetRenderedValues(false);
            var lossy = bubbleScore.transform.lossyScale.x;
            return Mathf.Max(0.45f, size.x * Mathf.Abs(lossy));
        }

        private void PlayCameraPunch()
        {
            var cam = Camera.main;
            if (cam == null || DOTween.IsTweening(CameraPunchTweenId))
                return;

            var camTransform = cam.transform;
            var rest = camTransform.localPosition;
            camTransform
                .DOPunchPosition(new Vector3(0f, -0.11f, 0f), 0.3f, 9, 0.9f)
                .SetId(CameraPunchTweenId)
                .OnKill(() =>
                {
                    if (camTransform != null)
                        camTransform.localPosition = rest;
                });
        }

        private void EnsureBubbleScore()
        {
            if (bubbleScore == null)
            {
                var scoreTransform = transform.Find("Score");
                if (scoreTransform != null)
                    bubbleScore = scoreTransform.GetComponent<TextMeshPro>();
            }

            if (bubbleScore == null)
                return;

            if (!didCacheScoreScale)
            {
                didCacheScoreScale = true;
                bubbleScoreRestScale = bubbleScore.transform.localScale;
                if (bubbleScoreRestScale.sqrMagnitude < 0.000001f)
                    bubbleScoreRestScale = Vector3.one;
            }

            if (scoreFont != null && bubbleScore.font != scoreFont)
                bubbleScore.font = scoreFont;

            var renderer = bubbleScore.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sortingLayerID = CardSortingLayerId;
                renderer.sortingOrder = sortingOrder + 55;
            }

            ApplyBubbleScoreLook();
        }

        private void ApplyBubbleScoreLook()
        {
            if (bubbleScore == null)
                return;

            var tune = scoreTune != null ? scoreTune : new ScorePopupTune();
            bubbleScore.color = Color.white;
            bubbleScore.enableVertexGradient = true;
            bubbleScore.colorGradient = new VertexGradient(
                tune.goldTop,
                tune.goldTop,
                tune.goldBottom,
                tune.goldBottom
            );

            var mat = bubbleScore.fontMaterial;
            if (mat == null)
                return;

            mat.DisableKeyword("OUTLINE_ON");
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetFloat("_OutlineWidth", 0f);
            mat.SetFloat("_FaceDilate", Mathf.Clamp(tune.faceDilate, 0f, 0.6f));
            mat.SetColor("_UnderlayColor", tune.goldShade);
            mat.SetFloat("_UnderlayOffsetX", Mathf.Clamp(tune.shadeOffsetX, -1f, 1f));
            mat.SetFloat("_UnderlayOffsetY", Mathf.Clamp(tune.shadeOffsetY, -1f, 1f));
            mat.SetFloat("_UnderlayDilate", Mathf.Clamp01(tune.shadeDilate));
            mat.SetFloat("_UnderlaySoftness", Mathf.Clamp01(tune.shadeSoftness));
            bubbleScore.UpdateMeshPadding();
        }

        private void HideBubbleScore()
        {
            glyphScoreActive = false;
            bubbleScoreDisplayed = 0;
            DOTween.Kill(BubbleScoreTweenId);
            DOTween.Kill(ScoreIdleTweenId);
            DOTween.Kill(ScoreGoldTweenId);
            scoreShine?.StopAll();
            EnsureBubbleScore();
            if (bubbleScore == null)
                return;

            bubbleScore.text = "0";
            bubbleScore.alpha = 1f;
            bubbleScore.transform.localScale = bubbleScoreRestScale * scoreCollectScale;
            bubbleScore.gameObject.SetActive(false);
        }

        private int ResolveFinalScore(int armedScore)
        {
            if (armedScore > 0)
                return armedScore;

            var live = pointsService != null ? pointsService.Points.Value : 0;
            if (live > 0)
                return live;

            return defaultVictoryScore > 0 ? defaultVictoryScore : 500;
        }

        private void NudgeGlyphScoreFromAbsorb(int absorbedCount, int totalCount)
        {
            if (pendingScore <= 0)
                return;

            var total = Mathf.Max(1, totalCount);
            var floor = Mathf.RoundToInt(pendingScore * (absorbedCount / (float)total));
            if (floor > bubbleScoreDisplayed)
                SetBubbleScoreDisplay(floor);
        }

        private void TweenBubbleScoreTo(int target, float duration, Action onComplete = null)
        {
            EnsureBubbleScore();
            if (bubbleScore == null)
            {
                onComplete?.Invoke();
                return;
            }

            bubbleScore.gameObject.SetActive(true);
            DOTween.Kill(BubbleScoreTweenId);
            bubbleScoreTween = DOTween
                .To(
                    () => bubbleScoreDisplayed,
                    SetBubbleScoreDisplay,
                    Mathf.Max(0, target),
                    Mathf.Max(0.06f, duration)
                )
                .SetEase(Ease.OutQuad)
                .SetId(BubbleScoreTweenId)
                .OnComplete(() => onComplete?.Invoke());
        }

        private void SetBubbleScoreDisplay(int value)
        {
            bubbleScoreDisplayed = value;
            if (bubbleScore == null)
                return;

            bubbleScore.text = bubbleScoreDisplayed.ToString("N0");
        }

        private void SetBubbleShellVisible(bool visible)
        {
            bubbleShellHidden = !visible;
            if (sphereTransform != null)
            {
                if (visible)
                {
                    glossFade = 1f;
                    bodyFade = 1f;
                    sphereTransform.localScale = sphereRestScale;
                    sphereTransform.gameObject.SetActive(true);
                }
                else
                {
                    sphereTransform.localScale = sphereRestScale;
                    sphereTransform.gameObject.SetActive(false);
                }
            }
        }

        private void PlayFullBubbleFlash()
        {
            ApplyCharge(1f);
            VictorySequenceAudio.Play(audioService, Audio.SfxStreak, 0.72f);

            DOTween.Kill(SpawnFlashTweenId);
            glossFlashMul = 1.55f;
            ApplyGlossCharge(1f, glossFlashMul);
            DOTween
                .To(
                    () => glossFlashMul,
                    value =>
                    {
                        glossFlashMul = value;
                        ApplyGlossCharge(Mathf.Clamp01(chargeValue), glossFlashMul);
                    },
                    1f,
                    0.28f
                )
                .SetEase(Ease.OutQuad)
                .SetId(SpawnFlashTweenId);
        }

        private void PlaySpawnEntranceFlash()
        {
            DOTween.Kill(SpawnFlashTweenId);
            glossFlashMul = 1.14f;
            ApplyGlossCharge(0.14f, glossFlashMul);
            DOTween
                .To(
                    () => glossFlashMul,
                    value =>
                    {
                        glossFlashMul = value;
                        ApplyGlossCharge(Mathf.Clamp01(chargeValue), glossFlashMul);
                    },
                    1f,
                    0.24f
                )
                .SetEase(Ease.OutQuad)
                .SetId(SpawnFlashTweenId);
        }

        private void ClearGlossPropertyBlocks()
        {
            glossFlashMul = 1f;
            impactShaderStrength = 0f;
            if (glossRenderer != null)
                glossRenderer.SetPropertyBlock(null);
            if (interiorRenderer != null)
                interiorRenderer.SetPropertyBlock(null);
        }

        private MeshRenderer FindBodyRenderer()
        {
            var candidates = GetComponentsInChildren<MeshRenderer>(true);
            for (var i = 0; i < candidates.Length; i++)
            {
                if (!UsesGlossShader(candidates[i]))
                    return candidates[i];
            }

            return null;
        }

        private static bool UsesGlossShader(Renderer renderer)
        {
            if (renderer == null)
                return false;

            var mats = renderer.sharedMaterials;
            for (var i = 0; i < mats.Length; i++)
            {
                var mat = mats[i];
                if (mat != null && mat.shader != null && mat.shader.name == GlossShaderName)
                    return true;
            }

            return false;
        }

        private void PlaySpawnPunch()
        {
            DOTween.Kill(SpawnTweenId);
            var startMul = Mathf.Clamp(spawnStartScale, 0.55f, 0.95f);
            var peakMul = Mathf.Clamp(spawnPeakScale, 1f, 1.2f);
            var rise = spawnPunchDuration * 0.58f;
            var settle = spawnPunchDuration * 0.42f;

            transform.localRotation = Quaternion.Euler(0f, 0f, -10f);
            transform.localScale = restScale * startMul;

            if (sphereTransform != null)
                sphereTransform.localScale = sphereRestScale;

            var seq = DOTween.Sequence().SetId(SpawnTweenId);
            seq.Append(
                transform
                    .DOScale(restScale * peakMul, rise)
                    .SetEase(Ease.OutCubic)
            );
            seq.Join(
                transform.DOLocalRotate(Vector3.zero, rise + settle).SetEase(Ease.OutQuad)
            );
            seq.Append(
                transform.DOScale(restScale, settle).SetEase(Ease.OutQuad)
            );
            seq.InsertCallback(rise * 0.85f, PlaySpawnEntranceFlash);

            seq.OnComplete(() =>
            {
                transform.localScale = restScale;
                VictorySequenceAudio.Play(audioService, Audio.SfxStreakCharge, spawnStreakChargeVolume);
            });
        }

        private void PlayGummySquash(bool payoff, Vector3 hitWorld)
        {
            var punchRoot = sphereTransform != null ? sphereTransform : transform;
            var punchRest = GetChargeRestScale(punchRoot == sphereTransform);
            DOTween.Kill(ImpactTweenId);
            punchRoot.localScale = punchRest;
            punchRoot.localRotation = Quaternion.identity;

            var toward = hitWorld - punchRoot.position;
            var tiltZ = toward.x >= 0f ? -7f : 7f;
            if (payoff)
                tiltZ *= 1.35f;

            var seq = DOTween.Sequence().SetId(ImpactTweenId);
            seq.OnKill(() => RestoreImpactAfterSquash(punchRoot, punchRest, payoff));
            seq.OnComplete(() => RestoreImpactAfterSquash(punchRoot, punchRest, payoff));

            if (payoff)
            {
                var gulp = punchRest * 0.9f;
                seq.Append(punchRoot.DOScale(gulp, 0.07f).SetEase(Ease.InQuad));
                seq.Append(punchRoot.DOScale(punchRest, 0.16f).SetEase(Ease.OutBack, 2.4f));
            }
            else
            {
                var squash = new Vector3(punchRest.x * 1.07f, punchRest.y * 0.93f, punchRest.z);
                seq.Append(
                    punchRoot
                        .DOScale(squash, 0.05f)
                        .SetEase(Ease.OutQuad)
                        .SetLoops(2, LoopType.Yoyo)
                );
            }

            seq.Join(
                punchRoot
                    .DOLocalRotate(new Vector3(0f, 0f, tiltZ), 0.06f)
                    .SetEase(Ease.OutQuad)
                    .SetLoops(2, LoopType.Yoyo)
            );
        }

        private static void RestoreImpact(Transform punchRoot, Vector3 punchRest)
        {
            if (punchRoot == null)
                return;

            punchRoot.localScale = punchRest;
            punchRoot.localRotation = Quaternion.identity;
        }

        private Vector3 GetChargeRestScale(bool sphere)
        {
            if (sphere)
            {
                var mul = 1f + Mathf.Clamp01(chargeValue) * chargeScaleBoost;
                return sphereRestScale * mul;
            }

            return restScale;
        }

        private void RestoreImpactAfterSquash(Transform punchRoot, Vector3 punchRest, bool payoff)
        {
            if (punchRoot == null)
                return;

            punchRoot.localRotation = Quaternion.identity;
            if (payoff && punchRoot == sphereTransform)
                punchRoot.localScale = GetChargeRestScale(true);
            else
                punchRoot.localScale = punchRest;
        }

        private void PlayInteriorDrift()
        {
            if (interiorShell == null)
                return;

            DOTween.Kill(InteriorSpinId);
            interiorShell.localRotation = Quaternion.identity;
            interiorShell
                .DOLocalRotate(new Vector3(8f, 360f, -6f), 24f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetId(InteriorSpinId);
        }

        private struct AbsorbSlot
        {
            public Transform Slot;
        }
    }
}
