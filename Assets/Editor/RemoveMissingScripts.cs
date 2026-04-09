using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using UnityEditor.SceneManagement;

public static class RemoveMissingScripts
{
    [MenuItem("Tools/Cleanup/Remove Missing Scripts In Open Scenes")]
    public static void RemoveInOpenScenes()
    {
        int totalRemoved = 0;
        int goCount = 0;

        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            var scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var go in GetAllChildren(root))
                {
                    totalRemoved += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                    goCount++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log($"Done. Checked {goCount} objects, removed {totalRemoved} missing scripts.");
    }

    [MenuItem("Tools/Cleanup/Remove Missing Scripts In Selected Objects")]
    public static void RemoveInSelected()
    {
        int totalRemoved = 0;
        int goCount = 0;

        foreach (var root in Selection.gameObjects)
        {
            foreach (var go in GetAllChildren(root))
            {
                totalRemoved += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                goCount++;
            }
        }

        Debug.Log($"Done. Checked {goCount} objects, removed {totalRemoved} missing scripts.");
    }

    private static IEnumerable<GameObject> GetAllChildren(GameObject root)
    {
        var stack = new Stack<Transform>();
        stack.Push(root.transform);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current.gameObject;

            for (int i = 0; i < current.childCount; i++)
                stack.Push(current.GetChild(i));
        }
    }
}