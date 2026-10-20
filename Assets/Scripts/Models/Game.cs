using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Solitaire.Commands;
using Solitaire.Helpers;
using Solitaire.Presenters;
using Solitaire.Services;
using UniRx;
using UnityEngine;
using Zenject;
using Random = UnityEngine.Random;

namespace Solitaire.Models
{
    public class Game : DisposableEntity
    {
        public enum Popup
        {
            None,
            Match,
            Options,
            Leaderboard
        }

        public enum State
        {
            Home,
            Dealing,
            Playing,
            Paused,
            Win
        }

        [Inject]
        private readonly IAudioService _audioService;

        [Inject]
        private readonly Card.Config _cardConfig;

        [Inject]
        private readonly ICardSpawner _cardSpawner;

        [Inject]
        private readonly ICommandService _commandService;

        [Inject]
        private readonly DrawCardCommand.Factory _drawCardCommandFactory;

        [Inject]
        private readonly GamePopup _gamePopup;

        [Inject]
        private readonly GameState _gameState;

        [Inject]
        private readonly IVictorySequence _victorySequence;

        [Inject]
        private readonly IHintService _hintService;

        [Inject]
        private readonly Leaderboard _leaderboard;

        [Inject]
        private readonly MoveCardCommand.Factory _moveCardCommandFactory;

        [Inject]
        private readonly IMovesService _movesService;

        [Inject]
        private readonly IPointsService _pointsService;

        [Inject]
        private readonly IStreakService _streakService;

        [Inject]
        private readonly RefillStockCommand.Factory _refillStockCommandFactory;

        public Game()
        {
            HasStarted = new BoolReactiveProperty(false);

            RestartCommand = new ReactiveCommand();
            RestartCommand.Subscribe(_ => Restart()).AddTo(this);

            NewMatchCommand = new ReactiveCommand();
            NewMatchCommand.Subscribe(_ => NewMatch()).AddTo(this);

            ContinueCommand = new ReactiveCommand(HasStarted);
            ContinueCommand.Subscribe(_ => Continue()).AddTo(this);
        }

        public BoolReactiveProperty HasStarted { get; }
        public ReactiveCommand RestartCommand { get; }
        public ReactiveCommand NewMatchCommand { get; }
        public ReactiveCommand ContinueCommand { get; }

        public Pile PileStock { get; private set; }
        public Pile PileWaste { get; private set; }
        public IList<Pile> PileFoundations { get; private set; }
        public IList<Pile> PileTableaus { get; private set; }
        public IList<Card> Cards { get; private set; }

        private int debugVictoryScore;

        public void Init(
            Pile pileStock,
            Pile pileWaste,
            IList<Pile> pileFoundations,
            IList<Pile> pileTableaus
        )
        {
            PileStock = pileStock;
            PileWaste = pileWaste;
            PileFoundations = pileFoundations;
            PileTableaus = pileTableaus;

            SpawnCards();
            LoadLeaderboard();
        }

        public void RefillStock()
        {
            if (PileStock.HasCards || !PileWaste.HasCards)
            {
                PlayErrorSfx();
                return;
            }

            // Refill stock pile from waste pile
            var command = _refillStockCommandFactory.Create(PileStock, PileWaste);
            command.Execute();
            _commandService.Add(command);
            _movesService.Increment();
        }

        public bool MoveCard(Card card, Pile pile)
        {
            if (card == null)
                return false;

            // Try to find valid move for the card
            if (pile == null)
                pile = _hintService.FindValidMove(card);

            // Couldn't find move
            if (pile == null)
            {
                PlayErrorSfx();
                return false;
            }

            // Move card to pile
            var command = _moveCardCommandFactory.Create(card, card.Pile, pile);
            command.Execute();
            _commandService.Add(command);
            _movesService.Increment();
            return true;
        }

        public void DrawCard()
        {
            // Draw card(s) from stock
            var command = _drawCardCommandFactory.Create(PileStock, PileWaste);
            command.Execute();
            _commandService.Add(command);
            _movesService.Increment();
        }

        public void PlayErrorSfx()
        {
            _audioService.PlaySfx(Audio.SfxError, 0.5f);
        }

        public void DetectWinCondition()
        {
            if (_gameState.State.Value != State.Playing)
                return;

            if (!AreFoundationsComplete())
                return;

            WinAsync().Forget();
        }

