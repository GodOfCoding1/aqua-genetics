using System.Collections.Generic;
using System.IO;
using Aquarium.PixelArt;
using UnityEditor;
using UnityEngine;

namespace Aquarium.PixelArt.EditorTools
{
    /// <summary>
    /// Procedurally generates every pixel-art <see cref="FishPart"/> sprite +
    /// SO at the resolution specified in <see cref="PixelArtSettings"/> and
    /// registers them in the <see cref="FishPartLibrary"/>. Re-run after
    /// changing PPU / variant counts to rebuild the asset set.
    ///
    /// Output goes under <c>Assets/Textures/PixelFish/</c> (PNGs) and
    /// <c>Assets/ScriptableObjects/Pixel/Parts/</c> (FishPart SOs).
    /// </summary>
    public static class PixelFishGenerator
    {
        public const string SpriteRoot = "Assets/Textures/PixelFish";
        public const string PartsRoot = "Assets/ScriptableObjects/Pixel/Parts";

        // World-unit sizing for each part type, derived from PixelArtSettings.bodyBoundsPixels.
        // Multipliers are RELATIVE to the body bounding box.
        public const float TailWidthMul = 0.44f;
        public const float TailHeightMul = 0.78f;
        public const float PectoralWidthMul = 0.28f;
        public const float PectoralHeightMul = 0.42f;
        public const float DorsalWidthMul = 0.38f;
        public const float DorsalHeightMul = 0.34f;
        public const float EyeSizeMul = 0.24f;     // square, intentionally oversized for cute fish
        public const float MouthWidthMul = 0.16f;
        public const float MouthHeightMul = 0.10f;

        [MenuItem("Tools/Aquarium/Pixel Art/2. Generate Fish Part Sprites", false, 110)]
        public static void GenerateAll()
        {
            PixelArtSettings settings = PixelArtFoundation.GetOrCreateSettings();
            FishPartLibrary library = PixelArtFoundation.GetOrCreateLibrary();
            if (settings == null || library == null)
            {
                Debug.LogError("PixelFishGenerator: bootstrap the foundation first (Tools/Aquarium/Pixel Art/1.).");
                return;
            }

            PixelArtFoundation.EnsureFolder(SpriteRoot);
            PixelArtFoundation.EnsureFolder(PartsRoot);

            // NOTE: we deliberately do NOT wrap this in
            // AssetDatabase.StartAssetEditing/StopAssetEditing. That defers
            // all imports until StopAssetEditing fires, which means
            // LoadAssetAtPath<Sprite> inside WriteSprite returns null and
            // every FishPart frame ends up with a null sprite ref. Importing
            // each PNG synchronously is slower but correct.
            library.Clear();

            GenerateBodies(settings, library);
            GenerateTails(settings, library);
            GeneratePectoralFins(settings, library);
            GenerateDorsalFins(settings, library);
            GenerateEyes(settings, library);
            GenerateMouths(settings, library);

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"PixelFishGenerator: generated {library.Parts.Count} parts under '{PartsRoot}/' " +
                      $"(sprites in '{SpriteRoot}/'). PPU={settings.pixelsPerUnit}, body={settings.bodyBoundsPixels}.");
        }

        // ------------------------------------------------------------------
        // Bodies
        // ------------------------------------------------------------------

        static void GenerateBodies(PixelArtSettings s, FishPartLibrary lib)
        {
            int W = s.bodyBoundsPixels.x;
            int H = s.bodyBoundsPixels.y;
            int frames = Mathf.Max(1, s.bodySwimFrames);

            for (int v = 0; v < s.bodyShapeCount; v++)
            {
                FishPart part = ScriptableObject.CreateInstance<FishPart>();
                part.partType = PixelPartType.Body;
                part.variantIndex = v;
                part.tierIndex = -1;
                part.displayName = $"Body_{v}";
                part.attachToBodyAnchor = FishAnchor.PectoralAttach; // unused for body itself
                part.frames = new FishPartFrame[frames];

                for (int f = 0; f < frames; f++)
                {
                    var canvas = new PixelCanvas(W, H);
                    SilhouetteFn body = PixelFishSilhouettes.Body(v, f, frames, W, H);
                    PixelArtRaster.RasterizeBody(canvas, body, outlineThicknessPx: 2);

                    string spritePath = $"{SpriteRoot}/Body_{v}_{f:00}.png";
                    Sprite sprite = WriteSprite(spritePath, canvas, s.pixelsPerUnit, SpriteAlignment.Center);

                    var frame = new FishPartFrame
                    {
                        sprite = sprite,
                        anchorOffsets = ComputeBodyAnchors(v, f, frames, W, H, s.pixelsPerUnit),
                    };
                    part.frames[f] = frame;
                }

                string assetPath = $"{PartsRoot}/Body_{v}.asset";
                FishPart persisted = CreateOrReplaceAsset(part, assetPath);
                lib.AddOrReplace(persisted);
            }
        }

