using System;
using DG.Tweening;
using Solitaire.Models;
using Solitaire.Presenters;
using Solitaire.Services;
using UnityEngine;
using Zenject;

namespace Solitaire.Commands
{
    public class MoveCardCommand : ICommand, IDisposable, IPoolable<Card, Pile, Pile, IMemoryPool>
    {
        [Inject]
        private readonly IAudioService _audioService;

        [Inject]
        private readonly Card.Config _cardConfig;

        [Inject]
        private readonly ICardSpawner _cardSpawner;

        [Inject]
        private readonly Game.Config _gameConfig;

        [Inject]
        private readonly IPointsService _pointsService;

        [Inject]
        private readonly IScorePopup _scorePopup;

        [Inject(Optional = true)]
        private readonly ScorePopupTune _scorePopupTune;

        [Inject]
        private readonly IStreakService _streakService;

        private Card _card;
        private Pile _pileSource;
        private Pile _pileTarget;
        private IMemoryPool _pool;

        private bool _wasTopCardFlipped;
        private int _streakBefore;
        private bool _streakBonusAwarded;
        private int _streakBonusPoints;

        // Must outlast the presenter's burstDelay + burst length, or the orbs vanish mid-flare.
        private const float StreakResetDelay = 1.35f;

        public void Execute()
        {
            if (_pileSource.TopCard() == _card)
            {
                // Single card
                _pileTarget.AddCard(_card);
            }
            else
            {
                // Multiple cards
                var cards = _pileSource.SplitAt(_card);
                _pileSource.RemoveCards(cards);
                _pileTarget.AddCards(cards);
            }

            // Always float numbers on the seat the card just landed in — not the pickup pile.
            var landPos = GetLandWorldPosition();

            // Scoring — remember the land amount so a flip can pop first (+5), then this (+10).
            var scored = false;
            var landPoints = 0;
            if (_pileSource.IsWaste)
            {
                if (_pileTarget.IsTableau)
                {
                    landPoints = _gameConfig.PointsWasteToTableau;
                    scored = true;
                }
                else if (_pileTarget.IsFoundation)
                {
                    landPoints = _gameConfig.PointsWasteToFoundation;
                    scored = true;
                }
            }
            else if (_pileSource.IsTableau && _pileTarget.IsFoundation)
            {
                landPoints = _gameConfig.PointsTableauToFoundation;
                scored = true;
            }
            else if (_pileSource.IsFoundation && _pileTarget.IsTableau)
            {
                landPoints = _gameConfig.PointsFoundationToTableau;
            }

            PlayLandSfx();

            // Reveal card below if needed
            var cardBelow = _pileSource.TopCard();

            if (_pileSource.IsTableau && cardBelow != null && !cardBelow.IsFaceUp.Value)
            {
                cardBelow.Flip();
                _wasTopCardFlipped = true;
                AddPoints(_gameConfig.PointsTurnOverTableauCard, landPos, 0f);
                scored = true;
                if (landPoints != 0)
                    AddPoints(landPoints, landPos, StackStagger());
            }
            else if (landPoints != 0)
            {
                AddPoints(landPoints, landPos, 0f);
            }

            ApplyStreak(scored);

            if (_streakBonusAwarded)
                AwardStreakBonus();

            // Juice on successful lands
            if (_pileTarget.IsFoundation)
                TryPlayLandFx(foundation: true);
            else if (_pileTarget.IsTableau)
                TryPlayLandFx(foundation: false);

            // Ace alone on foundation gets a soft breathing rim
            if (_pileSource.IsFoundation || _pileTarget.IsFoundation)
                SyncFoundationAceRims();
        }

        private void AddPoints(int value, Vector3 worldPosition, float extraDelay)
        {
            _pointsService.Add(value);
            _scorePopup?.Show(
                value,
                worldPosition,
                _cardConfig.AnimationDuration + Mathf.Max(0f, extraDelay)
            );
        }

