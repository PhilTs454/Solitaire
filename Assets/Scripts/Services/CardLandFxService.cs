using System;
using System.Collections.Generic;
using DG.Tweening;
using Solitaire.Presenters;
using UnityEngine;
using Zenject;

namespace Solitaire.Services
{
    /// <summary>
    /// Small pool of foundation Pulse and tableau SoftRipple+Stars.
    /// Instances live under LandFxPool and temporarily parent to the landing card while playing.
    /// </summary>
    public sealed class CardLandFxService : ICardLandFxService, IInitializable, IDisposable
    {
        private const int PoolSize = 3;
        private const int TableauPoolSize = 8;
        private const int TrailPoolSize = 16;
        private const string PoolRootName = "LandFxPool";
        private const string PulseReturnIdPrefix = "LandFxPulseReturn";
        private const string TrailReturnIdPrefix = "LandFxTrailReturn";
        private const float PulseReturnSeconds = 1.25f;

        [Inject(Optional = true, Id = "ParticlePulse")]
        private readonly GameObject particlePulsePrefab;

        [Inject(Optional = true, Id = "Stars")]
        private readonly GameObject starsPrefab;

        [Inject(Optional = true, Id = "SoftRipple")]
        private readonly Sprite softRippleSprite;

        [Inject(Optional = true, Id = "SoftRippleMaterial")]
        private readonly Material softRippleMaterial;

        private Transform poolRoot;
        private readonly List<PulseSlot> pulseSlots = new List<PulseSlot>(PoolSize);
        private readonly List<TableauSlot> tableauSlots = new List<TableauSlot>(TableauPoolSize);
        private readonly List<TrailSlot> trailSlots = new List<TrailSlot>(TrailPoolSize);
        private int pulseStealIndex;
        private int tableauStealIndex;
        private int trailStealIndex;
        private bool didInit;

        public void Initialize()
        {
            EnsurePools();
        }

        public void Dispose()
        {
            for (var i = 0; i < pulseSlots.Count; i++)
            {
                DOTween.Kill(PulseReturnIdPrefix + i);
                if (pulseSlots[i].Fx != null)
                    pulseSlots[i].Fx.Stop();
            }

            for (var i = 0; i < tableauSlots.Count; i++)
            {
                if (tableauSlots[i].Ripple != null)
                    tableauSlots[i].Ripple.Stop();
            }

            for (var i = 0; i < trailSlots.Count; i++)
            {
                DOTween.Kill(TrailReturnIdPrefix + i);
                if (trailSlots[i].Trail != null)
                    trailSlots[i].Trail.ClearAndReset();
            }

            pulseSlots.Clear();
            tableauSlots.Clear();
            trailSlots.Clear();
            didInit = false;

            if (poolRoot != null)
            {
                UnityEngine.Object.Destroy(poolRoot.gameObject);
                poolRoot = null;
            }
        }

        public void PlayFoundation(
            Transform cardTransform,
            SpriteRenderer faceRenderer,
            Sprite suitSprite,
            Sprite rankSprite,
            bool isRedSuit,
            bool playAceRipple,
            int rippleSortingOrder,
            LandFxVolume volume
        )
        {
            EnsurePools();
            if (cardTransform == null)
                return;

            var slot = RentPulse();
            if (slot != null)
            {
                var index = pulseSlots.IndexOf(slot);
                DOTween.Kill(PulseReturnIdPrefix + index);

                slot.Root.SetParent(cardTransform, false);
                slot.Root.localPosition = Vector3.zero;
                slot.Root.localRotation = Quaternion.identity;
                slot.Root.localScale = Vector3.one * volume.BurstScale;
                slot.Root.gameObject.SetActive(true);
                slot.Busy = true;

                if (slot.Fx != null)
                    slot.Fx.Play(suitSprite, rankSprite, isRedSuit, volume);

                DOVirtual
                    .DelayedCall(PulseReturnSeconds, () => ReturnPulse(slot))
                    .SetId(PulseReturnIdPrefix + index);
            }
            else
            {
                Debug.LogWarning("[CardLandFxService] No pulse slot available.");
            }

            if (playAceRipple)
                PlayTableau(cardTransform, faceRenderer, rippleSortingOrder);
        }

        public void PlayTableau(
            Transform cardTransform,
            SpriteRenderer faceRenderer,
            int sortingOrder
        )
        {
            EnsurePools();
            if (cardTransform == null)
                return;

            var slot = RentTableau();
            if (slot == null)
            {
                Debug.LogWarning("[CardLandFxService] No tableau slot available.");
                return;
            }

            slot.Root.SetParent(cardTransform, false);
            slot.Root.localPosition = Vector3.zero;
            slot.Root.localRotation = Quaternion.identity;
            slot.Root.localScale = Vector3.one;
            slot.Root.gameObject.SetActive(true);
            slot.Busy = true;

            if (slot.Ripple != null)
            {
                slot.Ripple.Configure(softRippleSprite, softRippleMaterial, slot.Stars);
                slot.Ripple.SetCardFace(faceRenderer);
                slot.Ripple.Play(sortingOrder, () => ReturnTableau(slot));
            }
            else
            {
                ReturnTableau(slot);
            }
        }

