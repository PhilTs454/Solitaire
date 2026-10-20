using UnityEngine;

namespace Solitaire.Helpers
{
    /// <summary>
    /// Tiny procedural sprites for the victory stage overlay.
    /// </summary>
    public static class VictoryProcSprites
    {
        public static Material DefaultParticle()
        {
            return Resources.GetBuiltinResource<Material>("Default-Particle.mat");
        }

        private static Sprite white;
        private static Sprite star;
        private static Sprite glow;
        private static Sprite speck;
        private static Sprite ray;

        public static Sprite White()
        {
            if (white != null)
                return white;

            var tex = Texture2D.whiteTexture;
            white = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 4f);
            white.name = "VictoryWhite";
            white.hideFlags = HideFlags.HideAndDontSave;
            return white;
        }

        public static Sprite Star()
        {
            if (star != null)
                return star;

            const int size = 256;
            const float outer = 0.46f;
            const float inner = 0.18f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "VictoryStar",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var verts = new Vector2[10];
            var mid = (size - 1) * 0.5f;
            for (var i = 0; i < 10; i++)
            {
                var ang = (-90f + i * 36f) * Mathf.Deg2Rad;
                var r = ((i & 1) == 0 ? outer : inner) * size;
                verts[i] = new Vector2(mid + Mathf.Cos(ang) * r, mid + Mathf.Sin(ang) * r);
            }

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2(x, y);
                    var inside = PointInPolygon(p, verts);
                    var alpha = inside ? 1f : 0f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply(false, false);
            star = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            star.name = "VictoryStarSprite";
            star.hideFlags = HideFlags.HideAndDontSave;
            return star;
        }

        public static Sprite Glow()
        {
            if (glow != null)
                return glow;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "VictoryGlow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / center;
                    var dy = (y - center) / center;
                    var alpha = Mathf.Exp(-(dx * dx + dy * dy) * 2.8f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply(false, false);
            glow = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            glow.name = "VictoryGlowSprite";
            glow.hideFlags = HideFlags.HideAndDontSave;
            return glow;
        }

        public static Sprite Speck()
        {
            if (speck != null)
                return speck;

            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "VictorySpeck",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / center;
                    var dy = (y - center) / center;
                    var alpha = Mathf.Exp(-(dx * dx + dy * dy) * 5.4f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply(false, false);
            speck = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            speck.name = "VictorySpeckSprite";
            speck.hideFlags = HideFlags.HideAndDontSave;
            return speck;
        }

        public static Sprite Ray()
        {
            if (ray != null)
                return ray;

            const int width = 32;
            const int height = 128;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "VictoryRay",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var mid = (width - 1) * 0.5f;
            for (var y = 0; y < height; y++)
            {
                var along = 1f - y / (float)(height - 1);
                along = Mathf.Pow(Mathf.Clamp01(along), 1.35f);
                var half = Mathf.Lerp(1f, 0.12f, y / (float)(height - 1));
                for (var x = 0; x < width; x++)
                {
                    var nx = Mathf.Abs(x - mid) / mid;
                    float alpha;
                    if (nx > half)
                        alpha = 0f;
                    else
                        alpha = along * Mathf.Pow(1f - nx / Mathf.Max(0.001f, half), 1.15f);

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply(false, false);
            ray = Sprite.Create(
                tex,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0f),
                100f
            );
            ray.name = "VictoryRaySprite";
            ray.hideFlags = HideFlags.HideAndDontSave;
            return ray;
        }

        private static bool PointInPolygon(Vector2 point, Vector2[] poly)
        {
            var inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if (
                    ((poly[i].y > point.y) != (poly[j].y > point.y))
                    && (
                        point.x
                        < (poly[j].x - poly[i].x) * (point.y - poly[i].y) / (poly[j].y - poly[i].y)
                            + poly[i].x
                    )
                )
                    inside = !inside;
            }

            return inside;
        }
    }
}