        /// <summary>
        /// Per-frame anchor offsets for the body, in body-local world units.
        /// Origin = body sprite centre. Y-up matches Unity world space.
        /// </summary>
        static Vector2[] ComputeBodyAnchors(int variant, int frame, int totalFrames, int W, int H, int ppu)
        {
            Vector2[] anchors = new Vector2[(int)FishAnchor.Count];

            float upp = 1f / ppu;
            float halfWUnits = W * 0.5f * upp;
            Vector2 bodyRange = PixelFishSilhouettes.BodyXRange01(variant);
            float tailX = Mathf.Lerp(-halfWUnits, halfWUnits, bodyRange.x);
            float headX = Mathf.Lerp(-halfWUnits, halfWUnits, bodyRange.y);
            float bodyWidthU = Mathf.Max(1f * upp, headX - tailX);

            // Spine sway samples at t=0 (tail) and t=1 (head).
            float swayTail = PixelFishSilhouettes.SpineSway(0f, frame, totalFrames, 1.05f) * upp;
            float swayHead = PixelFishSilhouettes.SpineSway(1f, frame, totalFrames, 1.05f) * upp;

            float spineBiasTail = PixelFishSilhouettes.BodySpineBias(variant, 0f) * upp;
            float spineBiasHead = PixelFishSilhouettes.BodySpineBias(variant, 1f) * upp;

            // Half-heights at t = 0.5 (mid) and 0.95 (head) for fin / eye attach.
            float baseHalf = (H - 8) * 0.5f;
            float midHalfPx = PixelFishSilhouettes.BodyHalfHeight(variant, 0.5f, baseHalf);
            float headHalfPx = PixelFishSilhouettes.BodyHalfHeight(variant, 0.92f, baseHalf);
            float midHalfU = midHalfPx * upp;
            float headHalfU = headHalfPx * upp;

            // Tail: at t=0 (left edge), spine y.
            anchors[(int)FishAnchor.TailAttach] = new Vector2(tailX + 5f * upp, swayTail + spineBiasTail);

            // Pectoral: roughly mid-body, just below spine (lower half).
            anchors[(int)FishAnchor.PectoralAttach] = new Vector2(
                tailX + bodyWidthU * 0.58f,
                PixelFishSilhouettes.SpineSway(0.60f, frame, totalFrames, 1.05f) * upp
                + PixelFishSilhouettes.BodySpineBias(variant, 0.55f) * upp
                - midHalfU * 0.18f);

            // Dorsal: slightly inside the top contour so the body hides the root.
            anchors[(int)FishAnchor.DorsalAttach] = new Vector2(
                tailX + bodyWidthU * 0.48f,
                PixelFishSilhouettes.SpineSway(0.48f, frame, totalFrames, 1.05f) * upp
                + PixelFishSilhouettes.BodySpineBias(variant, 0.50f) * upp
                + midHalfU * 0.58f);

            // Eye: high on the head.
            anchors[(int)FishAnchor.EyeAttach] = new Vector2(
                tailX + bodyWidthU * 0.78f,
                swayHead + spineBiasHead + headHalfU * 0.26f);

            // Mouth: scan the actual body silhouette and inset from the visible snout,
            // so every generated body shape keeps the mouth on the fish.
            anchors[(int)FishAnchor.MouthAttach] = ComputeMouthAnchor(variant, frame, totalFrames, W, H, ppu);

            return anchors;
        }

