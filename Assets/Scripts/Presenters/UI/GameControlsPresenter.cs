using System.Collections;
using DG.Tweening;
using Solitaire.Models;
using Solitaire.Services;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Solitaire.Presenters
{
    public class GameControlsPresenter : OrientationAwarePresenter
    {
        private const string UndoSpinTweenId = "UndoArrowSpin";
        private const string UndoArrowChildName = "Image Inner";
        private const string UndoRattleId = "UndoRattle";
        private const string HintLitTweenId = "HintLitFlash";
        private const string HintLitAttractId = "HintLitAttract";

        [SerializeField]
        private Button _buttonOptions;

        [SerializeField]
        private Button _buttonHome;

        [SerializeField]
        private Button _buttonMatch;

        [SerializeField]
        private Button _buttonUndo;

        [SerializeField]
        private Button _buttonHint;

        [SerializeField]
        private Button _buttonLeaderboard;

        [Header("Undo juice")]
        [Tooltip("Child arrow that spins on undo (defaults to Image Inner).")]
        [SerializeField]
        private RectTransform _undoArrow;

        // Not [SerializeField] on purpose. Unity was keeping an old Inspector copy
        // of this number, so editing it in the script (even 20f) did nothing.
        private const float UndoSpinSeconds = 0.4f;

        [Tooltip("How far undo rattles when a draw has a legal play.")]
        [SerializeField]
        private float undoRattleDistance = 10f;

        [SerializeField]
        private float undoRattleDuration = 0.28f;

        [Header("Hint juice (lit flash on press)")]
        [Tooltip("Lit lightbulb sprite (e.g. HintLightbulb). Overlay flashes on Hint press.")]
        [SerializeField]
        private Sprite _hintLitSprite;

        [SerializeField]
        private float hintLitFlashDuration = 0.52f;

        [SerializeField]
        private float hintPunchStrength = 0.2f;

        [SerializeField]
        private float hintPunchDuration = 0.16f;

        [Header("Hint idle attract")]
        [Tooltip("Seconds with no successful move before the bulb starts pulsing.")]
        [SerializeField]
        private float hintIdleDelay = 10f;

        [SerializeField]
        private float hintAttractMinAlpha = 0.12f;

        [SerializeField]
        private float hintAttractMaxAlpha = 0.55f;

        [Tooltip("One way of the pulse (min→max). Full cycle is ~2x this.")]
        [SerializeField]
        private float hintAttractHalfDuration = 0.95f;

        [Header("Appear pop (after deal)")]
        [Tooltip("Gap between each button in the left-to-right wave. Undo is a silent slot.")]
        [SerializeField]
        private float appearStagger = 0.03f;

        [Inject]
        private readonly GameControls _gameControls;

        [Inject]
        private readonly GamePopup _gamePopup;

        [Inject]
        private readonly GameState _gameState;

        [Inject]
        private readonly IMovesService _movesService;

        private RectTransform _rectHint;
        private RectTransform _rectHome;
        private RectTransform _rectLeaderboard;
        private RectTransform _rectMatch;
        private RectTransform _rectOptions;
        private RectTransform _rectUndo;
        private RectTransform _rectUndoArrow;
        private Coroutine _undoSpinRoutine;
        private Image _hintLitImage;
        private CanvasGroup _hintLitCanvasGroup;
        private float _lastActivityTime;
        private bool _attractActive;
        private bool _flashPlaying;

        private void Awake()
        {
            _rectOptions = _buttonOptions.GetComponent<RectTransform>();
            _rectHome = _buttonHome.GetComponent<RectTransform>();
            _rectMatch = _buttonMatch.GetComponent<RectTransform>();
            _rectUndo = _buttonUndo.GetComponent<RectTransform>();
            _rectUndoArrow = ResolveUndoArrow();
            _rectHint = _buttonHint.GetComponent<RectTransform>();
            _rectLeaderboard = _buttonLeaderboard.GetComponent<RectTransform>();

            AddBlob(_buttonOptions);
            AddBlob(_buttonHome);
            AddBlob(_buttonMatch);
            AddBlob(_buttonUndo);
            AddBlob(_buttonHint);
            AddBlob(_buttonLeaderboard);
        }

        protected override void Start()
        {
            base.Start();

            EnsureHintLitOverlay();
            SetHintLitAlpha(0f);
            NotifyPlayerActivity();

            _gamePopup.OptionsCommand.BindTo(_buttonOptions).AddTo(this);
            _gameControls.HomeCommand.BindTo(_buttonHome).AddTo(this);
            _gamePopup.MatchCommand.BindTo(_buttonMatch).AddTo(this);
            _gameControls.UndoCommand.BindTo(_buttonUndo).AddTo(this);
            _gameControls.HintCommand.BindTo(_buttonHint).AddTo(this);
            _gamePopup.LeaderboardCommand.BindTo(_buttonLeaderboard).AddTo(this);

            _gameControls.UndoCommand.Subscribe(_ => SpinUndoArrow()).AddTo(this);
            _gameControls.UndoRattle.Subscribe(_ => RattleUndoButton()).AddTo(this);

            _buttonHint
                .OnClickAsObservable()
                .Subscribe(_ => PlayHintLitFlash())
                .AddTo(this);

            // Any successful move / draw / refill / undo increments Moves → reset idle
            _movesService.Moves.Subscribe(_ => NotifyPlayerActivity()).AddTo(this);

            _gameState
                .State.Subscribe(state =>
                {
                    if (state == Game.State.Playing)
                        NotifyPlayerActivity();
                    else
                        StopAttract(resetAlpha: true);
                })
                .AddTo(this);

            _gameState
                .State.Pairwise()
                .Subscribe(pair =>
                {
                    if (pair.Previous == Game.State.Dealing && pair.Current == Game.State.Playing)
                        PlayAppearStagger();
                })
                .AddTo(this);
        }

        private void Update()
        {
            if (_flashPlaying || _attractActive)
                return;

            if (_gameState == null || _gameState.State.Value != Game.State.Playing)
                return;

            if (Time.time - _lastActivityTime < hintIdleDelay)
                return;

            StartAttract();
        }

        private void NotifyPlayerActivity()
        {
            _lastActivityTime = Time.time;
            if (!_flashPlaying)
                StopAttract(resetAlpha: true);
        }

        private void StartAttract()
        {
            EnsureHintLitOverlay();
            if (_hintLitCanvasGroup == null)
                return;

            _attractActive = true;
            DOTween.Kill(HintLitAttractId);
            SetHintLitAlpha(hintAttractMinAlpha);

            DOTween
                .To(
                    () => _hintLitCanvasGroup.alpha,
                    a => SetHintLitAlpha(a),
                    hintAttractMaxAlpha,
                    Mathf.Max(0.1f, hintAttractHalfDuration)
                )
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetId(HintLitAttractId);
        }

        private void StopAttract(bool resetAlpha)
        {
            _attractActive = false;
            DOTween.Kill(HintLitAttractId);

            if (resetAlpha && !_flashPlaying)
                SetHintLitAlpha(0f);
        }

        private void EnsureHintLitOverlay()
        {
            if (_buttonHint == null)
                return;

            if (_hintLitSprite == null)
            {
                Debug.LogWarning(
                    "[GameControlsPresenter] Hint Lit Sprite is not assigned — Hint flash skipped."
                );
                return;
            }

            var existing = _buttonHint.transform.Find("HintLit");
            if (existing != null)
            {
                _hintLitImage = existing.GetComponent<Image>();
                if (_hintLitImage == null)
                    _hintLitImage = existing.gameObject.AddComponent<Image>();
            }
            else
            {
                var go = new GameObject(
                    "HintLit",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );
                go.transform.SetParent(_buttonHint.transform, false);
                _hintLitImage = go.GetComponent<Image>();
            }

            _hintLitCanvasGroup = _hintLitImage.GetComponent<CanvasGroup>();
            if (_hintLitCanvasGroup == null)
                _hintLitCanvasGroup = _hintLitImage.gameObject.AddComponent<CanvasGroup>();

            _hintLitImage.gameObject.SetActive(true);
            _hintLitImage.enabled = true;
            _hintLitImage.sprite = _hintLitSprite;
            _hintLitImage.preserveAspect = true;
            _hintLitImage.raycastTarget = false;
            _hintLitImage.color = Color.white;
            _hintLitImage.transform.SetAsLastSibling();

            var rect = _hintLitImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            _hintLitCanvasGroup.blocksRaycasts = false;
            _hintLitCanvasGroup.interactable = false;
        }

        private void PlayHintLitFlash()
        {
            EnsureHintLitOverlay();

            if (_rectHint != null)
            {
                DOTween.Kill(HintLitTweenId + "Punch");
                _rectHint.localScale = Vector3.one;
                _rectHint
                    .DOPunchScale(Vector3.one * hintPunchStrength, hintPunchDuration, 8, 0.55f)
                    .SetId(HintLitTweenId + "Punch");
            }

            if (_hintLitCanvasGroup == null)
                return;

            _flashPlaying = true;
            StopAttract(resetAlpha: false);

            DOTween.Kill(HintLitTweenId);
            SetHintLitAlpha(0f);

            var fade = Mathf.Max(0.05f, hintLitFlashDuration * 0.5f);
            var hold = Mathf.Max(0f, hintLitFlashDuration * 0.3f);
            var sequence = DOTween.Sequence().SetId(HintLitTweenId);
            sequence.Append(
                DOTween
                    .To(() => _hintLitCanvasGroup.alpha, a => SetHintLitAlpha(a), 1f, fade)
                    .SetEase(Ease.Linear)
            );
            if (hold > 0f)
                sequence.AppendInterval(hold);
            sequence.Append(
                DOTween
                    .To(() => _hintLitCanvasGroup.alpha, a => SetHintLitAlpha(a), 0f, fade)
                    .SetEase(Ease.Linear)
            );
            sequence.OnComplete(() =>
            {
                _flashPlaying = false;
                // Using a hint counts as activity — restart idle timer
                NotifyPlayerActivity();
            });
            sequence.OnKill(() =>
            {
                _flashPlaying = false;
            });
        }

        private void SetHintLitAlpha(float alpha)
        {
            if (_hintLitCanvasGroup != null)
                _hintLitCanvasGroup.alpha = Mathf.Clamp01(alpha);

            if (_hintLitImage != null)
            {
                var color = _hintLitImage.color;
                color.a = 1f;
                _hintLitImage.color = color;
            }
        }

        private RectTransform ResolveUndoArrow()
        {
            if (_undoArrow != null)
                return _undoArrow;

            if (_buttonUndo == null)
                return null;

            var child = _buttonUndo.transform.Find(UndoArrowChildName);
            if (child != null)
            {
                if (!child.gameObject.activeSelf)
                {
                    Debug.LogWarning(
                        "[GameControlsPresenter] 'Image Inner' was inactive. Enabling it so the undo spin is visible."
                    );
                    child.gameObject.SetActive(true);
                }

                return child as RectTransform ?? child.GetComponent<RectTransform>();
            }

            Debug.LogWarning(
                "[GameControlsPresenter] Undo arrow child 'Image Inner' was not found — undo spin skipped."
            );
            return null;
        }

        private void SpinUndoArrow()
        {
            if (_rectUndoArrow == null)
                _rectUndoArrow = ResolveUndoArrow();

            if (_rectUndoArrow == null)
                return;

            if (_undoSpinRoutine != null)
                StopCoroutine(_undoSpinRoutine);

            DOTween.Kill(UndoSpinTweenId);
            _undoSpinRoutine = StartCoroutine(SpinUndoArrowRoutine(_rectUndoArrow, UndoSpinSeconds));
        }

        private IEnumerator SpinUndoArrowRoutine(RectTransform arrow, float duration)
        {
            Debug.Log(
                $"[Undo] spinning '{arrow.name}' for {duration:0.00}s (active={arrow.gameObject.activeInHierarchy})",
                arrow
            );

            var elapsed = 0f;
            arrow.localEulerAngles = Vector3.zero;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var z = Mathf.Lerp(0f, 360f, Mathf.Clamp01(elapsed / duration));
                arrow.localEulerAngles = new Vector3(0f, 0f, z);
                yield return null;
            }

            arrow.localEulerAngles = Vector3.zero;
            _undoSpinRoutine = null;
        }

        private void RattleUndoButton()
        {
            if (_rectUndo == null)
                return;

            DOTween.Kill(UndoRattleId);
            _rectUndo
                .DOPunchAnchorPos(new Vector2(undoRattleDistance, 0f), undoRattleDuration, 12, 0.7f)
                .SetId(UndoRattleId);
        }

        /// <summary>
        /// Left-to-right wave after the deal. Undo keeps its slot in time but does not pop.
        /// </summary>
        private void PlayAppearStagger()
        {
            var delay = 0f;
            var step = Mathf.Max(0f, appearStagger);
            PlayAppear(_buttonOptions, delay);
            delay += step;
            PlayAppear(_buttonHome, delay);
            delay += step;
            PlayAppear(_buttonMatch, delay);
            delay += step;
            // Undo: skip pop, keep the beat so Hint/Leaderboard stay on the wave.
            delay += step;
            PlayAppear(_buttonHint, delay);
            delay += step;
            PlayAppear(_buttonLeaderboard, delay);
        }

        private static void PlayAppear(Button button, float delay)
        {
            if (button == null)
                return;

            var blob = button.GetComponent<BlobButton>();
            blob?.PlayAppearPop(delay);
        }

        private static void AddBlob(Button button)
        {
            if (button == null || button.GetComponent<BlobButton>() != null)
                return;

            button.gameObject.AddComponent<BlobButton>();
        }

        protected override void OnOrientationChanged(bool isLandscape)
        {
            DOTween.Kill(UndoRattleId);
            _rectOptions.anchoredPosition = new Vector2(
                isLandscape ? -350 : -275,
                _rectOptions.anchoredPosition.y
            );
            _rectHome.anchoredPosition = new Vector2(
                isLandscape ? -210 : -165,
                _rectHome.anchoredPosition.y
            );
            _rectMatch.anchoredPosition = new Vector2(
                isLandscape ? -70 : -55,
                _rectMatch.anchoredPosition.y
            );
            _rectUndo.anchoredPosition = new Vector2(
                isLandscape ? 70 : 55,
                _rectUndo.anchoredPosition.y
            );
            _rectHint.anchoredPosition = new Vector2(
                isLandscape ? 210 : 165,
                _rectHint.anchoredPosition.y
            );
            _rectLeaderboard.anchoredPosition = new Vector2(
                isLandscape ? 350 : 275,
                _rectLeaderboard.anchoredPosition.y
            );
        }

        private void OnDisable()
        {
            StopAttract(resetAlpha: true);
            DOTween.Kill(HintLitTweenId);
            DOTween.Kill(HintLitTweenId + "Punch");
            DOTween.Kill(UndoSpinTweenId);
            DOTween.Kill(UndoRattleId);
            if (_undoSpinRoutine != null)
            {
                StopCoroutine(_undoSpinRoutine);
                _undoSpinRoutine = null;
            }
            _flashPlaying = false;
        }
    }
}