        /// <summary>
        /// Foundation gets Place; tableau and other piles keep Draw.
        /// </summary>
        private void PlayLandSfx()
        {
            if (_pileTarget != null && _pileTarget.IsFoundation)
                _audioService.PlaySfx(Audio.SfxPlace, 0.55f);
            else
                _audioService.PlaySfx(Audio.SfxDraw, 0.5f);
        }

        /// <summary>Gap between a flip pop and the land pop so both numbers can be read.</summary>
        private float StackStagger()
        {
            return _scorePopupTune != null ? Mathf.Max(0f, _scorePopupTune.stackStagger) : 0.2f;
        }

        /// <summary>
        /// Where the moved card sits on the target pile (after AddCard).
        /// Flip points use this too, so +5 shows on the drop, not the old pile.
        /// </summary>
        private Vector3 GetLandWorldPosition()
        {
            if (_pileTarget != null && _card != null)
            {
                var index = _pileTarget.Cards.IndexOf(_card);
                if (index >= 0)
                {
                    var prev = index > 0 ? _pileTarget.Cards[index - 1] : null;
                    return _pileTarget.CalculateCardPosition(
                        index,
                        _pileTarget.Cards.Count,
                        prev
                    );
                }

                return _pileTarget.Position;
            }

            return _card != null ? _card.Position.Value : Vector3.zero;
        }

        private void TryPlayLandFx(bool foundation)
        {
            for (var i = 0; i < _cardSpawner.Cards.Count; i++)
            {
                var presenter = _cardSpawner.Cards[i];
                if (presenter == null || presenter.Card != _card)
                    continue;

                if (foundation)
                    presenter.PlayFoundationLandFx(_cardConfig.AnimationDuration);
                else
                    presenter.PlaySoftWhiteLandFx(_cardConfig.AnimationDuration);
                return;
            }
        }

        private void SyncFoundationAceRims()
        {
            if (_pileSource != null && _pileSource.IsFoundation)
                SyncPileAceRim(_pileSource, delayedStart: false);

            if (_pileTarget != null && _pileTarget.IsFoundation)
                SyncPileAceRim(_pileTarget, delayedStart: true);

            // Ace left the foundation → stop immediately
            if (
                _card != null
                && _card.Type == Card.Types.Ace
                && (_pileTarget == null || !_pileTarget.IsFoundation)
            )
            {
                FindPresenter(_card)?.StopAceFoundationIdle();
            }
        }

        private void SyncPileAceRim(Pile pile, bool delayedStart)
        {
            if (pile == null || !pile.IsFoundation)
                return;

            Card aloneAce = null;
            if (pile.Cards.Count == 1 && pile.Cards[0].Type == Card.Types.Ace)
                aloneAce = pile.Cards[0];

            for (var i = 0; i < pile.Cards.Count; i++)
            {
                var card = pile.Cards[i];
                var presenter = FindPresenter(card);
                if (presenter == null)
                    continue;

                if (card == aloneAce)
                {
                    if (delayedStart)
                        presenter.StartAceFoundationIdle(_cardConfig.AnimationDuration);
                    else
                        presenter.StartAceFoundationIdle(0f);
                }
                else
                {
                    presenter.StopAceFoundationIdle();
                }
            }
        }

        private CardPresenter FindPresenter(Card card)
        {
            if (card == null)
                return null;

            for (var i = 0; i < _cardSpawner.Cards.Count; i++)
            {
                var presenter = _cardSpawner.Cards[i];
                if (presenter != null && presenter.Card == card)
                    return presenter;
            }

            return null;
        }

        private void ApplyStreak(bool scored)
        {
            _streakBefore = _streakService.Count.Value;
            _streakBonusAwarded = false;

            if (_pileSource.IsFoundation && _pileTarget.IsTableau)
                _streakService.Break();
            else if (scored)
                _streakBonusAwarded = _streakService.AddPip();
            else
                _streakService.Break();
        }

