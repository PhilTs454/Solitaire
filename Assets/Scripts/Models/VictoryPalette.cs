using System;
using UnityEngine;

namespace Solitaire.Models
{
    [Serializable]
    public class VictoryPalette
    {
        [Tooltip("Dim overlay. Alpha is the target veil opacity.")]
        public Color Veil = new Color(0.07f, 0.03f, 0.14f, 0.78f);

        public Color Star = new Color(1f, 0.86f, 0.38f, 1f);

        public Color StarGlow = new Color(1f, 0.42f, 0.78f, 0.5f);

        public Color Ray = new Color(1f, 0.9f, 0.55f, 0.32f);

        [Tooltip("Theater legs. Purple/pink — not Disney red.")]
        public Color Curtain = new Color(0.4f, 0.1f, 0.48f, 1f);

        public Color Title = Color.white;

        public Color Score = new Color(1f, 0.92f, 0.55f, 1f);

        public Color Button = new Color(0.95f, 0.38f, 0.62f, 1f);

        public Color ButtonLabel = Color.white;
    }
}
