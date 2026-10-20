using System;
using Cysharp.Threading.Tasks;

namespace Solitaire.Services
{
    public interface IVictoryStage
    {
        bool IsVisible { get; }
        UniTask PlayAsync(int score, Func<bool> stillActive, Action onNewMatch, Action onHome);
        void Hide();
    }
}
