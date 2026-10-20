using System.Collections.Generic;
using System.Linq;
using Solitaire.Models;
using Solitaire.Presenters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Solitaire.Editor
{
    /// <summary>
    /// Recreates Stock/Waste/Foundation/Tableau piles after a wiped Pile.prefab
    /// left the Game scene with Missing Prefab instances.
    /// </summary>
    public static class FixMissingPilePrefabs
    {
        private const string PilePrefabPath = "Assets/Prefabs/Game/Pile.prefab";
        private const string GameScenePath = "Assets/Scenes/Game.unity";

        private struct PileSpec
        {
            public string Name;
            public Pile.PileType Type;
            public Pile.CardArrangement Arrangement;
            public Vector3 Portrait;
            public Vector3 Landscape;
        }

        // Matches the pre-breakage Game scene layout.
        private static readonly PileSpec[] Specs =
        {
            new PileSpec
            {
                Name = "Stock",
                Type = Pile.PileType.Stock,
                Arrangement = Pile.CardArrangement.Stack,
                Portrait = new Vector3(3.45f, 5.25f, 0f),
                Landscape = new Vector3(6.45f, 3f, 0f)
            },
            new PileSpec
            {
                Name = "Waste",
                Type = Pile.PileType.Waste,
                Arrangement = Pile.CardArrangement.TopThree,
                Portrait = new Vector3(1.7f, 5.25f, 0f),
                Landscape = new Vector3(4.7f, 3f, 0f)
            },
            new PileSpec
            {
                Name = "Foundation1",
                Type = Pile.PileType.Foundation,
                Arrangement = Pile.CardArrangement.Stack,
                Portrait = new Vector3(-3.45f, 5.25f, 0f),
                Landscape = new Vector3(5.3f, 1f, 0f)
            },
            new PileSpec
            {
                Name = "Foundation2",
                Type = Pile.PileType.Foundation,
                Arrangement = Pile.CardArrangement.Stack,
                Portrait = new Vector3(-2.3f, 5.25f, 0f),
                Landscape = new Vector3(6.45f, 1f, 0f)
            },
            new PileSpec
            {
                Name = "Foundation3",
                Type = Pile.PileType.Foundation,
                Arrangement = Pile.CardArrangement.Stack,
                Portrait = new Vector3(-1.15f, 5.25f, 0f),
                Landscape = new Vector3(5.3f, -1f, 0f)
            },
            new PileSpec
            {
                Name = "Foundation4",
                Type = Pile.PileType.Foundation,
                Arrangement = Pile.CardArrangement.Stack,
                Portrait = new Vector3(0f, 5.25f, 0f),
                Landscape = new Vector3(6.45f, -1f, 0f)
            },
            new PileSpec
            {
                Name = "Tableau1",
                Type = Pile.PileType.Tableau,
                Arrangement = Pile.CardArrangement.Waterfall,
                Portrait = new Vector3(-3.45f, 3f, 0f),
                Landscape = new Vector3(-3.45f, 3f, 0f)
            },
            new PileSpec
            {
                Name = "Tableau2",
                Type = Pile.PileType.Tableau,
                Arrangement = Pile.CardArrangement.Waterfall,
                Portrait = new Vector3(-2.3f, 3f, 0f),
                Landscape = new Vector3(-2.3f, 3f, 0f)
            },
            new PileSpec
            {
                Name = "Tableau3",
                Type = Pile.PileType.Tableau,
                Arrangement = Pile.CardArrangement.Waterfall,
                Portrait = new Vector3(-1.15f, 3f, 0f),
                Landscape = new Vector3(-1.15f, 3f, 0f)
            },
            new PileSpec
            {
                Name = "Tableau4",
                Type = Pile.PileType.Tableau,
                Arrangement = Pile.CardArrangement.Waterfall,
                Portrait = new Vector3(0f, 3f, 0f),
                Landscape = new Vector3(0f, 3f, 0f)
            },
            new PileSpec
            {
                Name = "Tableau5",
                Type = Pile.PileType.Tableau,
                Arrangement = Pile.CardArrangement.Waterfall,
                Portrait = new Vector3(1.15f, 3f, 0f),
                Landscape = new Vector3(1.15f, 3f, 0f)
            },
            new PileSpec
            {
                Name = "Tableau6",
                Type = Pile.PileType.Tableau,
                Arrangement = Pile.CardArrangement.Waterfall,
                Portrait = new Vector3(2.3f, 3f, 0f),
                Landscape = new Vector3(2.3f, 3f, 0f)
            },
            new PileSpec
            {
                Name = "Tableau7",
                Type = Pile.PileType.Tableau,
                Arrangement = Pile.CardArrangement.Waterfall,
                Portrait = new Vector3(3.45f, 3f, 0f),
                Landscape = new Vector3(3.45f, 3f, 0f)
            }
        };

        [MenuItem("Solitaire/Fix Missing Pile Prefabs")]
        public static void Fix()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PilePrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Fix Missing Piles",
                    $"Could not load {PilePrefabPath}. Restore that prefab first.",
                    "OK"
                );
                return;
            }

            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            var presenter = Object.FindObjectOfType<GamePresenter>();
            if (presenter == null)
            {
                EditorUtility.DisplayDialog(
                    "Fix Missing Piles",
                    "No GamePresenter in the Game scene.",
                    "OK"
                );
                return;
            }

            Undo.SetCurrentGroupName("Fix Missing Pile Prefabs");
            var undoGroup = Undo.GetCurrentGroup();

            // Remove broken / duplicate pile objects by name.
            var names = new HashSet<string>(Specs.Select(s => s.Name));
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!names.Contains(root.name))
                    continue;

                // Keep healthy live PilePresenters; destroy Missing Prefab shells and dupes.
                var pile = root.GetComponent<PilePresenter>();
                if (pile == null || PrefabUtility.IsPrefabAssetMissing(root))
                {
                    Undo.DestroyObjectImmediate(root);
                }
            }

            // Also destroy any leftover missing-prefab roots that still claim these names.
            for (var i = scene.GetRootGameObjects().Length - 1; i >= 0; i--)
            {
                var root = scene.GetRootGameObjects()[i];
                if (names.Contains(root.name) && PrefabUtility.IsPrefabAssetMissing(root))
                    Undo.DestroyObjectImmediate(root);
            }

            var created = new Dictionary<string, PilePresenter>();
            foreach (var spec in Specs)
            {
                // Reuse existing healthy instance if present.
                var existing = Object
                    .FindObjectsOfType<PilePresenter>()
                    .FirstOrDefault(p => p != null && p.gameObject.name == spec.Name);
                if (existing != null && !PrefabUtility.IsPrefabAssetMissing(existing.gameObject))
                {
                    ApplySpec(existing, spec);
                    created[spec.Name] = existing;
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Create " + spec.Name);
                instance.name = spec.Name;
                instance.transform.position = spec.Portrait;
                instance.transform.localScale = Vector3.one * 0.45f;

                var pilePresenter = instance.GetComponent<PilePresenter>();
                if (pilePresenter == null)
                {
                    Debug.LogError($"[FixMissingPilePrefabs] Prefab missing PilePresenter on {spec.Name}");
                    Object.DestroyImmediate(instance);
                    continue;
                }

                ApplySpec(pilePresenter, spec);
                created[spec.Name] = pilePresenter;
            }

            WireGamePresenter(presenter, created);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Undo.CollapseUndoOperations(undoGroup);

            EditorUtility.DisplayDialog(
                "Fix Missing Piles",
                $"Recreated/wired {created.Count}/13 piles and saved Game.unity.\nEnter Play Mode and try New Game.",
                "OK"
            );
        }

        private static void ApplySpec(PilePresenter pilePresenter, PileSpec spec)
        {
            var so = new SerializedObject(pilePresenter);
            so.FindProperty("Type").enumValueIndex = (int)spec.Type;
            so.FindProperty("Arrangement").enumValueIndex = (int)spec.Arrangement;
            so.FindProperty("PosPortrait").vector3Value = spec.Portrait;
            so.FindProperty("PosLandscape").vector3Value = spec.Landscape;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pilePresenter);
        }

        private static void WireGamePresenter(
            GamePresenter presenter,
            Dictionary<string, PilePresenter> created
        )
        {
            var so = new SerializedObject(presenter);
            so.FindProperty("_pileStock").objectReferenceValue = created["Stock"];
            so.FindProperty("_pileWaste").objectReferenceValue = created["Waste"];

            var foundations = so.FindProperty("_pileFoundations");
            foundations.arraySize = 4;
            foundations.GetArrayElementAtIndex(0).objectReferenceValue = created["Foundation1"];
            foundations.GetArrayElementAtIndex(1).objectReferenceValue = created["Foundation2"];
            foundations.GetArrayElementAtIndex(2).objectReferenceValue = created["Foundation3"];
            foundations.GetArrayElementAtIndex(3).objectReferenceValue = created["Foundation4"];

            var tableaus = so.FindProperty("_pileTableaus");
            tableaus.arraySize = 7;
            tableaus.GetArrayElementAtIndex(0).objectReferenceValue = created["Tableau1"];
            tableaus.GetArrayElementAtIndex(1).objectReferenceValue = created["Tableau2"];
            tableaus.GetArrayElementAtIndex(2).objectReferenceValue = created["Tableau3"];
            tableaus.GetArrayElementAtIndex(3).objectReferenceValue = created["Tableau4"];
            tableaus.GetArrayElementAtIndex(4).objectReferenceValue = created["Tableau5"];
            tableaus.GetArrayElementAtIndex(5).objectReferenceValue = created["Tableau6"];
            tableaus.GetArrayElementAtIndex(6).objectReferenceValue = created["Tableau7"];

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presenter);
        }
    }
}
