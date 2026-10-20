using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Tiny pink star spray, same art language as the streak payoff burst.
    /// World sprites, additive, pooled.
    /// </summary>
    public sealed class CrestStarBurst
    {
        private const string AdditiveShaderName = "Solitaire/SpriteAdditive";
        private const string PoolName = "CrestStarBurstPool";
        private const int PoolSize = 32;
        private const int BurstCount = 5;
        private const float TargetHeight = 0.18f;
        private const float DistanceMin = 0.22f;
        private const float DistanceMax = 0.4f;
        private const float Life = 0.34f;
        private const string TweenIdPrefix = "CrestStar";

        private static readonly Color Pink = new Color(1f, 0.42f, 0.82f, 0.72f);

        private static Material sharedMaterial;
        private static bool didWarnMissingShader;

        private readonly Sprite sprite;
        private readonly float restScale;
        private readonly Transform poolRoot;
        private readonly List<SpriteRenderer> pool = new List<SpriteRenderer>(PoolSize);

        public CrestStarBurst(Sprite starSprite)
        {
            sprite = starSprite;
            restScale = ComputeScale(starSprite);

            var rootGo = new GameObject(PoolName);
            poolRoot = rootGo.transform;

            var material = GetAdditiveMaterial();
            for (var i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("CrestStar_Pooled_" + i);
                go.transform.SetParent(poolRoot, false);
                go.SetActive(false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = starSprite;
                renderer.color = Pink;
                if (material != null)
                    renderer.sharedMaterial = material;
                renderer.sortingOrder = 6;
                pool.Add(renderer);
            }
        }

        public void Play(Vector3 origin, int sortingOrder)
        {
            if (sprite == null)
                return;

            for (var i = 0; i < BurstCount; i++)
            {
                var renderer = Rent();
                if (renderer == null)
                    return;

                var t = renderer.transform;
                var id = TweenIdPrefix + renderer.GetInstanceID();
                DOTween.Kill(id);

                var angle = (i / (float)BurstCount) * 360f + Random.Range(-18f, 18f);
                var rad = angle * Mathf.Deg2Rad;
                var dist = Random.Range(DistanceMin, DistanceMax);
                var end = origin + new Vector3(Mathf.Cos(rad) * dist, Mathf.Sin(rad) * dist, 0f);
                end.z = origin.z;

                renderer.sortingOrder = sortingOrder;
                renderer.sprite = sprite;
                renderer.color = Pink;
                renderer.enabled = true;

                t.position = origin;
                t.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                t.localScale = Vector3.one * restScale * Random.Range(0.75f, 1f);
                t.gameObject.SetActive(true);

                var spin = Random.Range(70f, 140f) * (Random.value < 0.5f ? -1f : 1f);
                var fly = DOTween.Sequence().SetId(id).SetTarget(renderer);
                fly.Append(t.DOMove(end, Life).SetEase(Ease.OutCubic));
                fly.Join(t.DOScale(restScale * 0.12f, Life).SetEase(Ease.InQuad));
                fly.Join(
                    t.DORotate(new Vector3(0f, 0f, spin), Life, RotateMode.LocalAxisAdd)
                        .SetEase(Ease.Linear)
                );
                fly.Join(renderer.DOFade(0f, Life).SetEase(Ease.InQuad));
                fly.OnComplete(() => Return(renderer));
            }
        }

        public void StopAll()
        {
            for (var i = 0; i < pool.Count; i++)
            {
                var renderer = pool[i];
                if (renderer == null)
                    continue;
                DOTween.Kill(TweenIdPrefix + renderer.GetInstanceID());
                Return(renderer);
            }
        }

        public void Dispose()
        {
            StopAll();
            pool.Clear();
            if (poolRoot != null)
                Object.Destroy(poolRoot.gameObject);
        }

        private SpriteRenderer Rent()
        {
            for (var i = 0; i < pool.Count; i++)
            {
                var renderer = pool[i];
                if (renderer != null && !renderer.gameObject.activeSelf)
                    return renderer;
            }

            return pool.Count > 0 ? pool[0] : null;
        }

        private static void Return(SpriteRenderer renderer)
        {
            if (renderer == null)
                return;

            renderer.enabled = false;
            renderer.gameObject.SetActive(false);
        }

        private static float ComputeScale(Sprite star)
        {
            if (star == null)
                return 1f;

            var height = star.bounds.size.y;
            if (height < 0.001f)
                return 1f;

            return TargetHeight / height;
        }

        private static Material GetAdditiveMaterial()
        {
            if (sharedMaterial != null)
                return sharedMaterial;

            var shader = Shader.Find(AdditiveShaderName);
            if (shader == null)
            {
                if (!didWarnMissingShader)
                {
                    Debug.LogWarning(
                        "[CrestStarBurst] Shader Solitaire/SpriteAdditive not found."
                    );
                    didWarnMissingShader = true;
                }

                return null;
            }

            sharedMaterial = new Material(shader) { name = "CrestStarAdditive" };
            return sharedMaterial;
        }
    }
}
