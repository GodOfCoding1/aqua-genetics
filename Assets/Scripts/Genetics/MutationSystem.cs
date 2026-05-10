using UnityEngine;

public static class MutationSystem
{
    static bool _hasExtraGaussian;
    static float _extraGaussian;

    /// <summary>Standard per-birth mutations using <paramref name="globalMutationRate"/> per gene.</summary>
    public static void Mutate(FishGenome genome, GeneLibrary lib, float globalMutationRate = 0.03f)
    {
        if (genome == null || lib == null || lib.Genes == null)
            return;

        foreach (GeneDefinition def in lib.Genes)
        {
            if (def == null || string.IsNullOrEmpty(def.geneId))
                continue;

            if (UnityEngine.Random.value >= globalMutationRate)
                continue;

            MutateGenePair(genome, def, sigmaScale: 1f);
        }
    }

    /// <summary>Rare (~0.2% per birth): strong mutation applied to every gene.</summary>
    public static bool TriggerHypermutation(FishGenome genome, GeneLibrary lib)
    {
        if (UnityEngine.Random.value >= 0.002f)
            return false;

        if (genome == null || lib == null || lib.Genes == null)
            return false;

        foreach (GeneDefinition def in lib.Genes)
        {
            if (def == null || string.IsNullOrEmpty(def.geneId))
                continue;

            MutateGenePair(genome, def, sigmaScale: 5f);
        }

        return true;
    }

    /// <summary>
    /// Rainbow Ghost unlock: boosts bioluminescence when phenotype matches the lock.
    /// </summary>
    public static bool CheckPhenotypeLock(FishGenome genome, GeneLibrary lib)
    {
        if (genome == null || lib == null)
            return false;

        GeneDefinition ir = lib.GetGene("iridescence");
        GeneDefinition pt = lib.GetGene("pattern_type");
        GeneDefinition bs = lib.GetGene("base_saturation");
        GeneDefinition bio = lib.GetGene("bioluminescence");

        if (ir == null || pt == null || bs == null || bio == null)
            return false;

        float irP = genome.GetPhenotype("iridescence", ir);
        float ptP = genome.GetPhenotype("pattern_type", pt);
        float bsP = genome.GetPhenotype("base_saturation", bs);

        if (!(irP > 0.8f && Mathf.RoundToInt(ptP) == 5 && bsP < 0.2f))
            return false;

        if (!genome.alleles.TryGetValue("bioluminescence", out float[] pair) || pair == null || pair.Length < 2)
            genome.alleles["bioluminescence"] = new[]
            {
                Mathf.Lerp(bio.minValue, bio.maxValue, 0.5f),
                Mathf.Lerp(bio.minValue, bio.maxValue, 0.5f)
            };

        pair = genome.alleles["bioluminescence"];
        float avg = (pair[0] + pair[1]) * 0.5f;
        float boosted = Mathf.Min(avg + 0.3f, bio.maxValue);
        pair[0] = Mathf.Clamp(boosted, bio.minValue, bio.maxValue);
        pair[1] = Mathf.Clamp(boosted, bio.minValue, bio.maxValue);

        return true;
    }

    static void MutateGenePair(FishGenome genome, GeneDefinition def, float sigmaScale)
    {
        if (!genome.alleles.TryGetValue(def.geneId, out float[] pair) || pair == null || pair.Length < 2)
        {
            float x = SampleRandomValue(def);
            genome.alleles[def.geneId] = new[] { x, x };
            return;
        }

        pair[0] = MutateOneAllele(pair[0], def, sigmaScale);
        pair[1] = MutateOneAllele(pair[1], def, sigmaScale);
    }

    static float MutateOneAllele(float v, GeneDefinition def, float sigmaScale)
    {
        switch (def.type)
        {
            case GeneType.Continuous:
            {
                float noise = Gaussian() * def.mutationSigma * sigmaScale;
                return Mathf.Clamp(v + noise, def.minValue, def.maxValue);
            }
            case GeneType.Discrete:
            {
                int maxState = Mathf.Max(0, def.discreteStates - 1);
                int state = Mathf.Clamp(Mathf.RoundToInt(v), 0, maxState);
                int dir = UnityEngine.Random.value < 0.5f ? -1 : 1;
                int next = state + dir;
                if (next < 0)
                    next = maxState;
                else if (next > maxState)
                    next = 0;

                return next;
            }
            default:
                return v;
        }
    }

    static float SampleRandomValue(GeneDefinition def)
    {
        if (def.type == GeneType.Discrete)
        {
            int maxState = Mathf.Max(0, def.discreteStates - 1);
            return UnityEngine.Random.Range(0, maxState + 1);
        }

        return UnityEngine.Random.Range(def.minValue, def.maxValue);
    }

    /// <summary>Std-normal via Box–Muller.</summary>
    static float Gaussian()
    {
        if (_hasExtraGaussian)
        {
            _hasExtraGaussian = false;
            return _extraGaussian;
        }

        float u1 = Mathf.Max(float.Epsilon, UnityEngine.Random.value);
        float u2 = UnityEngine.Random.value;
        float mag = Mathf.Sqrt(-2f * Mathf.Log(u1));
        float z0 = mag * Mathf.Cos(2f * Mathf.PI * u2);
        float z1 = mag * Mathf.Sin(2f * Mathf.PI * u2);
        _extraGaussian = z1;
        _hasExtraGaussian = true;
        return z0;
    }
}
