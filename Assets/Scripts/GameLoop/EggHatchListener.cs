using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Subscribes to <see cref="BreedingManager.OnEggReady"/>, builds a <see cref="FishData"/> from the slot,
/// then releases the slot. Add to the same scene as <see cref="BreedingManager"/>.
/// </summary>
public class EggHatchListener : MonoBehaviour
{
    [Tooltip("Used for lineage names and parent generation (optional if FishLifecycleManager has a library).")]
    [SerializeField] GeneLibrary geneLibrary;

    [Tooltip("Runtime-only list for debugging in the Inspector during Play Mode.")]
    [SerializeField] List<FishData> hatchedFish = new List<FishData>();

    BreedingManager _breedingManager;

    public IReadOnlyList<FishData> HatchedFish => hatchedFish;

    void Start()
    {
        _breedingManager = BreedingManager.Instance;
        if (_breedingManager == null)
        {
            Debug.LogWarning("EggHatchListener: no BreedingManager in scene (Instance is null).");
            return;
        }

        _breedingManager.OnEggReady += OnEggReady;
    }

    void OnDestroy()
    {
        if (_breedingManager != null)
            _breedingManager.OnEggReady -= OnEggReady;
    }

    void OnEggReady(BreedingSlot slot)
    {
        if (slot == null || slot.pendingOffspring == null)
            return;

        GeneLibrary lib = geneLibrary != null ? geneLibrary : FishLifecycleManager.Instance != null
            ? FishLifecycleManager.Instance.GeneLibrary
            : null;

        DateTime born = DateTime.UtcNow;
        var fish = new FishData
        {
            fishId = Guid.NewGuid().ToString("N"),
            genome = FishGenome.Deserialize(slot.pendingOffspring.Serialize()),
            isHypermutant = slot.pendingOffspringIsHypermutant,
            birthTime = born,
            isAlive = true,
            stage = FishLifeStage.Egg,
            currentAge = 0f
        };

        if (slot.parentA != null)
            fish.parentAId = slot.parentA.fishId;
        if (slot.parentB != null)
            fish.parentBId = slot.parentB.fishId;

        int gen = 0;
        if (slot.parentA != null)
            gen = Mathf.Max(gen, slot.parentA.generationNumber);
        if (slot.parentB != null)
            gen = Mathf.Max(gen, slot.parentB.generationNumber);
        fish.generationNumber = gen + 1;

        if (lib != null)
            fish.lineageTag = fish.GenerateLineageTag(lib);
        else
            fish.lineageTag = "Fish";

        if (slot.parentA != null)
            slot.parentA.offspringIds.Add(fish.fishId);
        if (slot.parentB != null)
            slot.parentB.offspringIds.Add(fish.fishId);

        hatchedFish.Add(fish);

        if (FishLifecycleManager.Instance != null)
            FishLifecycleManager.Instance.RegisterFish(fish);

        if (_breedingManager != null)
            _breedingManager.ReleaseSlot(slot);

        Debug.Log($"Egg hatched → {fish.lineageTag} id {fish.fishId}, hypermutant={fish.isHypermutant}, gen={fish.generationNumber}");
    }
}
