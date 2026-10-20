using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Solitaire.Models;
using Solitaire.Presenters;
using UniRx;
using Zenject;

namespace Solitaire.Services
{
    /// <summary>
    /// Owns victory overlay: HUD tuck, bubble glyphs, then a short result CTA.
    /// Board harvest stays on Game and is passed in as a callback.
    /// </summary>
    public sealed class VictorySequenceService : IVictorySequence, IInitializable, IDisposable
    {
        [Inject]
        private readonly GameState _gameState;

        [Inject]
        private readonly IHudTuck _hudTuck;

        [Inject]
        private readonly IVictoryGlyphs _glyphs;

        [Inject]
        private readonly IVictoryStage _stage;

        [Inject(Optional = true)]
        private readonly ICardLandFxService _landFx;

        [Inject(Optional = true)]
        private readonly ICardSpawner _cardSpawner;

        [Inject(Optional = true)]
        private readonly VictoryBubblePresenter _bubble;

        [Inject(Optional = true)]
        private readonly IScoreHud _scoreHud;

        private readonly CompositeDisposable disposables = new CompositeDisposable();
        private int playId;

        public void Initialize()
        {
            _gameState
                .State.Pairwise()
                .Subscribe(pair =>
                {
                    if (pair.Previous == Game.State.Win && pair.Current != Game.State.Win)
                        Reset();
                })
                .AddTo(disposables);
        }

        public void Dispose()
        {
            disposables.Dispose();
            Reset();
        }

        public async UniTask PlayAsync(
            int score,
            Func<UniTask<IReadOnlyList<VictoryGlyphLaunch>>> harvestAsync,
            Func<bool> stillWinning,
            Action onBeforeStage,
            Action onNewMatch,
            Action onHome
        )
        {
            var id = ++playId;
            HideDeckFoundations();

            if (_hudTuck != null)
            {
                var keepPoints = _bubble != null ? _scoreHud?.PointsRect : null;
                await _hudTuck.TuckAsync(keepPoints);
            }
            if (!Still(id, stillWinning))
                return;

            IReadOnlyList<VictoryGlyphLaunch> launches = Array.Empty<VictoryGlyphLaunch>();
            if (harvestAsync != null)
                launches = await harvestAsync() ?? Array.Empty<VictoryGlyphLaunch>();
            if (!Still(id, stillWinning))
                return;

            _bubble?.SetPayoffScore(score);
            if (_glyphs != null)
                await _glyphs.PlayAsync(launches, () => Still(id, stillWinning));
            if (!Still(id, stillWinning))
                return;

            onBeforeStage?.Invoke();
            if (_stage != null)
            {
                await _stage.PlayAsync(
                    score,
                    () => Still(id, stillWinning),
                    onNewMatch,
                    onHome
                );
            }
            else
            {
                _hudTuck?.Restore(true);
            }
        }

        public void PlayStagePreview(int score, Action onNewMatch, Action onHome)
        {
            Reset();
            if (_stage == null)
                return;

            _stage
                .PlayAsync(score, () => _stage.IsVisible, onNewMatch, onHome)
                .Forget();
        }

        public void PlayBubblePreview(IReadOnlyList<VictoryGlyphLaunch> launches)
        {
            Reset();
            PlayBubblePreviewAsync(launches).Forget();
        }

        private async UniTask PlayBubblePreviewAsync(IReadOnlyList<VictoryGlyphLaunch> launches)
        {
            var id = ++playId;
            HideDeckFoundations();

            if (_hudTuck != null)
            {
                var keepPoints = _bubble != null ? _scoreHud?.PointsRect : null;
                await _hudTuck.TuckAsync(keepPoints);
            }

            if (!Still(id, null))
                return;

            _bubble?.SetPayoffScore(0);

            if (_glyphs != null)
                await _glyphs.PlayAsync(
                    launches ?? Array.Empty<VictoryGlyphLaunch>(),
                    () => Still(id, null)
                );

            if (!Still(id, null))
                return;

            _hudTuck?.Restore(true);
        }

        public void Reset()
        {
            playId++;
            _hudTuck?.Restore(true);
            _glyphs?.StopAll();
            _stage?.Hide();
            _landFx?.StopAllFlightTrails();
            StopCardMotion();
            PilePresenter.RestorePlayfieldSlotsFromVictory();
        }

        private bool Still(int id, Func<bool> stillWinning)
        {
            if (id != playId)
                return false;
            return stillWinning == null || stillWinning();
        }

        private void HideDeckFoundations()
        {
            PilePresenter.FadePlayfieldSlotsForVictory();
            if (_cardSpawner == null)
                return;

            var presenters = _cardSpawner.Cards;
            for (var i = 0; i < presenters.Count; i++)
            {
                var presenter = presenters[i];
                var pile = presenter != null ? presenter.Card?.Pile : null;
                if (pile == null)
                    continue;
                if (!pile.IsFoundation && !pile.IsStock && !pile.IsWaste)
                    continue;

                presenter.PlayVictoryClear(false);
            }
        }

        private void StopCardMotion()
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
            }
        }
    }
}
