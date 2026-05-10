using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Spawns playable test fish under a dedicated scene root so you get immediate visual genetics feedback.
/// </summary>
public static class TestFishSpawner
{
    public const string TestFishRootName = "Aquarium_TestFish_Root";

    const string GeneLibraryAssetPath = "Assets/ScriptableObjects/Genes/DefaultGeneLibrary.asset";
    const string DefaultPrefabPath = "Assets/Prefabs/Fish/PrototypeFish.prefab";

    const float AreaHalfWidth = 5f;
    const float AreaHalfHeight = 3f;

    [MenuItem("Tools/Aquarium/Spawn Test Fish", false, 220)]
    public static void SpawnTestFish()
    {
        GeneLibrary lib = AssetDatabase.LoadAssetAtPath<GeneLibrary>(GeneLibraryAssetPath);
        if (lib == null)
        {
            EditorUtility.DisplayDialog(
                "Aquarium · Spawn Test Fish",
                $"Gene library not found at:\n{GeneLibraryAssetPath}\n\nAdjust the path in TestFishSpawner.cs or regenerate assets.",
                "OK");
            return;
        }

        GameObject prefab = ResolveFishPrefab(out string prefabUsedPath);
        if (prefab == null)
            return;

        Transform rootTransform = EnsureTestFishRootUndo();

        // Fresh batch: eight fish only (avoid stacking on repeated Spawn).
        for (int c = rootTransform.childCount - 1; c >= 0; c--)
            Undo.DestroyObjectImmediate(rootTransform.GetChild(c).gameObject);

        for (int i = 0; i < 8; i++)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, rootTransform) as GameObject;
            if (instance == null)
                continue;

            Undo.RegisterCreatedObjectUndo(instance, "Spawn Test Fish");

            float px = UnityEngine.Random.Range(-AreaHalfWidth, AreaHalfWidth);
            float py = UnityEngine.Random.Range(-AreaHalfHeight, AreaHalfHeight);
            instance.transform.localPosition = new Vector3(px, py, 0f);

            StripPrototypeBootstrap(instance);

            FishRenderer renderer = instance.GetComponent<FishRenderer>();
            EnsureGeneLibrary(renderer, lib);

            FishData fish = CreateRandomFishData(lib);

            if (renderer != null)
                renderer.ApplyGenome(fish);

            if (Application.isPlaying && FishLifecycleManager.Instance != null)
                FishLifecycleManager.Instance.RegisterFish(fish);

            if (instance.GetComponent<FishAnimator>() == null)
                Debug.LogWarning($"Aquarium · '{instance.name}' has no FishAnimator (optional for motion). Prefab path: {prefabUsedPath}", instance);
        }

        Selection.activeGameObject = rootTransform.gameObject;
        SceneView.FrameLastActiveSceneView();
        Debug.Log($"Aquarium · Spawned 8 test fish using prefab '{prefabUsedPath}' under '{TestFishRootName}'. SceneView framed root.");
    }

    [MenuItem("Tools/Aquarium/Clear Test Fish", false, 221)]
    public static void ClearTestFish()
    {
        Transform rootTransform = ResolveTestFishRoot();
        if (rootTransform == null)
            return;

        Undo.DestroyObjectImmediate(rootTransform.gameObject);
        Debug.Log($"Aquarium · Removed '{TestFishRootName}'.");
    }

    [MenuItem("Tools/Aquarium/Clear Test Fish", true)]
    public static bool ClearTestFishValidate()
        => ResolveTestFishRoot() != null;

    static GameObject ResolveFishPrefab(out string pathUsed)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPrefabPath);
        if (prefab != null)
        {
            pathUsed = DefaultPrefabPath;
            return prefab;
        }

        // Fall back: prefab asset selected in the Project window.
        if (Selection.activeObject is GameObject sel && PrefabUtility.IsPartOfPrefabAsset(sel))
        {
            string assetPath = AssetDatabase.GetAssetPath(sel);
            if (!string.IsNullOrEmpty(assetPath))
            {
                pathUsed = assetPath;
                return sel;
            }
        }

        pathUsed = null;
        EditorUtility.DisplayDialog(
            "Aquarium · Spawn Test Fish",
            $"No fish prefab at:\n{DefaultPrefabPath}\n\nRun Tools → Aquarium → Setup Prototype Fish In Scene, then Tools → Aquarium → Save Prototype Fish Prefab,\n—or select a Fish prefab in Project and retry.",
            "OK");
        return null;
    }

    static FishData CreateRandomFishData(GeneLibrary lib)
    {
        return FishSpawnService.CreateRandomFishData(lib);
    }

    static Transform EnsureTestFishRootUndo()
    {
        Transform existing = ResolveTestFishRoot();
        if (existing != null)
            return existing;

        var go = new GameObject(TestFishRootName);
        Undo.RegisterCreatedObjectUndo(go, "Create Test Fish Root");
        Undo.SetTransformParent(go.transform, null, "Aquarium fish root parenting");
        return go.transform;
    }

    /// <returns>Transforms parent of pooled test instances, else null.</returns>
    static Transform ResolveTestFishRoot()
    {
        GameObject go = GameObject.Find(TestFishRootName);
        return go != null ? go.transform : null;
    }

    static void StripPrototypeBootstrap(GameObject instance)
    {
        FishPrototypeBootstrap boot = instance.GetComponent<FishPrototypeBootstrap>();
        if (boot != null)
            Undo.DestroyObjectImmediate(boot);
    }

    static void EnsureGeneLibrary(FishRenderer renderer, GeneLibrary lib)
    {
        if (renderer == null || lib == null)
            return;

        SerializedObject so = new SerializedObject(renderer);
        SerializedProperty prop = so.FindProperty("geneLibrary");
        if (prop.objectReferenceValue == null)
            prop.objectReferenceValue = lib;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
