using Aquarium.PixelArt;
using UnityEditor;
using UnityEngine;

namespace Aquarium.PixelArt.EditorTools
{
    /// <summary>
    /// Migrates a fish prefab from the legacy mesh-based pipeline to the
    /// modular pixel-art compositor pipeline. Removes MeshFilter / MeshRenderer
    /// / FishBodyMorpher + the legacy child sprite overlays (Glow / Tail /
    /// SideFin / Eye), then ensures <see cref="FishCompositor"/> and
    /// <see cref="PixelFishAnimator"/> are present and wired to the
    /// foundation assets.
    /// </summary>
    public static class PixelFishPrefabConverter
    {
        public const string PrototypePrefabPath = "Assets/Prefabs/Fish/PrototypeFish.prefab";

        // Names of legacy child overlays we need to strip — anything matching
        // gets removed because the compositor will recreate its own
        // PixelPart_* children on demand.
        static readonly string[] LegacyChildNames = { "Glow", "Tail", "SideFin", "Eye" };

        [MenuItem("Tools/Aquarium/Pixel Art/3. Convert PrototypeFish Prefab", false, 120)]
        public static void Convert()
        {
            ConvertPrefab(PrototypePrefabPath);
        }

        public static void ConvertPrefab(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"PixelFishPrefabConverter: prefab not found at '{prefabPath}'.");
                return;
            }

            string contentsPath = AssetDatabase.GetAssetPath(prefab);
            GameObject root = PrefabUtility.LoadPrefabContents(contentsPath);
            try
            {
                ConvertGameObject(root);
                PrefabUtility.SaveAsPrefabAsset(root, contentsPath);
                Debug.Log($"PixelFishPrefabConverter: '{prefabPath}' converted to pixel-art compositor pipeline.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void ConvertGameObject(GameObject root)
        {
            if (root == null)
                return;

            // 1. Remove legacy mesh body components.
            RemoveComponent<MeshRenderer>(root);
            RemoveComponent<MeshFilter>(root);
            RemoveComponentByTypeName(root, "FishBodyMorpher");

            // 2. Remove legacy sprite overlay children (Glow / Tail / SideFin / Eye).
            //    The compositor manages its own child sprite renderers under
            //    PixelPart_* names; mixing them with legacy children would
            //    confuse the slot binding.
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = root.transform.GetChild(i);
                string n = child.name;
                bool isLegacy = false;
                foreach (string legacyName in LegacyChildNames)
                {
                    if (n == legacyName) { isLegacy = true; break; }
                }
                if (isLegacy)
                    Object.DestroyImmediate(child.gameObject);
            }

            // 3. Ensure compositor + animator components exist.
            FishCompositor compositor = root.GetComponent<FishCompositor>();
            if (compositor == null)
                compositor = root.AddComponent<FishCompositor>();

            PixelFishAnimator pixelAnim = root.GetComponent<PixelFishAnimator>();
            if (pixelAnim == null)
                root.AddComponent<PixelFishAnimator>();

            // 4. Wire compositor refs from the foundation assets.
            PixelArtSettings settings = PixelArtFoundation.GetOrCreateSettings();
            FishPartLibrary library = PixelArtFoundation.GetOrCreateLibrary();
            Material paletteMat = PixelArtFoundation.GetOrCreatePaletteMaterial();

            SerializedObject so = new SerializedObject(compositor);
            SetObjectRef(so, "settings", settings);
            SetObjectRef(so, "partLibrary", library);
            SetObjectRef(so, "paletteMaterialBase", paletteMat);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Build child slot renderers immediately so the prefab YAML
            // captures them.
            compositor.EnsureSlots();

            // 5. Wire FishRenderer's serialized fields if present.
            FishRenderer fr = root.GetComponent<FishRenderer>();
            if (fr != null)
            {
                SerializedObject frSo = new SerializedObject(fr);
                SetObjectRef(frSo, "compositor", compositor);
                // geneLibrary is left alone — already wired from the legacy version.
                frSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // 6. Re-wire FishPicker if present.
            FishPicker picker = root.GetComponent<FishPicker>();
            if (picker != null)
            {
                SerializedObject pSo = new SerializedObject(picker);
                SetObjectRef(pSo, "compositor", compositor);
                SetObjectRef(pSo, "fishRenderer", fr);
                FishAnimator anim = root.GetComponent<FishAnimator>();
                SetObjectRef(pSo, "fishAnimator", anim);
                pSo.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(root);
        }

        static void RemoveComponent<T>(GameObject go) where T : Component
        {
            T comp = go.GetComponent<T>();
            if (comp != null)
                Object.DestroyImmediate(comp, true);
        }

        static void RemoveComponentByTypeName(GameObject go, string typeName)
        {
            // FishBodyMorpher etc. live in the global namespace; Type lookup
            // by name keeps this util resilient if those classes are removed
            // entirely in Phase 5.
            System.Type t = System.Type.GetType($"{typeName}, Assembly-CSharp");
            if (t == null)
                return;
            Component comp = go.GetComponent(t);
            if (comp != null)
                Object.DestroyImmediate(comp, true);
        }

        static void SetObjectRef(SerializedObject so, string fieldName, Object value)
        {
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"PixelFishPrefabConverter: serialized field '{fieldName}' not found on {so.targetObject.GetType().Name}.");
                return;
            }
            prop.objectReferenceValue = value;
        }
    }
}
