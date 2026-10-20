using UnityEngine;

namespace Solitaire.Services
{
    /// <summary>
    /// Shared, pooled land FX so cards do not each own ParticlePulse / Stars.
    /// </summary>
    public interface ICardLandFxService
    {
        /// <summary>Foundation land burst at the card. Optional Ace soft-ripple.</summary>
        void PlayFoundation(
            Transform cardTransform,
            SpriteRenderer faceRenderer,
            Sprite suitSprite,
            Sprite rankSprite,
            bool isRedSuit,
            bool playAceRipple,
            int rippleSortingOrder,
            LandFxVolume volume
        );

        /// <summary>Tableau confirm rings + stars at the card face.</summary>
        void PlayTableau(Transform cardTransform, SpriteRenderer faceRenderer, int sortingOrder);

        /// <summary>Same candy rings as a legal tableau drop, at a world point (victory bubble hit).</summary>
        void PlayWorld(Vector3 worldPosition, float worldFit, int sortingOrder, float alpha = 1f);

        /// <summary>Additive ribbon behind a flying victory card.</summary>
        void PlayFlightTrail(Transform cardTransform, bool isRedSuit, int sortingOrder);

        void StopFlightTrail(Transform cardTransform);

        void StopAllFlightTrails();
    }
}
