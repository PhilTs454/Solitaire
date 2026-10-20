using System;
using System.Collections.Generic;
using UnityEngine;

namespace Solitaire.Models
{
    public static class Audio
    {
        public const string Music = "Music";
        public const string SfxShuffle = "Shuffle";
        public const string SfxDeal = "Deal";
        public const string SfxDraw = "Draw";
        public const string SfxHint = "Hint";
        public const string SfxClick = "Click";
        public const string SfxError = "Error";
        public const string SfxStreakPip = "StreakPip";
        public const string SfxStreakCharge = "StreakCharge";
        public const string SfxStreak = "Streak";
        public const string SfxPlace = "Place";
        public const string SfxFlip = "Flip";

        [Serializable]
        public class Config
        {
            public List<AudioClip> AudioClips;
        }
    }
}