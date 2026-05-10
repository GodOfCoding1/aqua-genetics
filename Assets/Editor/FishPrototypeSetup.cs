using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click Phase-4 prototype builder: creates the PrototypeFish GameObject /
/// prefab with procedural body meshes, child sprite layers (glow / tail /
/// side fin / eye), <see cref="FishPicker"/> collider, the pattern material
/// and a randomized-genome bootstrap.
/// </summary>
public static class FishPrototypeSetup
{
    const string GeneLibraryAssetPath = "Assets/ScriptableObjects/Genes/DefaultGeneLibrary.asset";
    const string MaterialsFolder = "Assets/Materials/Aquarium";
    const string MaterialAssetPath = MaterialsFolder + "/M_FishPattern.mat";
    const string PrefabFolder = "Assets/Prefabs/Fish";
    const string PrefabAssetPath = PrefabFolder + "/PrototypeFish.prefab";

    const int BodyMeshSortingOrder = 32;
    const int GlowSortingOrder = BodyMeshSortingOrder - 4;
    const int TailSortingOrder = BodyMeshSortingOrder - 2;
    const int SideFinSortingOrder = BodyMeshSortingOrder + 2;
    const int EyeSortingOrder = BodyMeshSortingOrder + 4;

    [MenuItem("Tools/Aquarium/Setup Prototype Fish In Scene", false, 210)]
    public static void SetupPrototypeFishInScene()
    {
        Undo.IncrementCurrentGroup();

        Shader shader = Shader.Find("Aquarium/FishPattern");
        if (shader == null)
        {
            EditorUtility.DisplayDialog(
                "Fish Shader Missing",
                " Shader Aquarium/FishPattern was not found. Generate it first:\nTools → Aquarium → Generate Fish Pattern Shader\n\nThen retry this menu item.",
                "OK");
            return;
        }

        GeneLibrary library = AssetDatabase.LoadAssetAtPath<GeneLibrary>(GeneLibraryAssetPath);
        if (library == null)
            Debug.LogWarning($"FishPrototypeSetup: GeneLibrary not at {GeneLibraryAssetPath}. Assign manually on Fish Renderer / Bootstrap.");

        Material material = LoadOrCreateFishMaterial(shader);

        // Bake body silhouettes if they're missing so this menu works on a fresh project.
        Mesh[] bodyMeshes = LoadOrBakeBodyMeshes();
        Mesh defaultBody = bodyMeshes[0] != null ? bodyMeshes[0] : AquariumMeshUtility.CloneReadableUnitQuad();

        Sprite[] tails = LoadOrBakeTailSprites();
        Sprite[] sideFins = LoadOrBakeFinSprites();
        Sprite eye = FishSpriteGenerator.LoadOrBakeEye();
        Sprite glow = FishSpriteGenerator.LoadOrBakeGlow();

        GameObject root = new GameObject("PrototypeFish");
        Undo.RegisterCreatedObjectUndo(root, "Setup Prototype Fish");
        Undo.SetTransformParent(root.transform, null, "Fish parenting");

        MeshFilter mf = root.AddComponent<MeshFilter>();
        MeshRenderer mr = root.AddComponent<MeshRenderer>();
        mf.sharedMesh = defaultBody;
        mr.sharedMaterial = material;
        mr.sortingLayerName = "Default";
        mr.sortingOrder = BodyMeshSortingOrder;

        FishBodyMorpher morpher = root.AddComponent<FishBodyMorpher>();
        AssignBodyMeshes(morpher, bodyMeshes);

        FishRenderer fishRenderer = root.AddComponent<FishRenderer>();

        // Picker + auto-fitting trigger collider.
        BoxCollider2D box = root.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        FishPicker picker = root.AddComponent<FishPicker>();

        // Build the four sprite layer children (glow back, eye front).
        SpriteRenderer glowRenderer = CreateSpriteChild(root.transform, "Glow", glow,
            new Vector3(0f, 0f, 0f), Vector3.one * 1.6f, GlowSortingOrder);
        glowRenderer.color = new Color(0.4f, 1f, 0.95f, 0f);
        glowRenderer.enabled = false;

        SpriteRenderer tailRenderer = CreateSpriteChild(root.transform, "Tail", tails != null && tails.Length > 0 ? tails[0] : null,
            new Vector3(-0.42f, 0f, 0f), Vector3.one, TailSortingOrder);

        SpriteRenderer sideFinRenderer = CreateSpriteChild(root.transform, "SideFin", sideFins != null && sideFins.Length > 0 ? sideFins[0] : null,
            new Vector3(0.05f, -0.04f, 0f), Vector3.one, SideFinSortingOrder);

        SpriteRenderer eyeRenderer = CreateSpriteChild(root.transform, "Eye", eye,
            new Vector3(0.27f, 0.06f, 0f), Vector3.one, EyeSortingOrder);

        SpriteRenderer[] layers = new SpriteRenderer[8];
        layers[4] = sideFinRenderer;
        layers[5] = tailRenderer;
        layers[6] = eyeRenderer;
        layers[7] = glowRenderer;

        SerializedObject soFishRenderer = new SerializedObject(fishRenderer);
        soFishRenderer.FindProperty("fishPatternMaterialBase").objectReferenceValue = material;
        soFishRenderer.FindProperty("geneLibrary").objectReferenceValue = library;
        soFishRenderer.FindProperty("bodyMorpher").objectReferenceValue = morpher;
        soFishRenderer.FindProperty("bodyMeshFilter").objectReferenceValue = mf;
        soFishRenderer.FindProperty("bodyMeshRenderer").objectReferenceValue = mr;

        AssignSpriteArray(soFishRenderer, "tailSpritesByType", tails);
        AssignSpriteArray(soFishRenderer, "sideFinSpritesByShape", sideFins);
        AssignSpriteRendererArray(soFishRenderer, "layerRenderers", layers);

        soFishRenderer.ApplyModifiedPropertiesWithoutUndo();

        root.AddComponent<FishAnimator>();

        FishPrototypeBootstrap bootstrap = root.AddComponent<FishPrototypeBootstrap>();
        SerializedObject soBoot = new SerializedObject(bootstrap);
        soBoot.FindProperty("geneLibrary").objectReferenceValue = library;
        soBoot.ApplyModifiedPropertiesWithoutUndo();

        root.transform.position = Vector3.zero;

        // Initial fit so the picker collider matches the default body silhouette.
        if (picker != null)
            picker.FitToMesh();

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);