        static Vector2 ComputeMouthAnchor(int variant, int frame, int totalFrames, int W, int H, int ppu)
        {
            SilhouetteFn body = PixelFishSilhouettes.Body(variant, frame, totalFrames, W, H);
            float upp = 1f / ppu;
            float baseHalf = (H - 8) * 0.5f;
            float targetY = H * 0.5f
                + PixelFishSilhouettes.SpineSway(1f, frame, totalFrames, 1.05f)
                + PixelFishSilhouettes.BodySpineBias(variant, 1f)
                + PixelFishSilhouettes.BodyHalfHeight(variant, 0.92f, baseHalf) * 0.03f;

            for (int x = W - 1; x >= 0; x--)
            {
                int minY = int.MaxValue;
                int maxY = int.MinValue;
                bool any = false;
                for (int y = 0; y < H; y++)
                {
                    if (!body(x, y))
                        continue;
                    any = true;
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }

                if (!any)
                    continue;

                float centerY = (minY + maxY) * 0.5f;
                float yPx = Mathf.Lerp(centerY, targetY, 0.35f);
                float xPx = Mathf.Max(0f, x - 4f);
                return new Vector2(
                    (xPx - W * 0.5f) * upp,
                    (yPx - H * 0.5f) * upp);
            }

            return Vector2.zero;
        }

        // ------------------------------------------------------------------
        // Tails
        // ------------------------------------------------------------------

        static void GenerateTails(PixelArtSettings s, FishPartLibrary lib)
        {
            int W = Mathf.Max(8, Mathf.RoundToInt(s.bodyBoundsPixels.x * TailWidthMul));
            int H = Mathf.Max(8, Mathf.RoundToInt(s.bodyBoundsPixels.y * TailHeightMul));
            int frames = Mathf.Max(1, s.tailWagFrames);

            for (int v = 0; v < s.tailTypeCount; v++)
            {
                FishPart part = ScriptableObject.CreateInstance<FishPart>();
                part.partType = PixelPartType.Tail;
                part.variantIndex = v;
                part.tierIndex = -1;
                part.displayName = $"Tail_{v}";
                part.attachToBodyAnchor = FishAnchor.TailAttach;
                part.frames = new FishPartFrame[frames];

                for (int f = 0; f < frames; f++)
                {
                    var canvas = new PixelCanvas(W, H);
                    SilhouetteFn tailFn = PixelFishSilhouettes.Tail(v, f, frames, W, H);
                    // attachEdge: from attach (right) toward tip (left), in pixel coords.
                    PixelArtRaster.RasterizeFinOrTail(canvas, tailFn, attachEdge: new Vector2(-1f, 0f), outlineThicknessPx: 1, rayBandPx: 6);

                    string spritePath = $"{SpriteRoot}/Tail_{v}_{f:00}.png";
                    // Pivot at right-center: the tail anchor on the body.
                    Sprite sprite = WriteSprite(spritePath, canvas, s.pixelsPerUnit, SpriteAlignment.RightCenter);

                    part.frames[f] = new FishPartFrame { sprite = sprite, anchorOffsets = new Vector2[(int)FishAnchor.Count] };
                }

                FishPart persisted = CreateOrReplaceAsset(part, $"{PartsRoot}/Tail_{v}.asset");
                lib.AddOrReplace(persisted);
            }
        }

        // ------------------------------------------------------------------
        // Pectoral / Dorsal Fins
        // ------------------------------------------------------------------

        static void GeneratePectoralFins(PixelArtSettings s, FishPartLibrary lib)
        {
            int W = Mathf.Max(8, Mathf.RoundToInt(s.bodyBoundsPixels.x * PectoralWidthMul));
            int H = Mathf.Max(8, Mathf.RoundToInt(s.bodyBoundsPixels.y * PectoralHeightMul));
            int frames = Mathf.Max(1, s.finFlutterFrames);

            for (int v = 0; v < s.pectoralFinCount; v++)
            {
                FishPart part = ScriptableObject.CreateInstance<FishPart>();
                part.partType = PixelPartType.PectoralFin;
                part.variantIndex = v;
                part.tierIndex = -1;
                part.displayName = $"Pectoral_{v}";
                part.attachToBodyAnchor = FishAnchor.PectoralAttach;
                part.frames = new FishPartFrame[frames];

                for (int f = 0; f < frames; f++)
                {
                    var canvas = new PixelCanvas(W, H);
                    SilhouetteFn finFn = PixelFishSilhouettes.PectoralFin(v, f, frames, W, H);
                    PixelArtRaster.RasterizeFinOrTail(canvas, finFn, attachEdge: new Vector2(0f, 1f), outlineThicknessPx: 1, rayBandPx: 4);

                    string spritePath = $"{SpriteRoot}/Pectoral_{v}_{f:00}.png";
                    // Pivot at top-center: attaches to the body's PectoralAttach anchor.
                    Sprite sprite = WriteSprite(spritePath, canvas, s.pixelsPerUnit, SpriteAlignment.TopCenter);

                    part.frames[f] = new FishPartFrame { sprite = sprite, anchorOffsets = new Vector2[(int)FishAnchor.Count] };
                }

                FishPart persisted = CreateOrReplaceAsset(part, $"{PartsRoot}/Pectoral_{v}.asset");
                lib.AddOrReplace(persisted);
            }
        }

