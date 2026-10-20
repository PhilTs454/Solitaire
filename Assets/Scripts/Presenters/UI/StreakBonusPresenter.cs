using System.Collections.Generic;
using DG.Tweening;
using Solitaire.Helpers;
using Solitaire.Models;
using Solitaire.Services;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Zenject;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Fills Well_0..Well_4 orbs with a punch, and pulses StreakGlow as an expanding ring while count == 4.
    /// At 4 the ribbon also breathes (tiny scale) with a synced material shine.
    /// At 4 the whole Streak pill does a rubber pop: pull big, snap smaller, rest.
    /// At 5 the panel pays off: orb flash, pill punch, glow flare, spark spray.
    /// Sparks at 4 are the same idea as home motes: pooled UI Images that fall and fade.
    /// </summary>
    public class StreakBonusPresenter : MonoBehaviour
    {
        private const string GlowTweenId = "StreakGlow";
        private const string RibbonTweenId = "StreakRibbon";
        private const string BurstTweenId = "StreakBurst";
        private const string PanelTweenId = "StreakPanel";
        private const string PanelFoilTweenId = "StreakPanelFoil";
        private const string OrbTweenIdPrefix = "StreakOrb";
        private const float FinishSpinDegrees = 70f;
        private const string SparkCoreShaderName = "Solitaire/UISparkCore";
        private const string RibbonShineShaderName = "Solitaire/UIRibbonShine";
        private static readonly int ShineAmountId = Shader.PropertyToID("_ShineAmount");

        [Header("Panel (punch at 4 orbs and again at 5)")]
        [Tooltip("The Streak pill. Leave empty to use the child named Pill.")]
        [SerializeField]
        private RectTransform streakPanel;

        [Tooltip("How long the whole rubber pop lasts, in seconds.")]
        [SerializeField]
        private float panelPopLifetime = 0.42f;

        [Tooltip("Relative time of the first pull. Bigger = the stretch lasts longer.")]
        [SerializeField]
        private float panelFirstDuration = 0.24f;

        [Tooltip("Relative time of the smaller bounce back. Bigger = the settle lasts longer.")]
        [SerializeField]
        private float panelSecondDuration = 0.18f;

        [Tooltip("How far the first pull goes. 1.14 = 14% bigger. This is the 'someone pulled it'.")]
        [SerializeField]
        private float panelFirstPeak = 1.14f;

        [Tooltip("0 = rest scale, 1 = First Peak. Draw a hump: up then back to 0.")]
        [SerializeField]
        private AnimationCurve panelFirstCurve;

        [Tooltip("How far the second bounce goes. Below 1 = smaller than rest, like a rubber ball overshoot.")]
        [SerializeField]
        private float panelSecondPeak = 0.93f;

        [Tooltip("0 = rest scale, 1 = Second Peak. Draw a smaller hump: dip (or bump) then back to 0.")]
        [SerializeField]
        private AnimationCurve panelSecondCurve;

        [Header("Complete burst (5th orb)")]
        [Tooltip("Wait before the payoff. Match Card AnimationDuration so it fires as the card lands.")]
        [SerializeField]
        private float burstDelay = 0.5f;

        [Tooltip("Gap between each orb's flash in the left-to-right sweep.")]
        [SerializeField]
        private float chainStagger = 0.045f;

        [Tooltip("How long one orb's flash-out takes.")]
        [SerializeField]
        private float chainStep = 0.07f;

        [Tooltip("How big an orb swells during its flash.")]
        [SerializeField]
        private float chainPunch = 1.35f;

        [Tooltip("Payoff flare multiplies Glow Size Max. 1.4 = 40% wider and taller than Max.")]
        [SerializeField]
        private float flareSizeScale = 1.4f;

        [SerializeField]
        private float flareDuration = 0.35f;

        [Tooltip("Sparks fired outward all at once on the payoff.")]
        [SerializeField]
        private int burstSparkCount = 14;

        [SerializeField]
        private float burstAngleMin = 15f;

        [SerializeField]
        private float burstAngleMax = 165f;

        [SerializeField]
        private float burstAngleJitter = 12f;

        [SerializeField]
        private float burstDistanceMin = 60f;

        [SerializeField]
        private float burstDistanceMax = 130f;

        [SerializeField]
        private float burstSparkLifetime = 0.55f;

        [SerializeField]
        private float burstSparkStartScale = 0.9f;

        [SerializeField]
        private float burstSparkEndScale = 0.15f;

        [Tooltip(
            "Shift the 5th-orb spark spray from StreakGlow's pivot, in canvas pixels. +X is right, +Y is up. Use this after moving the pill."
        )]
        [SerializeField]
        private Vector2 burstSpawnOffset = Vector2.zero;

        [Tooltip("Random scatter around Burst Spawn Offset where burst sparks are born.")]
        [SerializeField]
        private float burstSpawnJitter = 24f;

        [Header("Panel glow burst (5th orb)")]
        [Tooltip("Sibling on top of the pill. Sprite: PanelGlowBlured. Use the default UI material.")]
        [SerializeField]
        private Image panelFoilImage;

        [Tooltip("Optional. PanelGlowBluredOutline — same width/height tween as StreakGlow.")]
        [SerializeField]
        private Image panelFoilOutlineImage;

        [Tooltip("Alpha while the burst is visible. Lower = more of the pill shows through.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float panelFoilAlpha = 0.35f;

        [Tooltip("Child sprite under GlowBurst. Leave empty to use the first child Image.")]
        [SerializeField]
        private RectTransform panelFoilChild;

        [Tooltip("End scale of that child. 1.5 = 50% bigger. Runs at the same time as the glow size tween.")]
        [SerializeField]
        private float panelFoilChildScale = 1.5f;

        [Header("Drain")]
        [Tooltip("Gap between orbs as a spent streak empties, right to left.")]
        [SerializeField]
        private float drainStagger = 0.05f;

        [SerializeField]
        private float orbHideDuration = 0.1f;

        [Header("Glow (4th pip only)")]
        [SerializeField]
        private RectTransform streakGlow;

        [SerializeField]
        private Image streakGlowImage;

        [Tooltip("Width and height of the ring when it appears. Match the pill.")]
        [SerializeField]
        private Vector2 glowSizeMin = new Vector2(270f, 75f);

        [Tooltip("Width and height the ring grows to before it fades out.")]
        [SerializeField]
        private Vector2 glowSizeMax = new Vector2(280f, 80f);

        [Tooltip("Kept so old scene values still load. The pulse starts at Glow Alpha Max.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float glowAlphaMin = 0.5f;

        [Tooltip("Alpha when the ring appears. It always fades to 0 as it grows.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float glowAlphaMax = 0.9f;

        [Tooltip("How long one ping lasts: grow and fade.")]
        [SerializeField]
        private float glowPulseDuration = 0.7f;

        [Tooltip("Wait after the ring vanishes before the next ping.")]
        [SerializeField]
        private float glowPulseGap = 0.2f;

        [Tooltip("Extra glow. Alpha 0 until 4 orbs, then 0.5.")]
        [SerializeField]
        private Image secondaryGlowImage;

        [Header("Ribbon charge (4th pip)")]
        [Tooltip("Ribbon root (scales the badge +50 together). Leave empty to find child named Ribbon.")]
        [SerializeField]
        private RectTransform ribbon;

        [Tooltip("Ribbon Image using Solitaire/UIRibbonShine. Leave empty to use the first Image under Ribbon.")]
        [SerializeField]
        private Image ribbonImage;

        [Tooltip("Peak scale of the breath. 1.05 = 5% bigger.")]
        [SerializeField]
        private float ribbonBreathPeak = 1.05f;

        [Tooltip("One inhale (or exhale). Full breath in+out is twice this. Match Glow Pulse Duration for the same rhythm.")]
        [SerializeField]
        private float ribbonPulseDuration = 0.8f;

        [Tooltip("TMP +N on the ribbon (BonusValue). Leave empty to find under Ribbon.")]
        [SerializeField]
        private TextMeshProUGUI ribbonBonusLabel;

        [Tooltip("Ribbon/BackgroundShine — sun-ray + star flash after +N fades. Leave empty to find by name.")]
        [SerializeField]
        private GameObject ribbonBackgroundShine;

        [Tooltip("BackgroundShine/Shine — sun-ray sprite. Scales 0 → peak, slow Z spin.")]
        [SerializeField]
        private RectTransform ribbonRayShine;

        [Tooltip("BackgroundShine/Shine2 — second ray layer. Same scale + spin as Shine.")]
        [SerializeField]
        private RectTransform ribbonRayShine2;

        [Tooltip("BackgroundShine/StarImage — spins on Z during the flash.")]
        [SerializeField]
        private Image ribbonStarImage;

        [Tooltip("BackgroundShine/StarImageWhite — alpha pulse 0 → 1 → 0.")]
        [SerializeField]
        private Image ribbonStarImageWhite;

        [Tooltip("How long the sun-ray / star flash lasts. Shorter = punchier.")]
        [SerializeField]
        private float ribbonBackgroundShineDuration = 0.22f;

        [Tooltip("End scale of Shine / Shine2.")]
        [SerializeField]
        private float ribbonRayShineScale = 1.5f;

        [Tooltip("Shine / Shine2 alpha when spawned (0.5 = 50%). Fades to 0 by end.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float ribbonRayStartAlpha = 0.5f;

        [Tooltip("StarImage Z rotation over the flash lifetime (degrees).")]
        [SerializeField]
        private float ribbonStarSpinDegrees = 120f;

        [Tooltip("StarImage scale over flash life (X = 0..1 time, Y = scale). e.g. 1 → 1.4 → 1.1.")]
        [SerializeField]
        private AnimationCurve ribbonStarScaleOverLife;

        [Tooltip("Shine / Shine2 slow Z rotation over the flash lifetime (degrees).")]
        [SerializeField]
        private float ribbonRaySpinDegrees = 35f;

        [Tooltip("Fraction of flash life stars stay full before fading (0.5 = first half).")]
        [SerializeField]
        [Range(0f, 0.95f)]
        private float ribbonStarHoldNormalized = 0.5f;

        [Tooltip("Fraction of flash life for StarImageWhite fade-in (within the hold).")]
        [SerializeField]
        [Range(0.01f, 0.5f)]
        private float ribbonStarWhiteFadeInNormalized = 0.15f;

        [Tooltip("StarImage alpha at start of flash.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float ribbonStarStartAlpha = 1f;

        [Tooltip("StarImageWhite peak alpha after fade-in.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float ribbonStarWhitePeakAlpha = 1f;

        [Header("Ribbon payoff (5th pip)")]
        [Tooltip("Brief whole-ribbon scale punch before the +N flies off.")]
        [SerializeField]
        private float ribbonPayoffPunchScale = 1.16f;

        [SerializeField]
        private float ribbonPayoffPunchUp = 0.12f;

        [SerializeField]
        private float ribbonPayoffPunchDown = 0.1f;

        [Tooltip("How big the old +N grows while fading out.")]
        [SerializeField]
        private float ribbonTextOutScale = 1.7f;

        [SerializeField]
        private float ribbonTextOutDuration = 0.28f;

        [Tooltip(
            "Start BackgroundShine this many seconds before the old +N finishes fading. 0 = wait until +N is gone (and until the pill burst beat). 0.2 = shine begins while +N is still disappearing."
        )]
        [SerializeField]
        private float ribbonShineOverlap = 0.2f;

        [Tooltip(
            "Start ribbon BonusValue / BackgroundShine this many seconds before the glow + pill mote burst. Lets +N finish so both mote bursts share one beat."
        )]
        [SerializeField]
        private float ribbonPayoffLead = 0.4f;

        [Tooltip("Weak left/right rattle on the ribbon image (degrees Z).")]
        [SerializeField]
        private float ribbonRattleAngle = 7f;

        [SerializeField]
        private float ribbonRattleDuration = 0.28f;

        [Tooltip("New +N pops to this scale, then settles to 1.")]
        [SerializeField]
        private float ribbonTextPopOvershoot = 1.05f;

        [SerializeField]
        private float ribbonTextPopInDuration = 0.16f;

        [SerializeField]
        private float ribbonTextPopSettle = 0.08f;

        [Tooltip("Motes when BackgroundShine rays appear (same as 5th-orb burst, then fall). 5–8.")]
        [SerializeField]
        [Range(5, 8)]
        private int ribbonTextBurstCount = 6;

        [Tooltip("Spawn offset from StarImage centre, in canvas pixels. +X right, +Y up.")]
        [SerializeField]
        private Vector2 ribbonTextBurstOffset = Vector2.zero;

        [Header("Orbs (Well_0/Orb ... Well_4/Orb)")]
        [SerializeField]
        private Image[] orbs = new Image[5];

        [SerializeField]
        private float orbPunch = 1.18f;

        [SerializeField]
        private float orbPopDuration = 0.08f;

        [SerializeField]
        private float orbSettleDuration = 0.12f;

        [Header("Orb burst ring")]
        [Tooltip("Leave empty to use the same soft ring as the card drop ripple. Drop a PNG here later if you prefer.")]
        [SerializeField]
        private Sprite burstRingSprite;

        [Tooltip("Tint on the ring. White keeps the procedural ring as-is. Use white if you assign a colored PNG.")]
        [SerializeField]
        private Color burstColor = new Color(1f, 1f, 1f, 0.85f);

        [Tooltip("Ring size vs the orb when it appears. 1 = same as the orb.")]
        [SerializeField]
        private float burstStartScale = 0.6f;

        [Tooltip("Ring size vs the orb at the end of the pop.")]
        [SerializeField]
        private float burstEndScale = 2.4f;

        [SerializeField]
        private float burstDuration = 0.32f;

        [Header("Orb spawn shine")]
        [Tooltip("Soft flash that pops with the orb. Leave empty to reuse the orb sprite.")]
        [SerializeField]
        private Sprite shineSprite;

        [Tooltip("Tint for the flash. Brighter/whiter than the orb reads as a brief shine.")]
        [SerializeField]
        private Color shineColor = new Color(1f, 0.78f, 0.96f, 0.95f);

        [Tooltip("How long the flash lasts. Keep shorter than the ring so the shine is a blink.")]
        [SerializeField]
        private float shineDuration = 0.18f;

        [Tooltip("Extra bloom on the flash vs the orb. 1 = same size. 1.15 = a little bigger.")]
        [SerializeField]
        private float shineEndScale = 1.15f;

        [Header("Sparks (4th pip, same as home motes)")]
        [Tooltip("Child (or sibling) named ShineRoot. Sparks spawn here.")]
        [SerializeField]
        private RectTransform shineRoot;

        [Tooltip("Burst sparks (5th orb) and fallback if Frame 01 is empty.")]
        [SerializeField]
        private Sprite sparkSprite;

        [Tooltip("star_01 — plus-star. Falling sparks start here.")]
        [SerializeField]
        private Sprite sparkFrame01;

        [Tooltip("star_04 — sharper star. Middle of the clip.")]
        [SerializeField]
        private Sprite sparkFrame04;

        [Tooltip("star_08 — 4 rays. 3rd frame of a finished clip.")]
        [SerializeField]
        private Sprite sparkFrame08;

        [Tooltip("4th frame of a finished clip (after star_08). Leave empty to skip.")]
        [FormerlySerializedAs("sparkReticle01")]
        [SerializeField]
        private Sprite sparkFrame4;

        [Tooltip("5th frame of a finished clip.")]
        [FormerlySerializedAs("sparkReticle02")]
        [SerializeField]
        private Sprite sparkFrame5;

        [Tooltip("6th frame of a finished clip.")]
        [FormerlySerializedAs("sparkReticle03")]
        [SerializeField]
        private Sprite sparkFrame6;

        [Tooltip("How long frame 4 stays on. 1 = same as star_01. Lower = shorter flash.")]
        [SerializeField]
        private float sparkFrame4Hold = 0.55f;

        [Tooltip("How long frame 5 stays on. 1 = same as star_01.")]
        [SerializeField]
        private float sparkFrame5Hold = 1.15f;

        [Tooltip("How long frame 6 stays on. Highest of the three so the last frame lingers.")]
        [SerializeField]
        private float sparkFrame6Hold = 1.45f;

        [Tooltip("Chance a falling spark plays the full clip (01 → 04 → 08 → extra frames). 0.35 = about 1 in 3.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float sparkFinishChance = 0.35f;

        [Tooltip("How big a finished spark grows over its life. 1.25 = 25% bigger.")]
        [SerializeField]
        private float sparkFinishEndScale = 1.25f;

        [Tooltip("Tint on the rays only for frames 01 / 04 / 08. The hot white core is left alone.")]
        [SerializeField]
        private Color sparkColor = new Color(1f, 0.42f, 0.82f, 1f);

        [Tooltip("Tint for frames 4–6 only. No core shader. White = show your Photoshop colors as-is.")]
        [SerializeField]
        private Color sparkLateColor = Color.white;

        [Tooltip("How hot the white middle is. 2 = twice as bright as the PNG core.")]
        [SerializeField]
        private float sparkCoreBoost = 2f;

        [Tooltip("How white a pixel must be to count as core. Higher = smaller white blob.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float sparkCoreThreshold = 0.65f;

        [SerializeField]
        private float sparkSize = 14f;

        [Tooltip("How many sparks can be alive at once.")]
        [SerializeField]
        private int sparkMax = 8;

        [Tooltip("Wait between spawns. Smaller = busier.")]
        [SerializeField]
        private float sparkCooldown = 0.18f;

        [SerializeField]
        private float sparkLifetime = 0.45f;

        [Tooltip("Fall speed in canvas pixels per second. Each spark picks a value in this range.")]
        [SerializeField]
        private float sparkFallSpeedMin = 40f;

        [SerializeField]
        private float sparkFallSpeedMax = 90f;

        [SerializeField]
        private float sparkStartScale = 0.7f;

        [Tooltip("Nudge the shared drop line up/down from the pill's bottom edge, in canvas pixels.")]
        [SerializeField]
        private float sparkSpawnYOffset = 0f;

        [Tooltip("Width of the drop line in canvas pixels. Smaller than the pill keeps stars off the rounded ends. 0 = full pill width.")]
        [SerializeField]
        private float sparkSpawnWidth = 180f;

        [Tooltip("Z spin over one lifetime, in degrees. Each spark picks a speed in this range.")]
        [SerializeField]
        private float sparkSpinMin = 140f;

        [SerializeField]
        private float sparkSpinMax = 420f;

        [Tooltip("Chance a spark grows 50% → 100% instead of shrinking. 0.4 = about 2 in 5.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float sparkGrowChance = 0.4f;

        [Tooltip("Chance a spark is 30% smaller (70% size) for its whole life.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float sparkSmallChance = 0.4f;

        [Header("Payoff to score (5th orb)")]
        [Tooltip(
            "Wait after the streak hit before STREAK +N and motes start. Lets the burst have the stage first."
        )]
        [SerializeField]
        private float payoffToScoreDelay = 0.2f;

        [Tooltip(
            "Where STREAK +N and motes are born, in canvas pixels from the pill centre. +X is right, +Y is up. Use a negative X when the pill sits on the right (spawn to the left)."
        )]
        [SerializeField]
        private Vector2 bonusSpawnOffset = new Vector2(-160f, 5f);

        [Tooltip("How many sparks fly from the pill to the score.")]
        [SerializeField]
        private int moteCount = 5;

        [Tooltip("Seconds for one mote (and the +N) to reach the score.")]
        [SerializeField]
        private float moteFlight = 0.5f;

        [Tooltip("Gap before each extra mote launches.")]
        [SerializeField]
        private float moteStagger = 0.045f;

        [Tooltip("How high the mote arc bulges, in canvas pixels.")]
        [SerializeField]
        private float moteArc = 50f;

        [Tooltip("Random spread around the spawn point, in canvas pixels.")]
        [SerializeField]
        private float moteStartJitter = 18f;

        [Tooltip("UI size of STREAK +N. Bigger = easier to spot on the pill.")]
        [SerializeField]
        private float bonusLabelFontSize = 52f;

        private const string PayTweenId = "StreakPay";
        private const string BonusLabelTweenId = "StreakBonusLabel";

        [Inject]
        private readonly IStreakService streak;

        [Inject]
        private readonly IAudioService audioService;

        [Inject]
        private readonly IScorePopup scorePopup;

        [Inject(Optional = true)]
        private readonly IScoreHud scoreHud;

        [Inject]
        private readonly Game.Config gameConfig;

        [Inject(Optional = true)]
        private readonly ScorePopupTune scoreTune;

        [Inject(Optional = true, Id = "ScorePopupFont")]
        private readonly TMP_FontAsset bonusFont;

        private readonly List<RectTransform> sparkPool = new List<RectTransform>(16);
        private readonly List<RectTransform> burstPool = new List<RectTransform>(8);
        private readonly List<RectTransform> shinePool = new List<RectTransform>(8);
        private readonly Sprite[] sparkClipScratch = new Sprite[6];
        private readonly float[] sparkDurationScratch = new float[6];
        private static Material sharedSparkCoreMaterial;
        private static bool didWarnMissingSparkShader;
        private Color[] orbBaseColors = new Color[0];
        private int shownCount;
        private bool sparksActive;
        private float sparkCooldownLeft;
        private int pendingPayDelta;
        private int motesLeft;
        private TextMeshProUGUI bonusLabel;
        private Material ribbonMatInstance;
        private float ribbonBreathT;
        private Quaternion ribbonImageRestRotation = Quaternion.identity;
        private Vector3 ribbonBonusLabelRestScale = Vector3.one;
        private bool ribbonRestsCached;

        private void Awake()
        {
            TryFindPanel();
            TryFindGlow();
            TryFindRibbon();
            TryFindOrbs();
            TryFindShineRoot();
            TryFindPanelFoilChild();
            EnsurePanelCurves();
            CacheOrbColors();
            HidePanelFoilImmediate();
            EnsureRibbonMaterial();
            SetRibbonShineAmount(0f);
            if (ribbonBackgroundShine != null)
                ribbonBackgroundShine.SetActive(false);
            RefreshRibbonBonusLabel(streak != null ? streak.NextBonus : 50, visible: true);
        }

        private void OnValidate()
        {
            EnsurePanelCurves();
            if (sharedSparkCoreMaterial != null)
            {
                sharedSparkCoreMaterial.SetFloat("_CoreBoost", Mathf.Max(0.1f, sparkCoreBoost));
                sharedSparkCoreMaterial.SetFloat("_CoreThreshold", Mathf.Clamp01(sparkCoreThreshold));
            }
        }

        private void Start()
        {
            HideAllOrbsImmediate();
            StopGlow(resetSize: true);
            StopRibbonCharge(resetVisual: true);
            SetSecondaryGlow(false);

            if (streak == null)
            {
                Debug.LogError(
                    "[StreakBonusPresenter] IStreakService was not injected. Add this to the Game scene (same context as GameInfo).",
                    this
                );
                return;
            }

            streak.Count.Subscribe(OnCountChanged).AddTo(this);
        }

        /// <summary>One-shot. Missing clips log a warning in AudioService.</summary>
        private void PlaySfx(string key, float volume)
        {
            audioService.PlaySfx(key, volume);
        }

        /// <summary>Play Mode: right-click this component → Debug/Fill 4 orbs.</summary>
        [ContextMenu("Debug/Fill 4 orbs")]
        public void DebugFillFourOrbs()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[StreakBonusPresenter] Enter Play Mode, then Fill 4 orbs.");
                return;
            }

            if (streak == null)
            {
                Debug.LogWarning("[StreakBonusPresenter] Streak service missing.");
                return;
            }

            streak.Reset();
            streak.Set(StreakService.MaxPips - 1);
        }

        /// <summary>Play Mode: right-click this component → Debug/Clear orbs.</summary>
        [ContextMenu("Debug/Clear orbs")]
        public void DebugClearOrbs()
        {
            if (!Application.isPlaying || streak == null)
                return;

            streak.Reset();
        }

        /// <summary>Play Mode: jump to a full streak so you can check the 5th-orb payoff.</summary>
        [ContextMenu("Debug/Fill 5 orbs")]
        public void DebugFillFiveOrbs()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[StreakBonusPresenter] Enter Play Mode, then Fill 5 orbs.");
                return;
            }

            if (streak == null)
            {
                Debug.LogWarning("[StreakBonusPresenter] Streak service missing.");
                return;
            }

            streak.Reset();
            streak.Set(StreakService.MaxPips - 1);
            streak.Set(StreakService.MaxPips);
        }

        private void OnDisable()
        {
            CommitPay();
            DOTween.Kill(GlowTweenId);
            DOTween.Kill(RibbonTweenId);
            DOTween.Kill(BurstTweenId);
            DOTween.Kill(PanelTweenId);
            DOTween.Kill(PanelFoilTweenId);
            DOTween.Kill(PayTweenId);
            DOTween.Kill(BonusLabelTweenId);
            HideBonusLabel();
            HidePanelFoilImmediate();
            ResetPanelScale();
            StopRibbonCharge(resetVisual: true);
            KillAllOrbTweens();
            HideAllSparks();
            HideAllBursts();
            HideAllShines();
        }

        private void Update()
        {
            if (!sparksActive)
                return;

            var dt = Time.unscaledDeltaTime;
            if (sparkCooldownLeft > 0f)
                sparkCooldownLeft -= dt;

            if (sparkCooldownLeft <= 0f && CountActiveSparks() < Mathf.Max(1, sparkMax))
            {
                EmitSpark();
                sparkCooldownLeft = Mathf.Max(0.04f, sparkCooldown);
            }
        }

        private void OnCountChanged(int count)
        {
            if (count > shownCount)
            {
                for (var i = shownCount; i < count && i < orbs.Length; i++)
                    ShowOrb(i);
            }
            else if (count < shownCount)
            {
                // A spent streak drains right to left so it reads as "cashed in".
                var stagger = shownCount >= StreakService.MaxPips ? drainStagger : 0f;
                var step = 0;
                for (var i = shownCount - 1; i >= count; i--)
                {
                    HideOrb(i, step * stagger);
                    step++;
                }
            }

            shownCount = count;

            SetSecondaryGlow(count >= StreakService.MaxPips - 1);

            if (count >= StreakService.MaxPips)
            {
                // Leave the 4-pip glow and sparks running — they're the wind-up.
                PlayComplete();
                return;
            }

            DOTween.Kill(BurstTweenId);

            if (count == StreakService.MaxPips - 1)
            {
                PunchPanel();
                PlayGlow();
                PlayRibbonCharge();
                StartSparks();
                PlaySfx(Audio.SfxStreakCharge, 0.55f);
            }
            else
            {
                StopGlow(resetSize: true);
                StopRibbonCharge(resetVisual: true);
                StopSparks();
                if (count == 0 && streak != null)
                    RefreshRibbonBonusLabel(streak.NextBonus, visible: true);
            }
        }

        /// <summary>
        /// The full-streak payoff: ribbon cash-in, orb flash, glow flare, sparks, and
        /// STREAK +N popup all share one beat. Motes still commit the HUD when they land.
        /// </summary>
        private void PlayComplete()
        {
            DOTween.Kill(BurstTweenId);

            var chainDuration = Mathf.Max(1, orbs.Length) * chainStagger;
            var delay = Mathf.Max(0f, burstDelay);
            var lead = Mathf.Clamp(ribbonPayoffLead, 0f, delay);

            var burst = DOTween.Sequence().SetId(BurstTweenId).SetTarget(this);
            // Ribbon starts `lead` seconds before the glow/pill block (still begins at burstDelay).
            var ribbonPreRoll = delay - lead;
            if (ribbonPreRoll > 0f)
                burst.AppendInterval(ribbonPreRoll);

            // From ribbon start → pill BurstSparks: lead wait + orb chain.
            var shineAtFromRibbonStart = lead + chainDuration;

            var together = DOTween.Sequence();
            together.Join(BuildRibbonPayoffSequence(shineAtFromRibbonStart));

            var fx = DOTween.Sequence();
            if (lead > 0f)
                fx.AppendInterval(lead);
            fx.AppendCallback(() =>
            {
                StopSparks();
                FlashOrbChain();
            });
            fx.AppendInterval(chainDuration);
            fx.AppendCallback(() =>
            {
                FlareGlow();
                PunchPanel();
                PlayPanelFoilBurst();
                BurstSparks();
                PlaySfx(Audio.SfxStreak, 0.7f);
            });
            fx.AppendInterval(Mathf.Max(0f, payoffToScoreDelay));
            fx.AppendCallback(PayScore);
            together.Join(fx);

            burst.Append(together);
        }

        /// <summary>
        /// Ribbon punch → old +N fades. BackgroundShine is Inserted so it can
        /// overlap the end of that fade. New +N waits until shine finishes.
        /// Pill flare stays on its own clock in PlayComplete.
        /// </summary>
        private Sequence BuildRibbonPayoffSequence(float shineAtFromStart)
        {
            var seq = DOTween.Sequence();
            TryFindRibbon();
            if (ribbon == null)
                return seq;

            var punch = Mathf.Max(1.01f, ribbonPayoffPunchScale);
            var punchUp = Mathf.Max(0.05f, ribbonPayoffPunchUp);
            var punchDown = Mathf.Max(0.05f, ribbonPayoffPunchDown);

            seq.AppendCallback(() =>
            {
                StopRibbonCharge(resetVisual: false);
                SetRibbonShineAmount(0f);
                ribbon.localScale = Vector3.one;
                if (ribbonImage != null)
                    ribbonImage.rectTransform.localRotation = ribbonImageRestRotation;
                if (ribbonBonusLabel != null)
                {
                    ribbonBonusLabel.rectTransform.localScale = ribbonBonusLabelRestScale;
                    var c = ribbonBonusLabel.color;
                    ribbonBonusLabel.color = new Color(c.r, c.g, c.b, 1f);
                }
            });

            seq.Append(ribbon.DOScale(punch, punchUp).SetEase(Ease.OutBack));
            seq.Append(ribbon.DOScale(1f, punchDown).SetEase(Ease.InOutSine));

            var textOut = Mathf.Max(0.08f, ribbonTextOutDuration);
            var textPeak = Mathf.Max(1.1f, ribbonTextOutScale);
            var exit = DOTween.Sequence();

            if (ribbonBonusLabel != null)
            {
                exit.Join(
                    ribbonBonusLabel.rectTransform
                        .DOScale(ribbonBonusLabelRestScale * textPeak, textOut)
                        .SetEase(Ease.OutCubic)
                );
                exit.Join(ribbonBonusLabel.DOFade(0f, textOut).SetEase(Ease.InQuad));
            }

            exit.Join(BuildRibbonRattle(textOut));
            seq.Append(exit);

            var fadeStart = punchUp + punchDown;
            var fadeEnd = fadeStart + textOut;
            var overlap = Mathf.Max(0f, ribbonShineOverlap);
            var shineLife = Mathf.Max(0.05f, ribbonBackgroundShineDuration);
            // 0 = original: after +N is gone, then wait for the pill burst beat.
            // >0 = start this many seconds before +N finishes (never before the fade).
            var shineAt = overlap <= 0f
                ? Mathf.Max(fadeEnd, shineAtFromStart)
                : Mathf.Max(fadeStart, fadeEnd - overlap);

            seq.Insert(shineAt, BuildRibbonBackgroundShineSequence());

            var shineEnd = shineAt + shineLife;
            if (shineEnd > fadeEnd)
                seq.AppendInterval(shineEnd - fadeEnd);

            seq.AppendCallback(() =>
            {
                var next = streak != null ? streak.NextBonus : 50;
                RefreshRibbonBonusLabel(next, visible: true);
                if (ribbonBonusLabel != null)
                {
                    ribbonBonusLabel.rectTransform.localScale = ribbonBonusLabelRestScale * 0.01f;
                    var c = ribbonBonusLabel.color;
                    ribbonBonusLabel.color = new Color(c.r, c.g, c.b, 1f);
                }

                if (ribbonImage != null)
                    ribbonImage.rectTransform.localRotation = ribbonImageRestRotation;
            });

            if (ribbonBonusLabel != null)
            {
                var overshoot = Mathf.Max(1f, ribbonTextPopOvershoot);
                var popIn = Mathf.Max(0.05f, ribbonTextPopInDuration);
                var settle = Mathf.Max(0.04f, ribbonTextPopSettle);
                seq.Append(
                    ribbonBonusLabel.rectTransform
                        .DOScale(ribbonBonusLabelRestScale * overshoot, popIn)
                        .SetEase(Ease.OutBack)
                );
                seq.Append(
                    ribbonBonusLabel.rectTransform
                        .DOScale(ribbonBonusLabelRestScale, settle)
                        .SetEase(Ease.OutSine)
                );
            }

            return seq;
        }

        /// <summary>
        /// BackgroundShine rays/stars + BonusValue mote burst on the same beat.
        /// </summary>
        private Sequence BuildRibbonBackgroundShineSequence()
        {
            var seq = DOTween.Sequence();
            TryFindRibbon();

            var life = Mathf.Max(0.05f, ribbonBackgroundShineDuration);
            var peak = Mathf.Max(1f, ribbonRayShineScale);
            var holdNorm = Mathf.Clamp(ribbonStarHoldNormalized, 0f, 0.95f);
            var hold = life * holdNorm;
            var fadeOut = Mathf.Max(0.01f, life - hold);
            var whiteFadeIn = Mathf.Min(
                life * Mathf.Clamp(ribbonStarWhiteFadeInNormalized, 0.01f, 0.5f),
                hold
            );
            var rayStartAlpha = Mathf.Clamp01(ribbonRayStartAlpha);
            var starStartAlpha = Mathf.Clamp01(ribbonStarStartAlpha);
            var whitePeak = Mathf.Clamp01(ribbonStarWhitePeakAlpha);

            // Rays + stars + motes all start together on this beat.
            seq.AppendCallback(() =>
            {
                ActivateRibbonBackgroundShinePieces();
                BurstRibbonTextSparks();

                if (ribbonRayShine != null)
                {
                    ribbonRayShine.localScale = Vector3.zero;
                    ribbonRayShine.localRotation = Quaternion.identity;
                    SetImageAlpha(ribbonRayShine.GetComponent<Image>(), rayStartAlpha);
                }

                if (ribbonRayShine2 != null)
                {
                    ribbonRayShine2.localScale = Vector3.zero;
                    ribbonRayShine2.localRotation = Quaternion.identity;
                    SetImageAlpha(ribbonRayShine2.GetComponent<Image>(), rayStartAlpha);
                }

                if (ribbonStarImage != null)
                {
                    var c = ribbonStarImage.color;
                    ribbonStarImage.color = new Color(c.r, c.g, c.b, starStartAlpha);
                    EnsureRibbonStarScaleCurve();
                    var startScale = ribbonStarScaleOverLife.Evaluate(0f);
                    ribbonStarImage.rectTransform.localScale = Vector3.one * startScale;
                    ribbonStarImage.rectTransform.localRotation = Quaternion.identity;
                }

                if (ribbonStarImageWhite != null)
                {
                    var c = ribbonStarImageWhite.color;
                    ribbonStarImageWhite.color = new Color(c.r, c.g, c.b, 0f);
                }
            });

            var flash = DOTween.Sequence();

            if (ribbonRayShine != null)
                JoinRibbonRayFlash(flash, ribbonRayShine, peak, life, rayStartAlpha);
            if (ribbonRayShine2 != null)
                JoinRibbonRayFlash(flash, ribbonRayShine2, peak, life, rayStartAlpha);

            if (ribbonStarImage != null)
            {
                var starRt = ribbonStarImage.rectTransform;
                EnsureRibbonStarScaleCurve();
                var startScale = ribbonStarScaleOverLife.Evaluate(0f);
                starRt.localScale = Vector3.one * startScale;
                flash.Join(
                    DOVirtual
                        .Float(
                            0f,
                            1f,
                            life,
                            t =>
                            {
                                var s = ribbonStarScaleOverLife.Evaluate(t);
                                starRt.localScale = Vector3.one * s;
                            }
                        )
                        .SetEase(Ease.Linear)
                );
                flash.Join(
                    starRt
                        .DOLocalRotate(
                            new Vector3(0f, 0f, ribbonStarSpinDegrees),
                            life,
                            RotateMode.FastBeyond360
                        )
                        .SetEase(Ease.OutCubic)
                );
                var starFade = DOTween.Sequence();
                starFade.AppendInterval(hold);
                starFade.Append(ribbonStarImage.DOFade(0f, fadeOut).SetEase(Ease.InQuad));
                flash.Join(starFade);
            }

            if (ribbonStarImageWhite != null)
            {
                var pulse = DOTween.Sequence();
                pulse.Append(ribbonStarImageWhite.DOFade(whitePeak, whiteFadeIn).SetEase(Ease.OutQuad));
                if (hold - whiteFadeIn > 0f)
                    pulse.AppendInterval(hold - whiteFadeIn);
                pulse.Append(ribbonStarImageWhite.DOFade(0f, fadeOut).SetEase(Ease.InQuad));
                flash.Join(pulse);
            }

            if (ribbonRayShine == null
                && ribbonRayShine2 == null
                && ribbonStarImage == null
                && ribbonStarImageWhite == null)
                flash.AppendInterval(life);

            seq.Append(flash);
            seq.AppendCallback(() =>
            {
                if (ribbonBackgroundShine != null)
                    ribbonBackgroundShine.SetActive(false);
                ResetRibbonRay(ribbonRayShine);
                ResetRibbonRay(ribbonRayShine2);

                if (ribbonStarImage != null)
                {
                    ribbonStarImage.rectTransform.localRotation = Quaternion.identity;
                    EnsureRibbonStarScaleCurve();
                    var startScale = ribbonStarScaleOverLife.Evaluate(0f);
                    ribbonStarImage.rectTransform.localScale = Vector3.one * startScale;
                }

                if (ribbonStarImageWhite != null)
                {
                    var c = ribbonStarImageWhite.color;
                    ribbonStarImageWhite.color = new Color(c.r, c.g, c.b, 0f);
                }
            });

            return seq;
        }

        private void ActivateRibbonBackgroundShinePieces()
        {
            if (ribbonBackgroundShine != null)
            {
                ribbonBackgroundShine.SetActive(true);
                ribbonBackgroundShine.transform.SetAsLastSibling();
            }

            // Children left inactive in the Hierarchy stay off when only the parent is enabled.
            if (ribbonRayShine != null)
                ribbonRayShine.gameObject.SetActive(true);
            if (ribbonRayShine2 != null)
                ribbonRayShine2.gameObject.SetActive(true);
            if (ribbonStarImage != null)
                ribbonStarImage.gameObject.SetActive(true);
            if (ribbonStarImageWhite != null)
                ribbonStarImageWhite.gameObject.SetActive(true);
        }

        private void JoinRibbonRayFlash(
            Sequence flash,
            RectTransform ray,
            float peak,
            float life,
            float startAlpha
        )
        {
            if (flash == null || ray == null)
                return;

            flash.Join(ray.DOScale(peak, life).SetEase(Ease.OutBack));
            flash.Join(
                ray.DOLocalRotate(
                        new Vector3(0f, 0f, ribbonRaySpinDegrees),
                        life,
                        RotateMode.FastBeyond360
                    )
                    .SetEase(Ease.Linear)
            );

            var image = ray.GetComponent<Image>();
            if (image != null)
            {
                SetImageAlpha(image, startAlpha);
                flash.Join(image.DOFade(0f, life).SetEase(Ease.InQuad));
            }
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null)
                return;

            var c = image.color;
            image.color = new Color(c.r, c.g, c.b, alpha);
        }

        private static void ResetRibbonRay(RectTransform ray)
        {
            if (ray == null)
                return;

            ray.localScale = Vector3.zero;
            ray.localRotation = Quaternion.identity;
            SetImageAlpha(ray.GetComponent<Image>(), 1f);
        }

        private Sequence BuildRibbonRattle(float duration)
        {
            var rattle = DOTween.Sequence();
            if (ribbonImage == null)
                return rattle;

            var rt = ribbonImage.rectTransform;
            var angle = Mathf.Max(0.5f, ribbonRattleAngle);
            var life = Mathf.Max(0.08f, duration > 0f ? duration : ribbonRattleDuration);
            var step = life / 4f;

            rattle.Append(rt.DOLocalRotate(new Vector3(0f, 0f, angle), step).SetEase(Ease.OutSine));
            rattle.Append(rt.DOLocalRotate(new Vector3(0f, 0f, -angle), step).SetEase(Ease.InOutSine));
            rattle.Append(
                rt.DOLocalRotate(new Vector3(0f, 0f, angle * 0.55f), step).SetEase(Ease.InOutSine)
            );
            rattle.Append(
                rt.DOLocalRotate(ribbonImageRestRotation.eulerAngles, step).SetEase(Ease.OutSine)
            );
            return rattle;
        }

        /// <summary>
        /// STREAK +N is born beside the pill, then motes carry it to the Points HUD.
        /// The HUD punches when the last mote lands.
        /// </summary>
        private void PayScore()
        {
            DOTween.Kill(PayTweenId);

            var bonus = 0;
            if (streak != null && streak.LastAwardedBonus > 0)
                bonus = streak.LastAwardedBonus;
            else if (gameConfig != null && gameConfig.PointsStreakBonus > 0)
                bonus = gameConfig.PointsStreakBonus;
            else
                bonus = 50;

            pendingPayDelta = bonus;
            var flight = Mathf.Max(0.12f, moteFlight);
            PlayBonusLabel(bonus, flight);

            var count = Mathf.Max(0, moteCount);
            if (count == 0 || scoreHud == null || scoreHud.PointsRect == null || FirstSparkFrame() == null)
            {
                DOVirtual.DelayedCall(flight, CommitPay).SetId(PayTweenId);
                return;
            }

            FlyMotesToScore(count, flight);
            // Motes call SetAsLastSibling — put the number back on top so they don't cover it.
            if (bonusLabel != null)
                bonusLabel.rectTransform.SetAsLastSibling();
        }

        private void FlyMotesToScore(int count, float flight)
        {
            var root = GetTravelRoot();
            var hud = scoreHud.PointsRect;
            if (root == null || hud == null)
            {
                DOVirtual.DelayedCall(flight, CommitPay).SetId(PayTweenId);
                return;
            }

            var end = UiWorldPoint(hud, Vector2.zero);
            motesLeft = count;

            for (var i = 0; i < count; i++)
            {
                var jitter = Random.insideUnitCircle * moteStartJitter;
                var start = UiWorldPoint(
                    streakPanel != null ? streakPanel : transform as RectTransform,
                    bonusSpawnOffset + jitter
                );
                var delay = i * Mathf.Max(0f, moteStagger);
                EmitPayMote(root, start, end, flight, delay);
            }
        }

        /// <summary>One spark: pill → score, with a small arc. Last one landing banks the HUD.</summary>
        private void EmitPayMote(RectTransform root, Vector3 start, Vector3 end, float flight, float delay)
        {
            var spark = GetSpark();
            spark.SetParent(root, false);
            spark.SetAsLastSibling();
            spark.pivot = new Vector2(0.5f, 0.5f);
            spark.position = start;
            spark.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            spark.sizeDelta = new Vector2(sparkSize, sparkSize);
            spark.localScale = Vector3.one * Mathf.Max(0.3f, burstSparkStartScale);
            spark.gameObject.SetActive(true);

            var image = spark.GetComponent<Image>();
            image.sprite = FirstSparkFrame();
            image.color = sparkColor;
            ApplyStarFrameLook(image);

            var mid = Vector3.Lerp(start, end, 0.45f);
            if (root != null)
                mid += root.TransformVector(new Vector3(0f, moteArc, 0f));
            else
                mid += Vector3.up * moteArc;

            var spinZ = Random.Range(sparkSpinMin, sparkSpinMax) * (Random.value < 0.5f ? -1f : 1f);
            var life = Mathf.Max(0.12f, flight);

            DOTween.Kill(spark);
            var fly = DOTween.Sequence().SetId(PayTweenId).SetTarget(spark);
            if (delay > 0f)
                fly.AppendInterval(delay);

            fly.Append(
                DOTween
                    .To(
                        () => 0f,
                        t =>
                        {
                            var a = Vector3.Lerp(start, mid, t);
                            var b = Vector3.Lerp(mid, end, t);
                            spark.position = Vector3.Lerp(a, b, t);
                        },
                        1f,
                        life
                    )
                    .SetEase(Ease.InCubic)
            );
            fly.Join(spark.DOScale(0.35f, life).SetEase(Ease.InQuad));
            fly.Join(
                spark.DORotate(new Vector3(0f, 0f, spinZ), life, RotateMode.LocalAxisAdd).SetEase(Ease.Linear)
            );
            fly.Insert(delay + life * 0.7f, image.DOFade(0f, life * 0.3f).SetEase(Ease.InQuad));
            fly.OnComplete(() =>
            {
                spark.gameObject.SetActive(false);
                motesLeft--;
                if (motesLeft <= 0)
                    CommitPay();
            });
        }

        /// <summary>Banks the bonus on the HUD once. Safe if motes were cancelled.</summary>
        private void CommitPay()
        {
            var delta = pendingPayDelta;
            pendingPayDelta = 0;
            motesLeft = 0;
            if (delta != 0)
                scorePopup?.Commit(delta);
        }

        /// <summary>STREAK +N on the same canvas as the pill, so it sits on top of the UI.</summary>
        private void PlayBonusLabel(int bonus, float flight)
        {
            var root = GetTravelRoot();
            var label = EnsureBonusLabel(root);
            if (label == null || root == null)
            {
                Debug.LogWarning("[StreakBonusPresenter] Could not create STREAK +N label (missing TMP font?).");
                return;
            }

            var spawnFrom = streakPanel != null ? streakPanel : transform as RectTransform;
            var start = UiWorldPoint(spawnFrom, bonusSpawnOffset);
            var end =
                scoreHud != null && scoreHud.PointsRect != null
                    ? UiWorldPoint(scoreHud.PointsRect, Vector2.zero)
                    : start + Vector3.up * 80f;

            var rect = label.rectTransform;
            rect.SetParent(root, false);
            rect.SetAsLastSibling();
            rect.position = start;
            rect.localScale = Vector3.one * 0.01f;
            label.text = BuildBonusLabelText(bonus);
            label.alpha = 0f;
            ApplyBonusLabelLook(label);
            label.gameObject.SetActive(true);

            var popIn = 0.14f;
            var settle = 0.08f;
            var overshoot = 1.05f;
            DOTween.Kill(BonusLabelTweenId);
            var seq = DOTween.Sequence().SetId(BonusLabelTweenId).SetTarget(rect);
            seq.AppendCallback(() =>
            {
                if (label != null)
                    label.alpha = 1f;
                rect.position = start;
                rect.localScale = Vector3.one * 0.01f;
            });
            seq.Append(rect.DOScale(overshoot, popIn).SetEase(Ease.OutBack));
            seq.Append(rect.DOScale(1f, settle).SetEase(Ease.OutSine));
            seq.Append(rect.DOMove(end, flight).SetEase(Ease.InCubic));
            seq.Join(rect.DOScale(0.4f, flight).SetEase(Ease.InQuad));
            seq.Insert(
                popIn + settle + flight * 0.4f,
                label.DOFade(0f, flight * 0.6f).SetEase(Ease.InQuad)
            );
            seq.OnComplete(HideBonusLabel);
        }

        private TextMeshProUGUI EnsureBonusLabel(RectTransform parent)
        {
            if (bonusLabel != null)
                return bonusLabel;

            var fontAsset = bonusFont != null ? bonusFont : TMP_Settings.defaultFontAsset;
            if (fontAsset == null || parent == null)
                return null;

            var go = new GameObject("StreakBonusLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(320f, 160f);

            bonusLabel = go.GetComponent<TextMeshProUGUI>();
            bonusLabel.font = fontAsset;
            bonusLabel.fontSize = Mathf.Max(12f, bonusLabelFontSize);
            bonusLabel.alignment = TextAlignmentOptions.Center;
            bonusLabel.raycastTarget = false;
            bonusLabel.richText = false;
            bonusLabel.enableWordWrapping = false;
            bonusLabel.overflowMode = TextOverflowModes.Overflow;
            ApplyBonusLabelLook(bonusLabel);
            go.SetActive(false);
            return bonusLabel;
        }

        private string BuildBonusLabelText(int bonus)
        {
            var word = scoreTune != null ? scoreTune.bonusLabel : "STREAK";
            if (!string.IsNullOrEmpty(word))
                return word + "\n+" + bonus;

            return "+" + bonus;
        }

        private void ApplyBonusLabelLook(TextMeshProUGUI label)
        {
            if (label == null)
                return;

            label.fontSize = Mathf.Max(12f, bonusLabelFontSize);
            var tune = scoreTune != null ? scoreTune : new ScorePopupTune();
            var top = tune.bonusTop;
            var bottom = tune.bonusBottom;
            label.color = Color.white;
            label.enableVertexGradient = true;
            label.colorGradient = new VertexGradient(top, top, bottom, bottom);
            label.lineSpacing = tune.bonusLineSpacing;
        }

        private void HideBonusLabel()
        {
            if (bonusLabel != null && bonusLabel.gameObject.activeSelf)
                bonusLabel.gameObject.SetActive(false);
        }

        private RectTransform GetTravelRoot()
        {
            var from = streakPanel != null ? streakPanel : transform;
            var canvas = from.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.rootCanvas != null)
                return canvas.rootCanvas.transform as RectTransform;

            return shineRoot != null ? shineRoot : transform as RectTransform;
        }

        /// <summary>Canvas world point at a rect's centre plus an offset in that rect's local pixels.</summary>
        private static Vector3 UiWorldPoint(RectTransform from, Vector2 extraLocalOffset)
        {
            if (from == null)
                return Vector3.zero;

            return from.TransformPoint((Vector3)from.rect.center + (Vector3)extraLocalOffset);
        }

        /// <summary>Swells each orb to white in turn, left to right, like a charging fuse.</summary>
        private void FlashOrbChain()
        {
            for (var i = 0; i < orbs.Length; i++)
            {
                var orb = GetOrb(i);
                if (orb == null)
                    continue;

                var id = OrbTweenIdPrefix + i;
                DOTween.Kill(id);

                var t = orb.transform;
                var baseColor = OrbBaseColor(i);

                var flash = DOTween.Sequence().SetId(id).SetTarget(t);
                flash.AppendInterval(i * chainStagger);
                flash.Append(t.DOScale(chainPunch, chainStep).SetEase(Ease.OutQuad));
                flash.Join(orb.DOColor(Color.white, chainStep).SetEase(Ease.OutQuad));
                flash.Append(t.DOScale(1f, chainStep * 1.6f).SetEase(Ease.OutBack));
                flash.Join(orb.DOColor(baseColor, chainStep * 1.6f).SetEase(Ease.InQuad));
            }
        }

        /// <summary>One bigger ping instead of the looping pulse — this is the peak.</summary>
        private void FlareGlow()
        {
            if (streakGlow == null || streakGlowImage == null)
                return;

            ResetGlowVisual(glowAlphaMax);

            var life = Mathf.Max(0.05f, flareDuration);
            var flare = DOTween.Sequence().SetId(GlowTweenId).SetTarget(streakGlow);
            flare.Append(
                streakGlow.DOSizeDelta(glowSizeMax * Mathf.Max(1f, flareSizeScale), life).SetEase(Ease.OutCubic)
            );
            flare.Join(streakGlowImage.DOFade(0f, life).SetEase(Ease.InQuad));
        }

        private void BurstSparks()
        {
            if (FirstSparkFrame() == null)
                return;

            var count = Mathf.Max(1, burstSparkCount);
            for (var i = 0; i < count; i++)
            {
                var spread = count > 1 ? i / (float)(count - 1) : 0.5f;
                var angle = Mathf.Lerp(burstAngleMin, burstAngleMax, spread);
                EmitBurstSpark(angle + Random.Range(-burstAngleJitter, burstAngleJitter));
            }
        }

        /// <summary>
        /// Compact spray from StarImage when BackgroundShine starts — same burst sparks as the 5th orb, then fall.
        /// </summary>
        private void BurstRibbonTextSparks()
        {
            TryFindRibbon();
            if (FirstSparkFrame() == null)
                return;

            var anchor =
                ribbonStarImage != null
                    ? ribbonStarImage.rectTransform
                    : ribbonBonusLabel != null
                        ? ribbonBonusLabel.rectTransform
                        : null;
            if (anchor == null)
                return;

            var parent = GetTravelRoot();
            if (parent == null)
                return;

            var origin = TryWorldToParentLocal(anchor, parent) + ribbonTextBurstOffset;
            var count = Mathf.Clamp(ribbonTextBurstCount, 5, 8);
            for (var i = 0; i < count; i++)
            {
                var spread = count > 1 ? i / (float)(count - 1) : 0.5f;
                var angle = Mathf.Lerp(burstAngleMin, burstAngleMax, spread);
                EmitBurstSpark(
                    parent,
                    origin,
                    angle + Random.Range(-burstAngleJitter, burstAngleJitter),
                    fallAfterBurst: true
                );
            }
        }

        /// <summary>One spark thrown out from StreakGlow's pivot plus Burst Spawn Offset.</summary>
        private void EmitBurstSpark(float angleDeg)
        {
            var parent = shineRoot != null ? shineRoot : transform as RectTransform;
            if (parent == null)
                return;

            EmitBurstSpark(parent, BurstStartInParent(parent), angleDeg, fallAfterBurst: false);
        }

        private void EmitBurstSpark(RectTransform parent, Vector2 start, float angleDeg, bool fallAfterBurst)
        {
            var spark = GetSpark();
            spark.SetParent(parent, false);
            spark.SetAsLastSibling();
            spark.pivot = new Vector2(0.5f, 0.5f);
            spark.anchoredPosition = start;
            spark.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            spark.sizeDelta = new Vector2(sparkSize, sparkSize);
            spark.localScale = Vector3.one * burstSparkStartScale;
            spark.gameObject.SetActive(true);

            var image = spark.GetComponent<Image>();
            image.sprite = FirstSparkFrame();
            image.color = sparkColor;
            ApplyStarFrameLook(image);

            var life = Mathf.Max(0.1f, burstSparkLifetime);
            var rad = angleDeg * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            var end = start + dir * Random.Range(burstDistanceMin, burstDistanceMax);
            var spinZ = Random.Range(sparkSpinMin, sparkSpinMax) * (Random.value < 0.5f ? -1f : 1f);

            DOTween.Kill(spark);
            var fly = DOTween.Sequence().SetTarget(spark);
            fly.Append(spark.DOAnchorPos(end, life).SetEase(Ease.OutCubic));
            fly.Join(spark.DOScale(burstSparkEndScale, life).SetEase(Ease.InQuad));
            fly.Join(
                spark.DORotate(new Vector3(0f, 0f, spinZ), life, RotateMode.LocalAxisAdd).SetEase(Ease.Linear)
            );

            if (fallAfterBurst)
            {
                var fallLife = Mathf.Max(0.1f, sparkLifetime);
                var speedMin = Mathf.Min(sparkFallSpeedMin, sparkFallSpeedMax);
                var speedMax = Mathf.Max(sparkFallSpeedMin, sparkFallSpeedMax);
                var fallDistance = Random.Range(speedMin, speedMax) * fallLife;
                var fallEnd = end + new Vector2(0f, -fallDistance);
                fly.Append(spark.DOAnchorPos(fallEnd, fallLife).SetEase(Ease.InQuad));
                fly.Join(image.DOFade(0f, fallLife).SetEase(Ease.InQuad));
            }
            else
            {
                fly.Insert(life * 0.3f, image.DOFade(0f, life * 0.7f).SetEase(Ease.InQuad));
            }

            fly.OnComplete(() => spark.gameObject.SetActive(false));
        }

        /// <summary>Maps a rect's centre into another rect's local space (canvas-aware).</summary>
        private static bool TryWorldToParentLocal(RectTransform from, RectTransform parent, out Vector2 local)
        {
            local = Vector2.zero;
            if (from == null || parent == null)
                return false;

            var world = from.TransformPoint(from.rect.center);
            var canvas = parent.GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var screen = RectTransformUtility.WorldToScreenPoint(cam, world);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, cam, out local);
        }

        private static Vector2 TryWorldToParentLocal(RectTransform from, RectTransform parent)
        {
            return TryWorldToParentLocal(from, parent, out var local) ? local : Vector2.zero;
        }

        private Vector2 BurstStartInParent(RectTransform parent)
        {
            var localStart = burstSpawnOffset + Random.insideUnitCircle * burstSpawnJitter;
            if (streakGlow == null || parent == streakGlow)
                return localStart;

            var world = streakGlow.TransformPoint(new Vector3(localStart.x, localStart.y, 0f));
            var screen = RectTransformUtility.WorldToScreenPoint(null, world);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out var local))
                return local;

            return localStart;
        }

        private void CacheOrbColors()
        {
            orbBaseColors = new Color[orbs != null ? orbs.Length : 0];
            for (var i = 0; i < orbBaseColors.Length; i++)
                orbBaseColors[i] = orbs[i] != null ? orbs[i].color : Color.white;
        }

        private Color OrbBaseColor(int index)
        {
            if (index < 0 || index >= orbBaseColors.Length)
                return Color.white;

            return orbBaseColors[index];
        }

        private void PunchPanel()
        {
            if (streakPanel == null)
                return;

            EnsurePanelCurves();
            DOTween.Kill(PanelTweenId);
            streakPanel.localScale = Vector3.one;

            var life = Mathf.Max(0.08f, panelPopLifetime);
            var firstWeight = Mathf.Max(0.01f, panelFirstDuration);
            var secondWeight = Mathf.Max(0.01f, panelSecondDuration);
            var firstTime = life * firstWeight / (firstWeight + secondWeight);
            var secondTime = life - firstTime;

            var pop = DOTween.Sequence().SetId(PanelTweenId).SetTarget(streakPanel);
            pop.Append(
                DOVirtual
                    .Float(0f, 1f, firstTime, t => ApplyPanelPeak(panelFirstPeak, panelFirstCurve, t))
                    .SetEase(Ease.Linear)
            );
            pop.Append(
                DOVirtual
                    .Float(0f, 1f, secondTime, t => ApplyPanelPeak(panelSecondPeak, panelSecondCurve, t))
                    .SetEase(Ease.Linear)
            );
            pop.OnComplete(ResetPanelScale);
        }

        /// <summary>
        /// Curve is a 0-1 hump: 0 = normal size, 1 = the peak you set.
        /// LerpUnclamped lets a curve go past 1 if you want extra snap.
        /// </summary>
        private void ApplyPanelPeak(float peak, AnimationCurve curve, float t)
        {
            if (streakPanel == null)
                return;

            var weight = curve != null ? curve.Evaluate(t) : 0f;
            var scale = Mathf.LerpUnclamped(1f, peak, weight);
            streakPanel.localScale = Vector3.one * scale;
        }

        private void EnsurePanelCurves()
        {
            if (panelFirstCurve == null || panelFirstCurve.length == 0)
                panelFirstCurve = MakeHumpCurve(0.32f);
            if (panelSecondCurve == null || panelSecondCurve.length == 0)
                panelSecondCurve = MakeHumpCurve(0.4f);
            EnsureRibbonStarScaleCurve();
        }

        /// <summary>Default StarImage scale graph: 1 → 1.4 → 1.1 over the flash.</summary>
        private void EnsureRibbonStarScaleCurve()
        {
            if (ribbonStarScaleOverLife != null && ribbonStarScaleOverLife.length > 0)
                return;

            ribbonStarScaleOverLife = new AnimationCurve(
                new Keyframe(0f, 1f, 0f, 2f),
                new Keyframe(0.35f, 1.4f, 0f, 0f),
                new Keyframe(1f, 1.1f, -0.8f, 0f)
            );
        }

        /// <summary>
        /// A 0 → 1 → 0 hump. PeakAt is where it hits 1 (0.32 = snaps out fast, then eases back).
        /// </summary>
        private static AnimationCurve MakeHumpCurve(float peakAt)
        {
            peakAt = Mathf.Clamp(peakAt, 0.1f, 0.9f);
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 3.2f),
                new Keyframe(peakAt, 1f, 0f, 0f),
                new Keyframe(1f, 0f, -2.4f, 0f)
            );
        }

        private void ResetPanelScale()
        {
            if (streakPanel != null)
                streakPanel.localScale = Vector3.one;
        }

        private void PlayPanelFoilBurst()
        {
            if (panelFoilImage == null && panelFoilOutlineImage == null)
                return;

            DOTween.Kill(PanelFoilTweenId);

            var startSize = glowSizeMin;
            var endSize = glowSizeMax * Mathf.Max(1f, flareSizeScale);
            var life = Mathf.Max(0.05f, flareDuration);
            var childEnd = Vector3.one * Mathf.Max(0.01f, panelFoilChildScale);

            PreparePanelFoilRect(panelFoilImage, startSize, panelFoilAlpha);
            PreparePanelFoilRect(panelFoilOutlineImage, startSize, panelFoilAlpha);
            PreparePanelFoilChild();

            var pop = DOTween.Sequence().SetId(PanelFoilTweenId).SetTarget(this);
            TweenPanelFoilSize(pop, panelFoilImage, endSize, life);
            TweenPanelFoilSize(pop, panelFoilOutlineImage, endSize, life);
            if (panelFoilChild != null)
                pop.Join(panelFoilChild.DOScale(childEnd, life).SetEase(Ease.OutCubic));
            if (panelFoilImage != null)
                pop.Join(panelFoilImage.DOFade(0f, life).SetEase(Ease.InQuad));
            if (panelFoilOutlineImage != null)
                pop.Join(panelFoilOutlineImage.DOFade(0f, life).SetEase(Ease.InQuad));
            var childImage = GetPanelFoilChildImage();
            if (childImage != null)
                pop.Join(childImage.DOFade(0f, life).SetEase(Ease.InQuad));
            pop.OnComplete(HidePanelFoilImmediate);
        }

        private static void PreparePanelFoilRect(Image image, Vector2 startSize, float alpha)
        {
            if (image == null)
                return;

            var rect = image.rectTransform;
            rect.localScale = Vector3.one;
            rect.sizeDelta = startSize;
            var c = image.color;
            image.color = new Color(c.r, c.g, c.b, alpha);
            image.gameObject.SetActive(true);
        }

        private static void TweenPanelFoilSize(
            Sequence pop,
            Image image,
            Vector2 endSize,
            float life
        )
        {
            if (image == null)
                return;

            pop.Join(image.rectTransform.DOSizeDelta(endSize, life).SetEase(Ease.OutCubic));
        }

        private void HidePanelFoilImmediate()
        {
            DOTween.Kill(PanelFoilTweenId);
            HidePanelFoilImage(panelFoilImage);
            HidePanelFoilImage(panelFoilOutlineImage);
            HidePanelFoilChild();
        }

        private void PreparePanelFoilChild()
        {
            if (panelFoilChild == null)
                return;

            panelFoilChild.localScale = Vector3.one;
            panelFoilChild.gameObject.SetActive(true);

            var image = GetPanelFoilChildImage();
            if (image == null)
                return;

            image.enabled = true;
            var c = image.color;
            image.color = new Color(c.r, c.g, c.b, 1f);
        }

        private void HidePanelFoilChild()
        {
            if (panelFoilChild == null)
                return;

            panelFoilChild.localScale = Vector3.one;
            var image = GetPanelFoilChildImage();
            if (image == null)
                return;

            var c = image.color;
            image.color = new Color(c.r, c.g, c.b, 0f);
            image.enabled = false;
        }

        private Image GetPanelFoilChildImage()
        {
            return panelFoilChild == null ? null : panelFoilChild.GetComponent<Image>();
        }

        private void TryFindPanelFoilChild()
        {
            if (panelFoilChild != null || panelFoilImage == null)
                return;

            var root = panelFoilImage.transform;
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i) as RectTransform;
                if (child == null)
                    continue;

                panelFoilChild = child;
                return;
            }
        }

        private void HidePanelFoilImage(Image image)
        {
            if (image == null)
                return;

            image.rectTransform.localScale = Vector3.one;
            image.rectTransform.sizeDelta = glowSizeMin;
            var c = image.color;
            image.color = new Color(c.r, c.g, c.b, 0f);
        }

        /// <summary>
        /// Looping ping: ring starts at Glow Size Min, grows to Glow Size Max, fades to nothing,
        /// then snaps back and repeats. localScale stays at 1 — only width and height change.
        /// </summary>
        private void PlayGlow()
        {
            if (streakGlow == null || streakGlowImage == null)
                return;

            ResetGlowVisual(glowAlphaMax);

            var life = Mathf.Max(0.05f, glowPulseDuration);
            var pulse = DOTween.Sequence().SetId(GlowTweenId).SetTarget(streakGlow);
            pulse.Append(streakGlow.DOSizeDelta(glowSizeMax, life).SetEase(Ease.OutCubic));
            pulse.Join(streakGlowImage.DOFade(0f, life).SetEase(Ease.InQuad));

            var gap = Mathf.Max(0f, glowPulseGap);
            if (gap > 0f)
                pulse.AppendInterval(gap);

            pulse.SetLoops(-1, LoopType.Restart);
        }

        private void StopGlow(bool resetSize)
        {
            DOTween.Kill(GlowTweenId);
            if (streakGlow == null || streakGlowImage == null)
                return;

            var c = streakGlowImage.color;
            streakGlowImage.color = new Color(c.r, c.g, c.b, 0f);
            streakGlow.localScale = Vector3.one;
            if (resetSize)
                streakGlow.sizeDelta = glowSizeMin;
        }

        /// <summary>Show the ring at rest size and the given alpha, and kill any old glow tween.</summary>
        private void ResetGlowVisual(float alpha)
        {
            DOTween.Kill(GlowTweenId);
            streakGlow.gameObject.SetActive(true);
            streakGlow.localScale = Vector3.one;
            streakGlow.sizeDelta = glowSizeMin;

            var c = streakGlowImage.color;
            streakGlowImage.color = new Color(c.r, c.g, c.b, alpha);
        }

        private void SetSecondaryGlow(bool on)
        {
            if (secondaryGlowImage == null)
                return;

            var c = secondaryGlowImage.color;
            secondaryGlowImage.color = new Color(c.r, c.g, c.b, on ? 0.5f : 0f);
        }

        /// <summary>
        /// Tiny scale breath + _ShineAmount 0→1 on the same InOutSine Yoyo while count == 4.
        /// </summary>
        private void PlayRibbonCharge()
        {
            TryFindRibbon();
            if (ribbon == null)
                return;

            EnsureRibbonMaterial();
            DOTween.Kill(RibbonTweenId);
            ribbonBreathT = 0f;
            ribbon.localScale = Vector3.one;
            SetRibbonShineAmount(0f);

            var life = Mathf.Max(0.05f, ribbonPulseDuration);
            var peak = Mathf.Max(1f, ribbonBreathPeak);
            DOTween
                .To(
                    () => ribbonBreathT,
                    v =>
                    {
                        ribbonBreathT = v;
                        ribbon.localScale = Vector3.one * Mathf.Lerp(1f, peak, v);
                        SetRibbonShineAmount(v);
                    },
                    1f,
                    life
                )
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetId(RibbonTweenId)
                .SetTarget(ribbon);
        }

        private void StopRibbonCharge(bool resetVisual)
        {
            DOTween.Kill(RibbonTweenId);
            ribbonBreathT = 0f;
            if (!resetVisual)
                return;

            if (ribbon != null)
                ribbon.localScale = Vector3.one;
            if (ribbonImage != null)
                ribbonImage.rectTransform.localRotation = ribbonImageRestRotation;
            if (ribbonBackgroundShine != null)
                ribbonBackgroundShine.SetActive(false);
            if (ribbonRayShine != null)
            {
                ribbonRayShine.localScale = Vector3.zero;
                ribbonRayShine.localRotation = Quaternion.identity;
            }
            if (ribbonRayShine2 != null)
            {
                ribbonRayShine2.localScale = Vector3.zero;
                ribbonRayShine2.localRotation = Quaternion.identity;
            }
            if (ribbonStarImage != null)
            {
                ribbonStarImage.rectTransform.localRotation = Quaternion.identity;
                EnsureRibbonStarScaleCurve();
                var startScale = ribbonStarScaleOverLife.Evaluate(0f);
                ribbonStarImage.rectTransform.localScale = Vector3.one * startScale;
            }
            if (ribbonStarImageWhite != null)
            {
                var c = ribbonStarImageWhite.color;
                ribbonStarImageWhite.color = new Color(c.r, c.g, c.b, 0f);
            }
            SetRibbonShineAmount(0f);
        }

        private void RefreshRibbonBonusLabel(int amount, bool visible)
        {
            TryFindRibbon();
            if (ribbonBonusLabel == null)
                return;

            ribbonBonusLabel.text = "+" + Mathf.Max(0, amount);
            var c = ribbonBonusLabel.color;
            ribbonBonusLabel.color = new Color(c.r, c.g, c.b, visible ? 1f : 0f);
            if (visible)
                ribbonBonusLabel.rectTransform.localScale = ribbonBonusLabelRestScale;
        }

        private void EnsureRibbonMaterial()
        {
            if (ribbonMatInstance != null || ribbonImage == null)
                return;

            var source = ribbonImage.material;
            if (source == null || source.shader == null || source.shader.name != RibbonShineShaderName)
                return;

            ribbonMatInstance = Instantiate(source);
            ribbonMatInstance.name = source.name + " (Instance)";
            ribbonImage.material = ribbonMatInstance;
        }

        private void SetRibbonShineAmount(float amount)
        {
            if (ribbonMatInstance == null)
                EnsureRibbonMaterial();
            if (ribbonMatInstance == null)
                return;

            ribbonMatInstance.SetFloat(ShineAmountId, Mathf.Clamp01(amount));
        }

        private void ShowOrb(int index)
        {
            var orb = GetOrb(index);
            if (orb == null)
                return;

            var id = OrbTweenIdPrefix + index;
            DOTween.Kill(id);

            var t = orb.transform;
            orb.gameObject.SetActive(true);
            t.localScale = Vector3.zero;

            var pop = DOTween.Sequence().SetId(id).SetTarget(t);
            pop.Append(t.DOScale(orbPunch, orbPopDuration).SetEase(Ease.OutQuad));
            pop.Append(t.DOScale(1f, orbSettleDuration).SetEase(Ease.OutBack));

            EmitShine(orb);
            EmitBurst(orb);
            PlaySfx(Audio.SfxStreakPip, 0.5f);
        }

        private void HideOrb(int index, float delay = 0f)
        {
            var orb = GetOrb(index);
            if (orb == null)
                return;

            var id = OrbTweenIdPrefix + index;
            DOTween.Kill(id);

            var t = orb.transform;
            orb.color = OrbBaseColor(index);
            t.DOScale(0f, orbHideDuration)
                .SetDelay(delay)
                .SetEase(Ease.InQuad)
                .SetId(id)
                .SetTarget(t)
                .OnComplete(() =>
                {
                    if (orb != null)
                        orb.gameObject.SetActive(false);
                });
        }

        private void HideAllOrbsImmediate()
        {
            KillAllOrbTweens();
            for (var i = 0; i < orbs.Length; i++)
            {
                var orb = GetOrb(i);
                if (orb == null)
                    continue;
                orb.color = OrbBaseColor(i);
                orb.transform.localScale = Vector3.one;
                orb.gameObject.SetActive(false);
            }
        }

        private void KillAllOrbTweens()
        {
            for (var i = 0; i < orbs.Length; i++)
                DOTween.Kill(OrbTweenIdPrefix + i);
        }

        private Image GetOrb(int index)
        {
            if (orbs == null || index < 0 || index >= orbs.Length)
                return null;
            return orbs[index];
        }

        private void EmitBurst(Image orb)
        {
            var orbRect = orb.rectTransform;
            var parent = orbRect.parent as RectTransform;
            if (parent == null)
                return;

            var ring = GetBurstRing();
            // Sibling, not child: the orb's 1.18 punch would otherwise scale the ring too.
            ring.SetParent(parent, false);
            ring.SetAsFirstSibling();
            ring.anchorMin = orbRect.anchorMin;
            ring.anchorMax = orbRect.anchorMax;
            ring.pivot = orbRect.pivot;
            ring.anchoredPosition = orbRect.anchoredPosition;
            ring.sizeDelta = orbRect.rect.size;
            ring.localRotation = Quaternion.identity;
            ring.localScale = Vector3.one * burstStartScale;
            ring.gameObject.SetActive(true);

            var image = ring.GetComponent<Image>();
            image.sprite = burstRingSprite != null ? burstRingSprite : SoftRingSprite.Get();
            image.color = burstColor;

            var life = Mathf.Max(0.08f, burstDuration);
            DOTween.Kill(ring);
            var burst = DOTween.Sequence().SetTarget(ring);
            burst.Join(ring.DOScale(burstEndScale, life).SetEase(Ease.OutCubic));
            burst.Join(image.DOFade(0f, life).SetEase(Ease.InQuad));
            burst.OnComplete(() => ring.gameObject.SetActive(false));
        }

        private RectTransform GetBurstRing()
        {
            for (var i = 0; i < burstPool.Count; i++)
            {
                var pooled = burstPool[i];
                if (pooled != null && !pooled.gameObject.activeSelf)
                    return pooled;
            }

            var go = new GameObject(
                "StreakBurst",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            go.SetActive(false);
            burstPool.Add(rect);
            return rect;
        }

        private void HideAllBursts()
        {
            for (var i = 0; i < burstPool.Count; i++)
            {
                var ring = burstPool[i];
                if (ring == null)
                    continue;

                DOTween.Kill(ring);
                ring.gameObject.SetActive(false);
            }
        }

        private void EmitShine(Image orb)
        {
            var orbRect = orb.rectTransform;
            var shine = GetShine();
            // Child of the orb so it punches 0 → 1.18 → 1 with the orb.
            shine.SetParent(orbRect, false);
            shine.SetAsLastSibling();
            shine.anchorMin = new Vector2(0.5f, 0.5f);
            shine.anchorMax = new Vector2(0.5f, 0.5f);
            shine.pivot = new Vector2(0.5f, 0.5f);
            shine.anchoredPosition = Vector2.zero;
            shine.sizeDelta = orbRect.rect.size;
            shine.localRotation = Quaternion.identity;
            shine.localScale = Vector3.one;
            shine.gameObject.SetActive(true);

            var image = shine.GetComponent<Image>();
            image.sprite = shineSprite != null ? shineSprite : orb.sprite;
            image.color = shineColor;

            var life = Mathf.Max(0.06f, shineDuration);
            DOTween.Kill(shine);
            var flash = DOTween.Sequence().SetTarget(shine);
            flash.Join(shine.DOScale(shineEndScale, life).SetEase(Ease.OutQuad));
            flash.Join(image.DOFade(0f, life).SetEase(Ease.InQuad));
            flash.OnComplete(() => shine.gameObject.SetActive(false));
        }

        private RectTransform GetShine()
        {
            for (var i = 0; i < shinePool.Count; i++)
            {
                var pooled = shinePool[i];
                if (pooled != null && !pooled.gameObject.activeSelf)
                    return pooled;
            }

            var go = new GameObject(
                "StreakShine",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            go.SetActive(false);
            shinePool.Add(rect);
            return rect;
        }

        private void HideAllShines()
        {
            for (var i = 0; i < shinePool.Count; i++)
            {
                var shine = shinePool[i];
                if (shine == null)
                    continue;

                DOTween.Kill(shine);
                shine.gameObject.SetActive(false);
            }
        }

        private void StartSparks()
        {
            if (FirstSparkFrame() == null)
                return;

            sparksActive = true;
            sparkCooldownLeft = 0f;
        }

        private void StopSparks()
        {
            sparksActive = false;
        }

        private void EmitSpark()
        {
            var parent = shineRoot != null ? shineRoot : transform as RectTransform;
            if (parent == null || FirstSparkFrame() == null)
                return;

            var canFinish = sparkFrame04 != null || sparkFrame08 != null;
            var finish = canFinish && Random.value < sparkFinishChance;
            var frameCount = FillFallingSparkClip(finish, out var lateFromIndex);
            if (frameCount <= 0)
                return;

            var spark = GetSpark();
            spark.SetParent(parent, false);
            spark.SetAsLastSibling();
            spark.pivot = new Vector2(0.5f, 0.5f);
            spark.anchoredPosition = SparkStartInParent(parent);
            spark.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            spark.sizeDelta = new Vector2(sparkSize, sparkSize);
            spark.gameObject.SetActive(true);

            var grows = finish || Random.value < sparkGrowChance;
            var startScale = grows ? 0.5f : Mathf.Max(0.1f, sparkStartScale);
            var endScale = finish ? Mathf.Max(1f, sparkFinishEndScale) : grows ? 1f : 0.25f;
            if (Random.value < sparkSmallChance)
            {
                startScale *= 0.7f;
                endScale *= 0.7f;
            }
            spark.localScale = Vector3.one * startScale;

            var image = spark.GetComponent<Image>();
            image.sprite = sparkClipScratch[0];
            image.color = sparkColor;

            var life = Mathf.Max(0.1f, sparkLifetime);
            var start = spark.anchoredPosition;
            var speedMin = Mathf.Min(sparkFallSpeedMin, sparkFallSpeedMax);
            var speedMax = Mathf.Max(sparkFallSpeedMin, sparkFallSpeedMax);
            var fallDistance = Random.Range(speedMin, speedMax) * life;
            var spinZ = finish
                ? FinishSpinDegrees
                : Random.Range(
                    Mathf.Min(sparkSpinMin, sparkSpinMax),
                    Mathf.Max(sparkSpinMin, sparkSpinMax)
                );
            if (Random.value < 0.5f)
                spinZ = -spinZ;

            // Stop spin when star_08 (3rd frame) shows. Extra plus frames stay still.
            FillSparkFrameDurations(frameCount, lateFromIndex, life);
            var freezeFrame = Mathf.Min(2, frameCount - 1);
            var spinTime = freezeFrame > 0 ? SparkFrameStartTime(freezeFrame) : life;

            DOTween.Kill(spark);
            var fall = DOTween.Sequence().SetTarget(spark);
            fall.Append(
                spark.DOAnchorPosY(start.y - fallDistance, life).SetEase(Ease.InQuad)
            );
            fall.Join(
                spark.DOScale(endScale, life).SetEase(finish || grows ? Ease.OutQuad : Ease.InQuad)
            );
            if (spinTime > 0.01f)
            {
                fall.Join(
                    spark
                        .DORotate(new Vector3(0f, 0f, spinZ), spinTime, RotateMode.LocalAxisAdd)
                        .SetEase(Ease.Linear)
                );
            }
            fall.Join(image.DOFade(0f, life).SetEase(Ease.InQuad));
            ScheduleSparkFrames(fall, image, frameCount, lateFromIndex);
            fall.OnComplete(() => spark.gameObject.SetActive(false));
        }

        /// <summary>star_01 used by the falling pill motes.</summary>
        public Sprite SparkSprite => FirstSparkFrame();

        /// <summary>star_01, else the old burst sprite. Null means "no spark art yet".</summary>
        private Sprite FirstSparkFrame()
        {
            if (sparkFrame01 != null)
                return sparkFrame01;
            return sparkSprite;
        }

        /// <summary>
        /// Fills sparkClipScratch. Finish = 01 → 04 → 08 → 4 → 5 → 6.
        /// lateFromIndex is the first plus-reticle frame (4–6). Those skip the core shader.
        /// </summary>
        private int FillFallingSparkClip(bool finish, out int lateFromIndex)
        {
            var count = 0;
            lateFromIndex = 0;
            var first = FirstSparkFrame();
            if (first == null)
                return 0;

            sparkClipScratch[count++] = first;

            if (finish)
            {
                if (sparkFrame04 != null)
                    sparkClipScratch[count++] = sparkFrame04;
                if (sparkFrame08 != null)
                    sparkClipScratch[count++] = sparkFrame08;

                lateFromIndex = count;
                if (sparkFrame4 != null)
                    sparkClipScratch[count++] = sparkFrame4;
                if (sparkFrame5 != null)
                    sparkClipScratch[count++] = sparkFrame5;
                if (sparkFrame6 != null)
                    sparkClipScratch[count++] = sparkFrame6;
                return count;
            }

            if (sparkFrame04 != null && Random.value < 0.5f)
                sparkClipScratch[count++] = sparkFrame04;

            lateFromIndex = count;
            return count;
        }

        /// <summary>
        /// Splits life across clip frames. Frames 4–6 use Hold (1 = same as an early frame).
        /// Writes sparkDurationScratch so swaps and spin-stop share one timeline.
        /// </summary>
        private void FillSparkFrameDurations(int frameCount, int lateFromIndex, float life)
        {
            var totalWeight = 0f;
            for (var i = 0; i < frameCount; i++)
            {
                var weight = HoldForSparkFrame(sparkClipScratch[i], i, lateFromIndex);
                sparkDurationScratch[i] = weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0.001f)
                totalWeight = Mathf.Max(1, frameCount);

            for (var i = 0; i < frameCount; i++)
                sparkDurationScratch[i] = sparkDurationScratch[i] / totalWeight * life;
        }

        /// <summary>Frame 4 is a short flash. 5 and 6 linger. 1 = same length as star_01.</summary>
        private float HoldForSparkFrame(Sprite frame, int index, int lateFromIndex)
        {
            if (index < lateFromIndex)
                return 1f;
            if (frame == sparkFrame4)
                return Mathf.Max(0.05f, sparkFrame4Hold);
            if (frame == sparkFrame5)
                return Mathf.Max(0.05f, sparkFrame5Hold);
            if (frame == sparkFrame6)
                return Mathf.Max(0.05f, sparkFrame6Hold);
            return 1f;
        }

        /// <summary>When this frame first shows. Frame 0 is time 0.</summary>
        private float SparkFrameStartTime(int frameIndex)
        {
            var at = 0f;
            var last = Mathf.Min(frameIndex, sparkDurationScratch.Length);
            for (var i = 0; i < last; i++)
                at += sparkDurationScratch[i];
            return at;
        }

        /// <summary>Swap Image.sprite using Hold times. Frames 4–6 drop the core shader and use Spark Late Color.</summary>
        private void ScheduleSparkFrames(
            Sequence fall,
            Image image,
            int frameCount,
            int lateFromIndex
        )
        {
            if (fall == null || image == null || frameCount <= 1)
                return;

            var at = 0f;
            for (var i = 1; i < frameCount; i++)
            {
                at += sparkDurationScratch[i - 1];
                var frame = sparkClipScratch[i];
                if (frame == null)
                    continue;

                var late = i >= lateFromIndex;
                var when = at;
                fall.InsertCallback(
                    when,
                    () =>
                    {
                        image.sprite = frame;
                        if (late)
                            ApplyLateFrameLook(image);
                        else
                            ApplyStarFrameLook(image);
                    }
                );
            }
        }

        /// <summary>Frames 01 / 04 / 08: core shader + Spark Color. Keeps the fade alpha.</summary>
        private void ApplyStarFrameLook(Image image)
        {
            if (image == null)
                return;

            ApplySparkMaterial(image);
            var a = image.color.a;
            image.color = new Color(sparkColor.r, sparkColor.g, sparkColor.b, a);
        }

        /// <summary>Frames 4–6: default UI blend + Spark Late Color. Photoshop color shows through.</summary>
        private void ApplyLateFrameLook(Image image)
        {
            if (image == null)
                return;

            image.material = null;
            var a = image.color.a;
            image.color = new Color(sparkLateColor.r, sparkLateColor.g, sparkLateColor.b, a);
        }

        /// <summary>
        /// Random X along the pill, but every spark uses the same Y: the pill's bottom edge plus offset.
        /// Do not use StreakGlow's live height — that rect pulses, so later sparks would spawn lower.
        /// </summary>
        private Vector2 SparkStartInParent(RectTransform parent)
        {
            var spawnFrom = streakPanel != null ? streakPanel : streakGlow;

            var width = 120f;
            if (spawnFrom != null)
                width = spawnFrom.rect.width;
            else if (parent != null)
                width = parent.rect.width;

            var lineWidth = width;
            if (sparkSpawnWidth > 0f)
                lineWidth = Mathf.Min(width, sparkSpawnWidth);

            var halfW = Mathf.Max(4f, lineWidth * 0.5f);
            float localX;
            float localY;
            if (spawnFrom != null)
            {
                // rect.center.x works for any pivot (Pill is top-left, Glow is center).
                localX = spawnFrom.rect.center.x + Random.Range(-halfW, halfW);
                // Pill size is stable. Glow pulses, so use Glow Size Min instead of live height.
                localY =
                    spawnFrom == streakGlow
                        ? spawnFrom.rect.center.y - glowSizeMin.y * 0.5f + sparkSpawnYOffset
                        : spawnFrom.rect.yMin + sparkSpawnYOffset;
            }
            else
            {
                localX = Random.Range(-halfW, halfW);
                localY = sparkSpawnYOffset;
            }

            var localPos = new Vector3(localX, localY, 0f);
            if (spawnFrom == null || parent == spawnFrom)
                return localPos;

            var world = spawnFrom.TransformPoint(localPos);
            var screen = RectTransformUtility.WorldToScreenPoint(null, world);
            if (
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent,
                    screen,
                    null,
                    out var local
                )
            )
                return local;

            return localPos;
        }

        private RectTransform GetSpark()
        {
            for (var i = 0; i < sparkPool.Count; i++)
            {
                var pooled = sparkPool[i];
                if (pooled != null && !pooled.gameObject.activeSelf)
                {
                    ApplySparkMaterial(pooled.GetComponent<Image>());
                    return pooled;
                }
            }

            var go = new GameObject(
                "StreakSpark",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(sparkSize, sparkSize);

            var image = go.GetComponent<Image>();
            image.sprite = FirstSparkFrame();
            image.raycastTarget = false;
            image.preserveAspect = true;
            ApplySparkMaterial(image);
            go.SetActive(false);
            sparkPool.Add(rect);
            return rect;
        }

        /// <summary>
        /// White PNG core stays hot. Rays pick up Spark Color. Normal UI blend so it reads on the bright pill.
        /// </summary>
        private void ApplySparkMaterial(Image image)
        {
            if (image == null)
                return;

            var mat = GetOrCreateSparkCoreMaterial();
            if (mat == null)
            {
                image.material = null;
                return;
            }

            mat.SetFloat("_CoreBoost", Mathf.Max(0.1f, sparkCoreBoost));
            mat.SetFloat("_CoreThreshold", Mathf.Clamp01(sparkCoreThreshold));
            image.material = mat;
        }

        private static Material GetOrCreateSparkCoreMaterial()
        {
            if (sharedSparkCoreMaterial != null)
                return sharedSparkCoreMaterial;

            var shader = Shader.Find(SparkCoreShaderName);
            if (shader == null)
            {
                if (!didWarnMissingSparkShader)
                {
                    Debug.LogWarning(
                        "[StreakBonusPresenter] Shader Solitaire/UISparkCore not found — sparks will use default UI tint."
                    );
                    didWarnMissingSparkShader = true;
                }

                return null;
            }

            sharedSparkCoreMaterial = new Material(shader)
            {
                name = "StreakSparkCore",
                color = Color.white
            };
            return sharedSparkCoreMaterial;
        }

        private int CountActiveSparks()
        {
            var count = 0;
            for (var i = 0; i < sparkPool.Count; i++)
            {
                var spark = sparkPool[i];
                if (spark != null && spark.gameObject.activeSelf)
                    count++;
            }

            return count;
        }

        private void HideAllSparks()
        {
            sparksActive = false;
            for (var i = 0; i < sparkPool.Count; i++)
            {
                var spark = sparkPool[i];
                if (spark == null)
                    continue;

                DOTween.Kill(spark);
                spark.gameObject.SetActive(false);
            }
        }

        private void TryFindShineRoot()
        {
            if (shineRoot != null)
                return;

            var child = transform.Find("ShineRoot");
            if (child != null)
            {
                shineRoot = child as RectTransform;
                return;
            }

            if (transform.parent != null)
            {
                var sibling = transform.parent.Find("ShineRoot");
                if (sibling != null)
                    shineRoot = sibling as RectTransform;
            }
        }

        private void TryFindPanel()
        {
            if (streakPanel != null)
                return;

            var pill = transform.Find("Pill");
            if (pill != null)
            {
                streakPanel = pill as RectTransform;
                return;
            }

            // Last resort: this object is the Streak root. Pivot is top-left, so punch will look off.
            streakPanel = transform as RectTransform;
        }

        private void TryFindGlow()
        {
            if (streakGlow == null)
            {
                var glow = transform.Find("StreakGlow");
                if (glow != null)
                    streakGlow = glow as RectTransform;
            }

            if (streakGlowImage == null && streakGlow != null)
                streakGlowImage = streakGlow.GetComponent<Image>();
        }

        private void TryFindRibbon()
        {
            if (ribbon == null)
            {
                var found = transform.Find("Ribbon");
                if (found != null)
                    ribbon = found as RectTransform;
            }

            if (ribbonImage == null && ribbon != null)
                ribbonImage = ribbon.GetComponentInChildren<Image>(true);

            if (ribbonBonusLabel == null && ribbon != null)
                ribbonBonusLabel = ribbon.GetComponentInChildren<TextMeshProUGUI>(true);

            if (ribbon != null)
            {
                if (ribbonBackgroundShine == null)
                {
                    var bg = FindChildByName(ribbon, "BackgroundShine");
                    if (bg != null)
                        ribbonBackgroundShine = bg.gameObject;
                }

                if (ribbonBackgroundShine != null)
                {
                    var bgT = ribbonBackgroundShine.transform;
                    if (ribbonRayShine == null)
                    {
                        var shine = FindChildByName(bgT, "Shine");
                        if (shine != null)
                            ribbonRayShine = shine as RectTransform;
                    }

                    if (ribbonRayShine2 == null)
                    {
                        var shine2 = FindChildByName(bgT, "Shine2");
                        if (shine2 != null)
                            ribbonRayShine2 = shine2 as RectTransform;
                    }

                    if (ribbonStarImage == null)
                    {
                        var star = FindChildByName(bgT, "StarImage");
                        if (star != null)
                            ribbonStarImage = star.GetComponent<Image>();
                    }

                    if (ribbonStarImageWhite == null)
                    {
                        var starWhite = FindChildByName(bgT, "StarImageWhite");
                        if (starWhite != null)
                            ribbonStarImageWhite = starWhite.GetComponent<Image>();
                    }
                }
            }

            // Do not SetActive(false) here — BurstRibbonTextSparks / charge paths call
            // TryFindRibbon mid-flash and would kill BackgroundShine immediately after activate.

            if (!ribbonRestsCached)
            {
                if (ribbonImage != null)
                    ribbonImageRestRotation = ribbonImage.rectTransform.localRotation;
                if (ribbonBonusLabel != null)
                    ribbonBonusLabelRestScale = ribbonBonusLabel.rectTransform.localScale;
                if (ribbonImage != null || ribbonBonusLabel != null)
                    ribbonRestsCached = true;
            }
        }

        /// <summary>Finds a child by name even when inactive (Transform.Find skips inactive).</summary>
        private static Transform FindChildByName(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return null;

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name)
                    return child;

                var nested = FindChildByName(child, name);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private void TryFindOrbs()
        {
            var needsFind = orbs == null || orbs.Length != 5;
            if (!needsFind)
            {
                for (var i = 0; i < orbs.Length; i++)
                {
                    if (orbs[i] == null)
                    {
                        needsFind = true;
                        break;
                    }
                }
            }

            if (!needsFind)
                return;

            var wells = transform.Find("Wells");
            if (wells == null)
            {
                Debug.LogWarning(
                    "[StreakBonusPresenter] Assign the 5 Orb images, or name them Wells/Well_0/Orb … Well_4/Orb.",
                    this
                );
                return;
            }

            orbs = new Image[5];
            for (var i = 0; i < 5; i++)
            {
                var well = wells.Find("Well_" + i);
                var orb = well != null ? well.Find("Orb") : null;
                if (orb != null)
                    orbs[i] = orb.GetComponent<Image>();

                if (orbs[i] == null)
                    Debug.LogWarning("[StreakBonusPresenter] Missing Wells/Well_" + i + "/Orb.", this);
            }
        }
    }
}
