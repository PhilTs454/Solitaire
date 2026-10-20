using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Solitaire.Editor
{
    /// <summary>
    /// Creates a URP asset with a 2D Renderer and assigns it in Graphics/Quality settings.
    /// Uses reflection so this file compiles before the URP package finishes importing.
    /// </summary>
    public static class Urp2dBootstrap
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string PipelinePath = "Assets/Settings/UniversalRP.asset";
        private const string RendererPath = "Assets/Settings/Renderer2D.asset";

        [MenuItem("Tools/Solitaire/Setup URP 2D")]
        public static void Setup()
        {
            var renderer2dType = FindType("UnityEngine.Rendering.Universal.Renderer2DData");
            var pipelineType = FindType(
                "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"
            );

            if (renderer2dType == null || pipelineType == null)
            {
                EditorUtility.DisplayDialog(
                    "URP Missing",
                    "Universal RP is not imported yet.\n\n"
                        + "1) Wait for Package Manager to finish\n"
                        + "2) Fix any compile errors\n"
                        + "3) Run Tools → Solitaire → Setup URP 2D again",
                    "OK"
                );
                return;
            }

            EnsureSettingsFolder();

            var rendererData = LoadOrCreateAsset(renderer2dType, RendererPath);
            var pipelineAsset = LoadOrCreateAsset(pipelineType, PipelinePath);

            WireRendererList(pipelineAsset, rendererData);

            GraphicsSettings.renderPipelineAsset =
                pipelineAsset as RenderPipelineAsset;
            QualitySettings.renderPipeline = pipelineAsset as RenderPipelineAsset;

            EditorUtility.SetDirty(pipelineAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[Urp2dBootstrap] Assigned UniversalRP + Renderer2D. "
                    + "Check Edit → Project Settings → Graphics."
            );
            EditorUtility.DisplayDialog(
                "URP 2D Ready",
                "Universal RP (2D Renderer) is assigned.\n\n"
                    + "Next: Enter Play Mode and confirm cards/UI still render.",
                "OK"
            );
        }

        [MenuItem("Tools/Solitaire/Setup URP 2D", true)]
        private static bool ValidateSetup()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void EnsureSettingsFolder()
        {
            if (AssetDatabase.IsValidFolder(SettingsFolder))
                return;

            AssetDatabase.CreateFolder("Assets", "Settings");
        }

        private static ScriptableObject LoadOrCreateAsset(Type type, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath(path, type) as ScriptableObject;
            if (existing != null)
                return existing;

            var asset = ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void WireRendererList(
            ScriptableObject pipelineAsset,
            ScriptableObject rendererData
        )
        {
            var so = new SerializedObject(pipelineAsset);
            var rendererDataList = so.FindProperty("m_RendererDataList");
            if (rendererDataList == null)
            {
                Debug.LogError(
                    "[Urp2dBootstrap] Could not find m_RendererDataList on URP asset."
                );
                return;
            }

            rendererDataList.arraySize = 1;
            rendererDataList.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;

            var defaultIndex = so.FindProperty("m_DefaultRendererIndex");
            if (defaultIndex != null)
                defaultIndex.intValue = 0;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName);
                if (type != null)
                    return type;
            }

            return null;
        }
    }
}
