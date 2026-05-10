using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Phase 4.3 — Applies <see cref="FishData.genome"/> to body mesh material and sprite overlays.
/// </summary>
public class FishRenderer : MonoBehaviour
{
    const int RibbonBodyShape = 6;
    const float RibbonFinCap = 1.5f;

    [SerializeField] MeshFilter bodyMeshFilter;
    [SerializeField] MeshRenderer bodyMeshRenderer;
    [SerializeField] Material fishPatternMaterialBase;
    [SerializeField] FishBodyMorpher bodyMorpher;
    [SerializeField] GeneLibrary geneLibrary;

    [Tooltip("Eight optional sprite layers, back → front.")]
    [SerializeField] SpriteRenderer[] layerRenderers = new SpriteRenderer[8];

    [Tooltip("tail_type phenotype 0..5")]
    [SerializeField] Sprite[] tailSpritesByType = new Sprite[6];

    [Tooltip("fin_shape phenotype 0..4 — pectoral / side fin sprites.")]
    [SerializeField] Sprite[] sideFinSpritesByShape = new Sprite[5];

    [SerializeField] int sideFinLayerIndex = 4;

    [FormerlySerializedAs("finLayerIndex")]
    [SerializeField] int tailLayerIndex = 5;

    [SerializeField] int eyeLayerIndex = 6;
    [SerializeField] int glowLayerIndex = 7;

    [SerializeField] Vector3 eyeBaseScale = new Vector3(0.6f, 0.6f, 1f);

    [FormerlySerializedAs("finBaseScale")]
    [SerializeField] Vector3 tailBaseScale = Vector3.one;

    [SerializeField] Vector3 sideFinBaseScale = new Vector3(0.85f, 0.85f, 1f);

    [Tooltip("Tail/fin tint multiplier applied on top of the body base colour.")]
    [SerializeField] Color tailFinTint = new Color(0.85f, 0.85f, 0.85f, 1f);

    [Tooltip("URP 2D: mesh render queue/sorting differs from sprites — keep above background.")]
    [SerializeField] string meshSortingLayerName = "Default";
    [SerializeField] int meshSortingOrder = 32;

    Mesh _generatedBodyMesh;
    MaterialPropertyBlock _materialBlock;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int PatternColorId = Shader.PropertyToID("_PatternColor");
    static readonly int PatternTypeId = Shader.PropertyToID("_PatternType");
    static readonly int PatternScaleId = Shader.PropertyToID("_PatternScale");
    static readonly int PatternContrastId = Shader.PropertyToID("_PatternContrast");
    static readonly int IridescenceId = Shader.PropertyToID("_Iridescence");
    static readonly int BioluminescenceId = Shader.PropertyToID("_Bioluminescence");
    static readonly int TransparencyId = Shader.PropertyToID("_Transparency");

    public FishData AppliedFish { get; private set; }

    public GeneLibrary GeneLibrary => geneLibrary;

    /// <summary>Allows runtime spawners to inject the library if it wasn't pre-wired on the prefab.</summary>
    public void SetGeneLibrary(GeneLibrary lib)
    {
        if (lib != null)
            geneLibrary = lib;
    }

    void Awake()
    {
        CacheBodyMeshComponents(false);
    }

    /// <summary>
    /// <see cref="Awake"/> does not run for editor-spawned fish until Play;
    /// this must run before any mesh/material writes (e.g. <see cref="TestFishSpawner"/>).
    /// </summary>
    void CacheBodyMeshComponents(bool dirtyAfterAssign)
    {
        if (bodyMeshFilter == null)
            bodyMeshFilter = GetComponent<MeshFilter>();

        if (bodyMeshRenderer == null)
            bodyMeshRenderer = GetComponent<MeshRenderer>();

        if (bodyMorpher == null)
            bodyMorpher = GetComponent<FishBodyMorpher>();

        ConfigureSortingLayerForMeshes();

        if (fishPatternMaterialBase != null && bodyMeshRenderer != null && bodyMeshRenderer.sharedMaterial == null)
        {
            bodyMeshRenderer.sharedMaterial = fishPatternMaterialBase;
#if UNITY_EDITOR
            if (dirtyAfterAssign)
                EditorUtility.SetDirty(bodyMeshRenderer);
#endif
        }
    }

    void OnDestroy()
    {
        ReleaseGeneratedMesh();
    }