        /// <summary>
        /// Dev hook: snap to a completed board and play the bubble win.
        /// </summary>
        public void ForceVictory()
        {
            if (
                PileStock == null
                || PileWaste == null
                || PileFoundations == null
                || PileTableaus == null
            )
            {
                Debug.LogWarning("[Game] ForceVictory before Init — piles are not ready.");
                return;
            }

            if (_gameState.State.Value == State.Win)
                return;

            _victorySequence?.Reset();
            WinAsync().Forget();
        }

        /// <summary>
        /// Dev hook: 51 cards on foundations, last King face-up on tableau, score 500.
        /// One winning drop starts the full victory sequence.
        /// </summary>
        public void SetupOneMoveFromVictory()
        {
            if (
                PileStock == null
                || PileWaste == null
                || PileFoundations == null
                || PileTableaus == null
                || Cards == null
            )
            {
                Debug.LogWarning("[Game] SetupOneMoveFromVictory before Init — piles are not ready.");
                return;
            }

            _victorySequence?.Reset();
            debugVictoryScore = 500;
            StopVictoryCardMotion();

            var presenters = _cardSpawner != null ? _cardSpawner.Cards : null;
            if (presenters != null)
            {
                for (var i = 0; i < presenters.Count; i++)
                    presenters[i]?.BeginLayoutSnap();
            }

            PileStock.Reset();
            PileWaste.Reset();
            for (var i = 0; i < PileFoundations.Count; i++)
                PileFoundations[i].Reset();
            for (var i = 0; i < PileTableaus.Count; i++)
                PileTableaus[i].Reset();

            var stockPos = PileStock.Position;
            for (var i = 0; i < Cards.Count; i++)
                Cards[i]?.Reset(stockPos);

            var bySuit = AssignFoundationSuits();
            const Card.Suits leftoverSuit = Card.Suits.Spade;
            var leftoverKing = FindCard(leftoverSuit, Card.Types.King);

            foreach (Card.Suits suit in Enum.GetValues(typeof(Card.Suits)))
            {
                var dest = bySuit[(int)suit];
                if (dest == null)
                    continue;

                foreach (Card.Types type in Enum.GetValues(typeof(Card.Types)))
                {
                    if (suit == leftoverSuit && type == Card.Types.King)
                        continue;

                    var card = FindCard(suit, type);
                    if (card == null)
                        continue;

                    dest.AddCard(card);
                    FindPresenter(card)?.SnapImmediate(card.Position.Value, true);
                }
            }

            var tableau = PileTableaus.Count > 0 ? PileTableaus[0] : null;
            if (leftoverKing != null && tableau != null)
            {
                tableau.AddCard(leftoverKing);
                leftoverKing.IsFaceUp.Value = true;
                leftoverKing.IsInteractable.Value = true;
                leftoverKing.IsVisible.Value = true;
                FindPresenter(leftoverKing)?.SnapImmediate(leftoverKing.Position.Value, true);
            }

            if (presenters != null)
            {
                for (var i = 0; i < presenters.Count; i++)
                    presenters[i]?.EndLayoutSnap();
            }

            _commandService.Reset();
            _movesService.Reset();
            _streakService.Reset();
            _pointsService.Set(500);
            HasStarted.Value = true;
            _gamePopup.State.Value = Popup.None;
            _gameState.State.Value = State.Playing;
        }

        /// <summary>
        /// Dev hook: stage overlay only, fake score, no cascade.
        /// </summary>
        public void PreviewVictoryStage()
        {
            _victorySequence?.PlayStagePreview(520, () => NewMatch(), RequestHome);
        }

        /// <summary>
        /// Dev hook: bubble + glyph burst only. Skips foundation snap and card cascade.
        /// </summary>
        public void PreviewVictoryBubble()
        {
            _victorySequence?.Reset();
            _victorySequence?.PlayBubblePreview(BuildPreviewGlyphLaunches());
        }