        public void PlayWorld(Vector3 worldPosition, float worldFit, int sortingOrder, float alpha = 1f)
        {
            EnsurePools();
            var slot = RentTableau();
            if (slot == null)
            {
                Debug.LogWarning("[CardLandFxService] No tableau slot available.");
                return;
            }

            slot.Root.SetParent(poolRoot, false);
            slot.Root.position = worldPosition;
            slot.Root.rotation = Quaternion.identity;
            slot.Root.localScale = Vector3.one;
            slot.Root.gameObject.SetActive(true);
            slot.Busy = true;

            if (slot.Ripple != null)
            {
                slot.Ripple.Configure(softRippleSprite, softRippleMaterial, slot.Stars);
                slot.Ripple.SetCardFace(null);
                slot.Ripple.PlayAtWorld(worldFit, sortingOrder, () => ReturnTableau(slot), alpha);
            }
            else
            {
                ReturnTableau(slot);
            }
        }

        private void EnsurePools()
        {
            if (didInit)
                return;

            didInit = true;
            var rootGo = new GameObject(PoolRootName);
            poolRoot = rootGo.transform;

            BuildPulsePool();
            BuildTableauPool();
            BuildTrailPool();

            Debug.Log(
                $"[CardLandFxService] Pool ready — pulse={pulseSlots.Count} tableau={tableauSlots.Count} trail={trailSlots.Count}."
            );
        }

        private void BuildPulsePool()
        {
            if (particlePulsePrefab == null)
            {
                Debug.LogWarning(
                    "[CardLandFxService] ParticlePulse prefab missing — foundation land FX disabled."
                );
                return;
            }

            for (var i = 0; i < PoolSize; i++)
            {
                var instance = UnityEngine.Object.Instantiate(particlePulsePrefab, poolRoot);
                instance.name = "ParticlePulse_Pooled_" + i;
                instance.transform.localPosition = Vector3.zero;
                instance.SetActive(false);

                var fx =
                    instance.GetComponent<CardLandFx>()
                    ?? instance.GetComponentInChildren<CardLandFx>(true);
                if (fx == null)
                    fx = instance.AddComponent<CardLandFx>();

                pulseSlots.Add(
                    new PulseSlot
                    {
                        Root = instance.transform,
                        Fx = fx,
                        Busy = false
                    }
                );
            }
        }

        private void BuildTableauPool()
        {
            for (var i = 0; i < TableauPoolSize; i++)
            {
                var host = new GameObject("TableauLandFx_Pooled_" + i);
                host.transform.SetParent(poolRoot, false);
                host.SetActive(false);

                var rippleGo = new GameObject("SoftRippleConfirm");
                rippleGo.transform.SetParent(host.transform, false);
                rippleGo.AddComponent<SpriteRenderer>();
                var ripple = rippleGo.AddComponent<SoftRippleConfirm>();

                ParticleSystem stars = null;
                if (starsPrefab != null)
                {
                    var starsInstance = UnityEngine.Object.Instantiate(starsPrefab, host.transform);
                    starsInstance.name = "Stars";
                    starsInstance.transform.localPosition = Vector3.zero;
                    starsInstance.SetActive(true);
                    stars = starsInstance.GetComponent<ParticleSystem>();
                    if (stars == null)
                    {
                        Debug.LogWarning(
                            "[CardLandFxService] Stars prefab has no ParticleSystem."
                        );
                    }
                }

                ripple.Configure(softRippleSprite, softRippleMaterial, stars);

                tableauSlots.Add(
                    new TableauSlot
                    {
                        Root = host.transform,
                        Ripple = ripple,
                        Stars = stars,
                        Busy = false
                    }
                );
            }
        }

        public void PlayFlightTrail(Transform cardTransform, bool isRedSuit, int sortingOrder)
        {
            EnsurePools();
            if (cardTransform == null)
                return;

            var existing = FindTrailSlot(cardTransform);
            if (existing != null)
            {
                existing.Trail?.Play(isRedSuit, sortingOrder);
                return;
            }

            var slot = RentTrail();
            if (slot == null)
                return;

            var index = trailSlots.IndexOf(slot);
            DOTween.Kill(TrailReturnIdPrefix + index);

            slot.BoundTo = cardTransform;
            slot.Root.SetParent(cardTransform, false);
            slot.Root.localPosition = Vector3.zero;
            slot.Root.localRotation = Quaternion.identity;
            slot.Root.localScale = Vector3.one;
            slot.Root.gameObject.SetActive(true);
            slot.Busy = true;
            slot.Trail?.Play(isRedSuit, sortingOrder);
        }

        public void StopFlightTrail(Transform cardTransform)
        {
            var slot = FindTrailSlot(cardTransform);
            if (slot == null)
                return;

            ScheduleTrailReturn(slot);
        }

