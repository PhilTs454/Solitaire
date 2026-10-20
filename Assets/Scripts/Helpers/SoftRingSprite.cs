using UnityEngine;

namespace Solitaire.Helpers
{
    /// <summary>
    /// Draws one soft white ring texture in code and shares it.
    /// Alpha ramps up from the inner edge, holds across a thin peak band, then ramps
    /// down to the outer edge, which is what makes it look blurry instead of hard-edged.
    /// </summary>
    public static class SoftRingSprite
    {
        private static Sprite cached;

        public static Sprite Get()
        {
            if (cached != null)
                return cached;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "SoftRing",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var dist = Mathf.Sqrt(dx * dx + dy * dy) / center;

                    const float inner = 0.72f;
                    const float peakStart = 0.78f;
                    const float peakEnd = 0.82f;
                    const float outer = 0.90f;

                    float alpha;
                    if (dist < inner || dist > outer)
                        alpha = 0f;
                    else if (dist < peakStart)
                        alpha = Mathf.InverseLerp(inner, peakStart, dist);
                    else if (dist <= peakEnd)
                        alpha = 1f;
                    else
                        alpha = 1f - Mathf.InverseLerp(peakEnd, outer, dist);

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply(false, false);
            cached = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f
            );
            cached.name = "SoftRingSprite";
            cached.hideFlags = HideFlags.HideAndDontSave;
            return cached;
        }
    }
}
