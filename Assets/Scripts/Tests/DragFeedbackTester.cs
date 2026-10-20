using UnityEngine;
using Solitaire.Presenters;

namespace Solitaire.Tests
{
    /// <summary>
    /// Simple test script to preview drag feedback effects in the editor.
    /// Attach to a card GameObject and use the context menu to test effects.
    /// </summary>
    public class DragFeedbackTester : MonoBehaviour
    {
        private DragFeedback dragFeedback;
        private DragSparkle dragSparkle;
        private SpriteRenderer cardRenderer;

        private void Start()
        {
            cardRenderer = GetComponent<SpriteRenderer>();
            
            // Find or create components
            dragFeedback = GetComponent<DragFeedback>();
            if (dragFeedback == null)
            {
                dragFeedback = gameObject.AddComponent<DragFeedback>();
            }

            dragSparkle = GetComponentInChildren<DragSparkle>(true);
            if (dragSparkle == null && cardRenderer != null)
            {
                var go = new GameObject("DragSparkle", typeof(ParticleSystem));
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, -0.03f);
                dragSparkle = go.AddComponent<DragSparkle>();
                dragSparkle.Bind(cardRenderer);
            }
        }

        [ContextMenu("Start Drag Test")]
        public void StartDragTest()
        {
            if (dragFeedback != null)
                dragFeedback.StartDrag();
                
            if (dragSparkle != null)
                dragSparkle.StartDragSparkling();
                
            Debug.Log("[DragFeedbackTester] Started drag effects test");
        }

        [ContextMenu("Stop Drag Test")]
        public void StopDragTest()
        {
            if (dragFeedback != null)
                dragFeedback.EndDrag();
                
            if (dragSparkle != null)
                dragSparkle.StopDragSparkling();
                
            Debug.Log("[DragFeedbackTester] Stopped drag effects test");
        }

        [ContextMenu("Test Sequence (Start -> Wait 3s -> Stop)")]
        public void TestSequence()
        {
            StartDragTest();
            Invoke(nameof(StopDragTest), 3f);
        }
    }
}