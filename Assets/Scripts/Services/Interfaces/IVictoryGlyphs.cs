using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Solitaire.Models;
using UnityEngine;

namespace Solitaire.Services
{
    public readonly struct VictoryGlyphLaunch
    {
        public Vector3 Origin { get; }
        public Sprite Sprite { get; }
        public bool IsRedSuit { get; }
        public Card.Suits Suit { get; }

        public VictoryGlyphLaunch(
            Vector3 origin,
            Sprite sprite,
            bool isRedSuit,
            Card.Suits suit
        )
        {
            Origin = origin;
            Sprite = sprite;
            IsRedSuit = isRedSuit;
            Suit = suit;
        }
    }

    public interface IVictoryGlyphs
    {
        UniTask PlayAsync(IReadOnlyList<VictoryGlyphLaunch> launches, Func<bool> stillWinning);
        void StopAll();
    }
}
