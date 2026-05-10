using System;
using UnityEngine;

/// <summary>
/// Runtime fish spawning helpers. Used at runtime by <c>AquariumHUD</c>'s
/// Spawn button and at edit-time by <see cref="TestFishSpawner"/> via
/// <see cref="CreateRandomFishData"/>.
/// </summary>
public static class FishSpawnService
{
    /// <summary>Builds a freshly-randomized <see cref="FishData"/> from the library.</summary>
    public static FishData CreateRandomFishData(GeneLibrary lib)
    {
        if (lib == null)
            throw new ArgumentNullException(nameof(lib));

        var fish = new FishData
        {
            genome = lib.GenerateRandomGenome(),
            birthTime = DateTime.UtcNow,
            isAlive = true,
            generationNumber = 1,
            currentAge = 0.2f,
        };

        fish.stage = FishData.StageForNormalizedAge(fish.currentAge);
        fish.lineageTag = fish.GenerateLineageTag(lib);
        return fish;
    }

    /// <summary>
    /// Instantiates the supplied fish prefab, applies a fresh random genome, and
    /// registers it with the lifecycle manager. Safe to call from runtime UI.
    /// </summary>
    public static GameObject SpawnRandom(
        GameObject prefab,
        GeneLibrary lib,
        Transform parent,
        Vector3 worldPos)
    {
        if (prefab == null || lib == null)
        {
            Debug.LogWarning("FishSpawnService.SpawnRandom: missing prefab or gene library.");
            return null;
        }

        GameObject instance = UnityEngine.Object.Instantiate(prefab, parent);
        instance.transform.position = worldPos;

        // The prototype bootstrap re-rolls genome on Start; strip it so the
        // runtime-supplied genome below is what the fish gets.
        FishPrototypeBootstrap boot = instance.GetComponent<FishPrototypeBootstrap>();
        if (boot != null)
            UnityEngine.Object.Destroy(boot);

        FishRenderer renderer = instance.GetComponent<FishRenderer>();
        if (renderer != null)
            renderer.SetGeneLibrary(lib);

        FishData fish = CreateRandomFishData(lib);

        if (renderer != null)
            renderer.ApplyGenome(fish);

        if (FishLifecycleManager.Instance != null)
            FishLifecycleManager.Instance.RegisterFish(fish);

        return instance;
    }
}
