using System;
using UnityEngine;

namespace Aquarium.PixelArt
{
    /// <summary>
    /// One frame of a part's animation: a sprite plus the world-space anchor
    /// offsets that move with the silhouette wobble (so e.g. the eye stays
    /// glued to the head as the body undulates).
    /// </summary>
    [Serializable]
    public class FishPartFrame
    {
        public Sprite sprite;

        [Tooltip("Anchor offsets in body-local world units, indexed by FishAnchor. Length = (int)FishAnchor.Count.")]
        public Vector2[] anchorOffsets = new Vector2[(int)FishAnchor.Count];

        public Vector2 GetAnchor(FishAnchor anchor)
        {
            int idx = (int)anchor;
            if (anchorOffsets == null || idx < 0 || idx >= anchorOffsets.Length)
                return Vector2.zero;
            return anchorOffsets[idx];
        }
    }

    /// <summary>
    /// One pixel-art part variant (e.g. body_shape #3, tail_type #1, eye size
    /// tier #2). Holds all animation frames + the part's own attach offset
    /// (where on the parent body this part should be placed).
    /// </summary>
    [CreateAssetMenu(fileName = "FishPart", menuName = "Aquarium/Pixel Art/Fish Part", order = 1)]
    public class FishPart : ScriptableObject
    {
        [Tooltip("Which compositor slot this part fills.")]
        public PixelPartType partType;

        [Tooltip("Variant index this part represents (matches the gene phenotype value, e.g. body_shape == 3).")]
        public int variantIndex;

        [Tooltip("Optional length / size tier. -1 means 'all tiers' (no tier filtering).")]
        public int tierIndex = -1;

        [Tooltip("Optional human label for the inspector.")]
        public string displayName;

        [Tooltip("Animation frame strip. Compositor cycles through these based on swim speed.")]
        public FishPartFrame[] frames = Array.Empty<FishPartFrame>();

        [Tooltip("Frames-per-second when the fish is swimming at base speed. <=0 means use settings default.")]
        public float framesPerSecond = -1f;

        [Tooltip("If true, this part's frame index is driven by an event (e.g. eye blink) instead of looping.")]
        public bool playsOneShotOnly;

        [Tooltip("Default attach point on the *body* this part docks to (which FishAnchor on the body to align this part's pivot to).")]
        public FishAnchor attachToBodyAnchor = FishAnchor.PectoralAttach;

        [Tooltip("Optional content tags for filtering / curated content drops (e.g. \"rare\", \"glowing\", \"fancy\").")]
        public string[] tags = Array.Empty<string>();

        public int FrameCount => frames != null ? frames.Length : 0;

        public FishPartFrame GetFrame(int index)
        {
            if (frames == null || frames.Length == 0)
                return null;
            int wrapped = ((index % frames.Length) + frames.Length) % frames.Length;
            return frames[wrapped];
        }
    }
}
