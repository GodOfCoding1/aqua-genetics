using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Life stage thresholds are driven by <see cref="FishData.currentAge"/> (0–1 normalized lifetime).
/// </summary>
public enum FishLifeStage
{
    Egg,
    Fry,
    Juvenile,
    Adult,
    Elder
}

/// <summary>
/// Persistable fish identity, genetics, and lifecycle (Phase 3).
/// </summary>
[Serializable]
public class FishData
{
    public string fishId = Guid.NewGuid().ToString("N");

    public string ownerId;

    /// <summary>Full genotype (two alleles per gene).</summary>
    public FishGenome genome = new FishGenome();

    /// <summary>Auto-generated display name (body shape + color bucket + suffix).</summary>
    public string lineageTag;

    /// <summary>UTC when the fish record was created (hatch).</summary>
    public DateTime birthTime;

    /// <summary>UTC when the fish died; default/uninitialized when still alive.</summary>
    public DateTime deathTime;

    public bool isAlive = true;

    /// <summary>Preserved specimen / jar display mode.</summary>
    public bool isPreserved;

    /// <summary>Set when hypermutation fires at birth.</summary>
    public bool isHypermutant;

    public string parentAId;
    public string parentBId;

    public int generationNumber;

    public List<string> offspringIds = new List<string>();

    /// <summary>0–1 progress through natural lifespan (see <see cref="GetBaseLifespanDays"/>).</summary>
    [Range(0f, 1f)]
    public float currentAge;

    public FishLifeStage stage = FishLifeStage.Egg;

    /// <summary>
    /// Applied if the fry-care window expired without <see cref="FishLifecycleManager.FeedFry"/>.
    /// Subtract from lifespan/hardiness when presenting stats or rendering (not from stored alleles).
    /// </summary>
    [Range(0f, 1f)]
    public float neglectPenalty;

    /// <summary>UTC deadline for optional fry feeding after first Egg→Fry transition.</summary>
    public DateTime? fryCareDeadlineUtc;

    /// <summary>True once the player has fed the fry during the care window.</summary>
    public bool fryCareFed;

    public const float FryCareWindowHours = 24f;

    /// <summary>21 real-time days × lifespan gene phenotype (continuous multiplier).</summary>
    public float GetBaseLifespanDays(GeneLibrary lib)
    {
        if (lib == null || genome == null)
            return 21f;

        GeneDefinition def = lib.GetGene("lifespan");
        if (def == null)
            return 21f;

        float pheno = genome.GetPhenotype("lifespan", def);
        return 21f * pheno;
    }

    /// <summary>Phenotype for UI/rendering; applies <see cref="neglectPenalty"/> to lifespan/hardiness.</summary>
    public float GetPhenotypeForDisplay(string geneId, GeneDefinition def)
    {
        if (def == null || genome == null)
            return 0f;

        float v = genome.GetPhenotype(geneId, def);
        if (neglectPenalty > 0f && (geneId == "lifespan" || geneId == "hardiness"))
            return Mathf.Clamp(v - neglectPenalty, def.minValue, def.maxValue);
        return v;
    }

    /// <summary>Combines body_shape label, dominant base_hue color bucket, and a random 3-letter suffix.</summary>
    public string GenerateLineageTag(GeneLibrary lib)
    {
        if (lib == null || genome == null)
            return "Fish" + NewSuffix();

        var sb = new StringBuilder();

        GeneDefinition bodyDef = lib.GetGene("body_shape");
        if (bodyDef != null)
        {
            int shape = Mathf.RoundToInt(genome.GetPhenotype("body_shape", bodyDef));
            sb.Append(BodyShapeName(shape)).Append(' ');
        }

        GeneDefinition hueDef = lib.GetGene("base_hue");
        if (hueDef != null)
        {
            float hue = genome.GetPhenotype("base_hue", hueDef);
            sb.Append(HueBucketName(hue)).Append(' ');
        }

        sb.Append(NewSuffix());
        return sb.ToString().Trim();
    }

    static readonly string[] BodyShapeNames =
    {
        "Oval", "Elongated", "Deep", "Flat", "Round", "Torpedo", "Ribbon", "Diamond"
    };

    static string BodyShapeName(int index)
    {
        if (index >= 0 && index < BodyShapeNames.Length)
            return BodyShapeNames[index];
        return "Shape" + index;
    }

    static string HueBucketName(float hueDegrees)
    {
        float h = Mathf.Repeat(hueDegrees, 360f);
        if (h < 22.5f || h >= 337.5f) return "Red";
        if (h < 52.5f) return "Orange";
        if (h < 82.5f) return "Yellow";
        if (h < 142.5f) return "Green";
        if (h < 187.5f) return "Cyan";
        if (h < 262.5f) return "Blue";
        if (h < 307.5f) return "Purple";
        return "Magenta";
    }

    static string NewSuffix()
    {
        const string letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        var chars = new char[3];
        for (int i = 0; i < 3; i++)
            chars[i] = letters[UnityEngine.Random.Range(0, letters.Length)];
        return new string(chars);
    }

    public static FishLifeStage StageForNormalizedAge(float age01)
    {
        if (age01 < 0.05f) return FishLifeStage.Egg;
        if (age01 < 0.20f) return FishLifeStage.Fry;
        if (age01 < 0.35f) return FishLifeStage.Juvenile;
        if (age01 < 0.80f) return FishLifeStage.Adult;
        return FishLifeStage.Elder;
    }
}
