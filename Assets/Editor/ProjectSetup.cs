using UnityEditor;
using UnityEngine;

public static class ProjectSetup
{
    static readonly string[] Folders =
    {
        "Assets/Scripts/Genetics",
        "Assets/Scripts/Fish",
        "Assets/Scripts/Rendering",
        "Assets/Scripts/GameLoop",
        "Assets/Scripts/UI",
        "Assets/Scripts/Services",
        "Assets/ScriptableObjects/Genes",
        "Assets/Shaders",
        "Assets/Prefabs/Fish",
        "Assets/Prefabs/UI",
        "Assets/Scenes",
    };

    [MenuItem("Tools/Setup Project")]
    public static void SetupProject()
    {
        foreach (var path in Folders)
            EnsureAssetFolderExists(path);

        AssetDatabase.Refresh();
        Debug.Log("Project folders created or already present.");
    }

    static void EnsureAssetFolderExists(string assetPath)
    {
        assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(assetPath))
            return;

        int lastSlash = assetPath.LastIndexOf('/');
        if (lastSlash <= 0)
            return;

        string parent = assetPath[..lastSlash];
        string folderName = assetPath[(lastSlash + 1)..];

        EnsureAssetFolderExists(parent);

        if (!AssetDatabase.IsValidFolder(assetPath))
            AssetDatabase.CreateFolder(parent, folderName);
    }
}
