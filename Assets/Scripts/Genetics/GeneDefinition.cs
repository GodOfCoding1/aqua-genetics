using UnityEngine;

public enum GeneType
{
    Continuous,
    Discrete
}

[CreateAssetMenu(fileName = "GeneDefinition", menuName = "Aquarium/Genetics/Gene Definition")]
public class GeneDefinition : ScriptableObject
{
    [Tooltip("Stable id used in genome dictionaries and save data.")]
    public string geneId;

    [Tooltip("Human-readable name for UI.")]
    public string displayName;

    [Tooltip("Continuous genes blend as averages; discrete genes use dominance when expressed.")]
    public GeneType type;

    [Tooltip("Minimum value for continuous genes, or lowest discrete state index (inclusive).")]
    public float minValue;

    [Tooltip("Maximum value for continuous genes, or highest discrete state index (inclusive).")]
    public float maxValue;

    [Tooltip("Number of discrete states (e.g. 8 for body_shape). Ignored for continuous genes.")]
    public int discreteStates;

    [Tooltip("When expressing discrete traits, weights blend between the two allele states (0–1).")]
    [Range(0f, 1f)]
    public float dominanceWeight;

    [Tooltip("If true, this gene is more likely to be affected by hypermutation-style events.")]
    public bool isMutationSensitive;

    [Tooltip("Standard deviation scale for Gaussian mutation on continuous genes.")]
    public float mutationSigma;
}
