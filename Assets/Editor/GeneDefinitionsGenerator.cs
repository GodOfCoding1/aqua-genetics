using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click creation of Phase 2.4 <see cref="GeneDefinition"/> assets and a populated <see cref="GeneLibrary"/>.
/// </summary>
public static class GeneDefinitionsGenerator
{
    const string GenesFolder = "Assets/ScriptableObjects/Genes/GeneDefinitions";
    const string LibraryAssetPath = "Assets/ScriptableObjects/Genes/DefaultGeneLibrary.asset";

    /// <summary>Design doc leaves σ blank for discrete traits; kept for inspector consistency.</summary>
    const float DiscreteMutationSigmaPlaceholder = 0.08f;

    sealed class GeneSpec
    {
        public readonly string GeneId;
        public readonly string DisplayName;
        public readonly GeneType Type;
        public readonly float MinValue;
        public readonly float MaxValue;
        public readonly int DiscreteStates;
        public readonly float MutationSigma;

        public GeneSpec(string geneId, string displayName, GeneType type,
            float minValue, float maxValue, int discreteStates, float mutationSigma)
        {
            GeneId = geneId;
            DisplayName = displayName;
            Type = type;
            MinValue = minValue;
            MaxValue = maxValue;
            DiscreteStates = discreteStates;
            MutationSigma = mutationSigma;
        }
    }

    static GeneSpec D(string id, string title, int stateCount) =>
        new GeneSpec(id, title, GeneType.Discrete, 0f, Mathf.Max(0, stateCount - 1), stateCount,
            DiscreteMutationSigmaPlaceholder);

    static GeneSpec C(string id, string title, float minValue, float maxValue, float mutationSigma) =>
        new GeneSpec(id, title, GeneType.Continuous, minValue, maxValue, 0, mutationSigma);

    static readonly GeneSpec[] Phase24Genes =
    {
        D("body_shape", "Body shape", 8),
        C("body_size", "Body size", 0.4f, 2.0f, 0.08f),
        D("tail_type", "Tail type", 6),
        D("fin_count", "Fin count", 3),
        C("fin_length", "Fin length", 0.5f, 3.0f, 0.10f),
        D("fin_shape", "Fin shape", 5),
        C("eye_size", "Eye size", 0.3f, 1.8f, 0.06f),
        C("eye_color_h", "Eye hue", 0f, 360f, 15.0f),
        D("lip_type", "Lip type", 4),
        C("base_hue", "Base hue", 0f, 360f, 20.0f),
        C("base_saturation", "Base saturation", 0f, 1f, 0.07f),
        C("base_value", "Base value (brightness)", 0.3f, 1f, 0.06f),
        D("pattern_type", "Pattern type", 9),
        C("pattern_hue", "Pattern hue", 0f, 360f, 18.0f),
        C("pattern_scale", "Pattern scale", 0.2f, 2.0f, 0.09f),
        C("pattern_contrast", "Pattern contrast", 0f, 1f, 0.07f),
        C("iridescence", "Iridescence", 0f, 1f, 0.05f),
        C("bioluminescence", "Bioluminescence", 0f, 1f, 0.04f),
        C("transparency", "Transparency", 0f, 0.6f, 0.04f),
        C("temperament", "Temperament", -1f, 1f, 0.08f),
        D("swim_style", "Swim style", 5),
        C("depth_preference", "Depth preference", 0f, 1f, 0.06f),
        C("school_tendency", "School tendency", 0f, 1f, 0.07f),
        C("lifespan", "Lifespan", 0.5f, 2.0f, 0.06f),
        C("fertility", "Fertility", 0f, 1f, 0.06f),
        C("hardiness", "Hardiness", 0f, 1f, 0.05f),
        C("growth_rate", "Growth rate", 0.5f, 2.0f, 0.07f),
        D("rarity_class", "Rarity class", 5),
    };

    [MenuItem("Tools/Aquarium/Generate All Gene Definitions (Phase 2.4)")]
    public static void Generate()
    {
        EnsureFolderExists(GenesFolder);

        var created = new List<GeneDefinition>();
        try
        {
            foreach (GeneSpec spec in Phase24Genes)
            {
                string path = $"{GenesFolder}/{PascalCase(spec.GeneId)}.asset";
                var def = AssetDatabase.LoadAssetAtPath<GeneDefinition>(path);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<GeneDefinition>();
                    AssetDatabase.CreateAsset(def, path);
                }

                ApplySpec(def, spec);
                EditorUtility.SetDirty(def);
                created.Add(def);
            }

            EnsureFolderExists("Assets/ScriptableObjects/Genes");
            var library = AssetDatabase.LoadAssetAtPath<GeneLibrary>(LibraryAssetPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<GeneLibrary>();
                AssetDatabase.CreateAsset(library, LibraryAssetPath);
            }

            AssignGenesToLibrary(library, created);
            EditorUtility.SetDirty(library);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = library;
            Debug.Log($"Gene definitions: {created.Count} assets in '{GenesFolder}'. Library: '{LibraryAssetPath}'.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"GeneDefinitionsGenerator failed: {ex.Message}\n{ex}");
        }
    }

    static void AssignGenesToLibrary(GeneLibrary library, List<GeneDefinition> orderedGenes)
    {
        var so = new SerializedObject(library);
        SerializedProperty prop = so.FindProperty("genes");
        if (prop == null || !prop.isArray)
        {
            Debug.LogError("GeneLibrary: could not find serialized field 'genes'.");
            return;
        }

        prop.ClearArray();
        for (int i = 0; i < orderedGenes.Count; i++)
        {
            prop.InsertArrayElementAtIndex(i);
            prop.GetArrayElementAtIndex(i).objectReferenceValue = orderedGenes[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void ApplySpec(GeneDefinition def, GeneSpec spec)
    {
        def.geneId = spec.GeneId;
        def.displayName = spec.DisplayName;
        def.type = spec.Type;
        def.dominanceWeight = spec.Type == GeneType.Discrete ? 0.5f : 0f;
        def.isMutationSensitive = false;
        def.mutationSigma = spec.MutationSigma;

        if (spec.Type == GeneType.Discrete)
        {
            def.discreteStates = Mathf.Max(1, spec.DiscreteStates);
            def.minValue = 0f;
            def.maxValue = def.discreteStates - 1;
        }
        else
        {
            def.discreteStates = 0;
            def.minValue = spec.MinValue;
            def.maxValue = spec.MaxValue;
        }
    }

    static void EnsureFolderExists(string assetPath)
    {
        assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(assetPath))
            return;

        string parent = "Assets";
        foreach (string part in assetPath.Split('/'))
        {
            if (part == "Assets")
                continue;
            string next = $"{parent}/{part}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(parent, part);
            parent = next;
        }
    }

    static string PascalCase(string snake)
    {
        var sb = new StringBuilder();
        bool upper = true;
        foreach (char c in snake)
        {
            if (c == '_')
            {
                upper = true;
                continue;
            }

            sb.Append(upper ? char.ToUpper(c, CultureInfo.InvariantCulture) : c);
            upper = false;
        }

        return sb.Length > 0 ? sb.ToString() : "Gene";
    }
}
