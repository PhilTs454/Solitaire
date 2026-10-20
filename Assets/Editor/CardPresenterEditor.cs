using Solitaire.Presenters;
using UnityEditor;
using UnityEngine;

namespace Solitaire.Editor
{
    [CustomEditor(typeof(CardPresenter))]
    public class CardPresenterEditor : UnityEditor.Editor
    {
        private static float holdSeconds = 0.4f;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Stock Press Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Spring on press. Sparkles fly off the top and bottom when the card pops back. Tune StockPressSparkle on the card child in Play Mode.",
                MessageType.None
            );

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                holdSeconds = EditorGUILayout.Slider("Hold (seconds)", holdSeconds, 0.15f, 1.5f);

                var card = (CardPresenter)target;
                if (GUILayout.Button("Press Stock", GUILayout.Height(26)))
                    card.PreviewStockPress();

                if (GUILayout.Button("Release Stock", GUILayout.Height(26)))
                    card.PreviewStockRelease();

                if (GUILayout.Button("Play Press + Release", GUILayout.Height(32)))
                    card.PreviewStockPressThenRelease(holdSeconds);
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode, start a match, then use these buttons. You can select any card (or the Card prefab).",
                    MessageType.Info
                );
            }
        }
    }
}
