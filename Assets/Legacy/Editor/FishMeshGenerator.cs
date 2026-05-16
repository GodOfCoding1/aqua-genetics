using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-side baker that materialises the runtime <see cref="FishBodyMeshBuilder"/>
/// silhouettes as on-disk .asset files. The morpher will use the runtime
/// builder as a fallback regardless, so this is purely an optimisation /
/// "ship the meshes with the project" step.
/// </summary>
public static class FishMeshGenerator
{
    public const string MeshFolder = "Assets/Meshes/Fish";
    public const int ShapeCount = FishBodyMeshBuilder.ShapeCount;

    [MenuItem("Tools/Aquarium/Legacy/Bake Body Meshes", false, 900)]
    public static void GenerateAll()
    {
        EnsureFolder(MeshFolder);
        for (int s = 0; s < ShapeCount; s++)
            BakeShapeAsset(s);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Aquarium: baked {ShapeCount} fish body meshes into '{MeshFolder}/'.");
    }

    public static Mesh LoadOrBake(int shapeIndex)
    {
        EnsureFolder(MeshFolder);
        string path = AssetPath(shapeIndex);
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
            return existing;

        BakeShapeAsset(shapeIndex);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }

    public static string AssetPath(int shapeIndex)
        => $"{MeshFolder}/Body_{Mathf.Clamp(shapeIndex, 0, ShapeCount - 1)}.asset";

    static void BakeShapeAsset(int shapeIndex)
    {
        Mesh fresh = FishBodyMeshBuilder.Build(shapeIndex);
        string path = AssetPath(shapeIndex);

        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            existing.Clear();
            existing.indexFormat = fresh.indexFormat;
            existing.SetVertices(fresh.vertices);
            existing.SetUVs(0, fresh.uv);
            existing.SetUVs(1, fresh.uv2);
            existing.SetTriangles(fresh.triangles, 0);
            existing.RecalculateNormals();
            existing.RecalculateBounds();
            existing.name = fresh.name;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(fresh);
        }
        else
        {
            AssetDatabase.CreateAsset(fresh, path);
        }
    }

    public static Mesh BuildBodyMesh(int shapeIndex) => FishBodyMeshBuilder.Build(shapeIndex);

    static void EnsureFolder(string folder)
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
