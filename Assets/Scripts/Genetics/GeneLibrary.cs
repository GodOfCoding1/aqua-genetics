using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GeneLibrary", menuName = "Aquarium/Genetics/Gene Library")]
public class GeneLibrary : ScriptableObject
{
    [SerializeField] List<GeneDefinition> genes = new List<GeneDefinition>();

    readonly Dictionary<string, GeneDefinition> _cache = new Dictionary<string, GeneDefinition>();

    public IReadOnlyList<GeneDefinition> Genes => genes;

    void OnEnable()
    {
        RebuildCache();
    }

    void OnValidate()
    {
        RebuildCache();
    }

    public GeneDefinition GetGene(string geneId)
    {
        if (string.IsNullOrEmpty(geneId))
            return null;

        if (_cache.TryGetValue(geneId, out GeneDefinition cached))
            return cached;

        RebuildCache();
        return _cache.TryGetValue(geneId, out cached) ? cached : null;
    }

    void RebuildCache()
    {
        _cache.Clear();
        if (genes == null)
            return;

        foreach (GeneDefinition def in genes)
        {
            if (def != null && !string.IsNullOrEmpty(def.geneId))
                _cache[def.geneId] = def;
        }
    }

    public FishGenome GenerateRandomGenome()
    {
        var genome = new FishGenome();
        if (genes == null)
            return genome;

        foreach (GeneDefinition def in genes)
        {
            if (def == null || string.IsNullOrEmpty(def.geneId))
                continue;

            switch (def.type)
            {
                case GeneType.Continuous:
                {
                    float x = Random.Range(def.minValue, def.maxValue);
                    float y = Random.Range(def.minValue, def.maxValue);
                    genome.alleles[def.geneId] = new[] { x, y };
                    break;
                }
                case GeneType.Discrete:
                {
                    int maxState = Mathf.Max(0, def.discreteStates - 1);
                    int ia = Random.Range(0, maxState + 1);
                    int ib = Random.Range(0, maxState + 1);
                    genome.alleles[def.geneId] = new[] { (float)ia, (float)ib };
                    break;
                }
            }
        }

        return genome;
    }
}
