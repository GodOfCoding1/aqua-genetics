using UnityEngine;

namespace Aquarium.PixelArt
{
    /// <summary>
    /// Single source of truth for resolution + palette + animation defaults.
    /// Bumping <see cref="pixelsPerUnit"/> and rerunning the editor generator
    /// regenerates every part at the new size — no code edits required.
    /// </summary>
    [CreateAssetMenu(fileName = "PixelArtSettings", menuName = "Aquarium/Pixel Art/Pixel Art Settings", order = 0)]
    public class PixelArtSettings : ScriptableObject
    {
        [Header("Resolution")]
        [Tooltip("Pixels per Unity world unit. Fish bounding-box width in pixels = bodyWidthPixels; world width = bodyWidthPixels / PPU.")]
        [Min(8)]
        public int pixelsPerUnit = 64;

        [Tooltip("Body bounding box at body_size = 1.0, in pixels (medium-detail target ~128x64). Other parts size relative to this.")]
        public Vector2Int bodyBoundsPixels = new Vector2Int(128, 64);

        [Header("Animation")]
        [Tooltip("Body undulation frame count. 4 reads as a smooth swim cycle without per-fish CPU cost.")]
        [Min(1)] public int bodySwimFrames = 4;

        [Tooltip("Tail wag frame count. More frames = smoother wag.")]
        [Min(1)] public int tailWagFrames = 5;

        [Tooltip("Pectoral & dorsal fin flutter frame count.")]
        [Min(1)] public int finFlutterFrames = 4;

        [Tooltip("Eye blink frame count. Frame 0 is open; subsequent frames are closing (cycled rarely).")]
        [Min(1)] public int eyeBlinkFrames = 3;

        [Tooltip("Base swim cycle FPS at swim_speed = 1.0. Animator scales this by current swim speed.")]
        public float baseSwimFps = 6f;

        [Tooltip("Average seconds between blinks (Poisson-ish jitter applied per fish).")]
        public float averageBlinkInterval = 4.5f;

        [Header("Palette / shading")]
        [Tooltip("Number of brightness tiers used by the body shader. 4 reads as classic pixel art (outline / shadow / midtone / highlight).")]
        [Range(2, 6)] public int shadeTierCount = 4;

        [Tooltip("Outline color for fish silhouette (palette-relative; R/G/B used directly).")]
        public Color defaultOutlineColor = new Color(0.045f, 0.035f, 0.075f, 1f);

        [Tooltip("How dark the inner shadow tier is, multiplied with base color (0..1).")]
        [Range(0f, 1f)] public float shadowTierMul = 0.62f;

        [Tooltip("How bright the highlight tier is, multiplied with base color (>=1).")]
        [Min(1f)] public float highlightTierMul = 1.26f;

        [Header("Variant counts (must match GeneDefinition state counts)")]
        [Tooltip("Number of body shape variants generated. Matches body_shape gene.discreteStates.")]
        [Min(1)] public int bodyShapeCount = 8;

        [Tooltip("Number of tail variants generated. Matches tail_type gene.discreteStates.")]
        [Min(1)] public int tailTypeCount = 6;

        [Tooltip("Number of pectoral fin shape variants. Matches fin_shape gene.discreteStates.")]
        [Min(1)] public int pectoralFinCount = 5;

        [Tooltip("Number of dorsal fin shape variants. Sized to match fin_shape unless distinct silhouettes are wanted.")]
        [Min(1)] public int dorsalFinCount = 5;

        [Tooltip("Number of mouth/lip variants. Matches lip_type gene.discreteStates.")]
        [Min(1)] public int mouthTypeCount = 4;

        [Tooltip("Number of eye size tiers generated. eye_size gene snaps to nearest tier.")]
        [Min(1)] public int eyeSizeTierCount = 3;

        [Header("Procedural seeding")]
        [Tooltip("Master RNG seed for procedural pixel generators. Change to roll an entirely new asset set.")]
        public int generatorSeed = 1337;

        /// <summary>World units per pixel, derived from <see cref="pixelsPerUnit"/>.</summary>
        public float UnitsPerPixel => 1f / Mathf.Max(1, pixelsPerUnit);

        /// <summary>Body bounding box in world units (used for camera framing & compositor alignment).</summary>
        public Vector2 BodyBoundsWorld => new Vector2(bodyBoundsPixels.x, bodyBoundsPixels.y) * UnitsPerPixel;
    }
}