        Debug.Log("Aquarium: Created PrototypeFish with procedural body / fins / eye and randomized genetics on Play (FishPrototypeBootstrap).");
        Undo.SetCurrentGroupName("Aquarium Setup Prototype Fish");
    }

    [MenuItem("Tools/Aquarium/Save Prototype Fish Prefab", false, 211)]
    public static void SavePrototypeFishPrefab()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null ||
            selected.GetComponent<FishRenderer>() == null ||
            selected.GetComponent<FishBodyMorpher>() == null)
        {
            EditorUtility.DisplayDialog(
                "Prototype Prefab",
                "Select the GameObject that already has FishRenderer + FishBodyMorpher (e.g. the one from Setup Prototype Fish In Scene), then run this again.",
                "OK");
            return;
        }

        EnsureFolder(PrefabFolder);
        string assetPath = PrefabAssetPath;

        GameObject asset = PrefabUtility.SaveAsPrefabAsset(selected, assetPath);
        if (asset != null)
        {
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"Aquarium: Saved prefab to {assetPath}");
        }
    }

    static SpriteRenderer CreateSpriteChild(Transform parent, string name, Sprite sprite,
        Vector3 localPos, Vector3 localScale, int sortingOrder)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Add Fish Sprite Layer");
        Undo.SetTransformParent(go.transform, parent, "Fish sprite layer parenting");
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = sortingOrder;
        return sr;
    }

    static Material LoadOrCreateFishMaterial(Shader shader)
    {
        EnsureFolder(MaterialsFolder);
        var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialAssetPath);
        if (existing != null && existing.shader == shader)
            return existing;

        var mat = new Material(shader) { name = "M_FishPattern" };
        AssetDatabase.CreateAsset(mat, MaterialAssetPath);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static Mesh[] LoadOrBakeBodyMeshes()
    {
        Mesh[] meshes = new Mesh[FishMeshGenerator.ShapeCount];
        bool anyMissing = false;
        for (int i = 0; i < meshes.Length; i++)
        {
            meshes[i] = AssetDatabase.LoadAssetAtPath<Mesh>(FishMeshGenerator.AssetPath(i));
            if (meshes[i] == null)
                anyMissing = true;
        }

        if (anyMissing)
        {
            FishMeshGenerator.GenerateAll();
            for (int i = 0; i < meshes.Length; i++)
                meshes[i] = AssetDatabase.LoadAssetAtPath<Mesh>(FishMeshGenerator.AssetPath(i));
        }

        return meshes;
    }

    static Sprite[] LoadOrBakeTailSprites()
    {
        Sprite[] s = new Sprite[FishSpriteGenerator.TailCount];
        for (int i = 0; i < s.Length; i++)
            s[i] = FishSpriteGenerator.LoadOrBakeTail(i);
        return s;
    }

    static Sprite[] LoadOrBakeFinSprites()
    {
        Sprite[] s = new Sprite[FishSpriteGenerator.FinCount];
        for (int i = 0; i < s.Length; i++)
            s[i] = FishSpriteGenerator.LoadOrBakeFin(i);
        return s;
    }

    static void AssignBodyMeshes(FishBodyMorpher morpher, Mesh[] meshes)
    {
        SerializedObject so = new SerializedObject(morpher);
        SerializedProperty prop = so.FindProperty("bodyMeshes");
        prop.arraySize = 8;
        for (int i = 0; i < 8; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = i < meshes.Length ? meshes[i] : meshes[0];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void AssignSpriteArray(SerializedObject so, string propName, Sprite[] sprites)
    {
        SerializedProperty prop = so.FindProperty(propName);
        if (prop == null || sprites == null)
            return;
        prop.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }

    static void AssignSpriteRendererArray(SerializedObject so, string propName, SpriteRenderer[] renderers)
    {
        SerializedProperty prop = so.FindProperty(propName);
        if (prop == null || renderers == null)
            return;
        prop.arraySize = renderers.Length;
        for (int i = 0; i < renderers.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
    }

    static void EnsureFolder(string assetPath)
    {
        assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(assetPath))
            return;

        string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        string name = Path.GetFileName(assetPath);
        if (!string.IsNullOrEmpty(parent))
            EnsureFolder(parent);
        if (!AssetDatabase.IsValidFolder(assetPath))
            AssetDatabase.CreateFolder(parent, name);
    }
}
