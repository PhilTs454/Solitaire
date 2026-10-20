using System.Collections.Generic;
using DG.Tweening;
using Solitaire.Models;
using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// World-space victory crest. Suit sockets start empty and fill when a glyph docks.
    /// </summary>
    public sealed class CrestShieldPresenter : MonoBehaviour
    {
        public const float SpawnHold = 0.38f;

        private const string SpawnId = "CrestShieldSpawn";
        private const string PunchIdPrefix = "CrestSocketPunch";

        private readonly Dictionary<Card.Suits, Socket> sockets =
            new Dictionary<Card.Suits, Socket>(4);

        [SerializeField]
        private Sprite starSprite;

        private CrestStarBurst starBurst;
        private bool didCache;
        private bool allowAutoHide = true;
        private Vector3 restScale = Vector3.one;

        private void Awake()
        {
            restScale = transform.localScale;
            if (restScale.sqrMagnitude < 0.0001f)
                restScale = Vector3.one;

            CacheSockets();
            ClearSockets();
        }

        private void Start()
        {
            if (allowAutoHide && gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        public void Prepare()
        {
            allowAutoHide = false;
            CacheSockets();
            ClearSockets();
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            PlaySpawnPunch();
            BurstStars(transform.position, 5);
        }

        public void Hide()
        {
            DOTween.Kill(SpawnId);
            starBurst?.StopAll();
            ClearSockets();
            transform.localScale = restScale;
            transform.localRotation = Quaternion.identity;
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        public bool TryGetSocket(
            Card.Suits suit,
            out Transform slot,
            out Vector3 lossyScale,
            out Sprite socketSprite
        )
        {
            CacheSockets();
            if (sockets.TryGetValue(suit, out var socket) && socket.Slot != null)
            {
                slot = socket.Slot;
                lossyScale = socket.Slot.lossyScale;
                socketSprite = socket.CachedSprite;
                return true;
            }

            slot = null;
            lossyScale = Vector3.one;
            socketSprite = null;
            return false;
        }

        public void Fill(Card.Suits suit, Sprite fallback)
        {
            CacheSockets();
            if (!sockets.TryGetValue(suit, out var socket) || socket.Renderer == null)
                return;

            var sprite = socket.CachedSprite != null ? socket.CachedSprite : fallback;
            socket.Renderer.sprite = sprite;
            socket.Renderer.enabled = sprite != null;
            PunchLand(suit, socket);
            BurstStars(socket.Slot.position, 6);
        }

        private void PlaySpawnPunch()
        {
            DOTween.Kill(SpawnId);
            transform.localRotation = Quaternion.Euler(0f, 0f, -16f);
            transform.localScale = restScale * 0.12f;

            var pop = DOTween.Sequence().SetId(SpawnId);
            pop.Append(
                transform
                    .DOScale(restScale, 0.42f)
                    .SetEase(Ease.OutBack, 3.1f)
            );
            pop.Join(
                transform
                    .DOLocalRotate(Vector3.zero, 0.42f)
                    .SetEase(Ease.OutBack, 2.2f)
            );
            pop.Append(
                transform
                    .DOPunchScale(restScale * 0.14f, 0.28f, 5, 0.65f)
            );
        }

        private void PunchLand(Card.Suits suit, Socket socket)
        {
            if (socket.Slot == null)
                return;

            var id = PunchIdPrefix + suit;
            DOTween.Kill(id);

            var rest = socket.RestScale;
            socket.Slot.localScale = new Vector3(rest.x * 1.48f, rest.y * 0.5f, rest.z);

            var hit = DOTween.Sequence().SetId(id);
            hit.Append(socket.Slot.DOScale(rest * 1.28f, 0.09f).SetEase(Ease.OutQuad));
            hit.Append(socket.Slot.DOScale(rest, 0.28f).SetEase(Ease.OutBack, 2.9f));
        }

        private void BurstStars(Vector3 origin, int sortingOrder)
        {
            EnsureStarBurst();
            starBurst?.Play(origin, sortingOrder);
        }

        private void EnsureStarBurst()
        {
            if (starBurst != null || starSprite == null)
                return;

            starBurst = new CrestStarBurst(starSprite);
        }

        private void OnDestroy()
        {
            starBurst?.Dispose();
            starBurst = null;
        }

        private void CacheSockets()
        {
            if (didCache)
                return;

            didCache = true;
            sockets.Clear();

            var root = transform.Find("GylphPos") ?? transform.Find("GlyphPos");
            if (root == null)
            {
                Debug.LogWarning("[CrestShield] Missing child GylphPos / GlyphPos.");
                return;
            }

            TryAdd(root, "Heart", Card.Suits.Heart);
            TryAdd(root, "Diamond", Card.Suits.Diamond);
            TryAdd(root, "Club", Card.Suits.Club);
            TryAdd(root, "Spade", Card.Suits.Spade);
        }

        private void TryAdd(Transform root, string childName, Card.Suits suit)
        {
            var child = root.Find(childName);
            if (child == null)
            {
                Debug.LogWarning("[CrestShield] Missing socket '" + childName + "'.");
                return;
            }

            var renderer = child.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sortingOrder < 2)
                renderer.sortingOrder = 2;

            sockets[suit] = new Socket
            {
                Slot = child,
                Renderer = renderer,
                CachedSprite = renderer != null ? renderer.sprite : null,
                RestScale = child.localScale
            };
        }

        private void ClearSockets()
        {
            foreach (var pair in sockets)
            {
                DOTween.Kill(PunchIdPrefix + pair.Key);
                var socket = pair.Value;
                if (socket.Slot != null)
                    socket.Slot.localScale = socket.RestScale;

                var renderer = socket.Renderer;
                if (renderer == null)
                    continue;
                renderer.sprite = null;
                renderer.enabled = false;
            }
        }

        private struct Socket
        {
            public Transform Slot;
            public SpriteRenderer Renderer;
            public Sprite CachedSprite;
            public Vector3 RestScale;
        }
    }
}
