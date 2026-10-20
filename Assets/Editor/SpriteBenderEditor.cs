using Solitaire.Presenters;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Solitaire.Editor
{
    /// <summary>
    /// Inspector and Scene view handles for <see cref="SpriteBender"/>, plus the bake
    /// button that turns the live bend into a plain sprite asset.
    /// </summary>
    [CustomEditor(typeof(SpriteBender))]
    public class SpriteBenderEditor : UnityEditor.Editor
    {
        private const string SupersampleKey = "Solitaire.SpriteBend.Supersample";
        private const string ResolutionKey = "Solitaire.SpriteBend.Resolution";
        private const string PaddingKey = "Solitaire.SpriteBend.Padding";
        private const string GraphicMaterialKey = "Solitaire.SpriteBend.GraphicMaterial";
        private const string FlipKey = "Solitaire.SpriteBend.Flip";
        private const float Epsilon = 0.0001f;

        private static readonly int[] SupersampleValues = { 1, 2, 4 };
        private static readonly string[] SupersampleLabels = { "Off", "2x", "4x" };

        private static readonly Preset[] Presets =
        {
            new Preset
            {
                name = "Arc Up",
                bend = 0.25f,
                waveFrequency = 1f
            },
            new Preset
            {
                name = "Arc Down",
                bend = -0.25f,
                waveFrequency = 1f
            },
            new Preset
            {
                name = "Arch",
                bend = 0.12f,
                arch = 0.35f,
                waveFrequency = 1f
            },
            new Preset
            {
                name = "Wave",
                wave = 0.12f,
                waveFrequency = 1.5f
            },
            new Preset
            {
                name = "Taper",
                taper = 0.4f,
                waveFrequency = 1f
            },
            new Preset { name = "Flat", waveFrequency = 1f }
        };

        private SerializedProperty bendProperty;
        private SerializedProperty archProperty;
        private SerializedProperty taperProperty;
        private SerializedProperty waveProperty;
        private SerializedProperty waveFrequencyProperty;

        private SpriteBakeSettings settings;

        [MenuItem("Tools/Solitaire/Bend Selected UI Sprite")]
        private static void BendSelection()
        {
            var added = 0;
            foreach (var selected in Selection.gameObjects)
            {
                if (
                    selected.GetComponent<Image>() == null
                    || selected.GetComponent<SpriteBender>() != null
                )
                    continue;

                Undo.AddComponent<SpriteBender>(selected);
                added++;
            }

            if (added == 0)
            {
                EditorUtility.DisplayDialog(
                    "Sprite Bender",
                    "Select one or more UI objects that have an Image component and no Sprite Bender yet.",
                    "OK"
                );
            }
        }

        private void OnEnable()
        {
            bendProperty = serializedObject.FindProperty("bend");
            archProperty = serializedObject.FindProperty("arch");
            taperProperty = serializedObject.FindProperty("taper");
            waveProperty = serializedObject.FindProperty("wave");
            waveFrequencyProperty = serializedObject.FindProperty("waveFrequency");

            settings = SpriteBakeSettings.Default;
            settings.supersample = EditorPrefs.GetInt(SupersampleKey, settings.supersample);
            settings.resolutionScale = EditorPrefs.GetFloat(
                ResolutionKey,
                settings.resolutionScale
            );
            settings.paddingPixels = EditorPrefs.GetFloat(PaddingKey, settings.paddingPixels);
            settings.useGraphicMaterial = EditorPrefs.GetBool(GraphicMaterialKey, false);
            settings.flipVertically = EditorPrefs.GetBool(FlipKey, false);

            SceneView.duringSceneGui += OnSceneGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
        }

        public override void OnInspectorGUI()
        {
            var bender = (SpriteBender)target;
            var image = bender.GetComponent<Image>();

            EditorGUILayout.HelpBox(
                "Drag the Scene view handles or the sliders below to shape the bend, then "
                    + "Bake. Baking writes a new sprite next to the source, assigns it, and "
                    + "removes this component so nothing extra ships. Undo restores it.",
                MessageType.Info
            );

            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            var changed = EditorGUI.EndChangeCheck();

            EditorGUILayout.Space(4f);
            changed |= DrawPresets();

            if (changed)
            {
                serializedObject.ApplyModifiedProperties();
                MarkTargetsDirty();
            }

            EditorGUILayout.Space(10f);
            DrawBakeSection(bender, image);
        }

        private bool DrawPresets()
        {
            var applied = false;

            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (var i = 0; i < Presets.Length; i++)
                {
                    if (!GUILayout.Button(Presets[i].name, EditorStyles.miniButton))
                        continue;

                    var preset = Presets[i];
                    bendProperty.floatValue = preset.bend;
                    archProperty.floatValue = preset.arch;
                    taperProperty.floatValue = preset.taper;
                    waveProperty.floatValue = preset.wave;
                    waveFrequencyProperty.floatValue = preset.waveFrequency;
                    applied = true;
                }
            }

            return applied;
        }

        private void DrawBakeSection(SpriteBender bender, Image image)
        {
            EditorGUILayout.LabelField("Bake", EditorStyles.boldLabel);

            if (image == null)
            {
                EditorGUILayout.HelpBox(
                    "Baking needs an Image on this object. The live bend still works on any "
                        + "Graphic, but only Image can be baked to a sprite.",
                    MessageType.Warning
                );
                return;
            }

            if (image.sprite == null)
            {
                EditorGUILayout.HelpBox("Assign a sprite on the Image first.", MessageType.Warning);
                return;
            }

            if (image.type != Image.Type.Simple)
            {
                EditorGUILayout.HelpBox(
                    "Image type is "
                        + image.type
                        + ". Baking rasterises it as a single Simple sprite, so 9-slice borders "
                        + "and tiling are flattened into the result.",
                    MessageType.Warning
                );
            }

            EditorGUI.BeginChangeCheck();

            settings.supersample = EditorGUILayout.IntPopup(
                "Supersample",
                settings.supersample,
                SupersampleLabels,
                SupersampleValues
            );
            settings.resolutionScale = EditorGUILayout.Slider(
                "Resolution Scale",
                settings.resolutionScale,
                0.25f,
                4f
            );
            settings.paddingPixels = EditorGUILayout.Slider(
                "Padding (px)",
                settings.paddingPixels,
                0f,
                64f
            );
            settings.useGraphicMaterial = EditorGUILayout.Toggle(
                "Use Graphic Material",
                settings.useGraphicMaterial
            );
            settings.flipVertically = EditorGUILayout.Toggle(
                "Flip Vertically",
                settings.flipVertically
            );

            if (EditorGUI.EndChangeCheck())
                SaveSettings();

            EditorGUILayout.LabelField("Output", GetSizeEstimate(bender, image));

            using (new EditorGUI.DisabledScope(!bender.HasDeformation))
            {
                if (GUILayout.Button("Bake To Sprite", GUILayout.Height(32f)))
                {
                    settings.applyToImage = true;
                    var baked = SpriteBendBaker.Bake(bender, settings, out var error);

                    if (baked == null)
                    {
                        EditorUtility.DisplayDialog("Sprite Bender", error, "OK");
                    }
                    else
                    {
                        Debug.Log(
                            "[SpriteBender] Baked "
                                + AssetDatabase.GetAssetPath(baked)
                                + " and assigned it to "
                                + image.name
                                + ".",
                            baked
                        );
                        EditorGUIUtility.PingObject(baked);

                        // Bake destroys the component, so stop drawing this inspector.
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (!bender.HasDeformation)
                EditorGUILayout.HelpBox("Nothing to bake: every bend value is zero.", MessageType.None);
        }

        private string GetSizeEstimate(SpriteBender bender, Image image)
        {
            var sourceRect = GetSourceRect(image);
            if (sourceRect.width <= Epsilon || sourceRect.height <= Epsilon)
                return "-";

            var bounds = GetDeformedBounds(bender, sourceRect);
            var center = sourceRect.center;
            var halfWidth = Mathf.Max(center.x - bounds.min.x, bounds.max.x - center.x);
            var halfHeight = Mathf.Max(center.y - bounds.min.y, bounds.max.y - center.y);

            var sprite = image.sprite;
            var density = sprite.rect.width / sourceRect.width * settings.resolutionScale;
            var padding = settings.paddingPixels;

            var width = Mathf.CeilToInt(halfWidth * 2f * density + padding * 2f);
            var height = Mathf.CeilToInt(halfHeight * 2f * density + padding * 2f);
            return "~" + width + " x " + height + " px";
        }

        private void OnSceneGui(SceneView sceneView)
        {
            var bender = target as SpriteBender;
            if (bender == null || !bender.enabled)
                return;

            var image = bender.GetComponent<Image>();
            if (image == null || image.sprite == null)
                return;

            var sourceRect = GetSourceRect(image);
            if (sourceRect.width <= Epsilon || sourceRect.height <= Epsilon)
                return;

            using (new Handles.DrawingScope(image.rectTransform.localToWorldMatrix))
            {
                DrawOutline(bender, sourceRect);
                DrawSceneHandles(bender, sourceRect);
            }
        }

        private void DrawSceneHandles(SpriteBender bender, Rect rect)
        {
            serializedObject.Update();
            var moved = false;

            var bendPoint = new Vector3(
                rect.center.x,
                rect.center.y + bendProperty.floatValue * rect.height,
                0f
            );
            if (DragHandle(ref bendPoint, Vector3.up, Color.cyan, "Bend"))
            {
                bendProperty.floatValue = Mathf.Clamp(
                    (bendPoint.y - rect.center.y) / rect.height,
                    -1f,
                    1f
                );
                moved = true;
            }

            var topCenter = new Vector2(rect.center.x, rect.yMax);
            var archOrigin = bender.Deform(topCenter, rect);
            var archPoint = new Vector3(archOrigin.x, archOrigin.y, 0f);
            if (DragHandle(ref archPoint, Vector3.up, Color.green, "Arch"))
            {
                archProperty.floatValue = Mathf.Clamp(
                    archProperty.floatValue + (archPoint.y - archOrigin.y) / rect.height,
                    -1f,
                    1f
                );
                moved = true;
            }

            var topRight = new Vector2(rect.xMax, rect.yMax);
            var taperOrigin = bender.Deform(topRight, rect);
            var taperPoint = new Vector3(taperOrigin.x, taperOrigin.y, 0f);
            if (DragHandle(ref taperPoint, Vector3.right, Color.yellow, "Taper"))
            {
                taperProperty.floatValue = Mathf.Clamp(
                    taperProperty.floatValue
                        + (taperPoint.x - taperOrigin.x) / (rect.width * 0.5f),
                    -1f,
                    1f
                );
                moved = true;
            }

            if (!moved)
                return;

            serializedObject.ApplyModifiedProperties();
            MarkTargetsDirty();
        }

        private static bool DragHandle(ref Vector3 point, Vector3 axis, Color color, string label)
        {
            var size = HandleUtility.GetHandleSize(point);
            Handles.color = color;
            Handles.Label(point + Vector3.right * size * 0.12f, label);

            EditorGUI.BeginChangeCheck();
            var dragged = Handles.Slider(point, axis, size * 0.09f, Handles.DotHandleCap, 0f);
            if (!EditorGUI.EndChangeCheck())
                return false;

            point = dragged;
            return true;
        }

        private static void DrawOutline(SpriteBender bender, Rect rect)
        {
            Handles.color = new Color(0.35f, 0.8f, 1f, 0.9f);

            DrawEdge(bender, rect, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin));
            DrawEdge(bender, rect, new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMax, rect.yMax));
            DrawEdge(bender, rect, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax));
            DrawEdge(bender, rect, new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax));
        }

        private static void DrawEdge(SpriteBender bender, Rect rect, Vector2 from, Vector2 to)
        {
            const int steps = 24;
            var points = new Vector3[steps + 1];

            for (var i = 0; i <= steps; i++)
            {
                var bent = bender.Deform(Vector2.Lerp(from, to, (float)i / steps), rect);
                points[i] = new Vector3(bent.x, bent.y, 0f);
            }

            Handles.DrawAAPolyLine(2f, points);
        }

        private static Bounds GetDeformedBounds(SpriteBender bender, Rect rect)
        {
            const int steps = 32;
            var bounds = new Bounds(bender.Deform(rect.min, rect), Vector3.zero);

            for (var i = 0; i <= steps; i++)
            {
                var t = (float)i / steps;
                var x = Mathf.Lerp(rect.xMin, rect.xMax, t);
                var y = Mathf.Lerp(rect.yMin, rect.yMax, t);

                bounds.Encapsulate(bender.Deform(new Vector2(x, rect.yMin), rect));
                bounds.Encapsulate(bender.Deform(new Vector2(x, rect.yMax), rect));
                bounds.Encapsulate(bender.Deform(new Vector2(rect.xMin, y), rect));
                bounds.Encapsulate(bender.Deform(new Vector2(rect.xMax, y), rect));
            }

            return bounds;
        }

        private static Rect GetSourceRect(Image image)
        {
            var quad = SpriteBendBaker.GetDrawnQuad(image);
            return Rect.MinMaxRect(quad.x, quad.y, quad.z, quad.w);
        }

        private void MarkTargetsDirty()
        {
            for (var i = 0; i < targets.Length; i++)
            {
                if (targets[i] is SpriteBender bender)
                    bender.MarkDirty();
            }
        }

        private void SaveSettings()
        {
            EditorPrefs.SetInt(SupersampleKey, settings.supersample);
            EditorPrefs.SetFloat(ResolutionKey, settings.resolutionScale);
            EditorPrefs.SetFloat(PaddingKey, settings.paddingPixels);
            EditorPrefs.SetBool(GraphicMaterialKey, settings.useGraphicMaterial);
            EditorPrefs.SetBool(FlipKey, settings.flipVertically);
        }

        private struct Preset
        {
            public string name;
            public float bend;
            public float arch;
            public float taper;
            public float wave;
            public float waveFrequency;
        }
    }
}
