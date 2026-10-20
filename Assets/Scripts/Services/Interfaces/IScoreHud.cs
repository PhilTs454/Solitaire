using UnityEngine;

namespace Solitaire.Services
{
    /// <summary>The top-left score label. Used so streak motes can fly to it.</summary>
    public interface IScoreHud
    {
        RectTransform PointsRect { get; }
    }
}
