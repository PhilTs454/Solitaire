using UniRx;

namespace Solitaire.Services
{
    public interface IStreakService
    {
        IntReactiveProperty Count { get; }

        /// <summary>Bonus the ribbon promises next (50, 60, 70…).</summary>
        int NextBonus { get; }

        /// <summary>Bonus paid by the last AwardBonus call (0 if none).</summary>
        int LastAwardedBonus { get; }

        /// <summary>Adds one orb. Returns true only on the pip that fills the last slot.</summary>
        bool AddPip();

        void Break();

        /// <summary>Cashes in a full streak. Does nothing if the streak isn't full.</summary>
        void Complete();

        /// <summary>
        /// Pays the current NextBonus into LastAwardedBonus and steps NextBonus by +10 (config step).
        /// </summary>
        int AwardBonus();

        /// <summary>Undo of AwardBonus — restores NextBonus to what was just paid.</summary>
        void RevertLastAward();

        void Reset();
        void Set(int value);
    }
}
