using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class FishGenome
{
    public Dictionary<string, float[]> alleles = new Dictionary<string, float[]>();

    public float GetPhenotype(string geneId, GeneDefinition def)
    {
        if (def == null || !alleles.TryGetValue(geneId, out float[] pair) || pair == null || pair.Length < 2)
            return def != null ? Mathf.Lerp(def.minValue, def.maxValue, 0.5f) : 0f;

        float a0 = pair[0];
        float a1 = pair[1];

        switch (def.type)
        {
            case GeneType.Continuous:
                return Mathf.Clamp((a0 + a1) * 0.5f, def.minValue, def.maxValue);
            case GeneType.Discrete:
            {
                int maxState = Mathf.Max(0, def.discreteStates - 1);
                int i0 = Mathf.RoundToInt(a0);
                int i1 = Mathf.RoundToInt(a1);
                i0 = Mathf.Clamp(i0, 0, maxState);
                i1 = Mathf.Clamp(i1, 0, maxState);
                if (i0 == i1)
                    return i0;

                float t = def.dominanceWeight;
                float blended = Mathf.Lerp(i0, i1, t);
                int expressed = Mathf.RoundToInt(blended);
                return Mathf.Clamp(expressed, 0, maxState);
            }
            default:
                return (a0 + a1) * 0.5f;
        }
    }

    public static FishGenome Breed(FishGenome parentA, FishGenome parentB, GeneLibrary lib, out bool triggeredHypermutation)
    {
        if (lib == null)
            throw new ArgumentNullException(nameof(lib));

        var child = new FishGenome();

        foreach (var def in lib.Genes)
        {
            if (def == null || string.IsNullOrEmpty(def.geneId))
                continue;

            float aFromA = PickRandomAllele(parentA, def.geneId, def);
            float aFromB = PickRandomAllele(parentB, def.geneId, def);

            child.alleles[def.geneId] = new[] { aFromA, aFromB };
        }

        MutationSystem.Mutate(child, lib);
        triggeredHypermutation = MutationSystem.TriggerHypermutation(child, lib);

        MutationSystem.CheckPhenotypeLock(child, lib);

        return child;
    }

    public static FishGenome Breed(FishGenome parentA, FishGenome parentB, GeneLibrary lib)
    {
        return Breed(parentA, parentB, lib, out _);
    }

    static float PickRandomAllele(FishGenome parent, string geneId, GeneDefinition def)
    {
        if (parent != null && parent.alleles.TryGetValue(geneId, out float[] p) && p != null && p.Length >= 2)
        {
            int idx = UnityEngine.Random.Range(0, 2);
            float raw = p[idx];
            return def.type == GeneType.Discrete
                ? ClampDiscrete(raw, def)
                : Mathf.Clamp(raw, def.minValue, def.maxValue);
        }

        return RandomAllele(def);
    }

    static float RandomAllele(GeneDefinition def)
    {
        if (def.type == GeneType.Discrete)
        {
            int maxState = Mathf.Max(0, def.discreteStates - 1);
            return UnityEngine.Random.Range(0, maxState + 1);
        }

        return UnityEngine.Random.Range(def.minValue, def.maxValue);
    }

    static float ClampDiscrete(float v, GeneDefinition def)
    {
        int maxState = Mathf.Max(0, def.discreteStates - 1);
        int r = Mathf.RoundToInt(v);
        r = Mathf.Clamp(r, 0, maxState);
        return r;
    }

    public float GeneticDistanceTo(FishGenome other, GeneLibrary lib)
    {
        if (other == null || lib == null)
            return 0f;

        float sumSq = 0f;
        int count = 0;

        IEnumerable<string> keys = alleles.Keys.Union(other.alleles.Keys);
        foreach (string geneId in keys)
        {
            GeneDefinition def = lib.GetGene(geneId);
            if (def == null)
                continue;

            float p0 = GetPhenotype(geneId, def);
            float p1 = other.GetPhenotype(geneId, def);
            float span = def.maxValue - def.minValue;
            if (span <= Mathf.Epsilon)
                continue;

            float n0 = (p0 - def.minValue) / span;
            float n1 = (p1 - def.minValue) / span;
            float d = n0 - n1;
            sumSq += d * d;
            count++;
        }

        if (count == 0)
            return 0f;

        return Mathf.Sqrt(sumSq / count);
    }

    public string Serialize()
    {
        var dto = new GenomeDto
        {
            entries = alleles
                .Where(kv => kv.Value != null && kv.Value.Length >= 2)
                .Select(kv => new AlleleEntryDto
                {
                    geneId = kv.Key,
                    a0 = kv.Value[0],
                    a1 = kv.Value[1]
                })
                .ToArray()
        };

        return JsonUtility.ToJson(dto);
    }

    public static FishGenome Deserialize(string json)
    {
        var g = new FishGenome();
        if (string.IsNullOrEmpty(json))
            return g;

        var dto = JsonUtility.FromJson<GenomeDto>(json);
        if (dto?.entries == null)
            return g;

        foreach (var e in dto.entries)
        {
            if (e == null || string.IsNullOrEmpty(e.geneId))
                continue;
            g.alleles[e.geneId] = new[] { e.a0, e.a1 };
        }

        return g;
    }

    [Serializable]
    class GenomeDto
    {
        public AlleleEntryDto[] entries;
    }

    [Serializable]
    class AlleleEntryDto
    {
        public string geneId;
        public float a0;
        public float a1;
    }
}
