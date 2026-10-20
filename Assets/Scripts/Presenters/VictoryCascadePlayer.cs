using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Solitaire.Models;
using Solitaire.Services;
using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Classic Solitaire win: cards peel off the completed foundations and bounce.
    /// </summary>
    public sealed class VictoryCascadePlayer
    {
        private const float Gravity = -32f;
        private const float Restitution = 0.84f;
        private const float LaunchInterval = 0.045f;
        private const float MinBounceSpeed = 4.5f;
        private const float OffscreenPadding = 2.2f;
        private const int BounceSortBase = 200;

        private readonly ICardSpawner _cardSpawner;
        private readonly IAudioService _audioService;

        public VictoryCascadePlayer(ICardSpawner cardSpawner, IAudioService audioService)
        {
            _cardSpawner = cardSpawner;
            _audioService = audioService;
        }

        public async UniTask PlayAsync(IList<Pile> foundations, Func<bool> stillWinning)
        {
            if (foundations == null || foundations.Count == 0)
                return;

            var camera = Camera.main;
            if (camera == null)
                return;

            var bouncing = new List<Bounce>(52);
            var launched = 0;

            while (stillWinning != null && stillWinning() && HasCards(foundations))
            {
                for (var i = 0; i < foundations.Count; i++)
                {
                    if (stillWinning != null && !stillWinning())
                        break;

                    var pile = foundations[i];
                    var card = pile != null ? pile.TopCard() : null;
                    if (card == null)
                        continue;

                    var presenter = FindPresenter(card);
                    if (presenter == null)
                        continue;

                    pile.RemoveCard(card);
                    Launch(presenter, bouncing, launched, camera);
                    launched++;
                    _audioService?.PlaySfx(Audio.SfxPlace, 0.1f);

                    await UniTask.Delay(TimeSpan.FromSeconds(LaunchInterval));
                    if (stillWinning != null && !stillWinning())
                        break;
                }
            }

            var deadline = Time.realtimeSinceStartup + 8f;
            while (
                stillWinning != null
                && stillWinning()
                && AnyActive(bouncing)
                && Time.realtimeSinceStartup < deadline
            )
            {
                Step(bouncing, camera, Time.deltaTime);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            for (var i = 0; i < bouncing.Count; i++)
                bouncing[i].Presenter?.EndVictoryBounce();
        }

        private void Launch(
            CardPresenter presenter,
            List<Bounce> bouncing,
            int launched,
            Camera camera
        )
        {
            presenter.BeginVictoryBounce(BounceSortBase + launched);

            var pos = presenter.transform.position;
            var camPos = camera.transform.position;
            var away = pos.x >= camPos.x ? 1f : -1f;
            var vx = away * UnityEngine.Random.Range(3.2f, 6.4f);
            var vy = UnityEngine.Random.Range(5.5f, 9.5f);

            bouncing.Add(
                new Bounce
                {
                    Presenter = presenter,
                    Velocity = new Vector3(vx, vy, 0f),
                    Active = true,
                    FloorHits = 0
                }
            );
        }

        private static void Step(List<Bounce> bouncing, Camera camera, float dt)
        {
            if (dt <= 0f)
                dt = 0.016f;

            GetWorldBounds(camera, out var min, out var max);

            for (var i = 0; i < bouncing.Count; i++)
            {
                var bounce = bouncing[i];
                if (!bounce.Active || bounce.Presenter == null)
                    continue;

                var pos = bounce.Presenter.transform.position;
                var vel = bounce.Velocity;
                vel.y += Gravity * dt;
                pos += vel * dt;

                var extents = GetExtents(bounce.Presenter);
                var left = min.x + extents.x;
                var right = max.x - extents.x;
                var bottom = min.y + extents.y;

                if (pos.x < left)
                {
                    pos.x = left;
                    vel.x = Mathf.Abs(vel.x);
                }
                else if (pos.x > right)
                {
                    pos.x = right;
                    vel.x = -Mathf.Abs(vel.x);
                }

                if (pos.y < bottom)
                {
                    if (bounce.FloorHits < 2)
                    {
                        pos.y = bottom;
                        vel.y = Mathf.Max(Mathf.Abs(vel.y) * Restitution, MinBounceSpeed);
                        bounce.FloorHits++;
                    }
                }

                bounce.Presenter.SetWorldPosition(pos);
                bounce.Velocity = vel;

                if (pos.y < min.y - OffscreenPadding || Mathf.Abs(pos.x - camera.transform.position.x) > (max.x - min.x) + OffscreenPadding)
                {
                    bounce.Active = false;
                    bounce.Presenter.StopVictoryFlightTrail();
                    if (bounce.Presenter.Card != null)
                        bounce.Presenter.Card.IsVisible.Value = false;
                }

                bouncing[i] = bounce;
            }
        }

        private static Vector2 GetExtents(CardPresenter presenter)
        {
            var collider = presenter.GetComponent<BoxCollider2D>();
            if (collider != null)
                return collider.bounds.extents;

            return new Vector2(0.55f, 0.82f);
        }

        private static void GetWorldBounds(Camera camera, out Vector3 min, out Vector3 max)
        {
            var z = Mathf.Abs(camera.transform.position.z);
            min = camera.ViewportToWorldPoint(new Vector3(0f, 0f, z));
            max = camera.ViewportToWorldPoint(new Vector3(1f, 1f, z));
        }

        private static bool HasCards(IList<Pile> foundations)
        {
            for (var i = 0; i < foundations.Count; i++)
            {
                if (foundations[i] != null && foundations[i].HasCards)
                    return true;
            }

            return false;
        }

        private static bool AnyActive(List<Bounce> bouncing)
        {
            for (var i = 0; i < bouncing.Count; i++)
            {
                if (bouncing[i].Active)
                    return true;
            }

            return false;
        }

        private CardPresenter FindPresenter(Card card)
        {
            if (card == null || _cardSpawner == null)
                return null;

            var presenters = _cardSpawner.Cards;
            for (var i = 0; i < presenters.Count; i++)
            {
                var presenter = presenters[i];
                if (presenter != null && presenter.Card == card)
                    return presenter;
            }

            return null;
        }

        private struct Bounce
        {
            public CardPresenter Presenter;
            public Vector3 Velocity;
            public bool Active;
            public int FloorHits;
        }
    }
}
