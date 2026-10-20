using System;
using System.Collections.Generic;
using DG.Tweening;
using Solitaire.Models;
using Solitaire.Services;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Solitaire.Presenters
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class CardPresenter
        : MonoBehaviour,
            IPoolable<Card.Suits, Card.Types, IMemoryPool>,
            IDisposable,
            IBeginDragHandler,
            IDragHandler,
            IEndDragHandler,
            IDropHandler,
            IPointerClickHandler,
            IPointerDownHandler,
            IPointerExitHandler
    {
        private const float DoubleClickInterval = 0.4f;
        private const float MoveEpsilon = 0.00001f;
        private const int AnimOrder = 100;

        [Header("Sprites")]
        [SerializeField]
        private SpriteRenderer _back;

        [SerializeField]
        private SpriteRenderer _front;

        [SerializeField]
        private SpriteRenderer _type;

        [SerializeField]
        private SpriteRenderer _suit1;

        [SerializeField]
        private SpriteRenderer _suit2;

        [Inject]
        private readonly Card _card;

        [Inject]
        private readonly Card.Config _config;

        [Inject]
        private readonly IDragAndDropHandler _dndHandler;

        [Inject]
        private readonly Game _game;

        [Inject]
        private readonly GameState _gameState;

        [Inject]
        private readonly IAudioService _audioService;

        [Inject]
        private readonly ICardSpawner _cardSpawner;

        [Inject(Optional = true)]
        private readonly ICardLandFxService _landFxService;

        [Inject(Optional = true, Id = "AceHomeGlow")]
        private readonly Material _aceHomeGlowMaterial;

        [Inject]
        private readonly IStreakService _streakService;

        [Header("Stock draw")]
        [Tooltip("How high the card hops on the way to waste. 0 = straight slide.")]
        [SerializeField]
        private float stockDrawJump = 0.35f;

        [Tooltip("Seconds for the hop. Independent from other card moves.")]
        [SerializeField]
        private float stockDrawDuration = 0.28f;

        [Tooltip("How far a playable card rattles left/right.")]
        [SerializeField]
        private float stockDrawRattle = 0.1f;

        [SerializeField]
        private float stockDrawRattleDuration = 0.22f;

        [Tooltip("How many cards under the top one squash when you press stock.")]
        [SerializeField]
        private int stockSpringCount = 4;

        [SerializeField]
        private float stockSpringScaleX = 1.04f;

        [SerializeField]
        private float stockSpringScaleY = 0.83f;

        [SerializeField]
        private float stockSpringPressDuration = 0.07f;

        [SerializeField]
        private float stockSpringReleaseDuration = 0.16f;

        [Header("Stock recycle")]
        [Tooltip("How many remaining (already face-down) cards scoop and slinky onto stock.")]
        [SerializeField]
        private int recycleSlinkyCount = 6;

        [SerializeField]
        private float recycleGatherDuration = 0.12f;

        [SerializeField]
        private float recycleGatherScaleX = 0.92f;

        [SerializeField]
        private float recycleGatherScaleY = 0.78f;

        [Tooltip("Hop height for the face-down slinky packet.")]
        [SerializeField]
        private float recycleTravelJump = 0.22f;

        [SerializeField]
        private float recycleTravelDuration = 0.24f;

        [Tooltip("Delay between each slinky card taking off.")]
        [SerializeField]
        private float recycleStagger = 0.022f;

        [Tooltip("How quickly the last 3 faces slide into one stack.")]
        [SerializeField]
        private float recycleReturnDuration = 0.15f;

        [Tooltip("Scale-X flip time for the last 3. Shorter than a stock draw.")]
        [SerializeField]
        private float recycleReturnFlipDuration = 0.16f;

        [SerializeField]
        private float recycleLandPunch = 0.1f;

        [SerializeField]
        private float recycleLandPunchDuration = 0.14f;

        [Header("Tableau land")]
        [Tooltip("How wide the card goes on a legal tableau land. 1.08 = 8% wider.")]
        [SerializeField]
        private float tableauLandSquashX = 1.08f;

        [Tooltip("How short the card goes. 0.92 = 8% flatter.")]
        [SerializeField]
        private float tableauLandSquashY = 0.92f;

        [Tooltip("One squash out and back. Keep short so the next drag is clean.")]
        [SerializeField]
        private float tableauLandSquashDuration = 0.12f;

        [Tooltip("Card underneath uses this fraction of the squash. 0 = landing card only.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float tableauLandUnderStrength = 0.65f;

        [Header("Stock foil (top card back only)")]
        [Tooltip("How many stock cards count as 100%. Klondike leftover after deal is 24.")]
        [SerializeField]
        private int stockFoilFullCount = 24;

        [Header("Stock foil at 100% cards left")]
        [SerializeField]
        private float stockFoilStrengthAtFull = 0.35f;

        [SerializeField]
        private float stockFoilSpeedAtFull = 0.25f;

        [SerializeField]
        private float stockFoilPowerAtFull = 2f;

        [Header("Stock foil at 50% cards left")]
        [SerializeField]
        private float stockFoilStrengthAtHalf = 0.5f;

        [SerializeField]
        private float stockFoilSpeedAtHalf = 0.4f;

        [SerializeField]
        private float stockFoilPowerAtHalf = 1.5f;

        [Header("Stock foil at 25% cards left")]
        [SerializeField]
        private float stockFoilStrengthAtQuarter = 0.6f;

        [SerializeField]
        private float stockFoilSpeedAtQuarter = 0.5f;

        [SerializeField]
        private float stockFoilPowerAtQuarter = 1f;

        [Header("Stock foil at 0% / last card")]
        [SerializeField]
        private float stockFoilStrengthAtEmpty = 0.7f;

        [SerializeField]
        private float stockFoilSpeedAtEmpty = 0.55f;

        [SerializeField]
        private float stockFoilPowerAtEmpty = 0.85f;

        [Header("Streak charge foil (4 pips)")]
        [Tooltip("EdgeFoil material swapped onto Front while dragging at 4 pips. Uses the material's own Inspector values as-is.")]
        [SerializeField]
        private Material streakChargeFoilMaterial;

        private BoxCollider2D _collider;
        private AceHomeGlow _aceHomeGlow;
        private RejectGlint _rejectGlint;
        private StockPressSparkle _stockPressSparkle;
        private DragFeedback _dragFeedback;
        private DragSparkle _dragSparkle;
        private float _lastClick;
        private IMemoryPool _pool;
        private Transform _transform;
        private Tweener _tweenMove;

        private Tweener _tweenScale;
        private LandFxVolume _pendingLandVolume = LandFxVolume.Normal;
        private bool _rejectPlayed;
        private bool _homePending;
        private bool _stockDrawArch;
        private bool _stockDrawHold;
        private bool _stockDrawFlightCounted;
        private static int stockDrawFlights;
        private bool _stockPressed;
        private bool _recycleHold;
        private bool _layoutSnap;
        private bool _victoryBounce;
        private bool _recycleFlightCounted;
        private Action _recycleHopComplete;
        private static int recycleFlights;
        private static int recycleHolds;
        private MaterialPropertyBlock stockFoilBlock;
        private Material frontDefaultMaterial;
        private bool streakChargeFoilActive;

        public Card Card => _card;

        public static bool IsRecyclePlaying => recycleHolds > 0 || recycleFlights > 0;

        public int RecycleSlinkyCount => Mathf.Max(1, recycleSlinkyCount);

        public float StockDrawDuration => Mathf.Max(0.08f, stockDrawDuration);

        private void Start()
        {
            _collider = GetComponent<BoxCollider2D>();
            _transform = transform;
            StripLegacyPerCardLandFx();
            CacheFrontDefaultMaterial();

            _card.Alpha.Subscribe(UpdateAlpha).AddTo(this);
            _card.Order.Subscribe(UpdateOrder).AddTo(this);
            _card.IsVisible.Subscribe(UpdateVisiblity).AddTo(this);
            _card.IsInteractable.Subscribe(UpdateInteractability).AddTo(this);
            _card.IsFaceUp.Where(CanFlip).Subscribe(AnimateFlip).AddTo(this);
            _card.Position.Where(CanMove).Subscribe(AnimateMove).AddTo(this);
        }

        /// <summary>
        /// Plays ParticlePulse as the card arrives (slightly early) + a quick scale punch.
        /// Aces also get the same confirm ring as a tableau drop.
        /// </summary>
        public void PlayFoundationLandFx(float moveDurationSeconds)
        {
            PlayFoundationLandFx(moveDurationSeconds, LandFxVolume.Normal);
        }

        public void PlayFoundationLandFx(float moveDurationSeconds, LandFxVolume volume)
        {
            if (_landFxService == null)
                return;

            _pendingLandVolume = volume;

            // Fire before the slide fully ends so the burst peaks on impact
            var delay = Mathf.Max(0f, moveDurationSeconds * 0.55f);

            DOTween.Kill(GetLandFxTweenId());
            DOVirtual
                .DelayedCall(delay, PlayFoundationLandFxNow)
                .SetId(GetLandFxTweenId());
        }

        public void BeginLayoutSnap()
        {
            _layoutSnap = true;
            _victoryBounce = false;
            KillMoveTweens();
        }

        public void EndLayoutSnap()
        {
            _layoutSnap = false;
        }

        /// <summary>
        /// Teleport without a slide. Used to assemble completed foundations.
        /// </summary>
        public void SnapImmediate(Vector3 worldPosition, bool faceUp)
        {
            if (_transform == null)
                _transform = transform;

            _layoutSnap = true;
            _victoryBounce = false;
            KillMoveTweens();

            if (_transform != null)
            {
                _transform.localScale = Vector3.one;
                _transform.position = worldPosition;
            }

            Flip(faceUp);
            if (_card == null)
                return;

            if (_card.IsFaceUp.Value != faceUp)
                _card.IsFaceUp.Value = faceUp;
            _card.Position.Value = worldPosition;
        }

        public void BeginVictoryBounce(int order)
        {
            _victoryBounce = true;
            _layoutSnap = true;
            KillMoveTweens();

            if (_card != null)
            {
                _card.IsVisible.Value = true;
                _card.Alpha.Value = 1f;
                _card.Order.Value = order;
            }

            if (_transform != null)
                _transform.localScale = Vector3.one;

            PlayVictoryFlightTrail();
        }

        public void PlayVictoryFlightTrail()
        {
            if (_landFxService == null)
                return;

            var isRedSuit =
                _card != null
                && (_card.Suit == Card.Suits.Heart || _card.Suit == Card.Suits.Diamond);
            var order = _card != null ? _card.Order.Value * 10 - 2 : 198;
            _landFxService.PlayFlightTrail(
                _transform != null ? _transform : transform,
                isRedSuit,
                order
            );
        }

        public void StopVictoryFlightTrail()
        {
            _landFxService?.StopFlightTrail(_transform != null ? _transform : transform);
        }

        public void SetWorldPosition(Vector3 worldPosition)
        {
            if (_transform == null)
                _transform = transform;
            if (_transform != null)
                _transform.position = worldPosition;
        }

        public void EndVictoryBounce()
        {
            StopVictoryFlightTrail();
            DOTween.Kill(GetVictoryClearTweenId());
            _victoryBounce = false;
            _layoutSnap = false;
            if (_transform != null)
                _transform.localScale = Vector3.one;
            if (_card != null)
                _card.Alpha.Value = 1f;
        }

        /// <summary>
        /// Kings pop, then the whole board shrinks away so the bubble owns the win.
        /// </summary>
        public void PlayVictoryClear(bool king)
        {
            if (_transform == null)
                _transform = transform;

            KillMoveTweens();
            DOTween.Kill(GetVictoryClearTweenId());
            _layoutSnap = true;
            _victoryBounce = false;

            if (_card != null)
                _card.IsInteractable.Value = false;

            if (_transform == null)
                return;

            var fade = king ? 0.2f : 0.16f;
            var seq = DOTween.Sequence().SetId(GetVictoryClearTweenId());
            if (king)
                seq.Append(_transform.DOScale(1.22f, 0.08f).SetEase(Ease.OutQuad));

            seq.Append(_transform.DOScale(0f, fade).SetEase(Ease.InBack, 1.4f));
            if (_card != null)
            {
                seq.Join(
                    DOTween
                        .To(() => _card.Alpha.Value, a => _card.Alpha.Value = a, 0f, fade)
                        .SetEase(Ease.InQuad)
                );
            }
        }

        private void KillMoveTweens()
        {
            _tweenMove?.Kill();
            _tweenMove = null;
            _tweenScale?.Kill();
            _tweenScale = null;
            DOTween.Kill(GetLandFxTweenId());
            DOTween.Kill(GetLandPunchTweenId());
            if (_transform != null)
                _transform.localScale = Vector3.one;
        }

        /// <summary>
        /// Soft center glyph pulse while an Ace sits alone on a foundation.
        /// </summary>
        public void StartAceFoundationIdle(float moveDurationSeconds)
        {
            if (_card == null || _card.Type != Card.Types.Ace)
                return;

            EnsureAceHomeGlow();
            if (_aceHomeGlow == null)
                return;

            var delay = Mathf.Max(0f, moveDurationSeconds * 0.55f);
            DOTween.Kill(GetAceRimTweenId());
            DOVirtual
                .DelayedCall(delay, StartAceFoundationIdleNow)
                .SetId(GetAceRimTweenId());
        }

        public void StopAceFoundationIdle(bool fadeOut = true)
        {
            DOTween.Kill(GetAceRimTweenId());
            _aceHomeGlow?.Stop(fadeOut);
        }

        public void BeginStockDrawArch()
        {
            CancelRecycle();
            _stockDrawArch = true;
            _stockDrawHold = true;
        }

        public void ReleaseStockDrawToWaste()
        {
            _stockDrawHold = false;
            _stockDrawArch = false;
            if (_card == null)
                return;

            PlayStockDrawMove(_card.Position.Value);
        }

        /// <summary>
        /// Freeze this card while refill runs. Fan cards keep their face
        /// until they hop back with the same flip as a stock draw.
        /// </summary>
        public void BeginRecycleHold(bool stayVisible, bool keepFace)
        {
            if (!_recycleHold)
                recycleHolds++;

            _recycleHold = true;
            _recycleHopComplete = null;
            StopMoveNudge();
            _tweenMove?.Kill();
            _tweenMove = null;
            _tweenScale?.Kill();
            _tweenScale = null;
            DOTween.Kill(GetStockDrawTweenId());
            DOTween.Kill(GetStockSpringId());
            DOTween.Kill(GetRecycleTweenId());

            if (_transform == null)
                _transform = transform;

            if (!keepFace)
                Flip(false);

            ApplyVisibility(stayVisible);
            if (_collider != null)
                _collider.enabled = false;
        }

        /// <summary>
        /// Slide this card onto the fan stack so the last 3 look like one card.
        /// </summary>
        public void PlayRecycleSlideTo(Vector3 stackPosition, int stackOrder, Action onComplete)
        {
            if (_transform == null)
                _transform = transform;

            if (_transform == null)
            {
                onComplete?.Invoke();
                return;
            }

            _recycleHold = true;
            _recycleHopComplete = onComplete;
            DOTween.Kill(GetRecycleTweenId());
            _tweenMove?.Kill();
            _tweenMove = null;

            if (_card != null)
            {
                _card.OrderToRestore = stackOrder;
                _card.Order.Value = AnimOrder + stackOrder;
            }

            var duration = Mathf.Max(0.04f, recycleGatherDuration);
            _transform
                .DOLocalMove(stackPosition, duration)
                .SetEase(Ease.OutQuad)
                .SetId(GetRecycleTweenId())
                .OnComplete(InvokeRecycleHopComplete);
        }

        /// <summary>
        /// Hide this fan card under the stack so only one face is seen.
        /// </summary>
        public void HideUnderRecycleStack()
        {
            Flip(false);
            ApplyVisibility(false);
        }

        /// <summary>
        /// Same scale-X flip as a stock draw, face to back.
        /// </summary>
        public void PlayRecycleFlipToBack(Action onComplete)
        {
            if (_transform == null)
                _transform = transform;

            if (_transform == null)
            {
                onComplete?.Invoke();
                return;
            }

            _recycleHold = true;
            _recycleHopComplete = onComplete;
            PlayDrawFlipToBack(InvokeRecycleHopComplete);
        }

        /// <summary>
        /// Scoop toward a packet, then hop to stock. Used for the rest of the deck after the last 3.
        /// </summary>
        public void PlayRecycleSlinky(Vector3 packetPosition, int slinkyIndex, int sortOrder)
        {
            if (_transform == null)
                _transform = transform;

            if (_transform == null || _card == null)
                return;

            _recycleHold = true;
            _recycleHopComplete = null;
            DOTween.Kill(GetRecycleTweenId());
            _tweenMove?.Kill();
            _tweenMove = null;
            _tweenScale?.Kill();
            _tweenScale = null;

            Flip(false);
            _recycleFlightCounted = true;
            recycleFlights++;

            var dest = _card.Position.Value;
            var gather = Mathf.Max(0.04f, recycleGatherDuration);
            var travel = Mathf.Max(0.08f, recycleTravelDuration);
            var jump = Mathf.Max(0f, recycleTravelJump);
            var stagger = Mathf.Max(0f, recycleStagger) * Mathf.Max(0, slinkyIndex);
            var pressed = new Vector3(recycleGatherScaleX, recycleGatherScaleY, 1f);

            _card.OrderToRestore = Mathf.Max(0, sortOrder);
            _card.Order.Value = AnimOrder + Mathf.Max(0, sortOrder);

            var sequence = DOTween.Sequence().SetId(GetRecycleTweenId());
            sequence.Append(_transform.DOLocalMove(packetPosition, gather).SetEase(Ease.OutQuad));
            sequence.Join(_transform.DOScale(pressed, gather).SetEase(Ease.OutQuad));
            if (stagger > 0f)
                sequence.AppendInterval(stagger);
            sequence.Append(
                _transform.DOLocalJump(dest, jump, 1, travel).SetEase(Ease.OutQuad)
            );
            sequence.Join(
                _transform
                    .DOScale(Vector3.one, Mathf.Max(0.08f, travel * 0.45f))
                    .SetEase(Ease.OutBack)
            );
            sequence.OnComplete(CompleteRecycleFlight);
            sequence.OnKill(CompleteRecycleFlightOnKill);
        }

        public void FinishRecyclePile()
        {
            _recycleFlightCounted = true;
            recycleFlights = 1;
            CompleteRecycleFlight();
        }

        private void PlayDrawFlipToBack(Action onComplete)
        {
            _tweenScale?.Kill();
            if (_transform == null)
            {
                onComplete?.Invoke();
                return;
            }

            var half = Mathf.Max(0.04f, recycleReturnFlipDuration * 0.5f);
            _tweenScale = _transform
                .DOScaleX(0f, half)
                .SetLoops(2, LoopType.Yoyo)
                .SetEase(Ease.Linear)
                .SetAutoKill(false)
                .OnStepComplete(() => Flip(false))
                .OnComplete(() =>
                {
                    if (_transform != null)
                    {
                        var scale = _transform.localScale;
                        _transform.localScale = new Vector3(1f, scale.y, scale.z);
                    }

                    onComplete?.Invoke();
                });
        }

        private void InvokeRecycleHopComplete()
        {
            var done = _recycleHopComplete;
            _recycleHopComplete = null;
            done?.Invoke();
        }

        /// <summary>
        /// Stop mid-flight and snap to the model (undo / despawn).
        /// </summary>
        public void CancelRecycle()
        {
            if (!_recycleHold && !DOTween.IsTweening(GetRecycleTweenId()))
                return;

            _recycleHopComplete = null;

            if (_recycleFlightCounted)
            {
                _recycleFlightCounted = false;
                recycleFlights = Mathf.Max(0, recycleFlights - 1);
            }

            DOTween.Kill(GetRecycleTweenId());
            _tweenScale?.Kill();
            _tweenScale = null;
            FinishRecycle();
        }

        public void PlayMoveNudge()
        {
            if (_transform == null || _card == null || _card.IsDragged)
                return;

            if (IsStockDrawJumpActive() || (_tweenMove != null && _tweenMove.IsActive()))
                return;

            var strength = Mathf.Max(0.01f, stockDrawRattle);
            var duration = Mathf.Max(0.08f, stockDrawRattleDuration);
            StopMoveNudge();
            _transform
                .DOPunchRotation(new Vector3(0f, 0f, strength * 80f), duration, 12, 0.65f)
                .SetId(GetStockRattleTweenId())
                .OnComplete(SnapToPilePosition)
                .OnKill(SnapToPilePosition);
        }

        public void StopMoveNudge()
        {
            if (!DOTween.IsTweening(GetStockRattleTweenId()))
                return;

            DOTween.Kill(GetStockRattleTweenId());
        }

        private void SnapToPilePosition()
        {
            if (_transform == null || _card == null || _card.IsDragged)
                return;

            _transform.localPosition = _card.Position.Value;
            var euler = _transform.localEulerAngles;
            _transform.localEulerAngles = new Vector3(euler.x, euler.y, 0f);
        }

        public void PlayStockSpringPress()
        {
            if (_transform == null || IsStockDrawJumpActive())
                return;

            DOTween.Kill(GetStockSpringId());
            _tweenScale?.Kill();
            _tweenScale = null;
            var pressed = new Vector3(stockSpringScaleX, stockSpringScaleY, 1f);
            _transform
                .DOScale(pressed, Mathf.Max(0.04f, stockSpringPressDuration))
                .SetEase(Ease.OutQuad)
                .SetId(GetStockSpringId());
        }

        public void PlayStockSpringRelease()
        {
            if (_transform == null)
                return;

            DOTween.Kill(GetStockSpringId());
            _tweenScale?.Kill();
            _tweenScale = null;
            _transform
                .DOScale(Vector3.one, Mathf.Max(0.06f, stockSpringReleaseDuration))
                .SetEase(Ease.OutBack)
                .SetId(GetStockSpringId());
        }

        private const string StockPressPreviewId = "StockPressPreview";

        /// <summary>
        /// Editor / Inspector: play the same squash as a real stock press.
        /// Works even if this card is not on the stock pile.
        /// </summary>
        [ContextMenu("Preview Stock Press")]
        public void PreviewStockPress()
        {
            if (!TryGetStockForPreview(out var stock))
                return;

            DOTween.Kill(StockPressPreviewId);
            stock.PressStockSpring();
        }

        [ContextMenu("Preview Stock Release")]
        public void PreviewStockRelease()
        {
            if (!TryGetStockForPreview(out var stock))
                return;

            DOTween.Kill(StockPressPreviewId);
            stock.ReleaseStockSpring(stock.Card.Pile);
        }

        /// <summary>
        /// Press, hold, then release — same look as click-and-let-go on the stock.
        /// </summary>
        public void PreviewStockPressThenRelease(float holdSeconds = 0.4f)
        {
            if (!TryGetStockForPreview(out var stock))
                return;

            DOTween.Kill(StockPressPreviewId);
            stock.PressStockSpring();
            DOVirtual
                .DelayedCall(
                    Mathf.Max(0.15f, holdSeconds),
                    () =>
                    {
                        if (stock != null && stock.Card?.Pile != null)
                            stock.ReleaseStockSpring(stock.Card.Pile);
                    }
                )
                .SetId(StockPressPreviewId);
        }

        private bool TryGetStockForPreview(out CardPresenter stock)
        {
            stock = null;
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[CardPresenter] Enter Play Mode, start a match, then preview stock press."
                );
                return false;
            }

            stock = ResolveStockPresenter();
            if (stock == null)
            {
                Debug.LogWarning(
                    "[CardPresenter] No stock pile in play. Start a match first."
                );
                return false;
            }

            return true;
        }

        private CardPresenter ResolveStockPresenter()
        {
            if (_card?.Pile != null && _card.Pile.IsStock && _card.IsOnTop)
                return this;

            return FindTopStockPresenter(_cardSpawner != null ? _cardSpawner.Cards : null)
                ?? FindTopStockPresenter(FindObjectsByType<CardPresenter>(FindObjectsSortMode.None));
        }

        private static CardPresenter FindTopStockPresenter(IList<CardPresenter> presenters)
        {
            if (presenters == null)
                return null;

            CardPresenter top = null;
            var bestIndex = -1;
            for (var i = 0; i < presenters.Count; i++)
            {
                var presenter = presenters[i];
                var pile = presenter?.Card?.Pile;
                if (pile == null || !pile.IsStock)
                    continue;

                var index = pile.Cards.IndexOf(presenter.Card);
                if (index > bestIndex)
                {
                    bestIndex = index;
                    top = presenter;
                }
            }

            return top;
        }

        private void PressStockSpring()
        {
            var pile = _card != null ? _card.Pile : null;
            if (pile == null || !pile.IsStock)
                return;

            // You always press the top card — keep it in front of the stack.
            var top = pile.TopCard();
            if (top != null)
            {
                var index = pile.Cards.IndexOf(top);
                top.OrderToRestore = index;
                top.Order.Value = AnimOrder + index;
            }

            var under = Mathf.Max(0, pile.Cards.Count - 1);
            var start = Mathf.Max(0, under - Mathf.Max(1, stockSpringCount));
            for (var i = start; i < under; i++)
                FindPresenter(pile.Cards[i])?.PlayStockSpringPress();

            PlayStockSpringPress();
        }

        private void PlayStockPressSparkle()
        {
            EnsureStockPressSparkle();
            var popDelay = Mathf.Max(0.04f, stockSpringReleaseDuration) * 0.45f;
            _stockPressSparkle?.PlayAfterPop(popDelay);
        }

        private void EnsureStockPressSparkle()
        {
            if (_stockPressSparkle != null)
            {
                _stockPressSparkle.Bind(_back);
                return;
            }

            _stockPressSparkle = GetComponentInChildren<StockPressSparkle>(true);
            if (_stockPressSparkle == null)
            {
                var parent = _transform != null ? _transform : transform;
                var go = new GameObject("StockPressSparkle", typeof(ParticleSystem));
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(0f, 0.15f, -0.02f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                _stockPressSparkle = go.AddComponent<StockPressSparkle>();
            }

            _stockPressSparkle.Bind(_back);
        }

        private void EnsureDragFeedback()
        {
            if (_dragFeedback != null)
                return;

            _dragFeedback = GetComponent<DragFeedback>();
            if (_dragFeedback == null)
            {
                _dragFeedback = gameObject.AddComponent<DragFeedback>();
            }
        }

        private void EnsureDragSparkle()
        {
            if (_dragSparkle != null)
            {
                _dragSparkle.Bind(_front.gameObject.activeSelf ? _front : _back);
                return;
            }

            _dragSparkle = GetComponentInChildren<DragSparkle>(true);
            if (_dragSparkle == null)
            {
                var parent = _transform != null ? _transform : transform;
                var go = new GameObject("DragSparkle", typeof(ParticleSystem));
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(0f, 0f, -0.03f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                _dragSparkle = go.AddComponent<DragSparkle>();
            }

            _dragSparkle.Bind(_front.gameObject.activeSelf ? _front : _back);
        }

        private void ReleaseStockSpring(Pile stock)
        {
            if (stock == null || !stock.IsStock)
                return;

            for (var i = 0; i < stock.Cards.Count; i++)
            {
                var card = stock.Cards[i];
                if (card == null)
                    continue;

                card.OrderToRestore = i;
                card.Order.Value = i;
                FindPresenter(card)?.PlayStockSpringRelease();
            }

            FindPresenter(stock.TopCard())?.PlayStockPressSparkle();
        }

        private CardPresenter FindPresenter(Card card)
        {
            if (card == null || _cardSpawner == null)
                return null;

            for (var i = 0; i < _cardSpawner.Cards.Count; i++)
            {
                var presenter = _cardSpawner.Cards[i];
                if (presenter != null && presenter.Card == card)
                    return presenter;
            }

            return this.Card == card ? this : null;
        }

        /// <summary>
        /// Soft ring plus a short squash when a card lands on a valid tableau target.
        /// The card underneath also squashes a bit, so the stack feels like it caught it.
        /// </summary>
        public void PlaySoftWhiteLandFx(float moveDurationSeconds)
        {
            if (_landFxService == null)
                return;

            var delay = Mathf.Max(0f, moveDurationSeconds * 0.55f);

            DOTween.Kill(GetLandFxTweenId());
            DOVirtual
                .DelayedCall(delay, PlaySoftWhiteLandFxNow)
                .SetId(GetLandFxTweenId());
        }

        private void PlayFoundationLandFxNow()
        {
            if (_card == null || _landFxService == null)
                return;

            var volume = _pendingLandVolume;
            var suitIndex = (int)_card.Suit;
            var suitSprite =
                _config.SuitSprites != null && suitIndex < _config.SuitSprites.Length
                    ? _config.SuitSprites[suitIndex]
                    : null;
            var rankSprite = GetTypeSprite(_card.Suit, _card.Type);
            var isRedSuit =
                _card.Suit == Card.Suits.Heart || _card.Suit == Card.Suits.Diamond;
            var playAceRipple = volume.AceRipple && _card.Type == Card.Types.Ace;
            var order = _card.Order.Value * 10 + 25;

            _landFxService.PlayFoundation(
                _transform != null ? _transform : transform,
                GetFaceRendererForFx(),
                suitSprite,
                rankSprite,
                isRedSuit,
                playAceRipple,
                order,
                volume
            );

            if (volume.PunchCard)
                PunchCard(0.12f, 0.18f);
        }

        private void PlaySoftWhiteLandFxNow()
        {
            if (_landFxService == null)
                return;

            var order = _card != null ? _card.Order.Value * 10 + 25 : 500;
            _landFxService.PlayTableau(
                _transform != null ? _transform : transform,
                GetFaceRendererForFx(),
                order
            );
            PlayTableauLandSquash(1f);
            PlayUnderCardTableauSquash();
        }

        private SpriteRenderer GetFaceRendererForFx()
        {
            if (_front != null && _front.gameObject.activeSelf)
                return _front;
            return _back;
        }

        /// <summary>
        /// Old builds parented Pulse/Stars under every card. Strip leftovers so idle Hierarchy stays clean.
        /// </summary>
        private void StripLegacyPerCardLandFx()
        {
            var parent = _transform != null ? _transform : transform;
            DestroyLegacyNamed(parent, "ParticlePulse");
            DestroyLegacyNamed(parent, "Stars");
            DestroyLegacyNamed(parent, "SoftRippleConfirm");
            DestroyLegacyNamed(parent, "SoftRippleCore");
        }

        private static void DestroyLegacyNamed(Transform parent, string childName)
        {
            if (parent == null)
                return;

            // Collect first — destroying while walking GetComponentsInChildren is unsafe.
            var matches = new List<GameObject>(4);
            var transforms = parent.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null || t == parent)
                    continue;
                if (t.name == childName || t.name.StartsWith(childName + "_", StringComparison.Ordinal))
                    matches.Add(t.gameObject);
            }

            for (var i = 0; i < matches.Count; i++)
            {
                if (matches[i] != null)
                    Destroy(matches[i]);
            }
        }

        /// <summary>
        /// One short squash (wide + flat) then back to rest. Same idea as Home land kick.
        /// strength 1 = full squash. 0.65 = the card you dropped onto.
        /// </summary>
        private void PlayTableauLandSquash(float strength)
        {
            if (_transform == null)
                return;

            var amount = Mathf.Clamp01(strength);
            if (amount <= 0f)
                return;

            var punchId = GetLandPunchTweenId();
            DOTween.Kill(punchId);
            _transform.localScale = Vector3.one;

            var squash = new Vector3(
                Mathf.Lerp(1f, tableauLandSquashX, amount),
                Mathf.Lerp(1f, tableauLandSquashY, amount),
                1f
            );
            var duration = Mathf.Max(0.04f, tableauLandSquashDuration);

            _transform
                .DOScale(squash, duration * 0.5f)
                .SetEase(Ease.OutQuad)
                .SetLoops(2, LoopType.Yoyo)
                .SetId(punchId)
                .OnComplete(RestoreTableauLandScale)
                .OnKill(RestoreTableauLandScale);
        }

        private void RestoreTableauLandScale()
        {
            if (_transform != null)
                _transform.localScale = Vector3.one;
        }

        /// <summary>
        /// The card under this one also takes the hit, a bit softer.
        /// Empty tableau (King) has no under card — skip.
        /// </summary>
        private void PlayUnderCardTableauSquash()
        {
            if (_card?.Pile == null || tableauLandUnderStrength <= 0f)
                return;

            var cards = _card.Pile.Cards;
            var index = cards.IndexOf(_card);
            if (index <= 0)
                return;

            var under = FindPresenter(cards[index - 1]);
            if (under == null || under == this)
                return;

            under.PlayTableauLandSquash(tableauLandUnderStrength);
        }

        private void StartAceFoundationIdleNow()
        {
            if (_card == null || _card.Type != Card.Types.Ace)
                return;

            // Still alone on foundation?
            if (_card.Pile == null || !_card.Pile.IsFoundation || _card.Pile.Cards.Count != 1)
                return;

            EnsureAceHomeGlow();
            if (_aceHomeGlow == null)
                return;

            _aceHomeGlow.Play();
        }

        private void EnsureRejectGlint()
        {
            if (_rejectGlint == null)
            {
                _rejectGlint = GetComponentInChildren<RejectGlint>(true);
                if (_rejectGlint == null)
                {
                    var parent = _transform != null ? _transform : transform;
                    var go = new GameObject("RejectGlint");
                    go.transform.SetParent(parent, false);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localScale = Vector3.one;
                    go.AddComponent<SpriteRenderer>();
                    _rejectGlint = go.AddComponent<RejectGlint>();
                }
            }

            if (_aceHomeGlowMaterial != null)
                _rejectGlint.SetFoilMaterial(_aceHomeGlowMaterial);
        }

        private void PunchCard(float strength, float duration)
        {
            if (_transform == null)
                return;

            var punchId = GetLandPunchTweenId();
            DOTween.Kill(punchId);
            _transform.localScale = Vector3.one;
            _transform.DOPunchScale(Vector3.one * strength, duration, 6, 0.5f).SetId(punchId);
        }

        private void EnsureAceHomeGlow()
        {
            if (_aceHomeGlow != null)
                return;

            _aceHomeGlow = GetComponentInChildren<AceHomeGlow>(true);
            if (_aceHomeGlow != null)
            {
                if (_aceHomeGlowMaterial != null)
                    _aceHomeGlow.SetGlowMaterial(_aceHomeGlowMaterial);
                return;
            }

            var parent = _transform != null ? _transform : transform;
            var go = new GameObject("AceHomeGlow");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;
            go.AddComponent<SpriteRenderer>();
            _aceHomeGlow = go.AddComponent<AceHomeGlow>();
            if (_aceHomeGlowMaterial != null)
                _aceHomeGlow.SetGlowMaterial(_aceHomeGlowMaterial);
        }

        private string GetLandFxTweenId()
        {
            return "FoundationLandFx" + GetInstanceID();
        }

        private string GetLandPunchTweenId()
        {
            return "FoundationLandPunch" + GetInstanceID();
        }

        private string GetAceRimTweenId()
        {
            return "AceFoundationIdleDelay" + GetInstanceID();
        }

        private string GetRejectTweenId()
        {
            return "CardRejectJelly" + GetInstanceID();
        }

        private string GetVictoryClearTweenId()
        {
            return "VictoryBoardClear" + GetInstanceID();
        }

        private string GetStockDrawTweenId()
        {
            return "StockDrawJump" + GetInstanceID();
        }

        private string GetStockRattleTweenId()
        {
            return "StockDrawRattle" + GetInstanceID();
        }

        private string GetStockSpringId()
        {
            return "StockSpring" + GetInstanceID();
        }

        private string GetRecycleTweenId()
        {
            return "StockRecycle" + GetInstanceID();
        }

        private void CompleteRecycleFlightOnKill()
        {
            if (!_recycleFlightCounted)
                return;

            CompleteRecycleFlight();
        }

        private void CompleteRecycleFlight()
        {
            if (!_recycleFlightCounted)
                return;

            _recycleFlightCounted = false;
            recycleFlights = Mathf.Max(0, recycleFlights - 1);
            if (recycleFlights > 0)
                return;

            var pile = _card != null ? _card.Pile : null;
            if (pile == null)
            {
                FinishRecycle();
                return;
            }

            for (var i = 0; i < pile.Cards.Count; i++)
            {
                var card = pile.Cards[i];
                if (card == null)
                    continue;

                card.OrderToRestore = i;
                FindPresenter(card)?.FinishRecycle();
            }

            var top = FindPresenter(pile.TopCard());
            top?.PunchCard(recycleLandPunch, recycleLandPunchDuration);
            if (pile.Cards.Count >= 2)
            {
                var under = FindPresenter(pile.Cards[^2]);
                under?.PunchCard(recycleLandPunch * 0.6f, recycleLandPunchDuration);
            }
        }

        private void FinishRecycle()
        {
            if (_recycleHold)
                recycleHolds = Mathf.Max(0, recycleHolds - 1);

            _recycleHold = false;
            _recycleHopComplete = null;
            if (_transform == null)
                _transform = transform;

            if (_transform != null)
            {
                if (_tweenScale == null || !_tweenScale.IsActive())
                    _transform.localScale = Vector3.one;
                if (_card != null)
                    _transform.localPosition = _card.Position.Value;
            }

            if (_card == null)
                return;

            Flip(_card.IsFaceUp.Value);
            ApplyVisibility(_card.IsVisible.Value);
            UpdateInteractability(_card.IsInteractable.Value);
            _card.Order.Value = _card.OrderToRestore;
        }

        /// <summary>
        /// OnDrop already played the error sound — skip a second one in OnEndDrag.
        /// </summary>
        public void MarkRejectPlayed()
        {
            _rejectPlayed = true;
        }

        /// <summary>
        /// Squash → stretch → wobble like soft plastic. Optionally fly home after.
        /// </summary>
        public void PlayReject(bool returnHomeAfter)
        {
            ClearStreakChargeFoil();

            if (_transform == null)
                _transform = transform;

            _rejectPlayed = true;
            _homePending = returnHomeAfter;

            DOTween.Kill(GetRejectTweenId());
            DOTween.Kill(GetLandPunchTweenId());
            _transform.localScale = Vector3.one;

            EnsureRejectGlint();
            _rejectGlint?.Play();

            var squash = new Vector3(1.12f, 0.82f, 1f);
            var stretch = new Vector3(0.88f, 1.14f, 1f);
            var wobble = new Vector3(1.06f, 0.94f, 1f);

            var sequence = DOTween.Sequence().SetId(GetRejectTweenId());
            sequence.Append(_transform.DOScale(squash, 0.08f).SetEase(Ease.OutQuad));
            sequence.Append(_transform.DOScale(stretch, 0.08f).SetEase(Ease.OutQuad));
            sequence.Append(_transform.DOScale(wobble, 0.06f).SetEase(Ease.InOutSine));
            sequence.Append(_transform.DOScale(Vector3.one, 0.08f).SetEase(Ease.OutSine));
            sequence.OnComplete(FinishReject);
            sequence.OnKill(() =>
            {
                if (_transform != null)
                    _transform.localScale = Vector3.one;
            });
        }

        private void FinishReject()
        {
            ClearStreakChargeFoil();

            if (_transform != null)
                _transform.localScale = Vector3.one;

            // Make sure drag effects are cleaned up on reject
            _dragFeedback?.EndDrag();
            _dragSparkle?.StopDragSparkling();

            if (_homePending)
            {
                _homePending = false;
                _dndHandler.ReturnDraggedHome();
            }

            if (_card != null)
                _card.IsInteractable.Value = true;
        }

        #region IDisposable

        public void Dispose()
        {
            _pool.Despawn(this);
        }

        #endregion IDisposable

        private bool CanFlip(bool isFaceUp)
        {
            if (_recycleHold)
                return false;

            return (isFaceUp && !_front.gameObject.activeSelf)
                || (!isFaceUp && !_back.gameObject.activeSelf);
        }

        private void AnimateFlip(bool isFaceUp)
        {
            // Scale X from 1 to 0 then back to 1 again,
            // switching between front and back sprites in the middle.
            // This gives the illusion of flipping the card in 2D.
            PlayFlipSfx(isFaceUp);

            if (_tweenScale == null || !_tweenScale.IsActive())
            {
                _tweenScale?.Kill();
                _tweenScale = _transform
                    .DOScaleX(0f, _config.AnimationDuration / 2f)
                    .SetLoops(2, LoopType.Yoyo)
                    .SetEase(Ease.Linear)
                    .SetAutoKill(false)
                    .OnStepComplete(() => Flip(_card.IsFaceUp.Value))
                    .OnComplete(() => _transform.localScale = Vector3.one);
            }
            else
            {
                _tweenScale.Restart();
            }
        }

        /// <summary>
        /// Tableau reveal only. Deal already has Deal; stock draw already has Draw.
        /// </summary>
        private void PlayFlipSfx(bool isFaceUp)
        {
            if (!isFaceUp)
                return;

            if (_gameState == null || _gameState.State.Value != Game.State.Playing)
                return;

            if (_card?.Pile == null || !_card.Pile.IsTableau)
                return;

            _audioService.PlaySfx(Audio.SfxFlip, 0.45f);
        }

        private bool CanMove(Vector3 position)
        {
            if (_recycleHold || _layoutSnap || _victoryBounce)
                return false;

            return Vector3.SqrMagnitude(position - _transform.position) > MoveEpsilon;
        }

        private void AnimateMove(Vector3 position)
        {
            StopMoveNudge();
            if (_card.IsDragged)
            {
                _transform.position = position;
                return;
            }

            if (_recycleHold)
                return;

            // Draw-3 adds cards one by one. Hold until the fan's final slots are known
            // so we hop once, and keep the under card from sorting above its neighbors.
            if (_stockDrawHold)
                return;

            if (_stockDrawArch || IsStockDrawJumpActive())
            {
                _stockDrawArch = false;
                PlayStockDrawMove(position);
                return;
            }

            if (_tweenMove == null || !_tweenMove.IsActive())
            {
                _tweenMove?.Kill();
                // First play never rewinds, so lift now. OnPlay covers Restart().
                BoostOrderDuringMove();
                _tweenMove = _transform
                    .DOLocalMove(position, _config.AnimationDuration)
                    .SetEase(Ease.OutQuad)
                    .SetAutoKill(false)
                    .OnPlay(BoostOrderDuringMove)
                    .OnComplete(RestoreOrderAfterMove);
            }
            else
            {
                _tweenMove.ChangeEndValue(position, true).Restart();
            }
        }

        private void PlayStockDrawMove(Vector3 position)
        {
            StopMoveNudge();
            _tweenMove?.Kill();
            _tweenMove = null;
            DOTween.Kill(GetStockDrawTweenId());
            if (_transform != null)
                _transform.localScale = Vector3.one;

            _stockDrawFlightCounted = true;
            stockDrawFlights++;
            ElevatePileOrder(_card != null ? _card.Pile : null);

            var jump = Mathf.Max(0f, stockDrawJump);
            var duration = StockDrawDuration;
            _transform
                .DOLocalJump(position, jump, 1, duration)
                .SetEase(Ease.OutQuad)
                .SetId(GetStockDrawTweenId())
                .OnComplete(EndStockDrawFlight)
                .OnKill(EndStockDrawFlight);
        }

        private void EndStockDrawFlight()
        {
            if (!_stockDrawFlightCounted)
                return;

            _stockDrawFlightCounted = false;
            stockDrawFlights = Mathf.Max(0, stockDrawFlights - 1);
            var pile = _card != null ? _card.Pile : null;
            if (stockDrawFlights == 0)
                RestorePileOrder(pile);
            else
                ElevatePileOrder(pile);
        }

        private void BoostOrderDuringMove()
        {
            if (_card == null)
                return;

            var index = _card.IsInPile ? _card.Pile.Cards.IndexOf(_card) : _card.Order.Value;
            if (index < 0)
                index = 0;

            _card.OrderToRestore = index;
            _card.Order.Value = AnimOrder + index;
        }

        private void RestoreOrderAfterMove()
        {
            if (stockDrawFlights > 0)
            {
                ElevatePileOrder(_card != null ? _card.Pile : null);
                return;
            }

            if (_card == null)
                return;

            // Use the pile the card is in now, not the index from drag-start.
            if (_card.IsInPile)
            {
                var index = _card.Pile.Cards.IndexOf(_card);
                if (index < 0)
                    index = 0;
                _card.OrderToRestore = index;
                _card.Order.Value = index;
                return;
            }

            _card.Order.Value = _card.OrderToRestore;
        }

        private bool IsStockDrawJumpActive()
        {
            return DOTween.IsTweening(GetStockDrawTweenId());
        }

        private static void ElevatePileOrder(Pile pile)
        {
            if (pile == null)
                return;

            for (var i = 0; i < pile.Cards.Count; i++)
            {
                var card = pile.Cards[i];
                if (card == null)
                    continue;

                card.OrderToRestore = i;
                card.Order.Value = AnimOrder + i;
            }
        }

        private static void RestorePileOrder(Pile pile)
        {
            if (pile == null)
                return;

            for (var i = 0; i < pile.Cards.Count; i++)
            {
                var card = pile.Cards[i];
                if (card == null)
                    continue;

                card.OrderToRestore = i;
                card.Order.Value = i;
            }
        }

        private void UpdateOrder(int order)
        {
            // Update the sorting order of each sprite
            var sortingOrder = order * 10;
            if (_back != null)
                _back.sortingOrder = sortingOrder;
            if (_front != null)
                _front.sortingOrder = sortingOrder;
            if (_type != null)
                _type.sortingOrder = sortingOrder + 1;
            if (_suit1 != null)
                _suit1.sortingOrder = sortingOrder + 1;
            if (_suit2 != null)
                _suit2.sortingOrder = sortingOrder + 1;
            RefreshStockFoil();
        }

        private void UpdateAlpha(float alpha)
        {
            // Home decorative cards may have unassigned sprite refs after prefab wipes.
            if (_back != null)
            {
                var color = _back.color;
                color.a = alpha;
                _back.color = color;
            }

            if (_front != null)
            {
                var color = _front.color;
                color.a = alpha;
                _front.color = color;
            }

            if (_type != null)
            {
                var color = _type.color;
                color.a = alpha;
                _type.color = color;
            }

            if (_suit1 != null)
            {
                var color = _suit1.color;
                color.a = alpha;
                _suit1.color = color;
            }

            if (_suit2 != null)
            {
                var color = _suit2.color;
                color.a = alpha;
                _suit2.color = color;
            }
        }

        private void RefreshStockFoil()
        {
            ApplyStockFoil();
            var top = FindTopStockPresenter(
                _cardSpawner != null ? _cardSpawner.Cards : null
            );
            if (top != null && top != this)
                top.ApplyStockFoil();
        }

        private void ApplyStockFoil()
        {
            if (_back == null)
                return;

            var pile = _card != null ? _card.Pile : null;
            var liveTop =
                pile != null
                && pile.IsStock
                && _card.IsOnTop
                && !_card.IsFaceUp.Value;

            if (!liveTop)
            {
                ClearStockFoil();
                return;
            }

            var full = Mathf.Max(1, stockFoilFullCount);
            var remaining = Mathf.Clamp01(pile.Cards.Count / (float)full);
            var strength = LerpStockFoil(
                remaining,
                stockFoilStrengthAtFull,
                stockFoilStrengthAtHalf,
                stockFoilStrengthAtQuarter,
                stockFoilStrengthAtEmpty
            );
            var speed = LerpStockFoil(
                remaining,
                stockFoilSpeedAtFull,
                stockFoilSpeedAtHalf,
                stockFoilSpeedAtQuarter,
                stockFoilSpeedAtEmpty
            );
            var power = LerpStockFoil(
                remaining,
                stockFoilPowerAtFull,
                stockFoilPowerAtHalf,
                stockFoilPowerAtQuarter,
                stockFoilPowerAtEmpty
            );

            stockFoilBlock ??= new MaterialPropertyBlock();
            stockFoilBlock.Clear();
            stockFoilBlock.SetFloat("_FoilStrength", strength);
            stockFoilBlock.SetFloat("_FoilSpeed", speed);
            stockFoilBlock.SetFloat("_Power", power);
            if (_back.sprite != null && _back.sprite.texture != null)
                stockFoilBlock.SetTexture("_MainTex", _back.sprite.texture);
            _back.SetPropertyBlock(stockFoilBlock);
        }

        private void ClearStockFoil()
        {
            if (_back == null)
                return;
            _back.SetPropertyBlock(null);
        }

        /// <summary>
        /// Cache Sprites-Default once at spawn. Never overwrite with EdgeFoil.
        /// </summary>
        private void CacheFrontDefaultMaterial()
        {
            if (_front == null || frontDefaultMaterial != null)
                return;

            if (
                streakChargeFoilMaterial != null
                && _front.sharedMaterial == streakChargeFoilMaterial
            )
                return;

            frontDefaultMaterial = _front.sharedMaterial;
        }

        private void ApplyStreakChargeFoil()
        {
            if (_front == null || streakChargeFoilMaterial == null)
                return;

            CacheFrontDefaultMaterial();
            if (frontDefaultMaterial == null)
                return;

            // Swap only — do not override EdgeFoil floats; use the material as authored.
            _front.SetPropertyBlock(null);
            _front.sharedMaterial = streakChargeFoilMaterial;
            streakChargeFoilActive = true;
        }

        private void ClearStreakChargeFoil()
        {
            if (_front == null || !streakChargeFoilActive)
                return;

            _front.SetPropertyBlock(null);

            if (frontDefaultMaterial != null)
                _front.sharedMaterial = frontDefaultMaterial;

            // Material swap clears SpriteRenderer's texture bind — rebind card art.
            RebindFrontSprite();

            streakChargeFoilActive = false;
        }

        private void RebindFrontSprite()
        {
            if (_front == null)
                return;

            var sprite = _front.sprite;
            if (sprite == null)
                return;

            var flipX = _front.flipX;
            var flipY = _front.flipY;
            var color = _front.color;
            _front.sprite = null;
            _front.sprite = sprite;
            _front.flipX = flipX;
            _front.flipY = flipY;
            _front.color = color;
        }

        /// <summary>Any foundation/tableau that would accept this card right now.</summary>
        private bool HasLegalDrop(Card card)
        {
            if (card == null || _game == null)
                return false;

            if (_game.PileFoundations != null)
            {
                for (var i = 0; i < _game.PileFoundations.Count; i++)
                {
                    var pile = _game.PileFoundations[i];
                    if (pile != null && pile != card.Pile && pile.CanAddCard(card))
                        return true;
                }
            }

            if (_game.PileTableaus != null)
            {
                for (var i = 0; i < _game.PileTableaus.Count; i++)
                {
                    var pile = _game.PileTableaus[i];
                    if (pile != null && pile != card.Pile && pile.CanAddCard(card))
                        return true;
                }
            }

            return false;
        }

        private static float LerpStockFoil(
            float remaining,
            float atFull,
            float atHalf,
            float atQuarter,
            float atEmpty
        )
        {
            remaining = Mathf.Clamp01(remaining);
            if (remaining >= 0.5f)
                return Mathf.Lerp(atHalf, atFull, (remaining - 0.5f) / 0.5f);
            if (remaining >= 0.25f)
                return Mathf.Lerp(atQuarter, atHalf, (remaining - 0.25f) / 0.25f);
            return Mathf.Lerp(atEmpty, atQuarter, remaining / 0.25f);
        }

        private void UpdateVisiblity(bool isVisible)
        {
            if (_recycleHold)
                return;

            ApplyVisibility(isVisible);
        }

        private void ApplyVisibility(bool isVisible)
        {
            _back.enabled = isVisible;
            _front.enabled = isVisible;
            _type.enabled = isVisible;
            _suit1.enabled = isVisible;
            _suit2.enabled = isVisible;
        }

        private void UpdateInteractability(bool isInteractable)
        {
            if (_collider == null)
                return;

            if (_recycleHold)
            {
                _collider.enabled = false;
                return;
            }

            _collider.enabled = isInteractable;
        }

        private void Initialize()
        {
            name = $"Card_{_card.Suit}_{_card.Type}";

            var spriteSuit = _config.SuitSprites[(int)_card.Suit];
            var spriteType = GetTypeSprite(_card.Suit, _card.Type);

            // Precolored rank art (TypeSpritesRed assigned) → no runtime tint
            var usePrecoloredRanks =
                _config.TypeSpritesRed != null && _config.TypeSpritesRed.Length > 0;
            var rankColor = usePrecoloredRanks
                ? Color.white
                : _config.Colors[(int)_card.Suit];

            // Corner suit + rank (always)
            _suit1.sprite = spriteSuit;
            _type.sprite = spriteType;
            _type.color = rankColor;

            // Center (Suit2 / "SuitBig"): Aces show the letter A; other ranks show the suit.
            if (_card.Type == Card.Types.Ace)
            {
                _suit2.sprite = spriteType;
                _suit2.color = rankColor;
            }
            else
            {
                _suit2.sprite = spriteSuit;
                _suit2.color = Color.white;
            }

            ApplySuitLayout();
            // Heal cards left on EdgeFoil from earlier foil experiments.
            if (
                _front != null
                && streakChargeFoilMaterial != null
                && _front.sharedMaterial == streakChargeFoilMaterial
            )
            {
                streakChargeFoilActive = true;
                ClearStreakChargeFoil();
            }
        }

        private void ApplySuitLayout()
        {
            if (_config.Layout == null || _type == null || _suit1 == null || _suit2 == null)
                return;

            _config.Layout.ApplyFaceParts(
                _card.Suit,
                _card.Type,
                _type.transform,
                _suit1.transform,
                _suit2.transform
            );

            // Rank PNGs are not all the same pixel size (the black 2 is 128, others are 256).
            // Layout scale assumes one size, so we stretch Type to match the Ace sprite.
            FitRankSpriteToReference(_type);
            if (_card.Type == Card.Types.Ace)
                FitRankSpriteToReference(_suit2);
        }

        /// <summary>
        /// Keeps the corner (or Ace center) letter the same on-card size even when
        /// one rank PNG is half the pixels of the others. Does not change Suit pips.
        /// </summary>
        private void FitRankSpriteToReference(SpriteRenderer renderer)
        {
            if (renderer == null || renderer.sprite == null)
                return;

            var reference = GetTypeSpriteReferenceHeight();
            var height = renderer.sprite.bounds.size.y;
            if (reference < 0.0001f || height < 0.0001f)
                return;

            var t = renderer.transform;
            t.localScale *= reference / height;
        }

        private float GetTypeSpriteReferenceHeight()
        {
            var source = _config.TypeSprites;
            if (source == null || source.Length == 0 || source[0] == null)
                return 0f;

            return source[0].bounds.size.y;
        }

        private Sprite GetTypeSprite(Card.Suits suit, Card.Types type)
        {
            var index = (int)type;
            var isRed = suit == Card.Suits.Heart || suit == Card.Suits.Diamond;
            var useRed =
                isRed
                && _config.TypeSpritesRed != null
                && index >= 0
                && index < _config.TypeSpritesRed.Length
                && _config.TypeSpritesRed[index] != null;

            var source = useRed ? _config.TypeSpritesRed : _config.TypeSprites;
            if (source == null || index < 0 || index >= source.Length)
                return null;
            return source[index];
        }

        public void Flip(bool isFaceUp)
        {
            _back.gameObject.SetActive(!isFaceUp);
            _front.gameObject.SetActive(isFaceUp);
            RefreshStockFoil();
            if (!isFaceUp)
                ClearStreakChargeFoil();
        }

        public class Factory : PlaceholderFactory<Card.Suits, Card.Types, CardPresenter> { }

        #region IEventSystemHandlers

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_card.IsMoveable)
            {
                if (_homePending)
                    FinishReject();

                _rejectPlayed = false;
                DOTween.Kill(GetRejectTweenId());
                DOTween.Kill(GetLandPunchTweenId());
                if (_transform != null)
                    _transform.localScale = Vector3.one;

                // Check legal drop before BeginDrag so pile state is still the pickup state.
                var showFoil =
                    _streakService != null
                    && _streakService.Count.Value == StreakService.MaxPips - 1
                    && HasLegalDrop(_card);

                _dndHandler.BeginDrag(eventData, _card.Pile.SplitAt(_card));
                _card.IsInteractable.Value = false;

                // Start drag feedback effects
                EnsureDragFeedback();
                EnsureDragSparkle();
                _dragFeedback.StartDrag();
                _dragSparkle.StartDragSparkling();
                if (showFoil)
                    ApplyStreakChargeFoil();
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_card.IsMoveable)
                _dndHandler.Drag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            ClearStreakChargeFoil();

            if (!_card.IsMoveable)
                return;

            // Stop drag feedback effects
            _dragFeedback?.EndDrag();
            _dragSparkle?.StopDragSparkling();

            // Drop() clears IsDragged. If it is still true, this was an illegal / missed drop.
            var missedTarget = _card.IsDragged;
            if (missedTarget)
            {
                if (!_rejectPlayed)
                    _game.PlayErrorSfx();

                _dndHandler.EndDragKeepPlace();
                PlayReject(returnHomeAfter: true);
            }
            else
            {
                _dndHandler.EndDrag();
                _card.IsInteractable.Value = true;
            }

            _rejectPlayed = false;
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerDrag == null)
                return;

            if (
                eventData.pointerDrag.TryGetComponent(out CardPresenter cardPresenter)
                && _card.Pile.CanAddCard(cardPresenter.Card)
            )
            {
                _dndHandler.Drop();
                _game.MoveCard(cardPresenter.Card, _card.Pile);
            }
            else
            {
                _game.PlayErrorSfx();
                if (eventData.pointerDrag.TryGetComponent(out CardPresenter dragged))
                    dragged.MarkRejectPlayed();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || _card == null || !_card.IsDrawable)
                return;

            _stockPressed = true;
            PressStockSpring();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_stockPressed || _card == null || !_card.IsDrawable)
                return;

            _stockPressed = false;
            ReleaseStockSpring(_card.Pile);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null)
                return;

            if (_card.IsDrawable)
            {
                var stock = _card.Pile;
                _stockPressed = false;
                _game.DrawCard();
                ReleaseStockSpring(stock);
            }
            else if (_lastClick + DoubleClickInterval > Time.time)
            {
                if (_card.IsMoveable)
                {
                    if (!_game.MoveCard(_card, null))
                        PlayReject(returnHomeAfter: false);
                }
                else
                {
                    _game.PlayErrorSfx();
                    PlayReject(returnHomeAfter: false);
                }
            }

            _lastClick = Time.time;
        }

        #endregion IEventSystemHandlers

        #region IPoolable

        public void OnSpawned(Card.Suits suit, Card.Types type, IMemoryPool pool)
        {
            // Init model
            _pool = pool;
            _card.Init(suit, type);
            Initialize();
        }

        public void OnDespawned()
        {
            // Stop tweens before reset so pooled cards don't reuse dead tween refs
            DOTween.Kill(GetLandFxTweenId());
            DOTween.Kill(GetLandPunchTweenId());
            DOTween.Kill(GetAceRimTweenId());
            DOTween.Kill(GetStockDrawTweenId());
            DOTween.Kill(GetStockSpringId());
            CancelRecycle();
            StopMoveNudge();
            var homePending = _homePending;
            DOTween.Kill(GetRejectTweenId());
            DOTween.Kill(GetVictoryClearTweenId());
            if (homePending)
                FinishReject();

            _aceHomeGlow?.Stop(fadeOut: false);
            _rejectGlint?.Stop();
            ClearStockFoil();
            ClearStreakChargeFoil();
            _stockPressSparkle?.Stop();
            _dragFeedback?.EndDrag();
            _dragSparkle?.StopDragSparkling();

            _tweenMove?.Kill();
            _tweenMove = null;
            _tweenScale?.Kill();
            _tweenScale = null;
            _stockDrawArch = false;
            _stockDrawHold = false;
            _stockPressed = false;
            _recycleHold = false;
            _layoutSnap = false;
            _victoryBounce = false;
            _recycleFlightCounted = false;
            _recycleHopComplete = null;

            if (_transform != null)
                _transform.localScale = Vector3.one;

            // Reset model
            _pool = null;
            _card.Reset(Vector3.zero);
        }

        #endregion IPoolable
    }
}
