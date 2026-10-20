using DG.Tweening;
using Solitaire.Helpers;
using UnityEngine;
using UnityEngine.Rendering;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Pop climax: 2-frame overexposure, soap-film ring, then radial droplets.
    /// Unparented so the bursting sphere does not drag the debris.
    /// </summary>
    public sealed class VictoryBurstFx
    {
        private const string PoolName = "VictoryBurstFxPool";
        private const string FlashTweenId = "VictoryBurstFlash";
        private const string HaloTweenId = "VictoryBurstHalo";
        private const string RingTweenId = "VictoryBurstRing";
        private const string DropletTweenIdPrefix = "VictoryBurstDroplet";
        private const string ShardTweenIdPrefix = "VictoryBurstShard";
        private const int CardSortingLayerId = 34509423;
        private const int DropletCount = 18;
        private const int ShardCount = 5;
        private const float SpikeHold = 0.033f;
        private const float SpikeFade = 0.04f;
        private const float HaloDelay = 0.04f;
        private const float HaloDuration = 0.1f;
        private const float RingDelay = 0.033f;
        private const float RingDuration = 0.15f;
        private const float RingEndMul = 2.5f;
        private const float DropletDelay = 0.04f;
        private const float ShardLife = 0.25f;

        private static readonly Color SpikeColor = new Color(2.5f, 2.9f, 3.1f, 1f);
        private static readonly Color HaloColor = new Color(1.15f, 1.85f, 2.15f, 0.85f);
        private static readonly Color RingColor = new Color(1.72f, 1.95f, 2.45f, 1f);
        private static readonly Color RingPink = new Color(2.05f, 0.72f, 1.62f, 1f);
        private static readonly Color DropletCyan = new Color(1.55f, 2.15f, 2.4f, 1f);
        private static readonly Color DropletPink = new Color(2.1f, 0.85f, 1.7f, 1f);
        private static readonly Color SuitRedFilm = new Color(1.45f, 0.32f, 0.62f, 1f);
        private static readonly Color SuitBlackFilm = new Color(0.42f, 0.58f, 1.18f, 1f);

        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int FilmColorId = Shader.PropertyToID("_FilmColor");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        private static Mesh sharedShardMesh;

        private Transform poolRoot;
        private SpriteRenderer spike;
        private SpriteRenderer halo;
        private SpriteRenderer ring;
        private readonly SpriteRenderer[] droplets = new SpriteRenderer[DropletCount];
        private readonly Shard[] shards = new Shard[ShardCount];
        private Color glossFilmRest = new Color(1.05f, 0.35f, 0.82f, 1f);
        private Material particleMaterial;
        private bool didBuild;
        private bool didBuildShards;

        public void Warmup(Transform sphere = null, Material defaultParticle = null)
        {
            if (defaultParticle != null)
                particleMaterial = defaultParticle;
            EnsurePool();
            EnsureShards(sphere);
        }

        public void Play(Vector3 worldCenter, float bubbleDiameter, int sortingOrder, Transform sphere = null)
        {
            EnsurePool();
            EnsureShards(sphere);
            if (!didBuild)
                return;

            StopAll();
            var diameter = Mathf.Max(0.2f, bubbleDiameter);
            poolRoot.position = worldCenter;

            PlaySpike(diameter, sortingOrder + 47);
            PlayHalo(diameter, sortingOrder + 46);
            PlayRing(diameter, sortingOrder + 46);
            PlayDroplets(diameter, sortingOrder + 45);
            PlayShards(diameter, sortingOrder + 44);
        }

        public void StopAll()
        {
            DOTween.Kill(FlashTweenId);
            DOTween.Kill(HaloTweenId);
            DOTween.Kill(RingTweenId);
            Hide(spike);
            Hide(halo);
            Hide(ring);
            for (var i = 0; i < droplets.Length; i++)
            {
                DOTween.Kill(DropletTweenIdPrefix + i);
                Hide(droplets[i]);
            }

            for (var i = 0; i < shards.Length; i++)
            {
                DOTween.Kill(ShardTweenIdPrefix + i);
                HideShard(shards[i]);
            }
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

            spike = null;
            halo = null;
            ring = null;
            for (var i = 0; i < droplets.Length; i++)
                droplets[i] = null;
            for (var i = 0; i < shards.Length; i++)
                shards[i] = null;
            didBuildShards = false;
        }

        private void PlaySpike(float diameter, int sortingOrder)
        {
            if (spike == null)
                return;

            var fit = SpriteFit(spike.sprite, diameter * 1.12f);
            spike.sortingOrder = sortingOrder;
            spike.color = SpikeColor;
            spike.transform.localPosition = Vector3.zero;
            spike.transform.localScale = Vector3.one * fit;
            spike.gameObject.SetActive(true);

            var seq = DOTween.Sequence().SetId(FlashTweenId);
            seq.AppendInterval(SpikeHold);
            seq.Append(spike.DOFade(0f, SpikeFade).SetEase(Ease.InQuad));
            seq.OnComplete(() => Hide(spike));
        }

        private void PlayHalo(float diameter, int sortingOrder)
        {
            if (halo == null)
                return;

            var startFit = SpriteFit(halo.sprite, diameter * 1.05f);
            var endFit = SpriteFit(halo.sprite, diameter * 1.7f);
            halo.sortingOrder = sortingOrder;
            halo.color = new Color(HaloColor.r, HaloColor.g, HaloColor.b, 0f);
            halo.transform.localPosition = Vector3.zero;
            halo.transform.localScale = Vector3.one * startFit;
            halo.gameObject.SetActive(true);

            var seq = DOTween.Sequence().SetId(HaloTweenId);
            seq.AppendInterval(HaloDelay);
            seq.AppendCallback(() =>
            {
                if (halo != null)
                    halo.color = HaloColor;
            });
            seq.Append(halo.transform.DOScale(endFit, HaloDuration).SetEase(Ease.OutQuad));
            seq.Join(halo.DOFade(0f, HaloDuration).SetEase(Ease.InQuad));
            seq.OnComplete(() => Hide(halo));
        }

        private void PlayRing(float diameter, int sortingOrder)
        {
            if (ring == null)
                return;

            var startFit = SpriteFit(ring.sprite, diameter);
            var endFit = startFit * RingEndMul;
            var tint = Color.Lerp(RingColor, RingPink, 0.22f);
            ring.sortingOrder = sortingOrder;
            ring.color = new Color(tint.r, tint.g, tint.b, 0f);
            ring.transform.localPosition = Vector3.zero;
            ring.transform.localScale = Vector3.one * startFit;
            ring.gameObject.SetActive(true);

            var seq = DOTween.Sequence().SetId(RingTweenId);
            seq.AppendInterval(RingDelay);
            seq.AppendCallback(() =>
            {
                if (ring != null)
                    ring.color = tint;
            });
            seq.Append(ring.transform.DOScale(endFit, RingDuration).SetEase(Ease.OutQuad));
            seq.Join(ring.DOFade(0f, RingDuration).SetEase(Ease.InQuad));
            seq.OnComplete(() => Hide(ring));
        }

        private void PlayDroplets(float diameter, int sortingOrder)
        {
            var sprite = VictoryProcSprites.Speck();
            var radius = diameter * 0.5f;
            for (var i = 0; i < droplets.Length; i++)
            {
                var drop = droplets[i];
                if (drop == null)
                    continue;

                var angle = (i / (float)droplets.Length) * Mathf.PI * 2f;
                angle += Random.Range(-0.12f, 0.12f);
                var dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) + 0.32f, 0f).normalized;
                var start = dir * (radius * Random.Range(0.88f, 1.02f));
                var travel = radius * Random.Range(0.55f, 1.15f);
                var end = start + dir * travel;
                var life = Random.Range(0.2f, 0.34f);
                var size = diameter * Random.Range(0.04f, 0.085f);
                var fit = SpriteFit(sprite, size);
                var pink = (i & 3) == 0;
                var color = pink ? DropletPink : DropletCyan;

                drop.sprite = sprite;
                drop.sortingOrder = sortingOrder;
                drop.color = new Color(color.r, color.g, color.b, 0f);
                drop.transform.localPosition = start;
                drop.transform.localScale = Vector3.one * fit;
                drop.gameObject.SetActive(true);

                var id = DropletTweenIdPrefix + i;
                var seq = DOTween.Sequence().SetId(id);
                seq.AppendInterval(DropletDelay);
                seq.AppendCallback(() =>
                {
                    if (drop != null)
                        drop.color = color;
                });
                seq.Append(drop.transform.DOLocalMove(end, life).SetEase(Ease.OutQuad));
                seq.Join(drop.transform.DOScale(fit * 0.12f, life).SetEase(Ease.InQuad));
                seq.Join(drop.DOFade(0f, life).SetEase(Ease.InQuad));
                seq.OnComplete(() => Hide(drop));
            }
        }

        private void EnsurePool()
        {
            if (didBuild)
                return;

            poolRoot = new GameObject(PoolName).transform;
            var material = particleMaterial != null
                ? particleMaterial
                : VictoryProcSprites.DefaultParticle();
            spike = CreateSprite("BurstSpike", VictoryProcSprites.Glow(), material);
            halo = CreateSprite("BurstHalo", VictoryProcSprites.Glow(), material);
            ring = CreateSprite("BurstRing", SoftRingSprite.Get(), material);
            var speck = VictoryProcSprites.Speck();
            for (var i = 0; i < droplets.Length; i++)
                droplets[i] = CreateSprite("BurstDroplet_" + i, speck, material);

            didBuild = true;
        }

        private SpriteRenderer CreateSprite(string name, Sprite sprite, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(poolRoot, false);
            go.SetActive(false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerID = CardSortingLayerId;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
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

        private void PlayShards(float diameter, int sortingOrder)
        {
            if (!didBuildShards)
                return;

            var radius = diameter * 0.5f;
            var cam = Camera.main;
            var toCam = cam != null
                ? (cam.transform.position - poolRoot.position).normalized
                : Vector3.back;
            if (toCam.sqrMagnitude < 0.0001f)
                toCam = Vector3.back;

            for (var i = 0; i < shards.Length; i++)
            {
                var shard = shards[i];
                if (shard?.Root == null)
                    continue;

                var angle = (i / (float)shards.Length) * 360f + Random.Range(-14f, 14f);
                var radial = Quaternion.AngleAxis(angle, Vector3.forward) * Vector3.up;
                var outward = Vector3.Slerp(radial, toCam, 0.28f).normalized;
                var start = outward * (radius * Random.Range(0.86f, 0.98f));
                var travel = radius * Random.Range(0.85f, 1.35f);
                var end = start + Vector3.Slerp(outward, toCam, 0.2f).normalized * travel;
                var size = diameter * Random.Range(0.48f, 0.7f);
                var tumble = new Vector3(
                    Random.Range(-26f, 26f),
                    Random.Range(-18f, 18f),
                    Random.Range(-34f, 34f)
                );

                shard.Root.localPosition = start;
                shard.Root.localRotation = Quaternion.FromToRotation(Vector3.back, outward);
                shard.Root.localScale = Vector3.one * size;
                if (shard.Group != null)
                    shard.Group.sortingOrder = sortingOrder;
                shard.Renderer.sortingOrder = sortingOrder;
                ApplyShardLook(shard, 1f, i);
                shard.Root.gameObject.SetActive(true);

                var fade = 1f;
                var seq = DOTween.Sequence().SetId(shard.TweenId);
                seq.Append(shard.Root.DOLocalMove(end, ShardLife).SetEase(Ease.OutQuad));
                seq.Join(shard.Root.DOScale(size * 0.82f, ShardLife).SetEase(Ease.InQuad));
                seq.Join(
                    shard.Root.DOLocalRotate(tumble, ShardLife, RotateMode.LocalAxisAdd)
                        .SetEase(Ease.OutQuad)
                );
                seq.Join(
                    DOTween
                        .To(
                            () => fade,
                            value =>
                            {
                                fade = value;
                                ApplyShardLook(shard, fade, i);
                            },
                            0f,
                            ShardLife
                        )
                        .SetEase(Ease.InQuad)
                );
                seq.OnComplete(() => HideShard(shard));
            }
        }

        private void ApplyShardLook(Shard shard, float fade, int index)
        {
            if (shard?.Renderer == null)
                return;

            var t = Mathf.Clamp01(fade);
            var block = shard.Block;
            block.Clear();
            if (shard.HasFade)
                block.SetFloat(FadeId, t);
            if (shard.HasFilm)
            {
                var tint = index == 0
                    ? SuitRedFilm
                    : index == 2
                        ? SuitBlackFilm
                        : glossFilmRest;
                block.SetColor(FilmColorId, tint);
            }

            shard.Renderer.SetPropertyBlock(block);
        }

        private static void HideShard(Shard shard)
        {
            if (shard?.Root == null)
                return;

            shard.Root.gameObject.SetActive(false);
        }

        private void EnsureShards(Transform sphere)
        {
            if (didBuildShards || sphere == null || poolRoot == null)
                return;

            var gloss = sphere.Find("GlossLayer");
            var glossRenderer = gloss != null ? gloss.GetComponent<MeshRenderer>() : null;
            if (glossRenderer == null || glossRenderer.sharedMaterial == null)
                return;

            var mesh = GetShardMesh();
            var material = glossRenderer.sharedMaterial;
            if (material.HasProperty(FilmColorId))
                glossFilmRest = material.GetColor(FilmColorId);

            for (var i = 0; i < shards.Length; i++)
            {
                var go = new GameObject("BurstShard_" + i);
                go.transform.SetParent(poolRoot, false);
                go.SetActive(false);

                var filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.sortingLayerID = CardSortingLayerId;
                renderer.sortingOrder = glossRenderer.sortingOrder;

                var group = go.AddComponent<SortingGroup>();
                group.sortingLayerID = CardSortingLayerId;
                group.sortingOrder = glossRenderer.sortingOrder;

                shards[i] = new Shard
                {
                    Root = go.transform,
                    Renderer = renderer,
                    Group = group,
                    Block = new MaterialPropertyBlock(),
                    TweenId = ShardTweenIdPrefix + i,
                    HasFade = material.HasProperty(FadeId),
                    HasFilm = material.HasProperty(FilmColorId)
                };
            }

            didBuildShards = true;
        }

        private static Mesh GetShardMesh()
        {
            if (sharedShardMesh != null)
                return sharedShardMesh;

            const int lonSeg = 8;
            const int latSeg = 10;
            const float radius = 0.5f;
            var lonMin = -28f * Mathf.Deg2Rad;
            var lonMax = 28f * Mathf.Deg2Rad;
            var latMin = -42f * Mathf.Deg2Rad;
            var latMax = 42f * Mathf.Deg2Rad;
            var lonCount = lonSeg + 1;
            var latCount = latSeg + 1;
            var vertCount = lonCount * latCount;
            var vertices = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var triangles = new int[lonSeg * latSeg * 6];

            for (var y = 0; y < latCount; y++)
            {
                var lat = Mathf.Lerp(latMin, latMax, y / (float)latSeg);
                var cy = Mathf.Cos(lat);
                var sy = Mathf.Sin(lat);
                for (var x = 0; x < lonCount; x++)
                {
                    var lon = Mathf.Lerp(lonMin, lonMax, x / (float)lonSeg);
                    var index = y * lonCount + x;
                    var p = new Vector3(
                        radius * cy * Mathf.Sin(lon),
                        radius * sy,
                        -radius * cy * Mathf.Cos(lon)
                    );
                    vertices[index] = p;
                    normals[index] = p.normalized;
                    uvs[index] = new Vector2(x / (float)lonSeg, y / (float)latSeg);
                }
            }

            var tri = 0;
            for (var y = 0; y < latSeg; y++)
            {
                for (var x = 0; x < lonSeg; x++)
                {
                    var i0 = y * lonCount + x;
                    var i1 = i0 + 1;
                    var i2 = i0 + lonCount;
                    var i3 = i2 + 1;
                    triangles[tri++] = i0;
                    triangles[tri++] = i2;
                    triangles[tri++] = i1;
                    triangles[tri++] = i1;
                    triangles[tri++] = i2;
                    triangles[tri++] = i3;
                }
            }

            sharedShardMesh = new Mesh
            {
                name = "VictoryBurstShard",
                hideFlags = HideFlags.HideAndDontSave
            };
            sharedShardMesh.vertices = vertices;
            sharedShardMesh.normals = normals;
            sharedShardMesh.uv = uvs;
            sharedShardMesh.triangles = triangles;
            sharedShardMesh.RecalculateBounds();
            return sharedShardMesh;
        }

        private static float SpriteFit(Sprite sprite, float worldSize)
        {
            var width = sprite != null ? Mathf.Max(0.01f, sprite.bounds.size.x) : 1f;
            return Mathf.Max(0.001f, worldSize) / width;
        }

        private sealed class Shard
        {
            public Transform Root;
            public MeshRenderer Renderer;
            public SortingGroup Group;
            public MaterialPropertyBlock Block;
            public string TweenId;
            public bool HasFade;
            public bool HasFilm;
        }
    }
}