        private List<VictoryGlyphLaunch> BuildPreviewGlyphLaunches()
        {
            var launches = new List<VictoryGlyphLaunch>(4);
            var sprites = _cardConfig != null ? _cardConfig.SuitSprites : null;
            var origin = Vector3.zero;
            foreach (Card.Suits suit in Enum.GetValues(typeof(Card.Suits)))
            {
                Sprite sprite = null;
                var suitIndex = (int)suit;
                if (sprites != null && suitIndex >= 0 && suitIndex < sprites.Length)
                    sprite = sprites[suitIndex];
                if (sprite == null)
                    continue;

                var isRed = suit == Card.Suits.Heart || suit == Card.Suits.Diamond;
                launches.Add(new VictoryGlyphLaunch(origin, sprite, isRed, suit));
            }

            return launches;
        }

        public void RequestHome()
        {
            _victorySequence?.Reset();
            _gameState.State.Value = State.Home;
        }

        public async UniTask TryShowHintAsync()
        {
            // Try to get hint
            var hint = _hintService.GetHint();

            if (hint == null)
                return;

            // Make copies of the original card and all cards above it
            var pile = hint.Pile;
            var cardsToCopy = hint.Card.Pile.SplitAt(hint.Card);
            var copies = _cardSpawner.MakeCopies(cardsToCopy);

            // Initialize copies without adding them to the pile
            for (var i = 0; i < copies.Count; i++)
            {
                // Calculate order
                var copy = copies[i];
                var index = pile.Cards.Count + i;
                copy.Card.Order.Value = index;

                // Calculate position
                var count = pile.Cards.Count + 1 + i;
                var prevCard =
                    i > 0
                        ? copies[i - 1].Card
                        : pile.HasCards
                            ? pile.Cards[index - 1]
                            : null;
                copy.Card.Position.Value = pile.CalculateCardPosition(index, count, prevCard);
            }

            _audioService.PlaySfx(Audio.SfxHint, 0.5f);

            // Wait until the animation completes
            var delayMs = (int)(_cardConfig.AnimationDuration * 1000) + 50;
            await UniTask.Delay(delayMs);

            // Despawn copies
            for (var i = 0; i < copies.Count; i++)
                _cardSpawner.Despawn(copies[i]);

            await UniTask.Delay(delayMs);
        }

        private void Reset()
        {
            if (
                PileStock == null
                || PileWaste == null
                || PileFoundations == null
                || PileTableaus == null
                || Cards == null
            )
            {
                Debug.LogError(
                    "[Game] Reset called before Init — piles/cards are not ready. "
                        + "Usually means GamePresenter failed to bind Stock/Waste/Foundations/Tableaus."
                );
                return;
            }

            // Reset piles
            PileStock.Reset();
            PileWaste.Reset();

            for (var i = 0; i < PileFoundations.Count; i++)
                PileFoundations[i].Reset();

            for (var i = 0; i < PileTableaus.Count; i++)
                PileTableaus[i].Reset();

            // Reset cards
            StopVictoryCardMotion();
            for (var i = 0; i < Cards.Count; i++)
                Cards[i].Reset(PileStock.Position);

            // Reset services
            _movesService.Reset();
            _pointsService.Reset();
            _streakService.Reset();
            _commandService.Reset();
            _victorySequence?.Reset();
            debugVictoryScore = 0;

            HasStarted.Value = true;
            _gamePopup.State.Value = Popup.None;
        }

        private void ShuffleCards()
        {
            //// Shuffle by sorting
            //Cards = Cards.OrderBy(a => Guid.NewGuid()).ToList();

            // Fisher-Yates shuffle
            for (var i = Cards.Count - 1; i > 0; i--)
            {
                var n = Random.Range(0, i + 1);
                var temp = Cards[i];
                Cards[i] = Cards[n];
                Cards[n] = temp;
            }
        }

        private async UniTask DealAsync()
        {
            // Start dealing
            _gameState.State.Value = State.Dealing;
            var delayMs = (int)(_cardConfig.AnimationDuration * 1000) + 50;

            _audioService.PlaySfx(Audio.SfxShuffle, 0.5f);
            await UniTask.Delay(delayMs);

            // Add cards to the stock pile
            PileStock.AddCards(Cards);

            _audioService.PlaySfx(Audio.SfxDeal, 1.0f);
            await UniTask.Delay(delayMs);

            // Deal cards to the Tableau piles
            for (var i = 0; i < PileTableaus.Count; i++)
            for (var j = 0; j < i + 1; j++)
            {
                var topCard = PileStock.TopCard();
                PileTableaus[i].AddCard(topCard);

                if (j == i)
                    topCard.Flip();

                await UniTask.DelayFrame(3);
            }

            // Start playing
            _gameState.State.Value = State.Playing;
        }

