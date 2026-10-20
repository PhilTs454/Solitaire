using Solitaire.Presenters;
using UnityEditor;
using UnityEngine;

namespace Solitaire.Editor
{
    [CustomEditor(typeof(HomePresenter))]
    public class HomePresenterEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Restart Home Animation", GUILayout.Height(32)))
                    ((HomePresenter)target).RestartHomeAnimation();
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode, select CanvasHome, then press Restart to replay the card deal.",
                    MessageType.Info
                );
            }
        }
    }
}
