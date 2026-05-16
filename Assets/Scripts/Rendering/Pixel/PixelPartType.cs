namespace Aquarium.PixelArt
{
    /// <summary>
    /// All compositable pixel-art fish part categories. The compositor reserves
    /// one <see cref="UnityEngine.SpriteRenderer"/> slot per entry, so adding a
    /// new part type requires bumping <see cref="Count"/> and exposing a slot
    /// on the prefab. Keep order stable — values are persisted in
    /// <c>FishPartLibrary</c> lookups.
    /// </summary>
    public enum PixelPartType
    {
        Body = 0,
        Tail = 1,
        DorsalFin = 2,
        PectoralFin = 3,
        Eye = 4,
        Mouth = 5,
        // Reserved slots for future content drops (scales overlay, hat, scar...).
        // Compositor allocates renderers for them so prefabs don't need to be
        // edited when these are populated later.
        ScalesOverlay = 6,
        Accessory = 7,

        Count = 8,
    }

    /// <summary>
    /// Named anchors that connect parts together. Coordinates are stored in
    /// <see cref="UnityEngine.Vector2"/> body-local units (NOT pixels) so they
    /// survive a PPU change. The generator converts pixel coords → world units
    /// using <c>PixelArtSettings.PixelsPerUnit</c>.
    /// </summary>
    public enum FishAnchor
    {
        TailAttach = 0,
        DorsalAttach = 1,
        PectoralAttach = 2,
        EyeAttach = 3,
        MouthAttach = 4,

        Count = 5,
    }
}