        private async UniTask WinAsync()
        {
            _gameState.State.Value = State.Win;
            HasStarted.Value = false;

            if (_victorySequence == null)
                return;

            var score = debugVictoryScore > 0 ? debugVictoryScore : _pointsService.Points.Value;
            if (debugVictoryScore > 0)
            {
                _pointsService.Set(debugVictoryScore);
                debugVictoryScore = 0;
            }

            await _victorySequence.PlayAsync(
                score,
                HarvestAsync,
                () => _gameState.State.Value == State.Win,
                AddPointsAndSaveLeaderboard,
                () => NewMatch(),
                RequestHome
            );
        }

        /// <summary>
        /// Snap completed foundations, capture suit pips, clear the board behind the bubble.
        /// </summary>
        private async UniTask<IReadOnlyList<VictoryGlyphLaunch>> HarvestAsync()
        {
            if (!AreFoundationsComplete())
                AssembleCompletedFoundations();

            StopAceFoundationGlows();
            var launches = SnapshotFoundationGlyphs();
            ClearBoardForVictory();
            await UniTask.Yield();
            return launches;
        }

        private void ClearBoardForVictory()
        {
            if (_cardSpawner == null)
                return;

            var presenters = _cardSpawner.Cards;
            for (var i = 0; i < presenters.Count; i++)
            {
                var presenter = presenters[i];
                if (presenter == null)
                    continue;

                var card = presenter.Card;
                var king =
                    card != null
                    && card.Pile != null
                    && card.Type == Card.Types.King
                    && card.IsOnTop;
                presenter.PlayVictoryClear(king);
            }
        }

        private bool AreFoundationsComplete()
        {
            if (PileFoundations == null || PileFoundations.Count == 0)
                return false;

            var count = 0;
            for (var i = 0; i < PileFoundations.Count; i++)
            {
                var pile = PileFoundations[i];
                if (pile != null)
                    count += pile.Cards.Count;
            }

            return count == 52;
        }

        private void AssembleCompletedFoundations()
        {
            if (Cards == null || PileFoundations == null)
                return;

            var presenters = _cardSpawner != null ? _cardSpawner.Cards : null;
            if (presenters != null)
            {
                for (var i = 0; i < presenters.Count; i++)
                {
                    var presenter = presenters[i];
                    if (presenter == null)
                        continue;
                    presenter.StopAceFoundationIdle(fadeOut: false);
                    presenter.BeginLayoutSnap();
                }
            }

            var bySuit = AssignFoundationSuits();

            PileStock?.Reset();
            PileWaste?.Reset();
            for (var i = 0; i < PileFoundations.Count; i++)
                PileFoundations[i].Reset();
            if (PileTableaus != null)
            {
                for (var i = 0; i < PileTableaus.Count; i++)
                    PileTableaus[i].Reset();
            }

            foreach (Card.Suits suit in Enum.GetValues(typeof(Card.Suits)))
            {
                var dest = bySuit[(int)suit];
                if (dest == null)
                    continue;

                foreach (Card.Types type in Enum.GetValues(typeof(Card.Types)))
                {
                    var card = FindCard(suit, type);
                    if (card == null)
                        continue;

                    dest.AddCard(card);
                    var presenter = FindPresenter(card);
                    presenter?.SnapImmediate(card.Position.Value, true);
                }
            }

            if (presenters == null)
                return;

            for (var i = 0; i < presenters.Count; i++)
                presenters[i]?.EndLayoutSnap();
        }

        private void StopAceFoundationGlows()
        {
            if (_cardSpawner == null)
                return;

            var presenters = _cardSpawner.Cards;
            for (var i = 0; i < presenters.Count; i++)
                presenters[i]?.StopAceFoundationIdle(fadeOut: false);
        }

