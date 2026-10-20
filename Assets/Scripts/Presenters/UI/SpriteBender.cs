using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Tessellates a UI graphic's quads into a grid and bends them along a parabolic
    /// spine, so a sprite that contains text can arc like a banner. Authoring aid:
    /// bake it down from the inspector, which writes a normal sprite and removes this.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("UI/Effects/Sprite Bender")]
    public class SpriteBender : BaseMeshEffect
    {
        private const int MaxVertices = 60000;
        private const float Epsilon = 0.0001f;

        [Header("Tessellation")]
        [SerializeField, Range(1, 96)]
        private int columns = 32;

        [SerializeField, Range(1, 32)]
        private int rows = 4;

        [Header("Bend")]
        [SerializeField, Tooltip("Arcs the whole sprite. Fraction of height.")]
        [Range(-1f, 1f)]
        private float bend = 0f;

        [SerializeField, Tooltip("Curves the top edge only, fanning the letters.")]
        [Range(-1f, 1f)]
        private float arch = 0f;

        [SerializeField, Tooltip("Trapezoid: positive widens the top, negative the bottom.")]
        [Range(-1f, 1f)]
        private float taper = 0f;

        [SerializeField, Tooltip("Rotate each column to follow the spine instead of shearing.")]
        private bool rotateWithTangent = true;

        [Header("Wave")]
        [SerializeField, Range(-1f, 1f)]
        private float wave = 0f;

        [SerializeField, Range(0f, 8f)]
        private float waveFrequency = 1f;

        [SerializeField, Range(0f, 1f)]
        private float wavePhase = 0f;

        private static readonly List<UIVertex> Stream = new List<UIVertex>(256);

        public int Columns => columns;

        public int Rows => rows;

        public float Bend => bend;

        public float Arch => arch;

        public float Taper => taper;

        public bool HasDeformation =>
            Mathf.Abs(bend) > Epsilon
            || Mathf.Abs(arch) > Epsilon
            || Mathf.Abs(taper) > Epsilon
            || Mathf.Abs(wave) > Epsilon;

        /// <summary>
        /// Displaces one point of the source quad. The editor preview and the bake both
        /// route through here so what you see is what gets written to the PNG.
        /// </summary>
        public Vector2 Deform(Vector2 point, Rect sourceRect)
        {
            if (sourceRect.width <= Epsilon || sourceRect.height <= Epsilon)
                return point;

            var u = Mathf.Clamp01((point.x - sourceRect.xMin) / sourceRect.width);
            var v = Mathf.Clamp01((point.y - sourceRect.yMin) / sourceRect.height);
            var t = u - 0.5f;
            var center = sourceRect.center;

            // Trapezoid first, so the tapered shape feeds into the bend frame.
            var x = center.x + (point.x - center.x) * (1f + taper * (v - 0.5f) * 2f);

            var bendAmount = bend * sourceRect.height;
            var waveAmount = wave * sourceRect.height;
            var phase = 2f * Mathf.PI * (waveFrequency * u + wavePhase);

            var spineY =
                center.y + bendAmount * (1f - 4f * t * t) + waveAmount * Mathf.Sin(phase);
            var slope =
                (
                    -8f * bendAmount * t
                    + waveAmount * 2f * Mathf.PI * waveFrequency * Mathf.Cos(phase)
                ) / sourceRect.width;

            var tangent = rotateWithTangent ? new Vector2(1f, slope).normalized : Vector2.right;
            var normal = new Vector2(-tangent.y, tangent.x);

            var bent = new Vector2(x, spineY) + normal * ((v - 0.5f) * sourceRect.height);
            bent.y += arch * sourceRect.height * (1f - 4f * t * t) * v;
            return bent;
        }

        public void MarkDirty()
        {
            if (graphic != null)
                graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || !HasDeformation || vh.currentVertCount == 0)
                return;

            Stream.Clear();
            vh.GetUIVertexStream(Stream);
            if (Stream.Count < 3)
                return;

            var sourceRect = GetBounds(Stream);
            if (sourceRect.width <= Epsilon || sourceRect.height <= Epsilon)
                return;

            vh.Clear();

            if (!IsQuadStream(Stream))
            {
                // Unknown topology: displace in place and keep the original triangles.
                for (var i = 0; i < Stream.Count; i++)
                    Stream[i] = WithDeformedPosition(Stream[i], sourceRect);

                vh.AddUIVertexTriangleStream(Stream);
                return;
            }

            GetTessellation(Stream.Count / 6, out var cols, out var rws);

            for (var i = 0; i < Stream.Count; i += 6)
            {
                AddBentQuad(
                    vh,
                    Stream[i],
                    Stream[i + 1],
                    Stream[i + 2],
                    Stream[i + 4],
                    sourceRect,
                    cols,
                    rws
                );
            }
        }

        private void AddBentQuad(
            VertexHelper vh,
            UIVertex bottomLeft,
            UIVertex topLeft,
            UIVertex topRight,
            UIVertex bottomRight,
            Rect sourceRect,
            int cols,
            int rws
        )
        {
            var startIndex = vh.currentVertCount;

            for (var y = 0; y <= rws; y++)
            {
                var v = (float)y / rws;
                for (var x = 0; x <= cols; x++)
                {
                    var u = (float)x / cols;
                    var vertex = Bilerp(bottomLeft, topLeft, topRight, bottomRight, u, v);
                    vh.AddVert(WithDeformedPosition(vertex, sourceRect));
                }
            }

            var stride = cols + 1;
            for (var y = 0; y < rws; y++)
            {
                for (var x = 0; x < cols; x++)
                {
                    var i = startIndex + y * stride + x;
                    vh.AddTriangle(i, i + stride, i + stride + 1);
                    vh.AddTriangle(i, i + stride + 1, i + 1);
                }
            }
        }

        private UIVertex WithDeformedPosition(UIVertex vertex, Rect sourceRect)
        {
            var bent = Deform(vertex.position, sourceRect);
            vertex.position = new Vector3(bent.x, bent.y, vertex.position.z);
            return vertex;
        }

        private void GetTessellation(int quadCount, out int cols, out int rws)
        {
            cols = Mathf.Max(1, columns);
            rws = Mathf.Max(1, rows);

            while (
                (long)quadCount * (cols + 1) * (rws + 1) > MaxVertices && (cols > 1 || rws > 1)
            )
            {
                if (cols >= rws)
                    cols = Mathf.Max(1, cols / 2);
                else
                    rws = Mathf.Max(1, rws / 2);
            }
        }

        private static bool IsQuadStream(List<UIVertex> stream)
        {
            if (stream.Count == 0 || stream.Count % 6 != 0)
                return false;

            for (var i = 0; i < stream.Count; i += 6)
            {
                if (
                    stream[i + 3].position != stream[i + 2].position
                    || stream[i + 5].position != stream[i].position
                )
                    return false;
            }

            return true;
        }

        private static Rect GetBounds(List<UIVertex> stream)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            for (var i = 0; i < stream.Count; i++)
            {
                var p = stream[i].position;
                min.x = Mathf.Min(min.x, p.x);
                min.y = Mathf.Min(min.y, p.y);
                max.x = Mathf.Max(max.x, p.x);
                max.y = Mathf.Max(max.y, p.y);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static UIVertex Bilerp(
            UIVertex bottomLeft,
            UIVertex topLeft,
            UIVertex topRight,
            UIVertex bottomRight,
            float u,
            float v
        )
        {
            var bottom = LerpVertex(bottomLeft, bottomRight, u);
            var top = LerpVertex(topLeft, topRight, u);
            return LerpVertex(bottom, top, v);
        }

        private static UIVertex LerpVertex(UIVertex a, UIVertex b, float t)
        {
            return new UIVertex
            {
                position = Vector3.Lerp(a.position, b.position, t),
                normal = Vector3.Lerp(a.normal, b.normal, t),
                tangent = Vector4.Lerp(a.tangent, b.tangent, t),
                color = Color32.Lerp(a.color, b.color, t),
                uv0 = Vector4.Lerp(a.uv0, b.uv0, t),
                uv1 = Vector4.Lerp(a.uv1, b.uv1, t),
                uv2 = Vector4.Lerp(a.uv2, b.uv2, t),
                uv3 = Vector4.Lerp(a.uv3, b.uv3, t)
            };
        }
    }
}
