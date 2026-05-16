using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Aquarium.PixelArt.EditorTools
{
    /// <summary>
    /// One-click setup for the full pixel-art pipeline. Equivalent to running
    /// every menu under Tools/Aquarium/Pixel Art in order, plus generating the
    /// gene library if it doesn't exist.
    /// </summary>
    public static class PixelAquariumQuickStart
    {
        const string GeneLibraryAssetPath = "Assets/ScriptableObjects/Genes/DefaultGeneLibrary.asset";

        [MenuItem("Tools/Aquarium/Pixel Art/0. Build Everything (Pixel Art)", false, 90)]
        public static void BuildEverything()
        {
            Undo.IncrementCurrentGroup();

            // 0. Gene library (genome layer is unchanged from legacy pipeline).
            if (AssetDatabase.LoadAssetAtPath<GeneLibrary>(GeneLibraryAssetPath) == null)
                GeneDefinitionsGenerator.Generate();

            // 1. Foundation: settings SO, library SO, palette material, sorting layers, pixel-perfect camera.
            PixelArtFoundation.Bootstrap();

            // 2. Procedural pixel-art parts (bodies, tails, fins, eyes, mouths).
            PixelFishGenerator.GenerateAll();

            // 3. Convert the existing PrototypeFish prefab to use the new compositor.
            PixelFishPrefabConverter.Convert();

            // 4. Pixel-art aquarium environment (water, gravel, plants, glass, bubbles).
            PixelAquariumSetup.Setup();

            // 5. Clean any orphan legacy fish from previous runs.
            RemoveOrphanFishGameObjects();
            RemoveTestFishRoot();

            // 6. UI (legacy HUD remains for now — pixel UI is Phase 6).
            AquariumUiSetup.Setup();

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);

            Undo.SetCurrentGroupName("Aquarium · Pixel Art Build Everything");
            Debug.Log("Pixel Aquarium: full pipeline built. Press Play and use the HUD's 'Spawn Random Fish' button.");
        }

        static void RemoveTestFishRoot()
        {
            GameObject root = GameObject.Find("Aquarium_TestFish_Root");
            if (root != null)
                Undo.DestroyObjectImmediate(root);
        }

        static void RemoveOrphanFishGameObjects()
        {
            FishRenderer[] all = Object.FindObjectsByType<FishRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (FishRenderer fr in all)
            {
                if (fr == null) continue;
                if (PrefabUtility.IsPartOfPrefabAsset(fr.gameObject)) continue;
                if (fr.transform.parent == null)
                    Undo.DestroyObjectImmediate(fr.gameObject);
            }
        }
    }
}
