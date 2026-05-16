using UnityEngine;

namespace Aquarium.PixelArt
{
    /// <summary>
    /// Per-fish palette pushed onto the FishPalette shader via MaterialPropertyBlock.
    /// Genome → palette translation lives in <see cref="FromGenome"/> so the
    /// gene→visual mapping has exactly one source of truth.
    /// </summary>
    public struct PixelArtPalette
    {
        public Color baseColor;
        public Color patternColor;
        public Color outlineColor;
        public Color highlightColor;
        public float patternStrength;
        public float patternType;
        public float patternScale;
        public float patternContrast;
        public float iridescence;
        public float bioluminescence;
        public float transparency;
        public float iridescencePhase;
        public float shadowMul;
        public float highlightMul;

        public static PixelArtPalette Default(PixelArtSettings settings)
        {
            return new PixelArtPalette
            {
                baseColor = new Color(0.25f, 0.75f, 1f, 1f),
                patternColor = new Color(1f, 0.78f, 0.24f, 1f),
                outlineColor = settings != null ? settings.defaultOutlineColor : new Color(0.07f, 0.05f, 0.10f, 1f),
                highlightColor = new Color(1f, 1f, 1f, 1f),
                patternStrength = 0.85f,
                patternType = 0f,
                patternScale = 2.6f,
                patternContrast = 0.7f,
                iridescence = 0f,
                bioluminescence = 0f,
                transparency = 0f,
                iridescencePhase = 0f,
                shadowMul = settings != null ? settings.shadowTierMul : 0.62f,
                highlightMul = settings != null ? settings.highlightTierMul : 1.26f,
            };
        }

        /// <summary>
        /// Build a palette directly from a fish genome. Centralised so
        /// FishRenderer + FishCompositor + UI previews stay in sync.
        /// </summary>
        public static PixelArtPalette FromGenome(FishData fish, GeneLibrary lib, PixelArtSettings settings)
        {
            PixelArtPalette p = Default(settings);
            if (fish?.genome == null || lib == null)
                return p;

            float Phen(string id, float fallback)
            {
                GeneDefinition def = lib.GetGene(id);
                if (def == null)
                    return fallback;
                return fish.genome.GetPhenotype(id, def);
            }

            float baseH = Phen("base_hue", 200f);
            float baseS = Mathf.Clamp01(Mathf.Lerp(0.62f, 1f, Phen("base_saturation", 0.7f)));
            float baseV = Mathf.Clamp01(Mathf.Lerp(0.76f, 1f, Phen("base_value", 0.85f)));
            p.baseColor = Color.HSVToRGB(Mathf.Repeat(baseH, 360f) / 360f, baseS, baseV);
            p.baseColor.a = 1f;

            // Pattern colour: bias hue away from base if too close so contrast is
            // never lost (mirrors the legacy logic so existing fish look familiar).
            float patH = Phen("pattern_hue", baseH + 60f);
            float hueDiff = Mathf.Abs(Mathf.Repeat(patH - baseH + 540f, 360f) - 180f);
            if (hueDiff > 30f && hueDiff < 60f)
                patH = Mathf.Repeat(baseH + 65f, 360f);

            float patS = Mathf.Clamp01(baseS * 0.80f + 0.18f);
            float patV = Mathf.Clamp01(baseV * 0.92f + 0.10f);
            p.patternColor = Color.HSVToRGB(Mathf.Repeat(patH, 360f) / 360f, patS, patV);
            p.patternColor.a = 1f;

            // Highlight is base shifted toward white so colours stay coherent across the palette.
            p.highlightColor = Color.Lerp(p.baseColor, Color.white, 0.68f);
            p.highlightColor.a = 1f;

            // Outline: deep version of base hue (keeps coherent silhouette across colours).
            float outV = Mathf.Clamp01(baseV * 0.13f + 0.025f);
            p.outlineColor = Color.HSVToRGB(Mathf.Repeat(baseH, 360f) / 360f, Mathf.Clamp01(baseS * 0.75f + 0.18f), outV);
            p.outlineColor.a = 1f;

            // Keep patterns bold enough to read, while still respecting faint-pattern fish.
            p.patternStrength = Mathf.Lerp(0.35f, 0.95f, Mathf.Clamp01(Phen("pattern_contrast", 0.6f)));
            p.patternType = Phen("pattern_type", 0f);
            p.patternScale = Mathf.Clamp(Phen("pattern_scale", 3f), 1.4f, 5.5f);
            p.patternContrast = Mathf.Lerp(0.45f, 0.9f, Mathf.Clamp01(Phen("pattern_contrast", 0.6f)));

            // Gating reproduces existing behaviour: only high-trait fish actually shimmer / glow.
            p.iridescence = Mathf.SmoothStep(0.45f, 0.95f, Mathf.Clamp01(Phen("iridescence", 0f)));
            p.bioluminescence = Mathf.SmoothStep(0.55f, 1f, Mathf.Clamp01(Phen("bioluminescence", 0f)));
            p.transparency = Mathf.Clamp01(Phen("transparency", 0f));

            // Per-fish phase derived from id hash so animated effects don't all sync.
            int seed = 17;
            if (!string.IsNullOrEmpty(fish.fishId))
            {
                foreach (char c in fish.fishId)
                    seed = unchecked(seed * 31 + c);
            }
            p.iridescencePhase = (seed & 0xFFFF) / 65535f * Mathf.PI * 2f;

            return p;
        }

        public void ApplyToBlock(MaterialPropertyBlock block)
        {
            if (block == null)
                return;

            block.SetColor(ShaderIds.BaseColor, baseColor);
            block.SetColor(ShaderIds.PatternColor, patternColor);
            block.SetColor(ShaderIds.OutlineColor, outlineColor);
            block.SetColor(ShaderIds.HighlightColor, highlightColor);
            block.SetFloat(ShaderIds.PatternStrength, patternStrength);
            block.SetFloat(ShaderIds.PatternType, patternType);
            block.SetFloat(ShaderIds.PatternScale, patternScale);
            block.SetFloat(ShaderIds.PatternContrast, patternContrast);
            block.SetFloat(ShaderIds.Iridescence, iridescence);
            block.SetFloat(ShaderIds.Bioluminescence, bioluminescence);
            block.SetFloat(ShaderIds.Transparency, transparency);
            block.SetFloat(ShaderIds.IridescencePhase, iridescencePhase);
            block.SetFloat(ShaderIds.ShadowMul, shadowMul);
            block.SetFloat(ShaderIds.HighlightMul, highlightMul);
        }
    }

    public static class ShaderIds
    {
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int PatternColor = Shader.PropertyToID("_PatternColor");
        public static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");
        public static readonly int HighlightColor = Shader.PropertyToID("_HighlightColor");
        public static readonly int PatternStrength = Shader.PropertyToID("_PatternStrength");
        public static readonly int PatternType = Shader.PropertyToID("_PatternType");
        public static readonly int PatternScale = Shader.PropertyToID("_PatternScale");
        public static readonly int PatternContrast = Shader.PropertyToID("_PatternContrast");
        public static readonly int Iridescence = Shader.PropertyToID("_Iridescence");
        public static readonly int Bioluminescence = Shader.PropertyToID("_Bioluminescence");
        public static readonly int Transparency = Shader.PropertyToID("_Transparency");
        public static readonly int IridescencePhase = Shader.PropertyToID("_IridescencePhase");
        public static readonly int ShadowMul = Shader.PropertyToID("_ShadowMul");
        public static readonly int HighlightMul = Shader.PropertyToID("_HighlightMul");
    }
}
