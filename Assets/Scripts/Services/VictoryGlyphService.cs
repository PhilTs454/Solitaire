using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Solitaire.Models;
using Solitaire.Presenters;
using UnityEngine;
using Zenject;

namespace Solitaire.Services
{
    /// <summary>
    /// Suit pips into the 3D victory bubble (coin-burst from Points) or CrestShield sockets.
    /// </summary>
    public sealed class VictoryGlyphService : IVictoryGlyphs, IInitializable, IDisposable
    {
        private const int PoolSize = 24;
        private const int MaxPool = 32;
        private const float PipVolume = 0.22f;
        private const float FinaleGuard = 3f;
        private const string PoolRootName = "VictoryGlyphPool";

        [Inject]
        private readonly IAudioService _audioService;

        [Inject(Optional = true)]
        private readonly CrestShieldPresenter _crest;

        [Inject(Optional = true)]
        private readonly VictoryBubblePresenter _bubble;

        [Inject(Optional = true)]
        private readonly IScoreHud _scoreHud;

        [Inject(Optional = true)]
        private readonly IHudTuck _hudTuck;

        private Transform poolRoot;
        private readonly List<VictorySuitGlyph> pool = new List<VictorySuitGlyph>(PoolSize);
        private readonly List<VictorySuitGlyph> active = new List<VictorySuitGlyph>(PoolSize);
        private bool didInit;

        public void Initialize()
        {
            EnsurePool();
        }

        public void Dispose()
        {
            StopAll();
            pool.Clear();
            didInit = false;
            if (poolRoot != null)
            {
                UnityEngine.Object.Destroy(poolRoot.gameObject);
                poolRoot = null;
            }
        }

