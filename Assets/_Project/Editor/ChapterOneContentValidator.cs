using System;
using System.Linq;
using CozyFoodFactory.Buildings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CozyFoodFactory.Editor
{
    public static class ChapterOneContentValidator
    {
        [MenuItem("Tools/Cozy Food Factory/Validate Chapter 1")]
        public static void Validate()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Demo.unity");
            try
            {
                var controllers = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<BuildingPlacementController>(true)).ToArray();
                if (controllers.Length != 1)
                    throw new InvalidOperationException("Demo requires exactly one BuildingPlacementController.");
                var errors = controllers[0].ValidateChapterOneContent();
                foreach (string error in errors) Debug.LogError("Chapter 1: " + error);
                if (errors.Count == 0)
                    Debug.Log("Chapter 1 content validation passed: identities, recipes and stage reachability. " +
                        "Production time, capacity, layout, economics and objective quantities require playtesting.");
            }
            catch (Exception exception) { Debug.LogError("Chapter 1 validation failed: " + exception.Message); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