        static void GenerateDorsalFins(PixelArtSettings s, FishPartLibrary lib)
        {
            int W = Mathf.Max(8, Mathf.RoundToInt(s.bodyBoundsPixels.x * DorsalWidthMul));
            int H = Mathf.Max(8, Mathf.RoundToInt(s.bodyBoundsPixels.y * DorsalHeightMul));
            int frames = Mathf.Max(1, s.finFlutterFrames);

            for (int v = 0; v < s.dorsalFinCount; v++)
            {
                FishPart part = ScriptableObject.CreateInstance<FishPart>();
                part.partType = PixelPartType.DorsalFin;
                part.variantIndex = v;
                part.tierIndex = -1;
                part.displayName = $"Dorsal_{v}";
                part.attachToBodyAnchor = FishAnchor.DorsalAttach;
                part.frames = new FishPartFrame[frames];

                for (int f = 0; f < frames; f++)
                {
                    var canvas = new PixelCanvas(W, H);
                    SilhouetteFn finFn = PixelFishSilhouettes.DorsalFin(v, f, frames, W, H);
                    PixelArtRaster.RasterizeFinOrTail(canvas, finFn, attachEdge: new Vector2(0f, -1f), outlineThicknessPx: 1, rayBandPx: 4);

                    string spritePath = $"{SpriteRoot}/Dorsal_{v}_{f:00}.png";
                    // Pivot at bottom-center: attaches to body's DorsalAttach anchor.
                    Sprite sprite = WriteSprite(spritePath, canvas, s.pixelsPerUnit, SpriteAlignment.BottomCenter);

                    part.frames[f] = new FishPartFrame { sprite = sprite, anchorOffsets = new Vector2[(int)FishAnchor.Count] };
                }

                FishPart persisted = CreateOrReplaceAsset(part, $"{PartsRoot}/Dorsal_{v}.asset");
                lib.AddOrReplace(persisted);
            }
        }

        // ------------------------------------------------------------------
        // Eyes — sized per tier; blink frames optional
        // ------------------------------------------------------------------

        static void GenerateEyes(PixelArtSettings s, FishPartLibrary lib)
        {
            int side = Mathf.Max(8, Mathf.RoundToInt(s.bodyBoundsPixels.y * EyeSizeMul));
            // Even side count keeps the iris symmetric.
            if ((side & 1) == 1) side++;
            int frames = Mathf.Max(1, s.eyeBlinkFrames);

            for (int tier = 0; tier < s.eyeSizeTierCount; tier++)
            {
                FishPart part = ScriptableObject.CreateInstance<FishPart>();
                part.partType = PixelPartType.Eye;
                part.variantIndex = 0;
                part.tierIndex = tier;
                part.displayName = $"Eye_T{tier}";
                part.attachToBodyAnchor = FishAnchor.EyeAttach;
                part.frames = new FishPartFrame[frames];
                part.playsOneShotOnly = true; // blink is event-driven

                for (int f = 0; f < frames; f++)
                {
                    var canvas = new PixelCanvas(side, side);
                    PixelFishSilhouettes.DrawEye(canvas, tier, f, frames, s.eyeSizeTierCount);

                    string spritePath = $"{SpriteRoot}/Eye_T{tier}_{f:00}.png";
                    Sprite sprite = WriteSprite(spritePath, canvas, s.pixelsPerUnit, SpriteAlignment.Center);

                    part.frames[f] = new FishPartFrame { sprite = sprite, anchorOffsets = new Vector2[(int)FishAnchor.Count] };
                }

                FishPart persisted = CreateOrReplaceAsset(part, $"{PartsRoot}/Eye_T{tier}.asset");
                lib.AddOrReplace(persisted);
            }
        }

        // ------------------------------------------------------------------
        // Mouth (lip_type) — single static frame each
        // ------------------------------------------------------------------

