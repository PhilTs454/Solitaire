using System.IO;
using Solitaire.Presenters;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Solitaire.Editor
{
    public struct SpriteBakeSettings
    {
        public int supersample;
        public float resolutionScale;
        public float paddingPixels;
        public bool useGraphicMaterial;
        public bool flipVertically;
        public bool applyToImage;

        public static SpriteBakeSettings Default =>
            new SpriteBakeSettings
            {
                supersample = 2,
                resolutionScale = 1f,
                paddingPixels = 4f,
                useGraphicMaterial = false,
                flipVertically = false,
                applyToImage = true
            };
    }

    /// <summary>
    /// Renders a <see cref="SpriteBender"/>'s deformed quad into a PNG next to the source
    /// sprite, so the bend costs nothing at runtime. Shares the deform math with the
    /// component, so the baked result matches the Scene view preview.
    /// </summary>
    public static class SpriteBendBaker
    {
        private const string BakeShaderName = "Solitaire/UISpriteBake";
        private const string MainTextureProperty = "_MainTex";
        private const string UndoName = "Bake Bent Sprite";
        private const int MaxTextureSide = 4096;
        private const int MaxRenderSide = 8192;
        private const float Epsilon = 0.0001f;

        public static Sprite Bake(SpriteBender bender, SpriteBakeSettings settings, out string error)
        {
            error = null;

            var image = bender == null ? null : bender.GetComponent<Image>();
            if (image == null)
            {
                error = "Baking needs an Image component on the same object.";
                return null;
            }

            var sprite = image.sprite;
            if (sprite == null || sprite.texture == null)
            {
                error = "Assign a sprite on the Image first.";
                return null;
            }

            var sourcePath = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrEmpty(sourcePath))
            {
                error = "The sprite is not a saved asset, so there is nowhere to write the bake.";
                return null;
            }

            var quad = GetDrawnQuad(image);
            var sourceRect = Rect.MinMaxRect(quad.x, quad.y, quad.z, quad.w);
            if (sourceRect.width <= Epsilon || sourceRect.height <= Epsilon)
            {
                error = "The Image rect is empty. Give it a size before baking.";
                return null;
            }

            var material = GetBakeMaterial(image, settings.useGraphicMaterial, out error);
            if (material == null)
                return null;

            var mesh = BuildMesh(bender, quad, GetOuterUv(sprite), sourceRect);

            try
            {
                var density = GetSourceDensity(sprite, sourceRect)
                    * Mathf.Max(0.05f, settings.resolutionScale);
                var bakeRect = GetBakeRect(mesh, sourceRect, settings.paddingPixels / density);

                // Keep the aspect ratio when the requested resolution is over the cap.
                var overflow =
                    Mathf.Max(bakeRect.width, bakeRect.height) * density / MaxTextureSide;
                if (overflow > 1f)
                    density /= overflow;

                var width = Mathf.Clamp(
                    Mathf.CeilToInt(bakeRect.width * density),
                    4,
                    MaxTextureSide
                );
                var height = Mathf.Clamp(
                    Mathf.CeilToInt(bakeRect.height * density),
                    4,
                    MaxTextureSide
                );
                var supersample = GetSupersample(settings.supersample, width, height);

                var pixels = RenderPixels(
                    mesh,
                    bakeRect,
                    sprite.texture,
                    material,
                    width * supersample,
                    height * supersample,
                    settings.flipVertically
                );

                if (supersample > 1)
                {
                    pixels = Downsample(
                        pixels,
                        width * supersample,
                        height * supersample,
                        supersample
                    );
                }

                var assetPath = WritePng(pixels, width, height, sourcePath);
                ImportAsSprite(assetPath, sourcePath, width, height);

                var baked = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (baked == null)
                {
                    error = "The PNG was written but did not import as a sprite: " + assetPath;
                    return null;
                }

                if (settings.applyToImage)
                    ApplyToImage(image, bender, baked, bakeRect);

                return baked;
            }
            finally
            {
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(material);
            }
        }

        /// <summary>
        /// The quad the Image actually draws, matching Image.GetDrawingDimensions: sprite
        /// mesh padding and Preserve Aspect both shrink it inside the RectTransform.
        /// </summary>
        public static Vector4 GetDrawnQuad(Image image)
        {
            var rect = image.rectTransform.rect;
            var sprite = image.sprite;
            if (sprite == null)
                return new Vector4(rect.xMin, rect.yMin, rect.xMax, rect.yMax);

            var padding = UnityEngine.Sprites.DataUtility.GetPadding(sprite);
            var size = new Vector2(sprite.rect.width, sprite.rect.height);
            var spriteWidth = Mathf.Max(1, Mathf.RoundToInt(size.x));
            var spriteHeight = Mathf.Max(1, Mathf.RoundToInt(size.y));

            var inset = new Vector4(
                padding.x / spriteWidth,
                padding.y / spriteHeight,
                (spriteWidth - padding.z) / spriteWidth,
                (spriteHeight - padding.w) / spriteHeight
            );

            if (image.preserveAspect && size.sqrMagnitude > 0f)
                PreserveAspect(ref rect, size, image.rectTransform.pivot);

            return new Vector4(
                rect.x + rect.width * inset.x,
                rect.y + rect.height * inset.y,
                rect.x + rect.width * inset.z,
                rect.y + rect.height * inset.w
            );
        }

        public static Vector4 GetOuterUv(Sprite sprite)
        {
            return UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
        }

        private static void PreserveAspect(ref Rect rect, Vector2 spriteSize, Vector2 pivot)
        {
            var spriteRatio = spriteSize.x / spriteSize.y;
            var rectRatio = rect.width / rect.height;

            if (spriteRatio > rectRatio)
            {
                var oldHeight = rect.height;
                rect.height = rect.width * (1f / spriteRatio);
                rect.y += (oldHeight - rect.height) * pivot.y;
            }
            else
            {
                var oldWidth = rect.width;
                rect.width = rect.height * spriteRatio;
                rect.x += (oldWidth - rect.width) * pivot.x;
            }
        }

        private static Mesh BuildMesh(
            SpriteBender bender,
            Vector4 quad,
            Vector4 uv,
            Rect sourceRect
        )
        {
            var columns = Mathf.Max(1, bender.Columns);
            var rows = Mathf.Max(1, bender.Rows);
            var stride = columns + 1;

            var vertices = new Vector3[stride * (rows + 1)];
            var uvs = new Vector2[vertices.Length];
            var colors = new Color32[vertices.Length];
            var triangles = new int[columns * rows * 6];
            var white = (Color32)Color.white;

            var index = 0;
            for (var y = 0; y <= rows; y++)
            {
                var v = (float)y / rows;
                for (var x = 0; x <= columns; x++, index++)
                {
                    var u = (float)x / columns;
                    var point = new Vector2(
                        Mathf.Lerp(quad.x, quad.z, u),
                        Mathf.Lerp(quad.y, quad.w, v)
                    );
                    var bent = bender.Deform(point, sourceRect);

                    vertices[index] = new Vector3(bent.x, bent.y, 0f);
                    uvs[index] = new Vector2(
                        Mathf.Lerp(uv.x, uv.z, u),
                        Mathf.Lerp(uv.y, uv.w, v)
                    );
                    colors[index] = white;
                }
            }

            var triangle = 0;
            for (var y = 0; y < rows; y++)
            {
                for (var x = 0; x < columns; x++)
                {
                    var corner = y * stride + x;
                    triangles[triangle++] = corner;
                    triangles[triangle++] = corner + stride;
                    triangles[triangle++] = corner + stride + 1;
                    triangles[triangle++] = corner;
                    triangles[triangle++] = corner + stride + 1;
                    triangles[triangle++] = corner + 1;
                }
            }

            var mesh = new Mesh { name = "SpriteBendBake", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Grows the source rect symmetrically to cover the bent mesh, which keeps the
        /// rect center fixed so the baked sprite lands where the original one was.
        /// </summary>
        private static Rect GetBakeRect(Mesh mesh, Rect sourceRect, float padding)
        {
            var bounds = mesh.bounds;
            var center = sourceRect.center;
            var halfWidth =
                Mathf.Max(center.x - bounds.min.x, bounds.max.x - center.x) + padding;
            var halfHeight =
                Mathf.Max(center.y - bounds.min.y, bounds.max.y - center.y) + padding;

            return new Rect(
                center.x - halfWidth,
                center.y - halfHeight,
                halfWidth * 2f,
                halfHeight * 2f
            );
        }

        private static float GetSourceDensity(Sprite sprite, Rect sourceRect)
        {
            var padding = UnityEngine.Sprites.DataUtility.GetPadding(sprite);
            var drawnPixels = sprite.rect.width - padding.x - padding.z;
            return drawnPixels > Epsilon ? drawnPixels / sourceRect.width : 100f;
        }

        private static int GetSupersample(int requested, int width, int height)
        {
            var supersample = Mathf.Clamp(requested, 1, 4);
            while (supersample > 1 && Mathf.Max(width, height) * supersample > MaxRenderSide)
                supersample--;

            return supersample;
        }

        private static Material GetBakeMaterial(Image image, bool useGraphicMaterial, out string error)
        {
            error = null;

            if (
                useGraphicMaterial
                && image.material != null
                && image.material.HasProperty(MainTextureProperty)
            )
                return new Material(image.material) { hideFlags = HideFlags.HideAndDontSave };

            var shader = Shader.Find(BakeShaderName);
            if (shader == null)
            {
                error = "Shader " + BakeShaderName + " not found. Is UISpriteBake.shader imported?";
                return null;
            }

            return new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private static Color32[] RenderPixels(
            Mesh mesh,
            Rect bakeRect,
            Texture texture,
            Material material,
            int width,
            int height,
            bool flipVertically
        )
        {
            material.SetTexture(MainTextureProperty, texture);

            var bottom = flipVertically ? bakeRect.yMax : bakeRect.yMin;
            var top = flipVertically ? bakeRect.yMin : bakeRect.yMax;
            var projection = Matrix4x4.Ortho(
                bakeRect.xMin,
                bakeRect.xMax,
                bottom,
                top,
                -1f,
                1f
            );

            var target = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB
            );

            var command = new CommandBuffer { name = "SpriteBender Bake" };
            command.SetRenderTarget(target);
            command.ClearRenderTarget(true, true, new Color(0f, 0f, 0f, 0f));
            command.SetViewProjectionMatrices(Matrix4x4.identity, projection);
            command.DrawMesh(mesh, Matrix4x4.identity, material, 0, 0);
            Graphics.ExecuteCommandBuffer(command);
            command.Release();

            var previous = RenderTexture.active;
            RenderTexture.active = target;

            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            readback.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
            readback.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);

            var pixels = readback.GetPixels32();
            Object.DestroyImmediate(readback);
            return pixels;
        }

        /// <summary>
        /// Box filter that weights colour by coverage, so transparent texels never bleed
        /// dark edges into the letters.
        /// </summary>
        private static Color32[] Downsample(Color32[] source, int width, int height, int factor)
        {
            var outWidth = width / factor;
            var outHeight = height / factor;
            var result = new Color32[outWidth * outHeight];
            var samples = factor * factor;

            for (var y = 0; y < outHeight; y++)
            {
                for (var x = 0; x < outWidth; x++)
                {
                    float r = 0f,
                        g = 0f,
                        b = 0f,
                        a = 0f;

                    for (var sampleY = 0; sampleY < factor; sampleY++)
                    {
                        var row = (y * factor + sampleY) * width + x * factor;
                        for (var sampleX = 0; sampleX < factor; sampleX++)
                        {
                            var pixel = source[row + sampleX];
                            var alpha = pixel.a / 255f;
                            r += pixel.r * alpha;
                            g += pixel.g * alpha;
                            b += pixel.b * alpha;
                            a += alpha;
                        }
                    }

                    var index = y * outWidth + x;
                    if (a <= Epsilon)
                    {
                        result[index] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    result[index] = new Color32(
                        ToByte(r / a),
                        ToByte(g / a),
                        ToByte(b / a),
                        ToByte(a / samples * 255f)
                    );
                }
            }

            return result;
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value), 0, 255);
        }

        private static string WritePng(Color32[] pixels, int width, int height, string sourcePath)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            texture.SetPixels32(pixels);
            texture.Apply();

            var bytes = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            var directory = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrEmpty(directory))
                directory = "Assets";

            var fileName = Path.GetFileNameWithoutExtension(sourcePath) + "_Bent.png";
            var assetPath = AssetDatabase.GenerateUniqueAssetPath(
                directory.Replace('\\', '/') + "/" + fileName
            );

            File.WriteAllBytes(assetPath, bytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            return assetPath;
        }

        private static void ImportAsSprite(
            string assetPath,
            string sourcePath,
            int width,
            int height
        )
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;

            var source = AssetImporter.GetAtPath(sourcePath) as TextureImporter;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = Mathf.Clamp(
                Mathf.NextPowerOfTwo(Mathf.Max(width, height)),
                32,
                8192
            );

            if (source != null)
            {
                importer.textureCompression = source.textureCompression;
                importer.filterMode = source.filterMode;
                importer.spritePixelsPerUnit = source.spritePixelsPerUnit;
                importer.npotScale = source.npotScale;
            }

            importer.SaveAndReimport();
        }

        private static void ApplyToImage(
            Image image,
            SpriteBender bender,
            Sprite baked,
            Rect bakeRect
        )
        {
            var rectTransform = image.rectTransform;
            Undo.RecordObject(image, UndoName);
            Undo.RecordObject(rectTransform, UndoName);

            // Stretched anchors have no fixed size to grow, so leave the layout alone.
            if (rectTransform.anchorMin == rectTransform.anchorMax)
            {
                var center = bakeRect.center;
                rectTransform.sizeDelta = bakeRect.size;
                rectTransform.anchoredPosition += center - rectTransform.rect.center;
            }

            image.sprite = baked;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;

            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rectTransform);

            Undo.DestroyObjectImmediate(bender);
        }
    }
}
