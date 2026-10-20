using Solitaire.Models;
using UniRx;
using UnityEngine;

namespace Solitaire.Services
{
    public class StreakService : IStreakService
    {
        public const int MaxPips = 5;

        private readonly Game.Config config;

        public IntReactiveProperty Count { get; } = new();

        public int NextBonus { get; private set; }

        public int LastAwardedBonus { get; private set; }

        public StreakService(Game.Config config)
        {
            this.config = config;
            ResetBonusValue();
        }

        public bool AddPip()
        {
            if (Count.Value >= MaxPips)
                return false;

            Count.Value++;
            return Count.Value >= MaxPips;
        }

        public void Break()
        {
            Count.Value = 0;
        }

        public void Complete()
        {
            if (Count.Value >= MaxPips)
                Count.Value = 0;
        }

        public int AwardBonus()
        {
            if (NextBonus <= 0)
                ResetBonusValue();

            LastAwardedBonus = NextBonus;
            NextBonus += Mathf.Max(1, BonusStep());
            return LastAwardedBonus;
        }

        public void RevertLastAward()
        {
            if (LastAwardedBonus <= 0)
                return;

            NextBonus = LastAwardedBonus;
            LastAwardedBonus = 0;
        }

        public void Reset()
        {
            Count.Value = 0;
            ResetBonusValue();
        }

        public void Set(int value)
        {
            Count.Value = Mathf.Clamp(value, 0, MaxPips);
        }

        private void ResetBonusValue()
        {
            NextBonus = BaseBonus();
            LastAwardedBonus = 0;
        }

        private int BaseBonus()
        {
            var n = config != null ? config.PointsStreakBonus : 50;
            return n > 0 ? n : 50;
        }

        private int BonusStep()
        {
            var n = config != null ? config.PointsStreakBonusStep : 10;
            return n > 0 ? n : 10;
        }
    }
}
