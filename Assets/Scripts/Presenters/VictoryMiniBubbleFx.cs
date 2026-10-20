using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Tiny outside bubbles that peel off the victory sphere on glyph hits.
    /// Reuses the live Sphere mesh / NewBubble / gloss / interior materials.
    /// Unparented so the gummy squash does not drag them.
    /// </summary>
    public sealed class VictoryMiniBubbleFx
    {
        private const string PoolName = "VictoryMiniBubblePool";
        private const string TweenIdPrefix = "VictoryMiniBubble";
        private const int PoolSize = 20;
        private const float PopIn = 0.05f;
        private const float FadeHold = 0.45f;
        private const float StartScaleMul = 0.88f;

        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int BubbleAlphaId = Shader.PropertyToID("_BubbleAlpha");
        private static readonly int CoreAlphaId = Shader.PropertyToID("_CoreAlpha");
        private static readonly int RimAlphaId = Shader.PropertyToID("_RimAlpha");

        private const float LastHitExtraSizeMul = 1.08f;

        private Transform poolRoot;
        private readonly Slot[] pool = new Slot[PoolSize];
        private int poolIndex;
        private bool didBuild;

        public readonly struct Tune
        {
            public readonly float SizeOnDiameter;
            public readonly Vector2 SpawnOffset;
            public readonly float SurfaceSpread;
            public readonly float Lifetime;
            public readonly float LifetimeSpread;
            public readonly float SizeVariation;
            public readonly float SpawnDelay;
            public readonly float RiseSpeed;
            public readonly float MoveSpeed;
            public readonly float Wobble;
            public readonly float Pulse;
            public readonly float Spin;
            public readonly float Growth;

            public Tune(
                float sizeOnDiameter,
                Vector2 spawnOffset,
                float surfaceSpread,
                float lifetime,
                float lifetimeSpread,
                float sizeVariation,
                float spawnDelay,
                float riseSpeed,
                float moveSpeed,
                float wobble,
                float pulse,
                float spin,
                float growth
            )
            {
                SizeOnDiameter = sizeOnDiameter;
                SpawnOffset = spawnOffset;
                SurfaceSpread = surfaceSpread;
                Lifetime = lifetime;
                LifetimeSpread = lifetimeSpread;
                SizeVariation = sizeVariation;
                SpawnDelay = spawnDelay;
                RiseSpeed = riseSpeed;
                MoveSpeed = moveSpeed;
                Wobble = wobble;
                Pulse = pulse;
                Spin = spin;
                Growth = growth;
            }
        }

        public void Warmup(Transform sphere, MeshRenderer bodyRenderer)
        {
            if (sphere == null)
                return;

            EnsurePool(sphere, bodyRenderer);
        }

        public void Play(
            Transform sphere,
            MeshRenderer bodyRenderer,
            int sortingOrder,
            Tune tune,
            int hitIndex,
            bool lastHit
        )
        {
            if (sphere == null)
                return;

            EnsurePool(sphere, bodyRenderer);
            if (!didBuild)
                return;

            var onBeat = (hitIndex & 1) == 1;
            if (!onBeat && !lastHit)
                return;

            SpawnOne(sphere, sortingOrder, tune, 1f);
            if (!lastHit)
                return;

            SpawnOne(sphere, sortingOrder, tune, LastHitExtraSizeMul);
        }

        public void StopAll()
        {
            for (var i = 0; i < pool.Length; i++)
                Return(pool[i]);
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
        }

        private void SpawnOne(
            Transform sphere,
            int sortingOrder,
            Tune tune,
            float sizeMul
        )
        {
            var slot = Rent();
            if (slot?.Root == null)
                return;

            var heroDiameter = Mathf.Max(0.01f, sphere.lossyScale.x);
            var variation = Mathf.Clamp01(tune.SizeVariation);
            if (variation > 0.001f)
                sizeMul *= 1f + Random.Range(-variation, variation);

            var sizeFrac = Mathf.Clamp(tune.SizeOnDiameter, 0.02f, 0.22f) * Mathf.Max(0.5f, sizeMul);
            var miniDiameter = heroDiameter * sizeFrac;
            var baseScale = Vector3.one * miniDiameter;

            var spawn = SurfaceSpawn(sphere, heroDiameter, miniDiameter, tune);
            var spread = Mathf.Clamp01(tune.LifetimeSpread);
            var duration = tune.Lifetime;
            if (spread > 0.001f)
                duration *= 1f + Random.Range(-spread, spread);
            duration = Mathf.Max(0.2f, duration);

            var riseSpeed = Mathf.Max(0f, tune.RiseSpeed) * (1.08f - sizeFrac * 0.35f);
            var rise = riseSpeed * duration;
            var drift = Mathf.Max(0f, tune.MoveSpeed) * duration;
            var driftSign = Random.value < 0.5f ? -1f : 1f;
            var driftNoise = Random.Range(0.35f, 1f);
            var end = spawn + new Vector3(drift * driftSign * driftNoise, rise, 0f);
            end.z = spawn.z;

            var wobbleAmp = miniDiameter * Mathf.Clamp(tune.Wobble, 0f, 0.25f);
            var pulseAmp = Mathf.Clamp(tune.Pulse, 0f, 0.12f);
            var growth = Mathf.Clamp(tune.Growth, 0f, 0.15f);
            var spin = Random.value < 0.5f ? -1f : 1f;
            var spinSpeed = tune.Spin * spin;
            var wobblePhaseX = Random.Range(0f, 6.28f);
            var wobblePhaseY = Random.Range(0f, 6.28f);
            var wobbleFreq = Random.Range(2.6f, 4.2f);
            var pulsePhase = Random.Range(0f, 6.28f);
            var pulseFreq = Random.Range(4.5f, 7f);
            var perlinSeed = Random.Range(0f, 100f);

            ApplySorting(slot, sortingOrder);
            ApplyFade(slot, 1f);

            DOTween.Kill(slot.TweenId);
            slot.Root.position = spawn;
            slot.Root.rotation = Quaternion.identity;
            slot.Root.localScale = baseScale * StartScaleMul;
            slot.Root.gameObject.SetActive(true);

            var fade = 1f;
            var elapsed = 0f;
            var spawnDelay = tune.SpawnDelay > 0.001f ? Random.Range(0f, tune.SpawnDelay) : 0f;
            var fadeAt = spawnDelay + duration * FadeHold;
            var fadeLenMotion = Mathf.Max(0.08f, duration * (1f - FadeHold));

            var seq = DOTween.Sequence().SetId(slot.TweenId);
            seq.OnComplete(() =>
            {
                ApplyFade(slot, 0f);
                if (slot.Root != null)
                    slot.Root.gameObject.SetActive(false);
            });

            seq.Insert(
                spawnDelay,
                DOTween
                    .To(() => elapsed, value => elapsed = value, duration, duration)
                    .SetEase(Ease.Linear)
                    .OnUpdate(() =>
                    {
                        if (slot.Root == null)
                            return;

                        var riseT = DOVirtual.EasedValue(
                            0f,
                            1f,
                            Mathf.Clamp01(elapsed / duration),
                            Ease.OutCubic
                        );
                        var pos = Vector3.LerpUnclamped(spawn, end, riseT);
                        var wobbleT = Time.time;
                        pos.x +=
                            Mathf.Sin(wobbleT * wobbleFreq + wobblePhaseX) * wobbleAmp;
                        pos.y +=
                            Mathf.Sin(wobbleT * (wobbleFreq * 0.82f) + wobblePhaseY)
                            * (wobbleAmp * 0.55f);
                        pos.x +=
                            (Mathf.PerlinNoise(perlinSeed, wobbleT * 0.85f) - 0.5f)
                            * wobbleAmp
                            * 0.6f;
                        slot.Root.position = pos;

                        var popT = Mathf.Clamp01(elapsed / PopIn);
                        var popMul = DOVirtual.EasedValue(
                            StartScaleMul,
                            1f,
                            popT,
                            Ease.OutQuad
                        );
                        var pulse = pulseAmp > 0.001f
                            ? 1f + pulseAmp * Mathf.Sin(wobbleT * pulseFreq + pulsePhase)
                            : 1f;
                        var grow = 1f + growth * riseT;
                        slot.Root.localScale = baseScale * popMul * pulse * grow;

                        if (Mathf.Abs(spinSpeed) > 0.01f)
                        {
                            slot.Root.Rotate(
                                0f,
                                0f,
                                spinSpeed * Time.deltaTime,
                                Space.Self
                            );
                        }
                    })
            );
            seq.Insert(
                fadeAt,
                DOTween
                    .To(() => fade, value =>
                    {
                        fade = value;
                        ApplyFade(slot, value);
                    }, 0f, fadeLenMotion)
                    .SetEase(Ease.InQuad)
            );
        }

        private static Vector3 SurfaceSpawn(
            Transform sphere,
            float heroDiameter,
            float miniDiameter,
            Tune tune
        )
        {
            var center = sphere.position;
            var heroRadius = heroDiameter * 0.5f;
            var miniRadius = miniDiameter * 0.5f;

            var cam = Camera.main;
            var toCam =
                cam != null ? (cam.transform.position - center) : Vector3.back;
            if (toCam.sqrMagnitude < 0.0001f)
                toCam = Vector3.back;
            toCam.Normalize();

            var outward = RandomFrontDir(toCam, tune.SurfaceSpread);
            var spawn = center + outward * (heroRadius + miniRadius * 0.9f);
            spawn += toCam * (miniRadius * 0.35f);
            spawn.x += tune.SpawnOffset.x;
            spawn.y += tune.SpawnOffset.y;
            return spawn;
        }

        private static Vector3 RandomFrontDir(Vector3 toCam, float spread)
        {
            var cover = Mathf.Clamp01(spread);
            var dir = Random.onUnitSphere;
            if (Vector3.Dot(dir, toCam) < 0f)
                dir = -dir;

            dir = Vector3.Slerp(toCam, dir, cover).normalized;
            dir = Vector3.Slerp(dir, Vector3.up, 0.28f * cover).normalized;
            if (Vector3.Dot(dir, toCam) < 0.12f)
                dir = Vector3.Slerp(dir, toCam, 0.55f).normalized;

            return dir;
        }

        private void EnsurePool(Transform sphere, MeshRenderer bodyRenderer)
        {
            if (didBuild)
                return;

            var body = bodyRenderer != null ? bodyRenderer : sphere.GetComponent<MeshRenderer>();
            var bodyFilter = sphere.GetComponent<MeshFilter>();
            if (body == null || bodyFilter == null || bodyFilter.sharedMesh == null)
                return;

            poolRoot = new GameObject(PoolName).transform;
            for (var i = 0; i < PoolSize; i++)
                pool[i] = CreateSlot(i, sphere, body, bodyFilter);

            didBuild = true;
        }

        private Slot CreateSlot(
            int index,
            Transform sphere,
            MeshRenderer body,
            MeshFilter bodyFilter
        )
        {
            var go = new GameObject("MiniBubble_" + index);
            go.transform.SetParent(poolRoot, false);
            go.SetActive(false);

            var group = go.AddComponent<SortingGroup>();
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = bodyFilter.sharedMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            CopyRenderer(body, renderer);

            CloneMeshChild(sphere.Find("InteriorShell"), go.transform);
            CloneMeshChild(sphere.Find("GlossLayer"), go.transform);

            var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
            return new Slot
            {
                Root = go.transform,
                Group = group,
                Renderers = renderers,
                Layers = CaptureLayers(renderers),
                TweenId = TweenIdPrefix + index
            };
        }

        private static void CloneMeshChild(Transform source, Transform parent)
        {
            if (source == null || parent == null)
                return;

            var srcFilter = source.GetComponent<MeshFilter>();
            var srcRenderer = source.GetComponent<MeshRenderer>();
            if (srcFilter == null || srcRenderer == null)
                return;

            var go = new GameObject(source.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = source.localPosition;
            go.transform.localRotation = source.localRotation;
            go.transform.localScale = source.localScale;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = srcFilter.sharedMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            CopyRenderer(srcRenderer, renderer);
        }

        private static void CopyRenderer(MeshRenderer source, MeshRenderer dest)
        {
            dest.sharedMaterials = source.sharedMaterials;
            dest.shadowCastingMode = ShadowCastingMode.Off;
            dest.receiveShadows = false;
            dest.lightProbeUsage = LightProbeUsage.Off;
            dest.reflectionProbeUsage = ReflectionProbeUsage.Off;
            dest.allowOcclusionWhenDynamic = false;
            dest.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            dest.sortingLayerID = source.sortingLayerID;
            dest.sortingOrder = source.sortingOrder;
        }

        private static LayerFade[] CaptureLayers(MeshRenderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0)
                return System.Array.Empty<LayerFade>();

            var layers = new LayerFade[renderers.Length];
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                var layer = new LayerFade
                {
                    Renderer = renderer,
                    Block = new MaterialPropertyBlock()
                };

                var mat = renderer != null ? renderer.sharedMaterial : null;
                if (mat != null)
                {
                    layer.HasFade = mat.HasProperty(FadeId);
                    layer.HasBubble = mat.HasProperty(BubbleAlphaId);
                    layer.HasInterior = mat.HasProperty(AlphaId) && !layer.HasBubble;
                    if (layer.HasBubble)
                    {
                        layer.BubbleAlpha = mat.GetFloat(BubbleAlphaId);
                        if (mat.HasProperty(CoreAlphaId))
                            layer.CoreAlpha = mat.GetFloat(CoreAlphaId);
                        if (mat.HasProperty(RimAlphaId))
                            layer.RimAlpha = mat.GetFloat(RimAlphaId);
                    }

                    if (layer.HasInterior)
                        layer.InteriorAlpha = mat.GetFloat(AlphaId);
                }

                layers[i] = layer;
            }

            return layers;
        }

        private static void ApplyFade(Slot slot, float fade)
        {
            if (slot?.Layers == null)
                return;

            var t = Mathf.Clamp01(fade);
            for (var i = 0; i < slot.Layers.Length; i++)
            {
                var layer = slot.Layers[i];
                if (layer?.Renderer == null)
                    continue;

                var block = layer.Block;
                block.Clear();
                if (layer.HasFade)
                    block.SetFloat(FadeId, t);
                if (layer.HasBubble)
                {
                    block.SetFloat(BubbleAlphaId, layer.BubbleAlpha * t);
                    block.SetFloat(CoreAlphaId, layer.CoreAlpha * t);
                    block.SetFloat(RimAlphaId, layer.RimAlpha * t);
                }

                if (layer.HasInterior)
                    block.SetFloat(AlphaId, layer.InteriorAlpha * t);

                layer.Renderer.SetPropertyBlock(block);
            }
        }

        private static void ApplySorting(Slot slot, int sortingOrder)
        {
            if (slot.Group != null)
                slot.Group.sortingOrder = sortingOrder;

            var renderers = slot.Renderers;
            if (renderers == null)
                return;

            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].sortingOrder = sortingOrder + i;
            }
        }

        private Slot Rent()
        {
            for (var i = 0; i < pool.Length; i++)
            {
                var idx = (poolIndex + i) % pool.Length;
                var slot = pool[idx];
                if (slot?.Root != null && !slot.Root.gameObject.activeSelf)
                {
                    poolIndex = (idx + 1) % pool.Length;
                    return slot;
                }
            }

            var stolen = pool[poolIndex];
            poolIndex = (poolIndex + 1) % pool.Length;
            Return(stolen);
            return stolen;
        }

        private static void Return(Slot slot)
        {
            if (slot?.Root == null)
                return;

            DOTween.Kill(slot.TweenId);
            slot.Root.gameObject.SetActive(false);
        }

        private sealed class Slot
        {
            public Transform Root;
            public SortingGroup Group;
            public MeshRenderer[] Renderers;
            public LayerFade[] Layers;
            public string TweenId;
        }

        private sealed class LayerFade
        {
            public MeshRenderer Renderer;
            public MaterialPropertyBlock Block;
            public bool HasFade;
            public bool HasBubble;
            public bool HasInterior;
            public float BubbleAlpha;
            public float CoreAlpha;
            public float RimAlpha;
            public float InteriorAlpha;
        }
    }
}
