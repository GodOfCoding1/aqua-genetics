using System.Collections.Generic;
using UnityEngine;

namespace Aquarium.PixelArt
{
    /// <summary>
    /// Lookup of all <see cref="FishPart"/> assets indexed by (PartType, VariantIndex, TierIndex).
    /// Replaces the implicit array indexing in the legacy mesh renderer; lets
    /// us add new parts (more body shapes, new tail types, accessories) by
    /// dropping SOs into the list.
    /// </summary>
    [CreateAssetMenu(fileName = "FishPartLibrary", menuName = "Aquarium/Pixel Art/Fish Part Library", order = 2)]
    public class FishPartLibrary : ScriptableObject
    {
        [SerializeField] List<FishPart> parts = new List<FishPart>();

        readonly Dictionary<long, FishPart> _cache = new Dictionary<long, FishPart>();
        bool _cacheBuilt;

        public IReadOnlyList<FishPart> Parts => parts;

        public void AddOrReplace(FishPart part)
        {
            if (part == null)
                return;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] != null
                    && parts[i].partType == part.partType
                    && parts[i].variantIndex == part.variantIndex
                    && parts[i].tierIndex == part.tierIndex)
                {
                    parts[i] = part;
                    _cacheBuilt = false;
                    return;
                }
            }
            parts.Add(part);
            _cacheBuilt = false;
        }

        public void Clear()
        {
            parts.Clear();
            _cache.Clear();
            _cacheBuilt = false;
        }

        public FishPart Get(PixelPartType type, int variantIndex, int tierIndex = -1)
        {
            EnsureCache();

            // Exact match (variant + tier).
            if (_cache.TryGetValue(Key(type, variantIndex, tierIndex), out FishPart hit))
                return hit;

            // Fallback to "any tier" (-1) for that variant.
            if (tierIndex != -1 && _cache.TryGetValue(Key(type, variantIndex, -1), out FishPart anyTier))
                return anyTier;

            // Last-resort: variant 0 of that type so the fish is never invisible.
            if (variantIndex != 0 && _cache.TryGetValue(Key(type, 0, -1), out FishPart fallback))
                return fallback;
            if (variantIndex != 0 && _cache.TryGetValue(Key(type, 0, 0), out FishPart fallback2))
                return fallback2;

            return null;
        }

        void EnsureCache()
        {
            if (_cacheBuilt && _cache.Count > 0)
                return;

            _cache.Clear();
            if (parts == null)
            {
                _cacheBuilt = true;
                return;
            }

            foreach (FishPart part in parts)
            {
                if (part == null)
                    continue;
                _cache[Key(part.partType, part.variantIndex, part.tierIndex)] = part;
            }
            _cacheBuilt = true;
        }

        void OnEnable() { _cacheBuilt = false; }
        void OnValidate() { _cacheBuilt = false; }

        static long Key(PixelPartType type, int variant, int tier)
        {
            // Pack into a long: type (8 bits) | variant (24 bits) | tier (32 bits, signed).
            // tier can be -1 so cast to uint via unchecked to preserve all bits in the key.
            unchecked
            {
                long t = (long)((int)type & 0xFF) << 56;
                long v = ((long)(variant & 0xFFFFFF)) << 32;
                long e = (uint)tier;
                return t | v | e;
            }
        }
    }
}
