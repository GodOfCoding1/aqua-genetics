using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-click setup for first-time use: generates the shader, bakes the body
/// meshes + sprites, builds the prototype prefab, configures the aquarium
/// environment, creates the HUD UI, and spawns a fresh batch of test fish.
/// </summary>
public static class AquariumQuickStart
{
    const string GeneLibraryAssetPath = "Assets/ScriptableObjects/Genes/DefaultGeneLibrary.asset";

    [MenuItem("Tools/Aquarium/Build Everything", false, 100)]
    public static void BuildEverything()
    {
        Undo.IncrementCurrentGroup();

        // 0. Make sure the gene library exists (generates 28 GeneDefinition assets if not).
        if (AssetDatabase.LoadAssetAtPath<GeneLibrary>(GeneLibraryAssetPath) == null)
            GeneDefinitionsGenerator.Generate();

        // 1. Pattern shader.
        if (Shader.Find("Aquarium/FishPattern") == null)
            FishShaderGenerator.GenerateFishPatternShader();

        // 2. Body meshes.
        FishMeshGenerator.GenerateAll();

        // 3. Eye / tail / fin / glow sprites.
        FishSpriteGenerator.GenerateAll();

        // 4. Pre-clean any standalone fish (and the old TestFish root) from
        //    previous runs so the scene starts empty — we now rely on the
        //    HUD's "Spawn Random Fish" button to populate the tank.
        RemoveOrphanFishGameObjects();
        RemoveTestFishRoot();

        // 5. Build the prototype fish in-scene + save the prefab.
        FishPrototypeSetup.SetupPrototypeFishInScene();
        FishPrototypeSetup.SavePrototypeFishPrefab();

        // The in-scene prototype is no longer needed (runtime spawning
        // instances the saved prefab directly).
        GameObject scenePrototype = GameObject.Find("PrototypeFish");
        if (scenePrototype != null)
            Undo.DestroyObjectImmediate(scenePrototype);

        // 6. Aquarium environment (background, gravel, glass, bubbles, camera).
        AquariumSceneSetup.Setup();

        // 7. HUD + inspector UI (the HUD's button spawns fish at runtime).
        AquariumUiSetup.Setup();

        // Persist scene changes.
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.IsValid())
            EditorSceneManager.MarkSceneDirty(scene);

        Undo.SetCurrentGroupName("Aquarium · Build Everything");
        Debug.Log("Aquarium: Build Everything complete. Press Play and use the 'Spawn Random Fish' button.");
    }

    /// <summary>Removes the editor TestFish root if it's hanging around from a previous build.</summary>
    static void RemoveTestFishRoot()
    {
        GameObject root = GameObject.Find("Aquarium_TestFish_Root");
        if (root != null)
            Undo.DestroyObjectImmediate(root);
    }

    /// <summary>
    /// Removes every top-level GameObject in the active scene that carries a
    /// <see cref="FishRenderer"/>. Called before re-running setup so we don't
    /// keep a pile of stale prototype fish from previous runs.
    /// </summary>
    static void RemoveOrphanFishGameObjects()
    {
        FishRenderer[] all = Object.FindObjectsByType<FishRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (FishRenderer fr in all)
        {
            if (fr == null)
                continue;

            Transform t = fr.transform;
            // Skip prefab assets (root.parent will be null for instances and assets;
            // PrefabUtility.IsPartOfPrefabAsset filters the asset case).
            if (PrefabUtility.IsPartOfPrefabAsset(fr.gameObject))
                continue;

            // Only remove top-level GameObjects so we don't kill children of
            // already-organised roots like Aquarium_TestFish_Root (those are
            // cleaned up inside TestFishSpawner.SpawnTestFish).
            if (t.parent == null)
                Undo.DestroyObjectImmediate(t.gameObject);
        }
    }
}