        public void StopAllFlightTrails()
        {
            for (var i = 0; i < trailSlots.Count; i++)
                ReturnTrail(trailSlots[i]);
        }

        private void BuildTrailPool()
        {
            for (var i = 0; i < TrailPoolSize; i++)
            {
                var host = new GameObject("FlightTrail_Pooled_" + i);
                host.transform.SetParent(poolRoot, false);
                host.SetActive(false);
                host.AddComponent<TrailRenderer>();
                var trail = host.AddComponent<CardFlightTrail>();

                trailSlots.Add(
                    new TrailSlot
                    {
                        Root = host.transform,
                        Trail = trail,
                        Busy = false
                    }
                );
            }
        }

        private PulseSlot RentPulse()
        {
            for (var i = 0; i < pulseSlots.Count; i++)
            {
                if (!pulseSlots[i].Busy)
                    return pulseSlots[i];
            }

            if (pulseSlots.Count == 0)
                return null;

            // Steal oldest-ish slot so overlapping lands never Instantiate.
            var stolen = pulseSlots[pulseStealIndex % pulseSlots.Count];
            pulseStealIndex++;
            ReturnPulse(stolen);
            stolen.Busy = true;
            return stolen;
        }

        private TableauSlot RentTableau()
        {
            for (var i = 0; i < tableauSlots.Count; i++)
            {
                if (!tableauSlots[i].Busy)
                    return tableauSlots[i];
            }

            if (tableauSlots.Count == 0)
                return null;

            var stolen = tableauSlots[tableauStealIndex % tableauSlots.Count];
            tableauStealIndex++;
            ReturnTableau(stolen);
            stolen.Busy = true;
            return stolen;
        }

        private void ReturnPulse(PulseSlot slot)
        {
            if (slot == null)
                return;

            var index = pulseSlots.IndexOf(slot);
            if (index >= 0)
                DOTween.Kill(PulseReturnIdPrefix + index);

            if (slot.Fx != null)
                slot.Fx.Stop();

            if (slot.Root != null)
            {
                slot.Root.SetParent(poolRoot, false);
                slot.Root.localScale = Vector3.one;
                slot.Root.gameObject.SetActive(false);
            }

            slot.Busy = false;
        }

        private void ReturnTableau(TableauSlot slot)
        {
            if (slot == null)
                return;

            if (slot.Ripple != null)
                slot.Ripple.Stop();

            if (slot.Stars != null)
                slot.Stars.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            if (slot.Root != null)
            {
                slot.Root.SetParent(poolRoot, false);
                slot.Root.gameObject.SetActive(false);
            }

            slot.Busy = false;
        }

        private TrailSlot RentTrail()
        {
            for (var i = 0; i < trailSlots.Count; i++)
            {
                if (!trailSlots[i].Busy)
                    return trailSlots[i];
            }

            if (trailSlots.Count == 0)
                return null;

            var stolen = trailSlots[trailStealIndex % trailSlots.Count];
            trailStealIndex++;
            ReturnTrail(stolen);
            stolen.Busy = true;
            return stolen;
        }

        private TrailSlot FindTrailSlot(Transform cardTransform)
        {
            if (cardTransform == null)
                return null;

            for (var i = 0; i < trailSlots.Count; i++)
            {
                if (trailSlots[i].Busy && trailSlots[i].BoundTo == cardTransform)
                    return trailSlots[i];
            }

            return null;
        }

        private void ScheduleTrailReturn(TrailSlot slot)
        {
            if (slot == null)
                return;

            slot.Trail?.StopEmitting();

            var index = trailSlots.IndexOf(slot);
            if (index >= 0)
                DOTween.Kill(TrailReturnIdPrefix + index);

            var fade = slot.Trail != null ? slot.Trail.FadeSeconds : 0.4f;
            DOVirtual
                .DelayedCall(fade, () => ReturnTrail(slot))
                .SetId(TrailReturnIdPrefix + index);
        }

        private void ReturnTrail(TrailSlot slot)
        {
            if (slot == null)
                return;

            var index = trailSlots.IndexOf(slot);
            if (index >= 0)
                DOTween.Kill(TrailReturnIdPrefix + index);

            slot.Trail?.ClearAndReset();

            if (slot.Root != null)
            {
                slot.Root.SetParent(poolRoot, false);
                slot.Root.gameObject.SetActive(false);
            }

            slot.BoundTo = null;
            slot.Busy = false;
        }

        private sealed class PulseSlot
        {
            public Transform Root;
            public CardLandFx Fx;
            public bool Busy;
        }

        private sealed class TableauSlot
        {
            public Transform Root;
            public SoftRippleConfirm Ripple;
            public ParticleSystem Stars;
            public bool Busy;
        }

        private sealed class TrailSlot
        {
            public Transform Root;
            public CardFlightTrail Trail;
            public Transform BoundTo;
            public bool Busy;
        }
    }
}
