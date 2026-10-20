using TMPro;
using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Candy reward look for ribbon +N: cream→hot orange gradient + underlay/glow.
    /// Does not touch Font Size, spacing, or style — those stay as set on the TMP in the Inspector.
    /// Assign Material: Baron Neue SDF - Streak Ribbon Bonus.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class StreakRibbonBonusLook : MonoBehaviour
    {
        [Tooltip("Optional. If empty, uses built-in ScorePopupTune bonus colors.")]
        [SerializeField]
        private ScorePopupTune tune;

        [SerializeField]
        private bool applyOnEnable = true;

        [Header("Face / shade (TMP material)")]
        [SerializeField]
        [Range(0f, 0.6f)]
        private float faceDilate = 0.4f;

        [SerializeField]
        [Range(0f, 1f)]
        private float underlayDilate = 0.35f;

        [SerializeField]
        [Range(0f, 1f)]
        private float underlaySoftness = 0.35f;

        [SerializeField]
        private float underlayOffsetX = 0.14f;

        [SerializeField]
        private float underlayOffsetY = -0.22f;

        [Header("Glow")]
        [SerializeField]
        private bool glow = true;

        [SerializeField]
        private Color glowColor = new Color(1f, 0.85f, 0.35f, 0.55f);

        [SerializeField]
        [Range(0f, 1f)]
        private float glowOuter = 0.28f;

        [SerializeField]
        [Range(0f, 1f)]
        private float glowPower = 0.7f;

        private TextMeshProUGUI label;

        private void OnEnable()
        {
            if (applyOnEnable)
                Apply();
        }

        [ContextMenu("Apply Bonus Look")]
        public void Apply()
        {
            if (label == null)
                label = GetComponent<TextMeshProUGUI>();

            if (label == null)
                return;

            var colors = tune != null ? tune : new ScorePopupTune();
            // Slightly hotter than flying popup so it reads on pink ribbon.
            var top = Color.Lerp(colors.bonusTop, Color.white, 0.25f);
            var bottom = Color.Lerp(colors.bonusBottom, new Color(1f, 0.55f, 0.05f, 1f), 0.35f);
            var shade = colors.bonusShade;
            shade.a = Mathf.Max(shade.a, 0.9f);

            // Keep Font Size / Character Spacing / Font Style from the TMP Inspector.
            label.color = Color.white;
            label.enableVertexGradient = true;
            label.colorGradient = new VertexGradient(top, top, bottom, bottom);
            label.outlineWidth = 0f;
            label.raycastTarget = false;

            var mat = label.fontMaterial;
            if (mat != null)
            {
                mat.DisableKeyword("OUTLINE_ON");
                mat.EnableKeyword("UNDERLAY_ON");
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
                mat.SetFloat(ShaderUtilities.ID_FaceDilate, Mathf.Clamp(faceDilate, 0f, 0.6f));
                mat.SetColor(ShaderUtilities.ID_UnderlayColor, shade);
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, Mathf.Clamp(underlayOffsetX, -1f, 1f));
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, Mathf.Clamp(underlayOffsetY, -1f, 1f));
                mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, Mathf.Clamp01(underlayDilate));
                mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, Mathf.Clamp01(underlaySoftness));

                if (glow)
                {
                    mat.EnableKeyword("GLOW_ON");
                    mat.SetColor(ShaderUtilities.ID_GlowColor, glowColor);
                    mat.SetFloat(ShaderUtilities.ID_GlowOuter, Mathf.Clamp01(glowOuter));
                    mat.SetFloat(ShaderUtilities.ID_GlowPower, Mathf.Clamp01(glowPower));
                    mat.SetFloat(ShaderUtilities.ID_GlowOffset, 0f);
                    mat.SetFloat(ShaderUtilities.ID_GlowInner, 0.05f);
                }
                else
                {
                    mat.DisableKeyword("GLOW_ON");
                }
            }

            label.UpdateMeshPadding();
        }
    }
}
