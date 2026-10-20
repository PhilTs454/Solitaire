using DG.Tweening;
using Solitaire.Helpers;
using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Additive ring on the bubble surface — readable impact even when shader ripples are subtle.
    /// </summary>
    public sealed class VictoryBubbleRippleFx : MonoBehaviour
    {
        private const string TweenIdPrefix = "VictoryBubbleRipple";
        private const string AdditiveShaderName = "Solitaire/SpriteAdditive";
        private const float LocalStartScale = 0.08f;
        private const float LocalEndScale = 0.5f;

        [SerializeField]
        private Sprite rippleSprite;

        [SerializeField]
        private Color rippleColor = new Color(1.6f, 2.4f, 2.6f, 1f);

        [SerializeField]
        private float duration = 0.22f;

        private static Material sharedAdditiveMaterial;
        private Transform poolRoot;
        private readonly RingSlot[] pool = new RingSlot[6];
        private int poolIndex;

        public void Configure(Sprite sprite)
        {
            if (sprite != null)
                rippleSprite = sprite;
        }

        public void Play(Transform sphere, Vector3 worldHit, int sortingOrder)
        {
            if (sphere == null)
                return;

            var sprite = rippleSprite != null ? rippleSprite : SoftRingSprite.Get();
            if (sprite == null)
                return;

            EnsurePool(sphere);
            var slot = Rent();
            if (slot == null)
                return;

            var local = sphere.InverseTransformPoint(worldHit);
            if (local.sqrMagnitude < 0.0001f)
                local = Vector3.up;
            local = local.normalized * 0.5f;

            var cam = Camera.main;
            slot.Root.SetParent(sphere, false);
            slot.Root.localPosition = local;
            slot.Root.localRotation = Quaternion.identity;
            if (cam != null)
                slot.Root.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);

            slot.Renderer.sprite = sprite;
            slot.Renderer.color = rippleColor;
            slot.Renderer.sortingOrder = sortingOrder;
            slot.Renderer.enabled = true;
            slot.Root.localScale = Vector3.one * LocalStartScale;

            DOTween.Kill(slot.TweenId);
            var seq = DOTween.Sequence().SetId(slot.TweenId);
            seq.Append(slot.Root.DOScale(LocalEndScale, duration).SetEase(Ease.OutBack, 2.2f));
            seq.Join(
                slot.Renderer
                    .DOColor(new Color(rippleColor.r, rippleColor.g, rippleColor.b, 0f), duration)
                    .SetEase(Ease.InQuad)
            );
            seq.OnComplete(() => Return(slot));
        }

        public void StopAll()
        {
            for (var i = 0; i < pool.Length; i++)
            {
                if (pool[i]?.Root == null)
                    continue;
                DOTween.Kill(pool[i].TweenId);
                pool[i].Renderer.enabled = false;
            }
        }

        private void EnsurePool(Transform sphere)
        {
            if (poolRoot != null)
                return;

            poolRoot = new GameObject("VictoryBubbleRipplePool").transform;
            poolRoot.SetParent(sphere, false);

            var material = GetAdditiveMaterial();
            for (var i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("Ripple_" + i);
                go.transform.SetParent(poolRoot, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                if (material != null)
                    renderer.material = material;
                pool[i] = new RingSlot
                {
                    Root = go.transform,
                    Renderer = renderer,
                    TweenId = TweenIdPrefix + i
                };
            }
        }

        private RingSlot Rent()
        {
            for (var i = 0; i < pool.Length; i++)
            {
                var idx = (poolIndex + i) % pool.Length;
                if (!pool[idx].Renderer.enabled)
                {
                    poolIndex = (idx + 1) % pool.Length;
                    return pool[idx];
                }
            }

            var stolen = pool[poolIndex];
            poolIndex = (poolIndex + 1) % pool.Length;
            DOTween.Kill(stolen.TweenId);
            stolen.Renderer.enabled = false;
            return stolen;
        }

        private void Return(RingSlot slot)
        {
            if (slot?.Renderer != null)
                slot.Renderer.enabled = false;
        }

        private static Material GetAdditiveMaterial()
        {
            if (sharedAdditiveMaterial != null)
                return sharedAdditiveMaterial;

            var shader = Shader.Find(AdditiveShaderName);
            if (shader == null)
                return null;

            sharedAdditiveMaterial = new Material(shader) { name = "VictoryBubbleRippleAdditive" };
            return sharedAdditiveMaterial;
        }

        private sealed class RingSlot
        {
            public Transform Root;
            public SpriteRenderer Renderer;
            public string TweenId;
        }
    }
}
