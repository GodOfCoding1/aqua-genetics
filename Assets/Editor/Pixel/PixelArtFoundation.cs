using Aquarium.PixelArt;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Aquarium.PixelArt.EditorTools
{
    /// <summary>
    /// One-click bootstrap for the pixel-art system.
    /// Creates the <see cref="PixelArtSettings"/> and <see cref="FishPartLibrary"/>
    /// SOs, the shared <c>M_FishPalette</c> material, and configures the active
    /// camera with a <see cref="PixelPerfectCamera"/> sized to the settings.
    /// </summary>
    public static class PixelArtFoundation
    {
        // Assets live in Resources/ so they're discoverable at runtime in builds
        // (and via Resources.Load in FishCompositor.AutoLoadRefs).
        public const string SettingsFolder = "Assets/Resources/PixelArt";
        public const string SettingsAssetPath = SettingsFolder + "/PixelArtSettings.asset";
        public const string LibraryAssetPath = SettingsFolder + "/FishPartLibrary.asset";

        public const string MaterialFolder = "Assets/Resources/PixelArt";
        public const string PaletteMaterialPath = MaterialFolder + "/M_FishPalette.mat";

        // Legacy paths (pre-Resources). Foundation auto-migrates assets here
        // to the new Resources path if found, so existing projects don't lose
        // their library on first re-bootstrap.
        const string LegacySettingsPath = "Assets/ScriptableObjects/Pixel/PixelArtSettings.asset";
        const string LegacyLibraryPath = "Assets/ScriptableObjects/Pixel/FishPartLibrary.asset";
        const string LegacyMaterialPath = "Assets/Material/M_FishPalette.mat";

        public const string PaletteShaderName = "Aquarium/FishPalette";

        [MenuItem("Tools/Aquarium/Pixel Art/1. Bootstrap Pixel Art Foundation", false, 100)]
        public static void Bootstrap()
        {
            EnsureFolder(SettingsFolder);
            EnsureFolder(MaterialFolder);

            // Move pre-existing assets at legacy paths into Resources/ so any
            // references already wired into prefabs continue to resolve and
            // Resources.Load can find them at runtime.
            MigrateLegacyAsset(LegacySettingsPath, SettingsAssetPath);
            MigrateLegacyAsset(LegacyLibraryPath, LibraryAssetPath);
            MigrateLegacyAsset(LegacyMaterialPath, PaletteMaterialPath);

            PixelArtSettings settings = LoadOrCreate<PixelArtSettings>(SettingsAssetPath);
            FishPartLibrary library = LoadOrCreate<FishPartLibrary>(LibraryAssetPath);
            Material paletteMat = LoadOrCreatePaletteMaterial();

            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(library);
            if (paletteMat != null)
                EditorUtility.SetDirty(paletteMat);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EnsureSortingLayers();
            ConfigurePixelPerfectCamera(settings);

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);

            Debug.Log("Pixel Art Foundation: settings, library, palette material, sorting layers, and camera configured. " +
                      "Run 'Tools/Aquarium/Pixel Art/2. Generate Fish Part Sprites' next.");
        }

        public static PixelArtSettings GetOrCreateSettings()
        {
            EnsureFolder(SettingsFolder);
            return LoadOrCreate<PixelArtSettings>(SettingsAssetPath);
        }

        public static FishPartLibrary GetOrCreateLibrary()
        {
            EnsureFolder(SettingsFolder);
            return LoadOrCreate<FishPartLibrary>(LibraryAssetPath);
        }

        public static Material GetOrCreatePaletteMaterial()
        {
            EnsureFolder(MaterialFolder);
            return LoadOrCreatePaletteMaterial();
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static Material LoadOrCreatePaletteMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(PaletteMaterialPath);
            Shader shader = Shader.Find(PaletteShaderName);
            if (shader == null)
            {
                Debug.LogError($"Pixel Art Foundation: shader '{PaletteShaderName}' not found. Did the FishPalette.shader compile?");
                return existing;
            }

            if (existing != null)
            {
                if (existing.shader != shader)
                    existing.shader = shader;
                return existing;
            }

            var mat = new Material(shader) { name = "M_FishPalette" };
            AssetDatabase.CreateAsset(mat, PaletteMaterialPath);
            return mat;
        }

        static void EnsureSortingLayers()
        {
            // Configures named sorting layers used by the compositor + environment generator.
            // Idempotent - existing layers preserved.
            string[] required = { "BackgroundFar", "Background", "Plants", "Fish", "Effects", "Glass", "Foreground" };

            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("m_SortingLayers");
            if (layers == null || !layers.isArray)
                return;

            var usedIds = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty elem = layers.GetArrayElementAtIndex(i);
                string n = elem.FindPropertyRelative("name").stringValue;
                int uid = elem.FindPropertyRelative("uniqueID").intValue;
                if (n == "Default")
                    continue;
                if (uid != 0)
                    usedIds.Add(uid);
            }

            // Repair corrupted TagManager rows where uniqueID was left at 0 (same as Default).
            // NameToID then returns 0 for those layers and FishCompositor skipped assigning them.
            bool repaired = false;
            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty elem = layers.GetArrayElementAtIndex(i);
                string n = elem.FindPropertyRelative("name").stringValue;
                if (string.IsNullOrEmpty(n) || n == "Default")
                    continue;
                SerializedProperty idProp = elem.FindPropertyRelative("uniqueID");
                if (idProp.intValue != 0)
                    continue;
                int nid = NextSortingLayerUniqueId(usedIds);
                idProp.intValue = nid;
                usedIds.Add(nid);
                repaired = true;
            }

            // Build set of existing names.
            var existing = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < layers.arraySize; i++)
                existing.Add(layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue);

            bool added = false;
            foreach (string name in required)
            {
                if (existing.Contains(name))
                    continue;
                layers.arraySize++;
                SerializedProperty newLayer = layers.GetArrayElementAtIndex(layers.arraySize - 1);
                newLayer.FindPropertyRelative("name").stringValue = name;
                newLayer.FindPropertyRelative("uniqueID").intValue = NextSortingLayerUniqueId(usedIds);
                usedIds.Add(newLayer.FindPropertyRelative("uniqueID").intValue);
                added = true;
            }

            if (added || repaired)
                tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Unity sorting-layer unique IDs must be non-zero except for the built-in Default layer.
        /// Random.Range(int.MinValue, int.MaxValue) can theoretically yield 0 and duplicates.
        /// </summary>
        static int NextSortingLayerUniqueId(System.Collections.Generic.HashSet<int> used)
        {
            for (int attempt = 0; attempt < 256; attempt++)
            {
                int id = Random.Range(1, int.MaxValue);
                if (!used.Contains(id))
                    return id;
            }

            unchecked
            {
                int h = (int)System.DateTime.UtcNow.Ticks;
                if (h == 0) h = 173863741;
                while (used.Contains(h) || h == 0)
                    h++;
                return h;
            }
        }

        static void ConfigurePixelPerfectCamera(PixelArtSettings settings)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("Pixel Art Foundation: no Main Camera in active scene; skipping pixel-perfect setup. Open SampleScene to configure.");
                return;
            }

            Undo.RecordObject(cam.gameObject, "Configure Pixel Perfect Camera");

            cam.orthographic = true;
            cam.backgroundColor = new Color(0.04f, 0.07f, 0.12f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            // Aquarium-tank sized; matches PixelAquariumSetup tank dimensions.
            // Reference resolution = world size * PPU.
            float tankWorldWidth = PixelAquariumSetup.TankHalfWidth * 2f;
            float tankWorldHeight = PixelAquariumSetup.TankHalfHeight * 2f;
            int refWidth = Mathf.RoundToInt(tankWorldWidth * settings.pixelsPerUnit);
            int refHeight = Mathf.RoundToInt(tankWorldHeight * settings.pixelsPerUnit);

            PixelPerfectCamera ppc = cam.GetComponent<PixelPerfectCamera>();
            if (ppc == null)
                ppc = Undo.AddComponent<PixelPerfectCamera>(cam.gameObject);

            ppc.assetsPPU = settings.pixelsPerUnit;
            ppc.refResolutionX = refWidth;
            ppc.refResolutionY = refHeight;
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
            ppc.cropFrame = PixelPerfectCamera.CropFrame.None;

            // Make sure orthographic size matches the tank height so non-pixel-perfect viewport still frames the tank.
            cam.orthographicSize = PixelAquariumSetup.TankHalfHeight + 0.4f;

            EditorUtility.SetDirty(cam);
            if (ppc != null)
                EditorUtility.SetDirty(ppc);
        }

        static void MigrateLegacyAsset(string fromPath, string toPath)
        {
            if (string.Equals(fromPath, toPath, System.StringComparison.OrdinalIgnoreCase))
                return;
            if (AssetDatabase.LoadMainAssetAtPath(fromPath) == null)
                return;
            if (AssetDatabase.LoadMainAssetAtPath(toPath) != null)
                return; // Already at destination, leave legacy duplicate to user to delete.

            string error = AssetDatabase.MoveAsset(fromPath, toPath);
            if (!string.IsNullOrEmpty(error))
                Debug.LogWarning($"Pixel Art Foundation: could not migrate '{fromPath}' -> '{toPath}': {error}");
            else
                Debug.Log($"Pixel Art Foundation: migrated '{fromPath}' -> '{toPath}'.");
        }

        public static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string parent = "Assets";
            foreach (string part in folder.Split('/'))
            {
                if (part == "Assets")
                    continue;
                string next = $"{parent}/{part}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(parent, part);
                parent = next;
            }
        }
    }
}