        public async UniTask PlayAsync(
            IReadOnlyList<VictoryGlyphLaunch> launches,
            Func<bool> stillWinning
        )
        {
            EnsurePool();
            StopAll();
            if (launches == null || launches.Count == 0)
                return;

            var useBubble = _bubble != null;
            if (useBubble)
            {
                GlyphSparkTrail.SetMoteSprite(_bubble.InteriorMoteSprite);
                GlyphSparkTrail.SetTune(_bubble.SparkSize, _bubble.SparkCount);
            }

            if (!useBubble && _crest == null)
            {
                Debug.LogWarning("[VictoryGlyphs] No VictoryBubble or CrestShield in scene.");
                return;
            }

            if (useBubble)
            {
                _crest?.Hide();
                _bubble.Prepare();
            }
            else
            {
                _bubble?.Hide();
                _crest.Prepare();
            }

            if (!useBubble)
                VictorySequenceAudio.Play(_audioService, Audio.SfxStreakCharge, 0.55f);

            var spawnHold = useBubble ? _bubble.SpawnHold : CrestShieldPresenter.SpawnHold;
            var spawnEnd = Time.realtimeSinceStartup + spawnHold;
            while (Time.realtimeSinceStartup < spawnEnd)
            {
                if (stillWinning != null && !stillWinning())
                {
                    StopAll();
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            var flights = useBubble ? ExpandBubbleBurst(launches) : launches;
            if (useBubble)
                PulsePointsHud();

            var spawned = 0;
            var consumed = 0;
            var total = flights.Count;
            var stagger = useBubble ? _bubble.Stagger : 0.09f;
            if (useBubble)
                _bubble.BeginGlyphScoreCount(total);

            for (var i = 0; i < flights.Count; i++)
            {
                if (stillWinning != null && !stillWinning())
                {
                    StopAll();
                    return;
                }

                var launch = flights[i];
                if (launch.Sprite == null)
                    continue;

                Sprite flySprite;
                var origin = launch.Origin;
                var glyph = Rent();
                if (glyph == null)
                    continue;

                var captured = glyph;
                var suit = launch.Suit;
                var absorbedIndex = spawned + 1;

                if (useBubble)
                {
                    if (
                        !_bubble.TryGetAbsorbFlight(
                            launch.Suit,
                            launch.Sprite,
                            out var sphere,
                            out var localDir,
                            out var pipScale
                        )
                    )
                    {
                        Return(captured);
                        continue;
                    }

                    flySprite = launch.Sprite;
                    var side = UnityEngine.Random.Range(-0.14f, 0.14f);
                    glyph.PlayIntoBubble(
                        flySprite,
                        launch.IsRedSuit,
                        origin,
                        sphere,
                        localDir,
                        pipScale,
                        420 + spawned,
                        _bubble.ApproachDuration,
                        _bubble.AbsorbDuration,
                        _bubble.ArcLift,
                        _bubble.StartScaleMul,
                        side,
                        () =>
                        {
                            _bubble.OnGlyphSurfaceHit(captured.transform.position);
                            if (absorbedIndex == 1 || absorbedIndex == total || absorbedIndex % 4 == 0)
                                VictorySequenceAudio.Play(_audioService, Audio.SfxPlace, 0.7f);
                        },
                        () =>
                        {
                            consumed++;
                            _bubble.OnGlyphConsumed(
                                captured.transform.position,
                                consumed,
                                total
                            );
                            Return(captured);
                        }
                    );
                }
                else
                {
                    if (!_crest.TryGetSocket(launch.Suit, out var slot, out var landScale, out var socketSprite))
                    {
                        Return(captured);
                        continue;
                    }

                    flySprite = socketSprite != null ? socketSprite : launch.Sprite;
                    glyph.Play(
                        flySprite,
                        launch.IsRedSuit,
                        origin,
                        slot,
                        landScale,
                        420 + spawned,
                        () =>
                        {
                            _crest.Fill(suit, flySprite);
                            VictorySequenceAudio.Play(_audioService, Audio.SfxPlace, 0.62f);
                            Return(captured);
                        }
                    );
                }

                VictorySequenceAudio.Play(_audioService, Audio.SfxStreakPip, PipVolume);
                spawned++;
                if (stagger > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(stagger));
            }

            if (spawned == 0)
                return;

            if (stillWinning != null && !stillWinning())
            {
                StopAll();
                return;
            }

            var flight = useBubble ? _bubble.FlightDuration : VictorySuitGlyph.CrestDuration;
            var end = Time.realtimeSinceStartup + flight + 0.4f;
            while (Time.realtimeSinceStartup < end && (!useBubble || consumed < spawned))
            {
                if (stillWinning != null && !stillWinning())
                {
                    StopAll();
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (!useBubble)
                return;

            _bubble.CompleteGlyphScoreCount();
            if (_hudTuck != null)
                _hudTuck.TuckAsync().Forget();
            _bubble.NotifyAllGlyphsConsumed();
            await WaitForFinale(stillWinning);
        }

        /// <summary>
        /// Holds the sequence open through charge, burst and score slam so the CTA
        /// can never land on top of the climax.
        /// </summary>
        private async UniTask WaitForFinale(Func<bool> stillWinning)
        {
            var guard = Time.realtimeSinceStartup + FinaleGuard;
            while (!_bubble.FinaleComplete)
            {
                if (stillWinning != null && !stillWinning())
                {
                    StopAll();
                    return;
                }

                if (!_bubble.RequiresTapToPop && Time.realtimeSinceStartup > guard)
                    return;

                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        public void StopAll()
        {
            for (var i = active.Count - 1; i >= 0; i--)
                Return(active[i]);
            _crest?.Hide();
            _bubble?.Hide();
        }

        private IReadOnlyList<VictoryGlyphLaunch> ExpandBubbleBurst(
            IReadOnlyList<VictoryGlyphLaunch> source
        )
        {
            var count = _bubble != null ? _bubble.BurstCount : source.Count;
            if (count <= 0 || source.Count == 0)
                return source;

            var z = _bubble != null && _bubble.Sphere != null
                ? _bubble.Sphere.position.z
                : source[0].Origin.z;
            var origin = source[0].Origin;
            if (_bubble != null && _bubble.FromPointsHud)
            {
                var fromHud = PointsWorld(z);
                if (fromHud.HasValue)
                    origin = fromHud.Value;
            }

            var jitter = _bubble != null ? _bubble.StartJitter : 0f;
            var burst = new List<VictoryGlyphLaunch>(count);
            for (var i = 0; i < count; i++)
            {
                var src = source[i % source.Count];
                var spawn = origin;
                if (jitter > 0.001f)
                {
                    var scatter = UnityEngine.Random.insideUnitCircle * jitter;
                    spawn.x += scatter.x;
                    spawn.y += scatter.y;
                }

                spawn.z = z;
                burst.Add(new VictoryGlyphLaunch(spawn, src.Sprite, src.IsRedSuit, src.Suit));
            }

            return burst;
        }

        private Vector3? PointsWorld(float worldZ)
        {
            var rect = _scoreHud != null ? _scoreHud.PointsRect : null;
            if (rect == null)
                return null;

            var canvas = rect.GetComponentInParent<Canvas>();
            Camera uiCam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                uiCam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

            var uiPoint = rect.TransformPoint(rect.rect.center);
            var screen = RectTransformUtility.WorldToScreenPoint(uiCam, uiPoint);
            var cam = Camera.main;
            if (cam == null)
                return null;

            var depth = Mathf.Abs(cam.transform.position.z - worldZ);
            var world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
            world.z = worldZ;
            return world;
        }

        private void PulsePointsHud()
        {
            var rect = _scoreHud != null ? _scoreHud.PointsRect : null;
            if (rect == null)
                return;

            rect.DOKill();
            rect.localScale = Vector3.one;
            rect.DOPunchScale(Vector3.one * 0.22f, 0.32f, 8, 0.4f);
        }

        private void EnsurePool()
        {
            if (didInit)
                return;

            didInit = true;
            var rootGo = new GameObject(PoolRootName);
            poolRoot = rootGo.transform;

            for (var i = 0; i < PoolSize; i++)
                pool.Add(CreateGlyph(i));
        }

        private VictorySuitGlyph CreateGlyph(int index)
        {
            var go = new GameObject("VictorySuitGlyph_Pooled_" + index);
            go.transform.SetParent(poolRoot, false);
            go.SetActive(false);
            go.AddComponent<SpriteRenderer>();
            return go.AddComponent<VictorySuitGlyph>();
        }

        private VictorySuitGlyph Rent()
        {
            for (var i = 0; i < pool.Count; i++)
            {
                if (!active.Contains(pool[i]))
                {
                    active.Add(pool[i]);
                    return pool[i];
                }
            }

            if (pool.Count < MaxPool && poolRoot != null)
            {
                var extra = CreateGlyph(pool.Count);
                pool.Add(extra);
                active.Add(extra);
                return extra;
            }

            if (pool.Count == 0)
                return null;

            var stolen = active[0];
            Return(stolen);
            active.Add(stolen);
            return stolen;
        }

        private void Return(VictorySuitGlyph glyph)
        {
            if (glyph == null)
                return;

            glyph.Stop();
            if (poolRoot != null)
                glyph.transform.SetParent(poolRoot, false);
            active.Remove(glyph);
        }
    }
}
