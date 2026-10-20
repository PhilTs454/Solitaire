using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Inspector knobs for score pops. Live on GameInstaller so they persist.
    /// One swoop: fast 45° burst (speed + scale lerp) → slower float up.
    /// </summary>
    [Serializable]
    public class ScorePopupTune
    {
        [Header("Text")]
        [Tooltip("World-space TMP size. Bigger = larger gold numbers.")]
        public float fontSize = 2.6f;

        [Tooltip("World units right of the card/slot. Negative = left.")]
        public float offsetX = 0f;

        [Tooltip("World units above the card/slot. Negative = below.")]
        public float offsetY = 0.22f;

        [Header("Burst (fast 45° pop)")]
        [Tooltip("Degrees from the ground. 45 = always up-right.")]
        public float burstAngle = 45f;

        [Tooltip("How far the burst travels, in world units.")]
        public float burstDistance = 0.5f;

        [Tooltip("Seconds for the burst. Smaller = snappier.")]
        public float burstDuration = 0.16f;

        [Tooltip("Speed at the start of the burst. Higher = harder launch.")]
        public float burstSpeedStart = 8f;

        [Tooltip("Speed at the end of the burst (then the slow float takes over).")]
        public float burstSpeedEnd = 1.2f;

        [Tooltip("Scale when it appears (0.2 = tiny). Lerps to Scale Float during the burst.")]
        public float scaleStart = 0.25f;

        [Tooltip("Scale once the burst has slowed — held during the float.")]
        public float scaleFloat = 1f;

        [Header("Float (slow rise after burst)")]
        [Tooltip("How far it drifts up after the burst, in world units.")]
        public float rise = 0.7f;

        [Tooltip("How far it drifts right after the burst, in world units. 0 = straight up.")]
        [FormerlySerializedAs("floatLeft")]
        public float floatRight = 0.25f;

        [Tooltip("Last this fraction of the number's life (burst+float) goes straight up — no more right.")]
        [Range(0f, 0.5f)]
        public float upOnlyTail = 0.2f;

        [Tooltip("Speed at the start of the float. Match Burst Speed End for a smooth handoff.")]
        public float floatSpeedStart = 1.2f;

        [Tooltip("Speed at the end of the float. Lower = a softer stop.")]
        public float floatSpeedEnd = 0.35f;

        [Tooltip("Seconds for the slow float + fade. Bigger = gentler.")]
        public float floatDuration = 0.75f;

        [Tooltip("When two pops share one land (flip + score), wait this long after the first before the second.")]
        public float stackStagger = 0.2f;

        [Header("Gold (plus)")]
        public Color goldTop = new Color(1f, 0.96f, 0.62f, 1f);

        public Color goldBottom = new Color(1f, 0.58f, 0.12f, 1f);

        [Tooltip("Drop shade under plus scores. Dark gold, not a hard outline.")]
        public Color goldShade = new Color(0.35f, 0.12f, 0.02f, 0.75f);

        [Header("Muted (minus)")]
        public Color muteTop = new Color(0.82f, 0.55f, 0.75f, 1f);

        public Color muteBottom = new Color(0.48f, 0.26f, 0.5f, 1f);

        public Color muteShade = new Color(0.18f, 0.05f, 0.16f, 0.75f);

        [Header("Fat + shade")]
        [Tooltip("Plumps the letters. 0 = font as-is, 0.3 = candy-thick.")]
        [Range(0f, 0.6f)]
        public float faceDilate = 0.28f;

        [Tooltip("How far the shade sits to the right. Negative = left.")]
        [Range(-1f, 1f)]
        public float shadeOffsetX = 0.12f;

        [Tooltip("How far the shade sits down. Negative = down.")]
        [Range(-1f, 1f)]
        public float shadeOffsetY = -0.18f;

        [Tooltip("How chunky the shade blob is.")]
        [Range(0f, 1f)]
        public float shadeDilate = 0.2f;

        [Tooltip("Soft edge on the shade. Higher = more blur, less stamp.")]
        [Range(0f, 1f)]
        public float shadeSoftness = 0.4f;

        [Header("Streak bonus")]
        [Tooltip("Small word above the number. Empty = number only.")]
        public string bonusLabel = "STREAK";

        [Tooltip("Gap between label and number. Negative pulls them together.")]
        public float bonusLineSpacing = -14f;

        [Tooltip("How much bigger the bonus number is than a normal pop.")]
        public float bonusFontScale = 1.6f;

        [Tooltip("How much further the bonus number drifts up.")]
        public float bonusRiseScale = 1.4f;

        [Tooltip("How much longer the bonus number lingers before fading.")]
        public float bonusHoldScale = 1.5f;

        [Tooltip("Hot top colour for the bonus number.")]
        public Color bonusTop = new Color(1f, 1f, 0.86f, 1f);

        public Color bonusBottom = new Color(1f, 0.4f, 0.08f, 1f);

        public Color bonusShade = new Color(0.4f, 0.07f, 0f, 0.8f);
    }
}
