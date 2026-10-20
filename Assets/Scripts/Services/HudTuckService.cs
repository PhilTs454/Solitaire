using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Solitaire.Presenters;
using UnityEngine;

namespace Solitaire.Services
{
    /// <summary>
    /// Tucks HUD widgets off the nearest screen edge for the victory sequence.
    /// </summary>
    public sealed class HudTuckService : IHudTuck, IDisposable
    {
        private const float Stagger = 0.045f;
        private static readonly string[] CanvasNames = { "CanvasGameInfo", "CanvasGameControls" };

        private readonly List<HudTuck> items = new List<HudTuck>(16);
        private bool didBind;

        public void Dispose()
        {
            Restore(true);
            items.Clear();
            didBind = false;
        }

        public async UniTask TuckAsync(RectTransform keepVisible = null)
        {
            EnsureItems();
            if (items.Count == 0)
                return;

            var delayed = 0;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] == null || ShouldKeep(items[i].transform, keepVisible))
                    continue;

                items[i].Tuck(delayed * Stagger);
                delayed++;
            }

            if (delayed == 0)
                return;

            var wait = HudTuck.Duration + Mathf.Max(0, delayed - 1) * Stagger;
            await UniTask.Delay(TimeSpan.FromSeconds(wait));
        }

        public void Restore(bool instant = true)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                    items[i].Restore(instant);
            }
        }

        private void EnsureItems()
        {
            if (didBind && items.Count > 0)
                return;

            items.Clear();
            var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>(true);
            for (var c = 0; c < canvases.Length; c++)
            {
                var canvas = canvases[c];
                if (canvas == null || !IsHudCanvas(canvas.name))
                    continue;

                BindChildren(canvas.transform);
            }

            didBind = true;
        }

        private void BindChildren(Transform canvas)
        {
            for (var i = 0; i < canvas.childCount; i++)
            {
                var child = canvas.GetChild(i);
                if (child == null || !child.gameObject.activeSelf)
                    continue;

                if (!(child is RectTransform))
                    continue;

                var tuck = child.GetComponent<HudTuck>();
                if (tuck == null)
                    tuck = child.gameObject.AddComponent<HudTuck>();

                items.Add(tuck);
            }
        }

        private static bool IsHudCanvas(string name)
        {
            for (var i = 0; i < CanvasNames.Length; i++)
            {
                if (name == CanvasNames[i])
                    return true;
            }

            return false;
        }

        private static bool ShouldKeep(Transform hudItem, RectTransform keepVisible)
        {
            if (hudItem == null || keepVisible == null)
                return false;

            return keepVisible == hudItem
                || keepVisible.IsChildOf(hudItem)
                || hudItem.IsChildOf(keepVisible);
        }
    }
}