    void ReleaseGeneratedMesh()
    {
        if (_generatedBodyMesh == null)
            return;

        if (Application.isPlaying)
            Destroy(_generatedBodyMesh);
        else
            DestroyImmediate(_generatedBodyMesh);

        _generatedBodyMesh = null;
    }

    /// <summary>Rebuild mesh + materials from phenotype values.</summary>
    public void ApplyGenome(FishData fish)
    {
        AppliedFish = fish;

        CacheBodyMeshComponents(!Application.isPlaying);

        ApplyGenomeSafely();

        FishAnimator animator = GetComponent<FishAnimator>();
        if (animator != null)
            animator.Bind(fish, geneLibrary);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
            if (bodyMeshRenderer != null)
                EditorUtility.SetDirty(bodyMeshRenderer);
            if (bodyMeshFilter != null)
                EditorUtility.SetDirty(bodyMeshFilter);
            if (gameObject.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    void ConfigureSortingLayerForMeshes()
    {
        if (bodyMeshRenderer == null)
            return;

        if (!string.IsNullOrEmpty(meshSortingLayerName))
        {
            int id = SortingLayer.NameToID(meshSortingLayerName);
            if (id >= 0)
                bodyMeshRenderer.sortingLayerID = id;
        }

        bodyMeshRenderer.sortingOrder = meshSortingOrder;
    }

    void ApplyGenomeSafely()
    {
        if (AppliedFish?.genome == null || geneLibrary == null)
            return;

        FishData fish = AppliedFish;

        GeneDefinition GeneDef(string id) => geneLibrary.GetGene(id);

        float P(string id)
        {
            GeneDefinition def = GeneDef(id);
            if (def == null)
                return 0f;
            return fish.genome.GetPhenotype(id, def);
        }

        int bodyDiscrete = Mathf.RoundToInt(P("body_shape"));
        float shapeT = Mathf.Clamp(P("body_shape"), 0f, 7f);
        float bodySize = P("body_size");

        ApplyBodyMesh(shapeT, bodySize);

        Color bodyBaseRgb = Color.white;

        if (fishPatternMaterialBase != null && bodyMeshRenderer != null)
        {
            // Always keep sharedMaterial set to the base asset — never touch
            // bodyMeshRenderer.material, which would clone a per-renderer copy
            // (leaks materials in edit mode and breaks SRP batching at runtime).
            if (bodyMeshRenderer.sharedMaterial == null || bodyMeshRenderer.sharedMaterial.shader != fishPatternMaterialBase.shader)
                bodyMeshRenderer.sharedMaterial = fishPatternMaterialBase;

            GeneDefinition hueDef = GeneDef("base_hue");
            GeneDefinition satDef = GeneDef("base_saturation");
            GeneDefinition valDef = GeneDef("base_value");

            float h = hueDef != null ? P("base_hue") : 0f;
            float s = satDef != null ? Mathf.Clamp01(P("base_saturation")) : 1f;
            float v = valDef != null ? Mathf.Clamp01(P("base_value")) : 1f;

            Color baseRgb = Color.HSVToRGB(h / 360f, s, v);
            bodyBaseRgb = baseRgb;

            float patternHueGene = hueDef != null ? P("pattern_hue") : 0f;
            float hueDiff = AngularHueDistance(patternHueGene, h);
            if (hueDiff > 30f && hueDiff < 60f)
                patternHueGene = Mathf.Repeat(h + 65f, 360f);

            float ps = GeneDef("pattern_type") != null ? Mathf.Clamp01(PatternSaturationDiscrete(P("pattern_type"))) : 1f;
            float pv = valDef != null ? Mathf.Clamp01(P("base_value")) * 0.95f + 0.05f : 1f;

            Color patternRgb = Color.HSVToRGB(patternHueGene / 360f, ps, pv);

            baseRgb.a = 1f;
            patternRgb.a = 1f;

            if (_materialBlock == null)
                _materialBlock = new MaterialPropertyBlock();

            bodyMeshRenderer.GetPropertyBlock(_materialBlock);
            _materialBlock.SetColor(BaseColorId, baseRgb);
            _materialBlock.SetColor(PatternColorId, patternRgb);

            GeneDefinition pattType = GeneDef("pattern_type");
            _materialBlock.SetFloat(PatternTypeId, pattType != null ? P("pattern_type") : 0f);

            GeneDefinition pattScaleDef = GeneDef("pattern_scale");
            _materialBlock.SetFloat(PatternScaleId, pattScaleDef != null ? P("pattern_scale") : 4f);

            GeneDefinition pattContrDef = GeneDef("pattern_contrast");
            _materialBlock.SetFloat(PatternContrastId, pattContrDef != null ? Mathf.Clamp01(P("pattern_contrast")) : 0.6f);

            // Gate the shimmer/glow so only fish with high genome rolls actually
            // animate — avoids every fish flickering colours.
            GeneDefinition iridDef = GeneDef("iridescence");
            float iridGated = iridDef != null
                ? Mathf.SmoothStep(0.45f, 0.95f, Mathf.Clamp01(P("iridescence")))
                : 0f;
            _materialBlock.SetFloat(IridescenceId, iridGated);

            GeneDefinition bioDef = GeneDef("bioluminescence");
            float bioGated = bioDef != null
                ? Mathf.SmoothStep(0.55f, 1.0f, Mathf.Clamp01(P("bioluminescence")))
                : 0f;
            _materialBlock.SetFloat(BioluminescenceId, bioGated);

            GeneDefinition transpDef = GeneDef("transparency");
            _materialBlock.SetFloat(
                TransparencyId,
                transpDef != null ? Mathf.Clamp(P("transparency"), transpDef.minValue, transpDef.maxValue) : 0f);

            bodyMeshRenderer.SetPropertyBlock(_materialBlock);
        }

        float fishFinLength = GeneDef("fin_length") != null ? P("fin_length") : 1f;
        if (bodyDiscrete == RibbonBodyShape)
            fishFinLength = Mathf.Min(fishFinLength, RibbonFinCap);

        // Re-anchor sprite layers to the actual body bounds — different
        // body_shape values produce dramatically different silhouettes, so
        // hard-coded child positions wouldn't follow the back of the fish.
        RepositionLayers();

        // Tints tails / fins darker than the body so they read as part of the
        // same fish but slightly recessed.
        Color tailFinColor = bodyBaseRgb * tailFinTint;
        tailFinColor.a = 1f;

        ApplyTailSprite(P("tail_type"), fishFinLength, tailFinColor);
        ApplySideFinSprite(P("fin_shape"), fishFinLength, tailFinColor);
        ApplyEyeSprite(P("eye_size"), P("eye_color_h"));
        ApplyGlow(P("bioluminescence"));
    }

    void RepositionLayers()
    {
        if (bodyMeshFilter == null || bodyMeshFilter.sharedMesh == null || layerRenderers == null)
            return;

        Bounds b = bodyMeshFilter.sharedMesh.bounds;
        float halfW = Mathf.Max(b.extents.x, 0.05f);
        float halfH = Mathf.Max(b.extents.y, 0.05f);
        float cx = b.center.x;
        float cy = b.center.y;

        SpriteRenderer tail = SafeLayer(tailLayerIndex);
        if (tail != null)
            tail.transform.localPosition = new Vector3(cx - halfW * 0.95f, cy, 0f);

        SpriteRenderer fin = SafeLayer(sideFinLayerIndex);
        if (fin != null)
            fin.transform.localPosition = new Vector3(cx + halfW * 0.10f, cy - halfH * 0.40f, 0f);

        SpriteRenderer eye = SafeLayer(eyeLayerIndex);
        if (eye != null)
            eye.transform.localPosition = new Vector3(cx + halfW * 0.65f, cy + halfH * 0.35f, 0f);

        SpriteRenderer glow = SafeLayer(glowLayerIndex);
        if (glow != null)
        {
            glow.transform.localPosition = new Vector3(cx, cy, 0f);
            // Glow scales to wrap the silhouette comfortably.
            glow.transform.localScale = new Vector3(halfW * 4.2f, halfH * 5.5f, 1f);
        }
    }

    SpriteRenderer SafeLayer(int idx)
    {
        if (layerRenderers == null || idx < 0 || idx >= layerRenderers.Length)
            return null;
        return layerRenderers[idx];
    }

    /// <summary>Palette helper: discrete pattern types vary secondary saturation somewhat.</summary>
    static float PatternSaturationDiscrete(float discretePattern)
    {
        int pi = Mathf.Clamp(Mathf.RoundToInt(discretePattern), 0, 8);
        return Mathf.Lerp(0.35f, 1f, 1f - pi / 8f * 0.25f);
    }

    static float AngularHueDistance(float a, float b)
    {
        float d = Mathf.Abs(Mathf.Repeat(a - b + 540f, 360f) - 180f);
        return d;
    }

    void ApplyBodyMesh(float bodyShapeGene, float bodySizeGene)
    {
        if (bodyMeshFilter == null || bodyMorpher == null)
            return;

        bodyMorpher.PrepareMorphMeshesForRuntime();

        ReleaseGeneratedMesh();

        _generatedBodyMesh = bodyMorpher.GetMorphedMesh(bodyShapeGene, Mathf.Max(bodySizeGene, 0.01f));
        if (_generatedBodyMesh != null)
            bodyMeshFilter.mesh = _generatedBodyMesh;
    }

    void ApplyTailSprite(float tailTypeGene, float finLengthGene, Color tint)
    {
        if (layerRenderers == null || tailSpritesByType == null || tailLayerIndex < 0 || tailLayerIndex >= layerRenderers.Length)
            return;

        SpriteRenderer tail = layerRenderers[tailLayerIndex];
        if (tail == null)
            return;

        int tt = Mathf.Clamp(Mathf.RoundToInt(tailTypeGene), 0, tailSpritesByType.Length - 1);
        if (tailSpritesByType.Length > tt && tailSpritesByType[tt] != null)
        {
            tail.sprite = tailSpritesByType[tt];
            tail.enabled = true;
        }

        tail.color = tint;
        Vector3 scl = tailBaseScale * Mathf.Lerp(0.7f, 1.5f, Mathf.InverseLerp(0.5f, 3f, finLengthGene));
        tail.transform.localScale = scl;
    }

    void ApplySideFinSprite(float finShapeGene, float finLengthGene, Color tint)
    {
        if (layerRenderers == null || sideFinSpritesByShape == null || sideFinLayerIndex < 0 || sideFinLayerIndex >= layerRenderers.Length)
            return;

        SpriteRenderer fin = layerRenderers[sideFinLayerIndex];
        if (fin == null)
            return;

        int fs = Mathf.Clamp(Mathf.RoundToInt(finShapeGene), 0, sideFinSpritesByShape.Length - 1);
        if (sideFinSpritesByShape.Length > fs && sideFinSpritesByShape[fs] != null)
        {
            fin.sprite = sideFinSpritesByShape[fs];
            fin.enabled = true;
        }

        // Side fins are slightly darker than the tail to suggest depth.
        Color sideTint = tint * 0.85f;
        sideTint.a = 1f;
        fin.color = sideTint;

        // Side fins scale modestly with fin_length so silhouette doesn't dominate the body.
        Vector3 scl = sideFinBaseScale * Mathf.Lerp(0.7f, 1.2f, Mathf.InverseLerp(0.5f, 3f, finLengthGene));
        fin.transform.localScale = scl;
    }

    void ApplyEyeSprite(float eyeSizeGene, float eyeHueGene)
    {
        if (layerRenderers == null || eyeLayerIndex < 0 || eyeLayerIndex >= layerRenderers.Length)
            return;

        SpriteRenderer eye = layerRenderers[eyeLayerIndex];
        if (eye == null)
            return;

        float t = Mathf.InverseLerp(0.3f, 1.8f, eyeSizeGene);
        eye.transform.localScale = eyeBaseScale * Mathf.Lerp(0.45f, 1.75f, t);

        Color c = Color.HSVToRGB(Mathf.Clamp(Mathf.Repeat(eyeHueGene, 360f), 0f, 360f) / 360f, 0.7f, 1f);
        eye.color = c;
    }

    void ApplyGlow(float bioGene)
    {
        if (layerRenderers == null || glowLayerIndex < 0 || glowLayerIndex >= layerRenderers.Length)
            return;

        SpriteRenderer glow = layerRenderers[glowLayerIndex];
        if (glow == null)
            return;

        // Match the gating used for the shader uniform so only high-trait fish glow.
        float a = Mathf.SmoothStep(0.55f, 1.0f, Mathf.Clamp01(bioGene));
        glow.enabled = a > 0.02f;

        Color c = Color.Lerp(Color.black, new Color(0.4f, 1f, 0.95f, 1f), a * 1.25f);
        c.a = Mathf.Clamp01(a * 0.85f);
        glow.color = c;
    }

    public SpriteRenderer Layer(int index)
    {
        if (layerRenderers == null || index < 0 || index >= layerRenderers.Length)
            return null;
        return layerRenderers[index];
    }
}
