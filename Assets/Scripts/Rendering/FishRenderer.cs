using Aquarium.PixelArt;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Facade between gameplay code and the pixel-art composition pipeline.
/// Keeps the public surface (<see cref="ApplyGenome(FishData)"/>,
/// <see cref="SetGeneLibrary(GeneLibrary)"/>, <see cref="AppliedFish"/>) so
/// existing spawners (<c>FishSpawnService</c>, <c>FishPrototypeBootstrap</c>)
/// don't need to change.
///
/// Internally it owns a <see cref="FishCompositor"/> + optional
/// <see cref="PixelFishAnimator"/> on the same GameObject.
/// </summary>
[DisallowMultipleComponent]
public class FishRenderer : MonoBehaviour
{
    [SerializeField] FishCompositor compositor;
    [SerializeField] GeneLibrary geneLibrary;

    public FishData AppliedFish { get; private set; }

    public GeneLibrary GeneLibrary => geneLibrary;

    public FishCompositor Compositor
    {
        get
        {
            if (compositor == null)
                compositor = GetComponent<FishCompositor>() ?? gameObject.AddComponent<FishCompositor>();
            return compositor;
        }
    }

    public void SetGeneLibrary(GeneLibrary lib)
    {
        if (lib != null)
            geneLibrary = lib;
    }

    void Awake()
    {
        EnsureCompositor(false);
    }

    void Reset()
    {
        EnsureCompositor(false);
    }

    void EnsureCompositor(bool dirtyAfterAssign)
    {
        if (compositor == null)
        {
            compositor = GetComponent<FishCompositor>();
            if (compositor == null)
                compositor = gameObject.AddComponent<FishCompositor>();
#if UNITY_EDITOR
            if (dirtyAfterAssign)
                EditorUtility.SetDirty(this);
#endif
        }
        compositor.EnsureSlots();
    }

    /// <summary>Rebuild the visual fish from the genome in <paramref name="fish"/>.</summary>
    public void ApplyGenome(FishData fish)
    {
        AppliedFish = fish;
        EnsureCompositor(!Application.isPlaying);

        if (compositor != null && geneLibrary != null && fish?.genome != null)
            compositor.Apply(fish, geneLibrary);

        FishAnimator animator = GetComponent<FishAnimator>();
        if (animator != null)
            animator.Bind(fish, geneLibrary);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }
}
