using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Real-time aging, life stages, death, and fry-care window (Phase 3).
/// Advances all registered fish on a one-minute tick using UTC timestamps for age.
/// </summary>
public class FishLifecycleManager : MonoBehaviour
{
    public static FishLifecycleManager Instance { get; private set; }

    [SerializeField] GeneLibrary geneLibrary;

    [SerializeField] List<FishData> tankFish = new List<FishData>();

    float _secondsUntilMinuteTick = 60f;

    /// <summary>Invoked when a fish dies (data is retained for lineage).</summary>
    public event Action<FishData> OnFishDied;

    public IReadOnlyList<FishData> TankFish => tankFish;

    public GeneLibrary GeneLibrary => geneLibrary;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        _secondsUntilMinuteTick -= Time.unscaledDeltaTime;
        if (_secondsUntilMinuteTick > 0f)
            return;

        _secondsUntilMinuteTick = 60f;
        UpdateAges();
    }

    /// <summary>Adds a fish to the lifecycle simulation (e.g. after egg hatch).</summary>
    public void RegisterFish(FishData fish)
    {
        if (fish == null || tankFish.Contains(fish))
            return;

        tankFish.Add(fish);
    }

    public bool UnregisterFish(FishData fish)
    {
        return fish != null && tankFish.Remove(fish);
    }

    /// <summary>Call when the player feeds a fry during the 24h window after Egg→Fry.</summary>
    public bool FeedFry(string fishId)
    {
        FishData fish = FindFish(fishId);
        if (fish == null || !fish.isAlive)
            return false;

        if (!fish.fryCareDeadlineUtc.HasValue)
            return false;

        if (DateTime.UtcNow > fish.fryCareDeadlineUtc.Value)
            return false;

        fish.fryCareFed = true;
        fish.fryCareDeadlineUtc = null;
        return true;
    }

    public void KillFish(string fishId)
    {
        FishData fish = FindFish(fishId);
        if (fish == null || !fish.isAlive)
            return;

        fish.isAlive = false;
        fish.deathTime = DateTime.UtcNow;
        fish.fryCareDeadlineUtc = null;
        OnFishDied?.Invoke(fish);
    }

    void UpdateAges()
    {
        DateTime now = DateTime.UtcNow;

        for (int i = tankFish.Count - 1; i >= 0; i--)
        {
            FishData fish = tankFish[i];
            if (fish == null || !fish.isAlive)
                continue;

            float lifeDays = fish.GetBaseLifespanDays(geneLibrary);
            double lifeSeconds = lifeDays * 86400.0;
            if (lifeSeconds <= 0.0)
            {
                KillFish(fish.fishId);
                continue;
            }

            double elapsedSecs = (now - fish.birthTime).TotalSeconds;
            fish.currentAge = Mathf.Clamp01((float)(elapsedSecs / lifeSeconds));

            FishLifeStage prevStage = fish.stage;
            fish.stage = FishData.StageForNormalizedAge(fish.currentAge);
            if (prevStage == FishLifeStage.Egg && fish.stage == FishLifeStage.Fry)
            {
                fish.fryCareDeadlineUtc = DateTime.UtcNow.AddHours(FishData.FryCareWindowHours);
                fish.fryCareFed = false;
            }

            ResolveFryCareDeadline(fish, now);

            if (fish.currentAge >= 1f)
                KillFish(fish.fishId);
        }
    }

    void ResolveFryCareDeadline(FishData fish, DateTime now)
    {
        if (!fish.fryCareDeadlineUtc.HasValue || fish.fryCareFed)
            return;

        if (now < fish.fryCareDeadlineUtc.Value)
            return;

        fish.neglectPenalty = 0.2f;
        fish.fryCareDeadlineUtc = null;
    }

    FishData FindFish(string fishId)
    {
        if (string.IsNullOrEmpty(fishId))
            return null;

        foreach (FishData f in tankFish)
        {
            if (f != null && f.fishId == fishId)
                return f;
        }

        return null;
    }
}