        static void GenerateMouths(PixelArtSettings s, FishPartLibrary lib)
        {
            int W = Mathf.Max(8, Mathf.RoundToInt(s.bodyBoundsPixels.x * MouthWidthMul));
            int H = Mathf.Max(6, Mathf.RoundToInt(s.bodyBoundsPixels.y * MouthHeightMul));
            // Ensure odd x even etc. — keep simple for now.

            for (int v = 0; v < s.mouthTypeCount; v++)
            {
                FishPart part = ScriptableObject.CreateInstance<FishPart>();
                part.partType = PixelPartType.Mouth;
                part.variantIndex = v;
                part.tierIndex = -1;
                part.displayName = $"Mouth_{v}";
                part.attachToBodyAnchor = FishAnchor.MouthAttach;
                part.frames = new FishPartFrame[1];

                var canvas = new PixelCanvas(W, H);
                SilhouetteFn mouthFn = PixelFishSilhouettes.Mouth(v, W, H);
                // Mouths render thin so we draw without outline ring (outline pixels would dominate).
                EncodeSimpleShape(canvas, mouthFn, shade: 0.15f);

                string spritePath = $"{SpriteRoot}/Mouth_{v}.png";
                Sprite sprite = WriteSprite(spritePath, canvas, s.pixelsPerUnit, SpriteAlignment.LeftCenter);

                part.frames[0] = new FishPartFrame { sprite = sprite, anchorOffsets = new Vector2[(int)FishAnchor.Count] };

                FishPart persisted = CreateOrReplaceAsset(part, $"{PartsRoot}/Mouth_{v}.asset");
                lib.AddOrReplace(persisted);
            }
        }

        /// <summary>
        /// Quick encoding for thin shapes: every inside pixel becomes outline-tier
        /// with full silhouette. Good for mouth lines / accent details.
        /// </summary>
        static void EncodeSimpleShape(PixelCanvas canvas, SilhouetteFn inside, float shade)
        {
            for (int y = 0; y < canvas.height; y++)
            for (int x = 0; x < canvas.width; x++)
            {
                if (!inside(x, y))
                {
                    canvas.SetEncoded(x, y, 0f, 0f, 0f, 0f);
                    continue;
                }
                canvas.SetEncoded(x, y, shade, 0f, 0f, 1f); // interior=0 → renders as outline colour
            }
        }

        // ------------------------------------------------------------------
        // Asset I/O
        // ------------------------------------------------------------------

        static Sprite WriteSprite(string path, PixelCanvas canvas, int ppu, SpriteAlignment alignment)
        {
            Texture2D tex = new Texture2D(canvas.width, canvas.height, TextureFormat.RGBA32, false, false);
            tex.SetPixels(canvas.pixels);
            tex.Apply(false);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            // ForceSynchronousImport guarantees the importer runs before we
            // try to read the resulting Sprite back. Without this — or when
            // wrapped in StartAssetEditing — LoadAssetAtPath returns null and
            // every FishPart frame ends up with a missing sprite ref.
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ConfigureSpriteImport(path, ppu, alignment);

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogWarning($"PixelFishGenerator: failed to load sprite at '{path}' after import. " +
                                 "FishPart frames referencing this sprite will be invisible.");
            return sprite;
        }

        static void ConfigureSpriteImport(string path, int ppu, SpriteAlignment alignment)
        {
            TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null)
                return;

            TextureImporterSettings settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);

            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.alphaIsTransparency = true;
            settings.spriteAlignment = (int)alignment;
            settings.spritePixelsPerUnit = ppu;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 0;
            // CRITICAL for pixel art: point filtering, no compression, no mipmaps.
            settings.filterMode = FilterMode.Point;
            settings.wrapMode = TextureWrapMode.Clamp;
            settings.mipmapEnabled = false;

            imp.SetTextureSettings(settings);
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }

        /// <summary>
        /// Persist <paramref name="part"/> at <paramref name="path"/>, reusing
        /// the existing on-disk asset if one is present so external references
        /// (library entries, prefab refs) keep resolving. Returns the
        /// PERSISTED FishPart — callers must use the return value because the
        /// in-memory <paramref name="part"/> may be destroyed when an existing
        /// asset is overwritten.
        /// </summary>
        static FishPart CreateOrReplaceAsset(FishPart part, string path)
        {
            FishPart existing = AssetDatabase.LoadAssetAtPath<FishPart>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(part, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(part);
                return existing;
            }

            AssetDatabase.CreateAsset(part, path);
            return part;
        }
    }
}
