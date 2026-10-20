using DG.Tweening;
using Solitaire.Models;
using Solitaire.Services;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Solitaire.Presenters
{
    public class PilePresenter
        : MonoBehaviour,
            IDropHandler,
            IPointerClickHandler,
            IPointerDownHandler,
            IPointerExitHandler
    {
        private const string SlotTweenIdPrefix = "EmptyStockPress";
        private const string SlotFadeTweenIdPrefix = "PileSlotVictoryFade";

        [SerializeField]
        private Pile.PileType Type;

        [SerializeField]
        private Pile.CardArrangement Arrangement;

        [SerializeField]
        private Vector3 PosPortrait;

        [SerializeField]
        private Vector3 PosLandscape;

        [Header("Empty stock press")]
        [Tooltip("X scale of the gummy squash vs the slot's rest size.")]
        [SerializeField]
        private float stockSpringScaleX = 1.04f;

        [SerializeField]
        private float stockSpringScaleY = 0.83f;

        [SerializeField]
        private float stockSpringPressDuration = 0.07f;

        [SerializeField]
        private float stockSpringReleaseDuration = 0.16f;

        [Inject]
        private readonly IDragAndDropHandler _dndHandler;

        [Inject]
        private readonly Game _game;

        [Inject]
        private readonly OrientationState _orientation;

        [Inject]
        private readonly Pile _pile;

        /// <summary>
        /// Hide stock / waste / foundation / tableau slot art behind the victory bubble.
        /// </summary>
        public void FadeSlotForVictory()
        {
            if (_slotRenderer == null)
                _slotRenderer = GetComponent<SpriteRenderer>();
            if (_slotRenderer == null)
                return;

            DOTween.Kill(SlotFadeTweenId);
            _slotRenderer.enabled = true;
            _slotRenderer
                .DOFade(0f, 0.12f)
                .SetEase(Ease.OutQuad)
                .SetId(SlotFadeTweenId)
                .OnComplete(() =>
                {
                    if (_slotRenderer != null)
                        _slotRenderer.enabled = false;
                });
        }

        public void RestoreSlotFromVictory(bool instant = true)
        {
            RestoreSlotAlpha(instant);
        }

        private void RestoreSlotAlpha(bool instant)
        {
            if (_slotRenderer == null)
                _slotRenderer = GetComponent<SpriteRenderer>();
            if (_slotRenderer == null)
                return;

            DOTween.Kill(SlotFadeTweenId);
            _slotRenderer.enabled = true;
            if (instant)
            {
                var color = _slotRenderer.color;
                color.a = _slotRestAlpha;
                _slotRenderer.color = color;
                return;
            }

            _slotRenderer.DOFade(_slotRestAlpha, 0.2f).SetEase(Ease.OutQuad).SetId(SlotFadeTweenId);
        }

        public Pile.PileType PileType => Type;

        public Pile Pile => _pile;

        public static void FadePlayfieldSlotsForVictory()
        {
            SetPlayfieldSlotVictoryHidden(true);
        }

        public static void RestorePlayfieldSlotsFromVictory()
        {
            SetPlayfieldSlotVictoryHidden(false);
        }

        private static void SetPlayfieldSlotVictoryHidden(bool hidden)
        {
            var piles = FindObjectsOfType<PilePresenter>(true);
            if (piles == null)
                return;

            for (var i = 0; i < piles.Length; i++)
            {
                var pile = piles[i];
                if (pile == null)
                    continue;

                if (hidden)
                    pile.FadeSlotForVictory();
                else
                    pile.RestoreSlotFromVictory(instant: true);
            }
        }

        private Vector3 _restScale = Vector3.one;
        private bool _slotPressed;
        private SpriteRenderer _slotRenderer;
        private StockPressSparkle _stockPressSparkle;
        private float _slotRestAlpha = 1f;

        private string SlotTweenId => SlotTweenIdPrefix + GetInstanceID();
        private string SlotFadeTweenId => SlotFadeTweenIdPrefix + GetInstanceID();

        private void Awake()
        {
            _restScale = transform.localScale;
            _slotRenderer = GetComponent<SpriteRenderer>();
            if (_slotRenderer != null)
                _slotRestAlpha = _slotRenderer.color.a;
            _pile.Init(Type, Arrangement, transform.position);
        }

        private void Start()
        {
            // Update layout on orientation change
            _orientation.State.Subscribe(UpdateLayout).AddTo(this);
        }

        private void OnDisable()
        {
            _slotPressed = false;
            DOTween.Kill(SlotTweenId);
            DOTween.Kill(SlotFadeTweenId);
            transform.localScale = _restScale;
            _stockPressSparkle?.Stop();
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerDrag == null)
                return;

            if (
                eventData.pointerDrag.TryGetComponent(out CardPresenter cardPresenter)
                && _pile.CanAddCard(cardPresenter.Card)
            )
            {
                _dndHandler.Drop();
                _game.MoveCard(cardPresenter.Card, _pile);
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
            if (eventData == null || !CanPressEmptyStock())
                return;

            _slotPressed = true;
            PlaySlotPress();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_slotPressed)
                return;

            _slotPressed = false;
            PlaySlotRelease(playSparkle: false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData?.clickCount != 1 || !_pile.IsStock)
                return;

            if (CardPresenter.IsRecyclePlaying)
                return;

            _slotPressed = false;

            if (_pile.HasCards || !WasteHasCards())
            {
                if (!_pile.HasCards)
                    PlaySlotReject();

                _game.RefillStock();
                return;
            }

            PlaySlotRelease(playSparkle: true);
            _game.RefillStock();
        }

        /// <summary>
        /// True when the faint stock slot is showing and a press should squash it.
        /// </summary>
        private bool CanPressEmptyStock()
        {
            return _pile != null
                && _pile.IsStock
                && !_pile.HasCards
                && !CardPresenter.IsRecyclePlaying;
        }

        private bool WasteHasCards()
        {
            return _game != null && _game.PileWaste != null && _game.PileWaste.HasCards;
        }

        /// <summary>
        /// Squash the ghost slot. Multiplies rest scale (0.45) — never tween toward 1.
        /// </summary>
        private void PlaySlotPress()
        {
            DOTween.Kill(SlotTweenId);
            var pressed = new Vector3(
                _restScale.x * stockSpringScaleX,
                _restScale.y * stockSpringScaleY,
                _restScale.z
            );
            transform
                .DOScale(pressed, Mathf.Max(0.04f, stockSpringPressDuration))
                .SetEase(Ease.OutQuad)
                .SetId(SlotTweenId);
        }

        /// <summary>
        /// Pop the slot back to rest. Sparkles match a stock-card release.
        /// </summary>
        private void PlaySlotRelease(bool playSparkle)
        {
            DOTween.Kill(SlotTweenId);
            transform
                .DOScale(_restScale, Mathf.Max(0.06f, stockSpringReleaseDuration))
                .SetEase(Ease.OutBack)
                .SetId(SlotTweenId);

            if (!playSparkle)
                return;

            EnsureStockPressSparkle();
            var popDelay = Mathf.Max(0.04f, stockSpringReleaseDuration) * 0.45f;
            _stockPressSparkle?.PlayAfterPop(popDelay);
        }

        /// <summary>
        /// Nothing to recycle — jelly wobble, then error SFX from RefillStock.
        /// </summary>
        private void PlaySlotReject()
        {
            DOTween.Kill(SlotTweenId);
            transform.localScale = _restScale;

            var squash = new Vector3(_restScale.x * 1.12f, _restScale.y * 0.82f, _restScale.z);
            var stretch = new Vector3(_restScale.x * 0.88f, _restScale.y * 1.14f, _restScale.z);
            var wobble = new Vector3(_restScale.x * 1.06f, _restScale.y * 0.94f, _restScale.z);

            var sequence = DOTween.Sequence().SetId(SlotTweenId);
            sequence.Append(transform.DOScale(squash, 0.08f).SetEase(Ease.OutQuad));
            sequence.Append(transform.DOScale(stretch, 0.08f).SetEase(Ease.OutQuad));
            sequence.Append(transform.DOScale(wobble, 0.06f).SetEase(Ease.InOutSine));
            sequence.Append(transform.DOScale(_restScale, 0.08f).SetEase(Ease.OutSine));
            sequence.OnKill(() =>
            {
                if (this != null)
                    transform.localScale = _restScale;
            });
        }

        private void EnsureStockPressSparkle()
        {
            if (_slotRenderer == null)
                _slotRenderer = GetComponent<SpriteRenderer>();

            if (_stockPressSparkle != null)
            {
                _stockPressSparkle.Bind(_slotRenderer);
                return;
            }

            _stockPressSparkle = GetComponentInChildren<StockPressSparkle>(true);
            if (_stockPressSparkle == null)
            {
                var go = new GameObject("StockPressSparkle", typeof(ParticleSystem));
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                _stockPressSparkle = go.AddComponent<StockPressSparkle>();
            }

            _stockPressSparkle.Bind(_slotRenderer);
        }

        private void UpdateLayout(Orientation orientation)
        {
            var position = orientation == Orientation.Landscape ? PosLandscape : PosPortrait;

            transform.position = position;
            _pile.UpdatePosition(position);
        }
    }
}
