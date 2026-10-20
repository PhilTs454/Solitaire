using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Solitaire.Services
{
    public interface IVictorySequence
    {
        UniTask PlayAsync(
            int score,
            Func<UniTask<IReadOnlyList<VictoryGlyphLaunch>>> harvestAsync,
            Func<bool> stillWinning,
            Action onBeforeStage,
            Action onNewMatch,
            Action onHome
        );

        void PlayStagePreview(int score, Action onNewMatch, Action onHome);

        void PlayBubblePreview(IReadOnlyList<VictoryGlyphLaunch> launches);

        void Reset();
    }
}
