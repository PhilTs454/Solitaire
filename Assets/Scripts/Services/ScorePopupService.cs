using System;
using System.Collections.Generic;
using Solitaire.Presenters;
using TMPro;
using UnityEngine;
using Zenject;

namespace Solitaire.Services
{
    /// <summary>
    /// Pools a few world-space score labels. Created at runtime — no scene object.
    /// </summary>
    public class ScorePopupService : IScorePopup, IDisposable
    {
        private const int PoolSize = 6;

        [Inject(Optional = true, Id = "ScorePopupFont")]
        private TMP_FontAsset font;

        [Inject(Optional = true)]
        private ScorePopupTune tune;

        private readonly List<ScorePopupPresenter> pool = new();
        private Transform root;
        private int showIndex;
        private bool didWarnMissingFont;

        public event Action<int> Committed;

        public void Show(
            int delta,
            Vector3 worldPosition,
            float delaySeconds,
            ScorePopupStyle style = ScorePopupStyle.Normal
        )
        {
            if (delta == 0)
                return;

            EnsurePool();
            if (pool.Count == 0)
            {
                RaiseCommitted(delta);
                return;
            }

            var popup = FindIdle() ?? pool[showIndex % pool.Count];
            showIndex++;
            popup.Play(delta, worldPosition, delaySeconds, style, RaiseCommitted);
        }

        public void ShowHoming(
            int delta,
            Vector3 worldPosition,
            Vector3 homeWorldPosition,
            float delaySeconds,
            float homeDuration,
            ScorePopupStyle style = ScorePopupStyle.Bonus
        )
        {
            if (delta == 0)
                return;

            EnsurePool();
            if (pool.Count == 0)
                return;

            var popup = FindIdle() ?? pool[showIndex % pool.Count];
            showIndex++;
            popup.Play(
                delta,
                worldPosition,
                delaySeconds,
                style,
                committed: null,
                homeToTarget: true,
                homeWorldPosition: homeWorldPosition,
                homeDuration: homeDuration,
                commitOnPeak: false
            );
        }

        public void Commit(int delta)
        {
            if (delta == 0)
                return;

            RaiseCommitted(delta);
        }

        private void RaiseCommitted(int delta)
        {
            Committed?.Invoke(delta);
        }

        public void Dispose()
        {
            for (var i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null)
                    pool[i].Stop();
            }

            pool.Clear();
            if (root != null)
                UnityEngine.Object.Destroy(root.gameObject);
        }

        private ScorePopupPresenter FindIdle()
        {
            for (var i = 0; i < pool.Count; i++)
            {
                var popup = pool[i];
                if (popup != null && !popup.IsPlaying)
                    return popup;
            }

            return null;
        }

        private void EnsurePool()
        {
            if (root != null)
                return;

            var fontAsset = font != null ? font : TMP_Settings.defaultFontAsset;
            if (fontAsset == null)
            {
                if (!didWarnMissingFont)
                {
                    Debug.LogWarning(
                        "[ScorePopupService] No TMP font — assign Baron Neue SDF on GameInstaller."
                    );
                    didWarnMissingFont = true;
                }

                return;
            }

            var rootGo = new GameObject("ScorePopupPool");
            root = rootGo.transform;

            var layer = LayerMask.NameToLayer("Interactable");
            if (layer >= 0)
                rootGo.layer = layer;

            for (var i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("ScorePopup", typeof(RectTransform), typeof(TextMeshPro));
                go.transform.SetParent(root, false);
                if (layer >= 0)
                    go.layer = layer;

                var popup = go.AddComponent<ScorePopupPresenter>();
                popup.Configure(fontAsset, tune);
                go.SetActive(false);
                pool.Add(popup);
            }
        }
    }
}