        private List<VictoryGlyphLaunch> SnapshotFoundationGlyphs()
        {
            var launches = new List<VictoryGlyphLaunch>(4);
            if (PileFoundations == null)
                return launches;

            var sprites = _cardConfig != null ? _cardConfig.SuitSprites : null;

            for (var i = 0; i < PileFoundations.Count; i++)
            {
                var pile = PileFoundations[i];
                if (pile == null)
                    continue;

                var card = pile.BottomCard() ?? pile.TopCard();
                if (card == null)
                    continue;

                Sprite sprite = null;
                var suitIndex = (int)card.Suit;
                if (sprites != null && suitIndex >= 0 && suitIndex < sprites.Length)
                    sprite = sprites[suitIndex];

                if (sprite == null)
                    continue;

                var isRed =
                    card.Suit == Card.Suits.Heart || card.Suit == Card.Suits.Diamond;
                launches.Add(new VictoryGlyphLaunch(pile.Position, sprite, isRed, card.Suit));
            }

            return launches;
        }

        private Pile[] AssignFoundationSuits()
        {
            var bySuit = new Pile[4];
            var used = new bool[PileFoundations.Count];

            for (var i = 0; i < PileFoundations.Count; i++)
            {
                var pile = PileFoundations[i];
                var bottom = pile != null ? pile.BottomCard() : null;
                if (bottom == null)
                    continue;

                var suit = (int)bottom.Suit;
                if (suit < 0 || suit >= bySuit.Length || bySuit[suit] != null)
                    continue;

                bySuit[suit] = pile;
                used[i] = true;
            }

            var emptyIndex = 0;
            for (var suit = 0; suit < bySuit.Length; suit++)
            {
                if (bySuit[suit] != null)
                    continue;

                while (emptyIndex < used.Length && used[emptyIndex])
                    emptyIndex++;

                if (emptyIndex >= PileFoundations.Count)
                    break;

                bySuit[suit] = PileFoundations[emptyIndex];
                used[emptyIndex] = true;
                emptyIndex++;
            }

            return bySuit;
        }

        private Card FindCard(Card.Suits suit, Card.Types type)
        {
            if (Cards == null)
                return null;

            for (var i = 0; i < Cards.Count; i++)
            {
                var card = Cards[i];
                if (card != null && card.Suit == suit && card.Type == type)
                    return card;
            }

            return null;
        }

        private void StopVictoryCardMotion()
        {
            if (_cardSpawner == null)
                return;

            var presenters = _cardSpawner.Cards;
            for (var i = 0; i < presenters.Count; i++)
            {
                var presenter = presenters[i];
                if (presenter == null)
                    continue;
                presenter.EndVictoryBounce();
                presenter.EndLayoutSnap();
            }
        }

        private CardPresenter FindPresenter(Card card)
        {
            if (card == null || _cardSpawner == null)
                return null;

            var presenters = _cardSpawner.Cards;
            for (var i = 0; i < presenters.Count; i++)
            {
                var presenter = presenters[i];
                if (presenter != null && presenter.Card == card)
                    return presenter;
            }

            return null;
        }

        private void Restart()
        {
            Reset();
            DealAsync().Forget();
        }

        private void NewMatch()
        {
            Reset();
            ShuffleCards();
            DealAsync().Forget();
        }

        private void Continue()
        {
            _gameState.State.Value = State.Playing;
            _gamePopup.State.Value = Popup.None;
        }

        private void SpawnCards()
        {
            _cardSpawner.SpawnAll();
            Cards = _cardSpawner.Cards.Select(c => c.Card).ToList();
        }

        private void AddPointsAndSaveLeaderboard()
        {
            var item = new Leaderboard.Item
            {
                Points = _pointsService.Points.Value,
                Date = DateTime.Now.ToString("HH:mm MM/dd/yyyy")
            };

            _leaderboard.Add(item);
            _leaderboard.Save();
        }

        private void LoadLeaderboard()
        {
            _leaderboard.Load();
        }

        [Serializable]
        public class Config
        {
            public int PointsWasteToTableau = 5;
            public int PointsWasteToFoundation = 10;
            public int PointsTableauToFoundation = 10;
            public int PointsTurnOverTableauCard = 5;
            public int PointsFoundationToTableau = -15;
            public int PointsRecycleWaste = -100;
            public int PointsStreakBonus = 50;

            [Tooltip("After each streak payoff, NextBonus grows by this (50 → 60 → 70…).")]
            public int PointsStreakBonusStep = 10;
        }
    }
}
