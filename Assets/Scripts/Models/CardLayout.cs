using System;
using UnityEngine;

namespace Solitaire.Models
{
    /// <summary>
    /// Stores local transforms for card face parts.
    /// Front/Back are shared; Type / Suit1 / Suit2 (and Ace center) can differ per suit.
    /// </summary>
    [CreateAssetMenu(fileName = "CardLayout", menuName = "Solitaire/Card Layout")]
    public class CardLayout : ScriptableObject
    {
        [Serializable]
        public struct PartTransform
        {
            public Vector3 LocalPosition;
            public Vector3 LocalScale;
            public Vector3 LocalEulerAngles;

            public bool HasScale =>
                LocalScale.x > 0.0001f || LocalScale.y > 0.0001f || LocalScale.z > 0.0001f;

            public static PartTransform FromTransform(Transform t)
            {
                return new PartTransform
                {
                    LocalPosition = t.localPosition,
                    LocalScale = t.localScale,
                    LocalEulerAngles = t.localEulerAngles
                };
            }

            public void ApplyTo(Transform t)
            {
                if (t == null)
                    return;

                t.localPosition = LocalPosition;
                t.localScale = LocalScale;
                t.localEulerAngles = LocalEulerAngles;
            }
        }

        [Serializable]
        public class SuitFaceLayout
        {
            [Tooltip("Corner rank letter/number")]
            public PartTransform Type;

            [Tooltip("Small corner suit")]
            public PartTransform Suit1;

            [Tooltip("Center suit (non-Ace cards)")]
            public PartTransform Suit2;

            [Tooltip("If on, Ace uses Ace Center on Suit2 instead of Suit2")]
            public bool UseAceCenter = true;

            [Tooltip("Center letter A on Aces only")]
            public PartTransform AceCenter;
        }

        [Header("Shared (all suits)")]
        public PartTransform Front;
        public PartTransform Back;

        [Header("Legacy shared face (fallback if a suit slot is empty)")]
        public PartTransform Type;
        public PartTransform Suit1;
        public PartTransform Suit2;

        [Header("Per-suit face layout (Spade, Club, Heart, Diamond)")]
        public SuitFaceLayout Spade = new SuitFaceLayout();
        public SuitFaceLayout Club = new SuitFaceLayout();
        public SuitFaceLayout Heart = new SuitFaceLayout();
        public SuitFaceLayout Diamond = new SuitFaceLayout();

        [Header("Optional collider (world units)")]
        public bool ApplyColliderSize;
        public Vector2 ColliderSize = new Vector2(1.09f, 1.64f);
        public Vector2 ColliderOffset;

        public SuitFaceLayout GetSuitLayout(Card.Suits suit)
        {
            switch (suit)
            {
                case Card.Suits.Spade:
                    return Spade ?? new SuitFaceLayout();
                case Card.Suits.Club:
                    return Club ?? new SuitFaceLayout();
                case Card.Suits.Heart:
                    return Heart ?? new SuitFaceLayout();
                case Card.Suits.Diamond:
                    return Diamond ?? new SuitFaceLayout();
                default:
                    return new SuitFaceLayout();
            }
        }

        public void SetSuitLayout(Card.Suits suit, SuitFaceLayout layout)
        {
            switch (suit)
            {
                case Card.Suits.Spade:
                    Spade = layout;
                    break;
                case Card.Suits.Club:
                    Club = layout;
                    break;
                case Card.Suits.Heart:
                    Heart = layout;
                    break;
                case Card.Suits.Diamond:
                    Diamond = layout;
                    break;
            }
        }

        /// <summary>
        /// Applies Type / Suit1 / Suit2 (or Ace center) for this suit.
        /// Falls back to legacy shared Type/Suit1/Suit2 when a part was never captured.
        /// </summary>
        public void ApplyFaceParts(
            Card.Suits suit,
            Card.Types type,
            Transform typeTransform,
            Transform suit1Transform,
            Transform suit2Transform
        )
        {
            var layout = GetSuitLayout(suit);

            var typePart = layout.Type.HasScale ? layout.Type : Type;
            var suit1Part = layout.Suit1.HasScale ? layout.Suit1 : Suit1;
            // Suit2 is the big pip (heart, spade, …). Aces draw a letter A in that slot.
            var pipPart = layout.Suit2.HasScale ? layout.Suit2 : Suit2;
            var suit2Part = pipPart;

            if (type == Card.Types.Ace)
            {
                if (layout.UseAceCenter && layout.AceCenter.HasScale)
                    suit2Part = layout.AceCenter;
                else if (layout.UseAceCenter && Suit2.HasScale)
                    // No Ace Center captured yet — keep the A at the shared size,
                    // not the per-suit pip scale (Heart Suit2 is smaller than the A).
                    suit2Part = Suit2;
            }

            typePart.ApplyTo(typeTransform);
            suit1Part.ApplyTo(suit1Transform);
            suit2Part.ApplyTo(suit2Transform);
        }
    }
}
