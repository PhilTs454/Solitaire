using DG.Tweening;
using Solitaire.Helpers;
using UnityEngine;
using UnityEngine.Rendering;

namespace Solitaire.Presenters
{
    /// <summary>
    /// After the bubble pops: a Pngtree sunburst that grows in, then a slow
    /// idle glow behind the slammed score.
    /// </summary>
    public sealed class VictoryScoreShine
    {
        private const string PoolName = "VictoryScoreShinePool";
        private const string SunRaysTweenId = "VictoryScoreSunRays";
        private const string IdleTweenId = "VictoryScoreShineIdle";
        private const int CardSortingLayerId = 34509423;
        private const float SunRaysStartScale = 0.1f;
        private const float SunRaysEndScale = 0.45f;
        private const float SunRaysGrow = 0.4f;
        private const float SunRaysSpinSeconds = 120f;
        private const float SunRaysAlphaMin = 0.10f;
        private const float SunRaysAlphaMax = 0.15f;
        private const float SunRaysAlphaPulse = 2.4f;

        private static readonly Color HaloGold = new Color(1.35f, 1.02f, 0.4f, 0.22f);
        private static readonly Color SunRaysColor = new Color(1f, 1f, 0f, SunRaysAlphaMin);

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        private Transform poolRoot;
        private SpriteRenderer idleGlow;
        private SpriteRenderer sunRays;
        private Sprite sunRaysSprite;
        private Material sunRaysMaterial;
        private Material particleMaterial;
        private bool didBuild;

        public void Warmup(
            Sprite raysSprite = null,
            Material raysMaterial = null,
            Material defaultParticle = null
        )
        {
            if (raysSprite != null)
                sunRaysSprite = raysSprite;
            if (raysMaterial != null)
                sunRaysMaterial = raysMaterial;
            if (defaultParticle != null)
                particleMaterial = defaultParticle;
            EnsurePool();
        }

        public void PlayBurst(Vector3 worldCenter, float scoreWidth, int sortingOrder)
        {
            EnsurePool();
            if (!didBuild || sunRays == null)
                return;

            StopSunRays();
            poolRoot.position = worldCenter;

            sunRays.sortingLayerName = "Default";
            sunRays.sortingOrder = 1;
            sunRays.color = SunRaysColor;
            sunRays.transform.localPosition = Vector3.zero;
            sunRays.transform.localRotation = Quaternion.identity;
            sunRays.transform.localScale = Vector3.one * SunRaysStartScale;
            sunRays.gameObject.SetActive(true);

            sunRays
                .transform.DOScale(SunRaysEndScale, SunRaysGrow)
                .SetEase(Ease.OutQuad)
                .SetId(SunRaysTweenId);
            sunRays
                .transform.DOLocalRotate(new Vector3(0f, 0f, 360f), SunRaysSpinSeconds, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetRelative(true)
                .SetId(SunRaysTweenId);
            DOTween
                .ToAlpha(
                    () => sunRays.color,
                    value => sunRays.color = value,
                    SunRaysAlphaMax,
                    SunRaysAlphaPulse
                )
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetId(SunRaysTweenId);
        }

        public void PlayIdle(Vector3 worldCenter, float scoreWidth, int sortingOrder)
        {
            EnsurePool();
            if (!didBuild)
                return;

            StopIdle();
            poolRoot.position = worldCenter;
            var width = Mathf.Max(0.35f, scoreWidth);

            if (idleGlow != null)
            {
                var fit = SpriteFit(idleGlow.sprite, width * 1.55f);
                idleGlow.sortingOrder = sortingOrder;
                idleGlow.color = new Color(HaloGold.r, HaloGold.g, HaloGold.b, 0f);
                idleGlow.transform.localPosition = Vector3.zero;
                idleGlow.transform.localScale = Vector3.one * fit;
                idleGlow.gameObject.SetActive(true);

                var glowSeq = DOTween.Sequence().SetId(IdleTweenId);
                glowSeq.Append(idleGlow.DOFade(HaloGold.a, 0.4f).SetEase(Ease.OutQuad));
                glowSeq.Append(
                    idleGlow
                        .DOFade(HaloGold.a * 0.55f, 1.2f)
                        .SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo)
                );
            }
        }

        public void StopAll()
        {
            StopSunRays();
            StopIdle();
        }

        public void Dispose()
        {
            StopAll();
            didBuild = false;
            if (poolRoot != null)
            {
                Object.Destroy(poolRoot.gameObject);
                poolRoot = null;
            }

            idleGlow = null;
            sunRays = null;
        }

        private void StopSunRays()
        {
            DOTween.Kill(SunRaysTweenId);
            Hide(sunRays);
        }

        private void StopIdle()
        {
            DOTween.Kill(IdleTweenId);
            Hide(idleGlow);
        }

        private void EnsurePool()
        {
            if (didBuild)
                return;

            poolRoot = new GameObject(PoolName).transform;
            var glowMaterial = particleMaterial != null
                ? particleMaterial
                : VictoryProcSprites.DefaultParticle();
            idleGlow = CreateSprite(
                "ScoreIdleGlow",
                VictoryProcSprites.Glow(),
                glowMaterial,
                CardSortingLayerId,
                0
            );
            if (sunRaysSprite != null)
            {
                sunRays = CreateSprite(
                    "ScoreSunRays",
                    sunRaysSprite,
                    sunRaysMaterial,
                    0,
                    1
                );
                sunRays.sortingLayerName = "Default";
            }

            didBuild = true;
        }

        private SpriteRenderer CreateSprite(
            string name,
            Sprite sprite,
            Material material,
            int sortingLayerId,
            int sortingOrder
        )
        {
            var go = new GameObject(name);
            go.transform.SetParent(poolRoot, false);
            go.SetActive(false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            if (material != null)
                renderer.sharedMaterial = material;
            BindSpriteTexture(renderer, sprite);
            return renderer;
        }

        private static void BindSpriteTexture(SpriteRenderer renderer, Sprite sprite)
        {
            if (renderer == null || sprite == null || sprite.texture == null)
                return;

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetTexture(MainTexId, sprite.texture);
            block.SetTexture(BaseMapId, sprite.texture);
            renderer.SetPropertyBlock(block);
        }

        private static void Hide(SpriteRenderer renderer)
        {
            if (renderer == null)
                return;

            renderer.gameObject.SetActive(false);
        }

        private static float SpriteFit(Sprite sprite, float worldSize)
        {
            var width = sprite != null ? Mathf.Max(0.01f, sprite.bounds.size.x) : 1f;
            return Mathf.Max(0.001f, worldSize) / width;
        }
    }
}