        /// <summary>
        /// Pays the full-streak bonus and books the reset that empties the orbs.
        /// The STREAK +N pop and motes live on StreakBonusPresenter so they start on the pill.
        /// </summary>
        private void AwardStreakBonus()
        {
            var bonus = _streakService.AwardBonus();
            if (bonus <= 0)
            {
                Debug.LogWarning(
                    "[MoveCardCommand] Streak AwardBonus returned 0 — check GameConfig PointsStreakBonus."
                );
                bonus = 50;
            }

            _pointsService.Add(bonus);
            _streakBonusPoints = bonus;

            // Local copy: this command may be despawned back to the pool before this fires.
            // Payoff SFX lives on StreakBonusPresenter so it hits with the flare, not Hint.
            var streak = _streakService;
            DOVirtual.DelayedCall(StreakResetDelay, () => streak.Complete());
        }

        public void Undo()
        {
            _streakService.Set(_streakBefore);

            if (_streakBonusAwarded)
            {
                _pointsService.Add(-_streakBonusPoints);
                _streakService.RevertLastAward();
                _streakBonusAwarded = false;
                _streakBonusPoints = 0;
            }

            // Hide top card of the source tableau pile
            var cardTop = _pileSource.TopCard();

            if (
                _pileSource.IsTableau
                && _wasTopCardFlipped
                && cardTop != null
                && cardTop.IsFaceUp.Value
            )
            {
                cardTop.Flip();
                _pointsService.Add(-_gameConfig.PointsTurnOverTableauCard);
            }

            // Scoring
            if (_pileSource.IsWaste)
            {
                if (_pileTarget.IsTableau)
                    _pointsService.Add(-_gameConfig.PointsWasteToTableau);
                else if (_pileTarget.IsFoundation)
                    _pointsService.Add(-_gameConfig.PointsWasteToFoundation);
            }
            else if (_pileSource.IsTableau && _pileTarget.IsFoundation)
            {
                _pointsService.Add(-_gameConfig.PointsTableauToFoundation);
            }
            else if (_pileSource.IsFoundation && _pileTarget.IsTableau)
            {
                _pointsService.Add(-_gameConfig.PointsFoundationToTableau);
            }

            _audioService.PlaySfx(Audio.SfxDraw, 0.5f);

            if (_pileTarget.TopCard() == _card)
            {
                // Single card
                _pileSource.AddCard(_card);
            }
            else
            {
                // Multiple cards
                var cards = _pileTarget.SplitAt(_card);
                _pileTarget.RemoveCards(cards);
                _pileSource.AddCards(cards);
            }

            if (_pileSource.IsFoundation || _pileTarget.IsFoundation)
                SyncFoundationAceRimsAfterUndo();
        }

        private void SyncFoundationAceRimsAfterUndo()
        {
            // After undo, piles are restored — sync both without land delay
            if (_pileSource != null && _pileSource.IsFoundation)
                SyncPileAceRim(_pileSource, delayedStart: false);
            if (_pileTarget != null && _pileTarget.IsFoundation)
                SyncPileAceRim(_pileTarget, delayedStart: false);

            if (
                _card != null
                && _card.Type == Card.Types.Ace
                && (_pileSource == null || !_pileSource.IsFoundation)
            )
            {
                FindPresenter(_card)?.StopAceFoundationIdle(fadeOut: false);
            }
        }

        public void Dispose()
        {
            _pool.Despawn(this);
        }

        public void OnDespawned()
        {
            _card = null;
            _pileSource = null;
            _pileTarget = null;
            _pool = null;
        }

        public void OnSpawned(Card card, Pile pileSource, Pile pileTarget, IMemoryPool pool)
        {
            _card = card;
            _pileSource = pileSource;
            _pileTarget = pileTarget;
            _pool = pool;
            _wasTopCardFlipped = false;
            _streakBefore = 0;
            _streakBonusAwarded = false;
            _streakBonusPoints = 0;
        }

        public class Factory : PlaceholderFactory<Card, Pile, Pile, MoveCardCommand> { }
    }
}
