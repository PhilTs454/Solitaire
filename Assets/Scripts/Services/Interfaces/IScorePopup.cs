using System;
using Solitaire.Presenters;
using UnityEngine;

namespace Solitaire.Services
{
    public interface IScorePopup
    {
        /// <summary>Fires when a +N pop hits full size (or immediately if there is no pop).</summary>
        event Action<int> Committed;

        void Show(
            int delta,
            Vector3 worldPosition,
            float delaySeconds,
            ScorePopupStyle style = ScorePopupStyle.Normal
        );

        /// <summary>
        /// Bonus pop that bursts, then homes to a world point. Does not tick the HUD —
        /// call Commit when the motes land.
        /// </summary>
        void ShowHoming(
            int delta,
            Vector3 worldPosition,
            Vector3 homeWorldPosition,
            float delaySeconds,
            float homeDuration,
            ScorePopupStyle style = ScorePopupStyle.Bonus
        );

        /// <summary>Tell the HUD this chunk of points arrived.</summary>
        void Commit(int delta);
    }
}
