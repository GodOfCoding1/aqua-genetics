using System;
using UnityEngine;

/// <summary>
/// For prototyping without spawning eggs or crafting Prefabs by hand:
/// builds random <see cref="FishData"/> and pushes genomics onto <see cref="FishRenderer"/>.
/// Toggle off when gameplay assigns genome explicitly (egg hatch, tank roster load-out).
/// </summary>
public class FishPrototypeBootstrap : MonoBehaviour
{
    [SerializeField] GeneLibrary geneLibrary;

    [Tooltip("Off once breeders/UI/other pipes inject genome.")]
    [SerializeField] bool applyRandomGenomeOnStart = true;

    [SerializeField] bool registerWithFishLifecycleManager = true;

    FishData _prototypeFish;

    void Start()
    {
        if (!applyRandomGenomeOnStart)
            return;

        GeneLibrary lib = geneLibrary != null ? geneLibrary : FishLifecycleManager.Instance != null ? FishLifecycleManager.Instance.GeneLibrary : null;

        if (lib == null)
        {
            Debug.LogWarning($"{nameof(FishPrototypeBootstrap)} ({gameObject.name}): assign Gene Library or put FishLifecycleManager + library in scene.", this);
            return;
        }

        FishRenderer visuals = GetComponent<FishRenderer>();
        if (visuals == null)
            return;

        FishGenome g = lib.GenerateRandomGenome();

        DateTime born = DateTime.UtcNow;

        var fish = new FishData
        {
            genome = g,
            birthTime = born,
            isAlive = true,
            stage = FishLifeStage.Juvenile,
            generationNumber = 1,
            currentAge = 0.15f
        };

        fish.stage = FishData.StageForNormalizedAge(fish.currentAge);
        fish.lineageTag = fish.GenerateLineageTag(lib);

        _prototypeFish = fish;

        visuals.ApplyGenome(fish);

        if (registerWithFishLifecycleManager && FishLifecycleManager.Instance != null && fish.isAlive)
            FishLifecycleManager.Instance.RegisterFish(fish);
    }

    /// <summary>Expose so tests or callers can toggle without modifying serialized fields.</summary>
    public void SetGeneLibrary(GeneLibrary lib)
    {
        geneLibrary = lib;
    }

    public FishData PrototypeFishRecord => _prototypeFish;
}
