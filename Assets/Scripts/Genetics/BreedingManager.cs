using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BreedingSlot
{
    public FishData parentA;
    public FishData parentB;
    public DateTime startTimeUtc;
    public float durationHours;
    public bool isComplete;
    public FishGenome pendingOffspring;
    public bool pendingOffspringIsHypermutant;
}

public class BreedingManager : MonoBehaviour
{
    const int MaxSlots = 4;

    public static BreedingManager Instance { get; private set; }

    [SerializeField] GeneLibrary geneLibrary;
    [SerializeField] int unlockedSlots = 2;

    [SerializeField] List<BreedingSlot> activeSlots = new List<BreedingSlot>();

    /// <summary>Fired when a slot timer completes.</summary>
    public event Action<BreedingSlot> OnEggReady;

    public IReadOnlyList<BreedingSlot> ActiveSlots => activeSlots;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        unlockedSlots = Mathf.Clamp(unlockedSlots, 0, MaxSlots);
        while (activeSlots.Count < MaxSlots)
            activeSlots.Add(new BreedingSlot());
        for (int i = 0; i < MaxSlots; i++)
        {
            if (activeSlots[i] == null)
                activeSlots[i] = new BreedingSlot();
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool TryStartBreeding(FishData a, FishData b)
    {
        if (geneLibrary == null || a == null || b == null || a.genome == null || b.genome == null)
            return false;

        BreedingSlot free = FindFreeSlot();
        if (free == null)
            return false;

        float distance = a.genome.GeneticDistanceTo(b.genome, geneLibrary);
        float compat = GetCompatibilityScore(a.genome, b.genome);

        FishGenome child = FishGenome.Breed(a.genome, b.genome, geneLibrary, out bool hyper);

        if (distance < 0.15f)
            ApplyInbreedingFertilityPenalty(child);

        free.parentA = a;
        free.parentB = b;
        free.startTimeUtc = DateTime.UtcNow;
        free.durationHours = Mathf.Lerp(2f, 8f, 1f - compat);
        free.isComplete = false;
        free.pendingOffspring = child;
        free.pendingOffspringIsHypermutant = hyper;

        return true;
    }

    /// <summary>Call after awarding the hatched genome so the slot can be reused.</summary>
    public void ReleaseSlot(BreedingSlot slot)
    {
        if (slot == null || !activeSlots.Contains(slot))
            return;

        slot.parentA = null;
        slot.parentB = null;
        slot.pendingOffspring = null;
        slot.pendingOffspringIsHypermutant = false;
        slot.isComplete = false;
        slot.durationHours = 0f;
    }

    void Update()
    {
        DateTime now = DateTime.UtcNow;
        foreach (BreedingSlot slot in activeSlots)
        {
            if (slot == null || slot.pendingOffspring == null || slot.isComplete)
                continue;
            if (slot.durationHours <= 0f)
                continue;

            double elapsed = (now - slot.startTimeUtc).TotalHours;
            if (elapsed < slot.durationHours)
                continue;

            slot.isComplete = true;
            OnEggReady?.Invoke(slot);
        }
    }

    public float GetCompatibilityScore(FishGenome genomeA, FishGenome genomeB)
    {
        if (geneLibrary == null || genomeA == null || genomeB == null)
            return 0f;

        float distance = genomeA.GeneticDistanceTo(genomeB, geneLibrary);
        float score = 1f - distance * 0.5f;

        GeneDefinition bodyShape = geneLibrary.GetGene("body_shape");
        if (bodyShape != null)
        {
            float s0 = genomeA.GetPhenotype("body_shape", bodyShape);
            float s1 = genomeB.GetPhenotype("body_shape", bodyShape);
            if (Mathf.Abs(s0 - s1) >= 3f)
                score *= 0.4f;
        }

        return Mathf.Clamp01(score);
    }

    BreedingSlot FindFreeSlot()
    {
        for (int i = 0; i < unlockedSlots && i < activeSlots.Count; i++)
        {
            BreedingSlot s = activeSlots[i];
            if (s != null && s.pendingOffspring == null)
                return s;
        }

        return null;
    }

    void ApplyInbreedingFertilityPenalty(FishGenome child)
    {
        if (child == null || geneLibrary == null)
            return;

        GeneDefinition fert = geneLibrary.GetGene("fertility");
        if (fert == null || !child.alleles.TryGetValue("fertility", out float[] pair) || pair == null || pair.Length < 2)
            return;

        pair[0] = Mathf.Clamp(pair[0] * 0.5f, fert.minValue, fert.maxValue);
        pair[1] = Mathf.Clamp(pair[1] * 0.5f, fert.minValue, fert.maxValue);
    }
}
