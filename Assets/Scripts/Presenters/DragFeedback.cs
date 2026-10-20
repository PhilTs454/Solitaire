using DG.Tweening;
using UnityEngine;

namespace Solitaire.Presenters
{
    /// <summary>
    /// Provides juicy visual feedback when a card is being dragged:
    /// - Lift scale (card grows slightly)
    /// - Soft shadow beneath the card
    /// - Optional rotation wobble
    /// </summary>
    public class DragFeedback : MonoBehaviour
    {
        private const string LiftTweenIdPrefix = "DragLift";
        private const string ShadowTweenIdPrefix = "DragShadow";

        [Header("Lift Scale")]
        [Tooltip("How much the card grows when lifted. 1.05 = 5% bigger")]
        [SerializeField]
        private float liftScale = 1.05f;

        [Tooltip("Time to scale up when drag starts")]
        [SerializeField]
        private float liftDuration = 0.12f;

        [Tooltip("Time to scale down when drag ends")]
        [SerializeField]
        private float dropDuration = 0.08f;

        [Header("Shadow")]
        [Tooltip("Enable soft shadow under dragged card")]
        [SerializeField]
        private bool enableShadow = true;

        [Tooltip("Shadow color")]
        [SerializeField]
        private Color shadowColor = new Color(0f, 0f, 0f, 0.3f);

        [Tooltip("Shadow offset from card")]
        [SerializeField]
        private Vector3 shadowOffset = new Vector3(0.08f, -0.08f, 0.01f);

        [Tooltip("Shadow scale vs card scale")]
        [SerializeField]
        private float shadowScale = 0.95f;

        [Header("Wobble")]
        [Tooltip("Enable slight rotation wobble while dragging")]
        [SerializeField]
        private bool enableWobble = true;

        [Tooltip("Max rotation angle for wobble")]
        [SerializeField]
        private float wobbleAngle = 1.5f;

        [Tooltip("Wobble frequency")]
        [SerializeField]
        private float wobbleSpeed = 1.8f;

        [Tooltip("Add random phase offset to wobble")]
        [SerializeField]
        private bool randomPhase = true;

        private Transform cardTransform;
        private SpriteRenderer cardRenderer;
        private GameObject shadowObject;
        private SpriteRenderer shadowRenderer;
        private Transform shadowTransform;
        private string liftTweenId;
        private string shadowTweenId;
        private bool isDragging;
        private Vector3 originalScale;
        private bool didCreateShadow;
        private float wobblePhase;
        private float wobbleStartTime;

        private void Awake()
        {
            cardTransform = transform;
            cardRenderer = GetComponent<SpriteRenderer>();
            liftTweenId = LiftTweenIdPrefix + GetInstanceID();
            shadowTweenId = ShadowTweenIdPrefix + GetInstanceID();
            originalScale = cardTransform.localScale;
        }

        public void StartDrag()
        {
            if (isDragging) return;

            isDragging = true;
            originalScale = cardTransform.localScale;

            // Initialize wobble
            wobblePhase = randomPhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;
            wobbleStartTime = Time.time;

            // Lift animation
            DOTween.Kill(liftTweenId);
            cardTransform
                .DOScale(originalScale * liftScale, liftDuration)
                .SetEase(Ease.OutBack, 1.2f)
                .SetId(liftTweenId);

            // Shadow
            if (enableShadow)
            {
                EnsureShadow();
                ShowShadow();
            }
        }

        public void EndDrag()
        {
            if (!isDragging) return;

            isDragging = false;

            // Reset rotation to zero
            if (enableWobble)
            {
                cardTransform.rotation = Quaternion.identity;
            }

            // Drop animation
            DOTween.Kill(liftTweenId);
            cardTransform
                .DOScale(originalScale, dropDuration)
                .SetEase(Ease.OutQuad)
                .SetId(liftTweenId);

            // Hide shadow
            if (enableShadow && shadowObject != null)
            {
                HideShadow();
            }
        }

        private void Update()
        {
            if (!isDragging) return;

            // Update shadow position to follow card
            if (enableShadow && shadowTransform != null)
            {
                shadowTransform.position = cardTransform.position + shadowOffset;
            }

            // Subtle wobble
            if (enableWobble)
            {
                var timeElapsed = Time.time - wobbleStartTime;
                
                // Combine two sine waves for more organic movement
                var wobble1 = Mathf.Sin((timeElapsed * wobbleSpeed + wobblePhase)) * wobbleAngle;
                var wobble2 = Mathf.Sin((timeElapsed * wobbleSpeed * 0.7f + wobblePhase * 1.3f)) * wobbleAngle * 0.4f;
                var totalWobble = wobble1 + wobble2;
                
                // Fade in wobble over first 0.2 seconds for smoother start
                var wobbleFade = Mathf.Clamp01(timeElapsed / 0.2f);
                totalWobble *= wobbleFade;
                
                cardTransform.rotation = Quaternion.Euler(0f, 0f, totalWobble);
            }
        }

        private void EnsureShadow()
        {
            if (didCreateShadow) return;

            if (cardRenderer == null || cardRenderer.sprite == null)
                return;

            // Create shadow object
            shadowObject = new GameObject("DragShadow");
            shadowTransform = shadowObject.transform;
            shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();

            // Configure shadow
            shadowRenderer.sprite = cardRenderer.sprite;
            shadowRenderer.color = shadowColor;
            shadowRenderer.sortingLayerID = cardRenderer.sortingLayerID;
            shadowRenderer.sortingOrder = cardRenderer.sortingOrder - 1;

            // Position and scale
            shadowTransform.position = cardTransform.position + shadowOffset;
            shadowTransform.localScale = cardTransform.localScale * shadowScale;
            shadowTransform.rotation = cardTransform.rotation;

            // Start hidden
            shadowRenderer.color = new Color(shadowColor.r, shadowColor.g, shadowColor.b, 0f);
            shadowObject.SetActive(false);

            didCreateShadow = true;
        }

        private void ShowShadow()
        {
            if (shadowObject == null || shadowRenderer == null) return;

            shadowObject.SetActive(true);
            DOTween.Kill(shadowTweenId);
            shadowRenderer
                .DOColor(shadowColor, liftDuration * 0.8f)
                .SetEase(Ease.OutQuad)
                .SetId(shadowTweenId);
        }

        private void HideShadow()
        {
            if (shadowRenderer == null) return;

            DOTween.Kill(shadowTweenId);
            var fadeColor = new Color(shadowColor.r, shadowColor.g, shadowColor.b, 0f);
            shadowRenderer
                .DOColor(fadeColor, dropDuration)
                .SetEase(Ease.InQuad)
                .SetId(shadowTweenId)
                .OnComplete(() =>
                {
                    if (shadowObject != null)
                        shadowObject.SetActive(false);
                });
        }

        private void OnDisable()
        {
            EndDrag();
            DOTween.Kill(liftTweenId);
            DOTween.Kill(shadowTweenId);
        }

        private void OnDestroy()
        {
            if (shadowObject != null)
                DestroyImmediate(shadowObject);
        }
    }
}