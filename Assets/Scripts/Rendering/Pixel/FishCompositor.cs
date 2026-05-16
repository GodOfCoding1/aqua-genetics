using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Aquarium.PixelArt
{
    /// <summary>
    /// Runtime composition of a pixel-art fish from modular <see cref="FishPart"/>
    /// sprites. Holds one child <see cref="SpriteRenderer"/> per
    /// <see cref="PixelPartType"/> slot, swaps sprites + transforms on
    /// genome apply, and exposes per-slot frame stepping for the animator.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class FishCompositor : MonoBehaviour
    {
        [SerializeField] PixelArtSettings settings;
        [SerializeField] FishPartLibrary partLibrary;
        [SerializeField] Material paletteMaterialBase;

        [Tooltip("Sorting layer used by all part renderers. Must exist in the project's SortingLayers.")]
        [SerializeField] string sortingLayerName = "Fish";

        [Tooltip("Order-in-layer assigned to each slot (back → front). Index by (int)PixelPartType.")]
        [SerializeField] int[] slotSortingOrders = new int[]
        {
            10, // Body
            5,  // Tail
            0,  // DorsalFin (behind body)
            15, // PectoralFin
            25, // Eye
            28, // Mouth
            20, // ScalesOverlay
            30, // Accessory
        };

        [SerializeField] SpriteRenderer[] slotRenderers = new SpriteRenderer[(int)PixelPartType.Count];

        FishPart[] _activeParts = new FishPart[(int)PixelPartType.Count];
        int[] _slotFrame = new int[(int)PixelPartType.Count];
        Vector2[] _bodyAnchorsAtCurrentFrame = new Vector2[(int)FishAnchor.Count];
        MaterialPropertyBlock _block;
        PixelArtPalette _palette;
        bool _hasPalette;

        public PixelArtSettings Settings => settings;
        public FishPartLibrary PartLibrary => partLibrary;
        public PixelArtPalette CurrentPalette => _palette;

        public void SetSettings(PixelArtSettings value) { if (value != null) settings = value; }
        public void SetPartLibrary(FishPartLibrary value) { if (value != null) partLibrary = value; }
        public void SetPaletteMaterial(Material value) { if (value != null) paletteMaterialBase = value; }

        void Awake()
        {
            AutoLoadRefs();
            EnsureSlots();
        }

        void Reset()
        {
            AutoLoadRefs();
            EnsureSlots();
        }

        /// <summary>
        /// Self-healing reference loader. Tries Resources first (works at
        /// runtime in builds), then falls back to AssetDatabase scanning at
        /// edit-time so a freshly-instantiated fish renders even if the
        /// inspector refs weren't pre-wired.
        /// </summary>
        public void AutoLoadRefs()
        {
            if (settings == null)
                settings = Resources.Load<PixelArtSettings>("PixelArt/PixelArtSettings");
            if (partLibrary == null)
                partLibrary = Resources.Load<FishPartLibrary>("PixelArt/FishPartLibrary");
            if (paletteMaterialBase == null)
                paletteMaterialBase = Resources.Load<Material>("PixelArt/M_FishPalette");

#if UNITY_EDITOR
            if (settings == null)
                settings = FindFirstAssetOfType<PixelArtSettings>();
            if (partLibrary == null)
                partLibrary = FindFirstAssetOfType<FishPartLibrary>();
            if (paletteMaterialBase == null)
            {
                Shader sh = Shader.Find("Aquarium/FishPalette");
                if (sh != null)
                {
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/M_FishPalette.mat");
                    if (mat == null)
                        mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/PixelArt/M_FishPalette.mat");
                    paletteMaterialBase = mat;
                }
            }
#endif
        }

#if UNITY_EDITOR
        static T FindFirstAssetOfType<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids == null || guids.Length == 0)
                return null;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
#endif

        void OnValidate()
        {
            // Keep slot array sized.
            if (slotRenderers == null || slotRenderers.Length != (int)PixelPartType.Count)
            {
                var resized = new SpriteRenderer[(int)PixelPartType.Count];
                if (slotRenderers != null)
                    for (int i = 0; i < slotRenderers.Length && i < resized.Length; i++)
                        resized[i] = slotRenderers[i];
                slotRenderers = resized;
            }
            if (slotSortingOrders == null || slotSortingOrders.Length != (int)PixelPartType.Count)
            {
                var resized = new int[(int)PixelPartType.Count];
                if (slotSortingOrders != null)
                    for (int i = 0; i < slotSortingOrders.Length && i < resized.Length; i++)
                        resized[i] = slotSortingOrders[i];
                slotSortingOrders = resized;
            }
        }

        /// <summary>
        /// Builds child GameObjects + <see cref="SpriteRenderer"/> for any
        /// missing slot. Idempotent — existing renderers are reused.
        /// </summary>
        public void EnsureSlots()
        {
            if (slotRenderers == null || slotRenderers.Length != (int)PixelPartType.Count)
                slotRenderers = new SpriteRenderer[(int)PixelPartType.Count];

            for (int i = 0; i < (int)PixelPartType.Count; i++)
            {
                if (slotRenderers[i] != null)
                    continue;

                string slotName = $"PixelPart_{(PixelPartType)i}";
                Transform existing = transform.Find(slotName);
                GameObject go = existing != null ? existing.gameObject : new GameObject(slotName);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localScale = Vector3.one;

                SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
                if (sr == null)
                    sr = go.AddComponent<SpriteRenderer>();
                slotRenderers[i] = sr;
            }

            // Apply default sorting + material to renderers (shared material → no per-instance leaks).
            for (int i = 0; i < slotRenderers.Length; i++)
            {
                SpriteRenderer sr = slotRenderers[i];
                if (sr == null)
                    continue;

                if (paletteMaterialBase != null && sr.sharedMaterial != paletteMaterialBase)
                    sr.sharedMaterial = paletteMaterialBase;

                // Assign by name — avoids relying on NameToID != 0 (Default is 0 and
                // TagManager entries with duplicate uniqueID 0 were resolving Fish to 0,
                // which skipped assignment and left fish on Default behind all tank layers).
                if (!string.IsNullOrEmpty(sortingLayerName))
                    sr.sortingLayerName = sortingLayerName;
                sr.sortingOrder = slotSortingOrders != null && i < slotSortingOrders.Length ? slotSortingOrders[i] : i * 5;
                sr.enabled = false; // hidden until Apply.
            }
        }

        /// <summary>
        /// Compose a fish from a genome: pick parts, snap sprites to frame 0,
        /// align children to the body's anchor points, push palette via MPB.
        /// Idempotent — safe to call repeatedly.
        /// </summary>
        public void Apply(FishData fish, GeneLibrary lib)
        {
            AutoLoadRefs();
            EnsureSlots();

            if (fish?.genome == null || lib == null)
                return;

            if (partLibrary == null || settings == null)
            {
                Debug.LogWarning(
                    $"FishCompositor on '{name}' is missing PixelArtSettings/FishPartLibrary refs " +
                    "and could not auto-load them from Resources or AssetDatabase. " +
                    "Run Tools/Aquarium/Pixel Art/0. Build Everything (Pixel Art) to bootstrap the pipeline.",
                    this);
                return;
            }

            if (partLibrary.Parts == null || partLibrary.Parts.Count == 0)
            {
                Debug.LogWarning(
                    $"FishCompositor on '{name}' has an empty FishPartLibrary. " +
                    "Run Tools/Aquarium/Pixel Art/2. Generate Fish Part Sprites to populate it.",
                    this);
                return;
            }

            // 1. Pick parts based on phenotypes.
            int bodyShape = ClampDiscrete(Phen(fish, lib, "body_shape", 0f), settings.bodyShapeCount);
            int tailType = ClampDiscrete(Phen(fish, lib, "tail_type", 0f), settings.tailTypeCount);
            int finShape = ClampDiscrete(Phen(fish, lib, "fin_shape", 0f), settings.pectoralFinCount);
            int dorsalShape = ClampDiscrete(Phen(fish, lib, "fin_shape", 0f), settings.dorsalFinCount);
            int lipType = ClampDiscrete(Phen(fish, lib, "lip_type", 0f), settings.mouthTypeCount);

            // eye_size gene maps continuous → tier index.
            float eyeSize = Phen(fish, lib, "eye_size", 1f);
            int eyeTier = MapEyeSizeToTier(eyeSize, settings.eyeSizeTierCount);

            // fin_count: 0=tail-only, 1=tail+pectoral, 2=tail+pectoral+dorsal.
            int finCount = ClampDiscrete(Phen(fish, lib, "fin_count", 1f), 3);

            _activeParts[(int)PixelPartType.Body]        = partLibrary.Get(PixelPartType.Body, bodyShape);
            _activeParts[(int)PixelPartType.Tail]        = partLibrary.Get(PixelPartType.Tail, tailType);
            _activeParts[(int)PixelPartType.PectoralFin] = finCount >= 1 ? partLibrary.Get(PixelPartType.PectoralFin, finShape) : null;
            _activeParts[(int)PixelPartType.DorsalFin]   = finCount >= 2 ? partLibrary.Get(PixelPartType.DorsalFin, dorsalShape) : null;
            _activeParts[(int)PixelPartType.Eye]         = partLibrary.Get(PixelPartType.Eye, 0, eyeTier);
            _activeParts[(int)PixelPartType.Mouth]       = partLibrary.Get(PixelPartType.Mouth, lipType);

            for (int i = 0; i < _slotFrame.Length; i++)
                _slotFrame[i] = 0;

            // 2. Apply sprites + transforms.
            ApplyPartFrames(forceLayout: true);

            // 3. Per-fish palette.
            _palette = PixelArtPalette.FromGenome(fish, lib, settings);
            _hasPalette = true;
            ApplyPalette();
        }

        /// <summary>
        /// Push the current palette onto every active renderer via MPB.
        /// Cheap to call — doesn't reallocate.
        /// </summary>
        public void ApplyPalette()
        {
            if (!_hasPalette)
                return;
            if (_block == null)
                _block = new MaterialPropertyBlock();

            for (int i = 0; i < slotRenderers.Length; i++)
            {
                SpriteRenderer sr = slotRenderers[i];
                if (sr == null || !sr.enabled)
                    continue;

                sr.GetPropertyBlock(_block);
                if (i == (int)PixelPartType.Eye)
                    EyePalette(_palette).ApplyToBlock(_block);
                else
                    _palette.ApplyToBlock(_block);
                sr.SetPropertyBlock(_block);
            }
        }

        static PixelArtPalette EyePalette(PixelArtPalette source)
        {
            source.baseColor = Color.white;
            source.patternColor = Color.white;
            source.highlightColor = Color.white;
            source.outlineColor = new Color(0.03f, 0.025f, 0.04f, 1f);
            source.patternStrength = 0f;
            source.patternContrast = 0f;
            source.iridescence = 0f;
            source.bioluminescence = 0f;
            source.transparency = 0f;
            source.shadowMul = 0.78f;
            source.highlightMul = 1f;
            return source;
        }

        /// <summary>Re-apply palette with a per-slot override (e.g. eye gets eye_color_h).</summary>
        public void OverrideSlotPalette(PixelPartType slot, PixelArtPalette palette)
        {
            int idx = (int)slot;
            if (idx < 0 || idx >= slotRenderers.Length)
                return;
            SpriteRenderer sr = slotRenderers[idx];
            if (sr == null)
                return;
            if (_block == null)
                _block = new MaterialPropertyBlock();
            sr.GetPropertyBlock(_block);
            palette.ApplyToBlock(_block);
            sr.SetPropertyBlock(_block);
        }

        /// <summary>Set the frame index for a single slot. Negative values disable the slot.</summary>
        public void SetSlotFrame(PixelPartType slot, int frame)
        {
            int i = (int)slot;
            if (i < 0 || i >= _slotFrame.Length)
                return;
            FishPart part = _activeParts[i];
            if (part == null || part.FrameCount == 0)
                return;
            int wrapped = ((frame % part.FrameCount) + part.FrameCount) % part.FrameCount;
            if (_slotFrame[i] == wrapped)
                return;
            _slotFrame[i] = wrapped;
            ApplyPartFrames(forceLayout: slot == PixelPartType.Body);
        }

        public int GetSlotFrame(PixelPartType slot)
        {
            int i = (int)slot;
            if (i < 0 || i >= _slotFrame.Length)
                return 0;
            return _slotFrame[i];
        }

        public FishPart GetActivePart(PixelPartType slot)
        {
            int i = (int)slot;
            if (i < 0 || i >= _activeParts.Length)
                return null;
            return _activeParts[i];
        }

        public SpriteRenderer GetSlotRenderer(PixelPartType slot)
        {
            int i = (int)slot;
            if (i < 0 || i >= slotRenderers.Length)
                return null;
            return slotRenderers[i];
        }

        /// <summary>
        /// World-space bounds of all enabled slot renderers. Used by
        /// <see cref="FishPicker"/> to fit a click target.
        /// </summary>
        public Bounds GetCompositeBounds()
        {
            Bounds b = default;
            bool init = false;
            for (int i = 0; i < slotRenderers.Length; i++)
            {
                SpriteRenderer sr = slotRenderers[i];
                if (sr == null || !sr.enabled || sr.sprite == null)
                    continue;
                Bounds rb = sr.bounds;
                if (!init)
                {
                    b = rb;
                    init = true;
                }
                else
                {
                    b.Encapsulate(rb);
                }
            }
            return b;
        }

        // ------------------------------------------------------------------

        void ApplyPartFrames(bool forceLayout)
        {
            // Body frame may have shifted anchors → relayout dependents.
            FishPart bodyPart = _activeParts[(int)PixelPartType.Body];
            if (bodyPart != null)
            {
                FishPartFrame bodyFrame = bodyPart.GetFrame(_slotFrame[(int)PixelPartType.Body]);
                if (bodyFrame != null)
                {
                    SpriteRenderer sr = slotRenderers[(int)PixelPartType.Body];
                    sr.sprite = bodyFrame.sprite;
                    sr.enabled = bodyFrame.sprite != null;
                    sr.transform.localPosition = Vector3.zero;
                    sr.transform.localRotation = Quaternion.identity;
                    System.Array.Copy(bodyFrame.anchorOffsets, _bodyAnchorsAtCurrentFrame,
                        Mathf.Min(bodyFrame.anchorOffsets?.Length ?? 0, _bodyAnchorsAtCurrentFrame.Length));
                }
            }
            else
            {
                slotRenderers[(int)PixelPartType.Body].enabled = false;
            }

            // Other slots position themselves at the body's anchor for their attach point.
            for (int i = 0; i < _activeParts.Length; i++)
            {
                if (i == (int)PixelPartType.Body) continue;

                FishPart part = _activeParts[i];
                SpriteRenderer sr = slotRenderers[i];
                if (sr == null) continue;

                if (part == null || part.FrameCount == 0)
                {
                    sr.enabled = false;
                    sr.sprite = null;
                    continue;
                }

                FishPartFrame frame = part.GetFrame(_slotFrame[i]);
                if (frame == null || frame.sprite == null)
                {
                    sr.enabled = false;
                    continue;
                }

                sr.sprite = frame.sprite;
                sr.enabled = true;

                if (forceLayout)
                {
                    Vector2 anchor = _bodyAnchorsAtCurrentFrame[(int)part.attachToBodyAnchor];
                    sr.transform.localPosition = new Vector3(anchor.x, anchor.y, 0f);
                    sr.transform.localRotation = Quaternion.identity;
                }
            }
        }

        static int ClampDiscrete(float phen, int count)
        {
            if (count <= 1) return 0;
            return Mathf.Clamp(Mathf.RoundToInt(phen), 0, count - 1);
        }

        static int MapEyeSizeToTier(float eyeSizePhenotype, int tierCount)
        {
            // eye_size gene range 0.3..1.8; tier index = floor((v - 0.3) / 1.5 * tierCount).
            float t = Mathf.InverseLerp(0.3f, 1.8f, eyeSizePhenotype);
            return Mathf.Clamp(Mathf.FloorToInt(t * tierCount), 0, Mathf.Max(0, tierCount - 1));
        }

        static float Phen(FishData fish, GeneLibrary lib, string id, float fallback)
        {
            GeneDefinition def = lib != null ? lib.GetGene(id) : null;
            if (def == null || fish?.genome == null) return fallback;
            return fish.genome.GetPhenotype(id, def);
        }
    }
}
