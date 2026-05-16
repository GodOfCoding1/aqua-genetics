using System.IO;
using Aquarium.PixelArt;
using UnityEditor;
using UnityEngine;

namespace Aquarium.PixelArt.EditorTools
{
    /// <summary>
    /// Procedurally generates pixel-art environment textures (water, gravel,
    /// bubble frames, glass tile, plant silhouettes) at the resolution
    /// specified in <see cref="PixelArtSettings"/>. Outputs go to
    /// <c>Assets/Textures/PixelEnvironment/</c>.
    /// </summary>
    public static class PixelEnvironmentGenerator
    {
        public const string EnvFolder = "Assets/Textures/PixelEnvironment";

        // Bayer 4x4 ordered-dither matrix → cheap, classic pixel-art gradient.
        // Values 0..15 mapped to threshold 1/17 .. 16/17.
        static readonly int[,] Bayer4 =
        {
            {  0,  8,  2, 10 },
            { 12,  4, 14,  6 },
            {  3, 11,  1,  9 },
            { 15,  7, 13,  5 },
        };

        public static (Sprite water, Sprite gravel, Sprite glass, Sprite[] bubbles, Sprite[] plants, Sprite[] corals, Sprite[] silhouettes) Generate(PixelArtSettings settings)
        {
            PixelArtFoundation.EnsureFolder(EnvFolder);
            int ppu = settings != null ? settings.pixelsPerUnit : 64;

            Sprite water = WriteSprite($"{EnvFolder}/Water.png", DrawWaterColumn(),       1, 256, ppu, SpriteAlignment.Center);
            Sprite gravel = WriteSprite($"{EnvFolder}/Gravel.png", DrawGravelTile(),     64, 32, ppu, SpriteAlignment.Center);
            Sprite glass = WriteSprite($"{EnvFolder}/GlassTile.png", DrawGlassTile(),     8,  8, ppu, SpriteAlignment.Center);

            Sprite[] bubbles = new Sprite[4];
            for (int f = 0; f < 4; f++)
                bubbles[f] = WriteSprite($"{EnvFolder}/Bubble_{f:00}.png", DrawBubbleFrame(f, 4), 12, 12, ppu, SpriteAlignment.Center);

            Sprite[] plants = new Sprite[6];
            for (int v = 0; v < plants.Length; v++)
                plants[v] = WriteSprite($"{EnvFolder}/Plant_{v}.png", DrawPlant(v),     32,  96, ppu, SpriteAlignment.BottomCenter);

            Sprite[] corals = new Sprite[5];
            for (int v = 0; v < corals.Length; v++)
                corals[v] = WriteSprite($"{EnvFolder}/Coral_{v}.png", DrawCoral(v),     48,  56, ppu, SpriteAlignment.BottomCenter);

            Sprite[] silhouettes = new Sprite[3];
            for (int v = 0; v < silhouettes.Length; v++)
                silhouettes[v] = WriteSprite($"{EnvFolder}/DistantReef_{v}.png", DrawDistantReef(v), 96, 64, ppu, SpriteAlignment.BottomCenter);

            return (water, gravel, glass, bubbles, plants, corals, silhouettes);
        }

        // ------------------------------------------------------------------
        // Drawing
        // ------------------------------------------------------------------

        /// <summary>1-px-wide dithered vertical water gradient (top lighter, bottom darker).</summary>
        static Color[] DrawWaterColumn()
        {
            const int W = 1;
            const int H = 256;
            Color[] px = new Color[W * H];

            // Top lighter teal, bottom deeper navy.
            Color top = new Color(0.30f, 0.62f, 0.74f, 1f);
            Color bot = new Color(0.04f, 0.10f, 0.20f, 1f);

            for (int y = 0; y < H; y++)
            {
                float t = y / (float)(H - 1);
                Color c = Color.Lerp(bot, top, Mathf.Pow(t, 0.85f));
                // Quantise channels to 5-bit for retro feel.
                c.r = Mathf.Round(c.r * 31f) / 31f;
                c.g = Mathf.Round(c.g * 31f) / 31f;
                c.b = Mathf.Round(c.b * 31f) / 31f;
                px[y * W] = c;
            }
            return px;
        }

        /// <summary>64x32 gravel tile with pixel-level blended sand and pebble colour.</summary>
        static Color[] DrawGravelTile()
        {
            const int W = 64;
            const int H = 32;
            Color[] px = new Color[W * H];

            const float Tau = Mathf.PI * 2f;
            Color deepSand = new Color(0.54f, 0.46f, 0.33f);
            Color warmSand = new Color(0.74f, 0.62f, 0.42f);
            Color paleSand = new Color(0.93f, 0.84f, 0.62f);
            Color coolStone = new Color(0.62f, 0.66f, 0.61f);
            Color shellLight = new Color(1.00f, 0.92f, 0.74f);
            Color darkMineral = new Color(0.36f, 0.31f, 0.24f);

            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int b = Bayer4[y & 3, x & 3];
                float dither = (b + 0.5f) / 16f;
                float yt = y / (float)(H - 1);

                // Wrapped value-noise fields prevent repeated square patches and
                // avoid obvious directional bands in the substrate.
                float u = (x + 0.5f) / W;
                float warmField = ValueNoiseTileable(x, y, W, H, 7, 5, 0xBEEF) * 0.48f
                    + ValueNoiseTileable(x, y, W, H, 13, 9, 0xC0DE) * 0.34f
                    + Hash01(x, y, 0x2112) * 0.18f;
                float coolField = ValueNoiseTileable(x, y, W, H, 9, 6, 0x5EA1) * 0.62f
                    + ValueNoiseTileable(x, y, W, H, 18, 10, 0xA11E) * 0.38f;
                warmField = Mathf.Clamp01(warmField);
                coolField = Mathf.Clamp01(coolField);

                Color c = Color.Lerp(deepSand, warmSand, Mathf.Clamp01(warmField + 0.12f));
                c = Color.Lerp(c, paleSand, Mathf.Clamp01((warmField - dither * 0.55f) * 0.58f));
                if (coolField > dither + 0.36f)
                    c = Color.Lerp(c, coolStone, 0.24f);

                // Light top edge with a darker buried base gives the ground depth.
                float shade = Mathf.Lerp(0.58f, 1.10f, Mathf.Pow(yt, 0.75f));
                if (y < 4)
                    shade *= 0.62f;
                if (y > H - 5)
                    shade *= 1.08f;
                if (b < 3)
                    shade *= 0.96f;
                c *= shade;

                // Sparse single-pixel darker/lighter shell flecks, never full blocks.
                float grain = Hash01(x, y, 0x51ED);
                if (grain < 0.025f)
                    c = Color.Lerp(c, darkMineral, 0.48f);
                else if (grain > 0.975f)
                    c = Color.Lerp(c, shellLight, 0.58f);
                else if (Hash01(x, y, 0xA7E1) > 0.70f + warmField * 0.18f)
                    c = Color.Lerp(c, grain > dither ? paleSand : warmSand, 0.16f);

                // A soft wave band along the top keeps it from being a flat rectangle.
                float topWave = H - 5f
                    + Mathf.Sin(Tau * u * 3f) * 1.1f
                    + Mathf.Sin(Tau * u * 7f + 0.8f) * 0.45f;
                if (y > topWave)
                    c = Color.Lerp(c, shellLight, 0.28f);

                px[y * W + x] = new Color(c.r, c.g, c.b, 1f);
            }
            return px;
        }

        /// <summary>8x8 translucent tile for the glass frame (used Sliced).</summary>
        static Color[] DrawGlassTile()
        {
            const int W = 8;
            const int H = 8;
            Color[] px = new Color[W * H];
            Color tint = new Color(0.78f, 0.94f, 1.0f, 0.40f);
            for (int i = 0; i < px.Length; i++)
                px[i] = tint;
            return px;
        }

        /// <summary>12x12 bubble frame: ring + small highlight, animated by frame index.</summary>
        static Color[] DrawBubbleFrame(int frame, int totalFrames)
        {
            const int S = 12;
            Color[] px = new Color[S * S];
            float c = (S - 1) * 0.5f;
            // Pulse radius ±1 px across frames.
            float r = 4.5f + Mathf.Sin(frame * Mathf.PI * 2f / totalFrames) * 0.6f;
            float rInner = r - 1.6f;
            float hx = c - 1.5f;
            float hy = c + 1.5f;

            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = x - c;
                float dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                bool ring = d <= r + 0.4f && d >= rInner;
                bool highlight = (Mathf.Abs(x - hx) < 1.2f && Mathf.Abs(y - hy) < 1.2f);

                Color out_;
                if (highlight)
                    out_ = new Color(1f, 1f, 1f, 0.9f);
                else if (ring)
                    out_ = new Color(0.85f, 0.95f, 1f, 0.85f);
                else
                    out_ = new Color(0f, 0f, 0f, 0f);
                px[y * S + x] = out_;
            }
            return px;
        }

        /// <summary>32x96 plant silhouette: kelp, grass, and fronds for ambient decoration.</summary>
        static Color[] DrawPlant(int variant)
        {
            const int W = 32;
            const int H = 96;
            Color[] px = new Color[W * H];

            Color leafBase = new Color(0.16f, 0.58f, 0.34f, 1f);
            Color leafShade = new Color(0.05f, 0.26f, 0.16f, 1f);
            Color leafLight = new Color(0.40f, 0.88f, 0.50f, 1f);
            Color kelpGold = new Color(0.44f, 0.72f, 0.28f, 1f);

            void SetPlantPixel(int x, int y, Color c)
            {
                if ((uint)x >= W || (uint)y >= H)
                    return;
                Color existing = px[y * W + x];
                if (existing.a <= 0f || c.a >= existing.a)
                    px[y * W + x] = c;
            }

            void DrawBlade(float rootX, float height, float bend, float width, Color main, int phase)
            {
                int maxY = Mathf.Clamp(Mathf.RoundToInt(height), 8, H - 1);
                for (int y = 0; y < maxY; y++)
                {
                    float t = y / Mathf.Max(1f, maxY - 1f);
                    float center = rootX + Mathf.Sin(t * Mathf.PI * 0.9f + phase * 0.3f) * bend * t;
                    float half = Mathf.Max(0.65f, width * (1f - t * 0.82f));
                    int minX = Mathf.FloorToInt(center - half - 1f);
                    int maxX = Mathf.CeilToInt(center + half + 1f);
                    for (int x = minX; x <= maxX; x++)
                    {
                        float dx = Mathf.Abs(x - center);
                        if (dx > half)
                            continue;
                        float edge = dx / Mathf.Max(0.01f, half);
                        Color c = edge > 0.72f ? leafShade : (edge < 0.22f ? leafLight : main);
                        if (((y + phase) % 13) == 0 && edge < 0.6f)
                            c = Color.Lerp(c, leafShade, 0.35f);
                        SetPlantPixel(x, y, c);
                    }
                }
            }

            void DrawStem(float rootX, float height, float sway, Color main)
            {
                int maxY = Mathf.Clamp(Mathf.RoundToInt(height), 10, H - 1);
                for (int y = 0; y < maxY; y++)
                {
                    float t = y / Mathf.Max(1f, maxY - 1f);
                    int x = Mathf.RoundToInt(rootX + Mathf.Sin(t * Mathf.PI * 2.3f) * sway);
                    SetPlantPixel(x - 1, y, leafShade);
                    SetPlantPixel(x, y, main);
                    SetPlantPixel(x + 1, y, leafLight);

                    if (variant == 4 && y % 10 < 5)
                    {
                        SetPlantPixel(x - 2, y, leafShade);
                        SetPlantPixel(x + 2, y, main);
                    }
                }
            }

            switch (variant)
            {
                case 0: // cluster of long grass blades
                    DrawBlade(8f, 70f, -5f, 2.8f, leafBase, 0);
                    DrawBlade(15f, 92f, 4f, 2.5f, leafBase, 4);
                    DrawBlade(22f, 66f, 6f, 2.2f, leafBase, 8);
                    break;
                case 1: // wavy kelp
                    DrawStem(12f, 92f, 3.5f, kelpGold);
                    DrawStem(18f, 84f, -3.0f, leafBase);
                    DrawBlade(24f, 58f, 5f, 1.8f, leafBase, 6);
                    break;
                case 2: // fine foreground grass
                    for (int i = 0; i < 7; i++)
                        DrawBlade(4f + i * 4f, 34f + (i % 3) * 18f, -5f + i * 1.7f, 1.4f, leafBase, i * 3);
                    break;
                case 3: // broad leafy plant
                    DrawBlade(10f, 74f, -6f, 3.6f, leafBase, 2);
                    DrawBlade(17f, 88f, 2.5f, 4.0f, leafBase, 9);
                    DrawBlade(24f, 64f, 6f, 3.2f, leafBase, 5);
                    break;
                case 4: // segmented kelp stems
                    DrawStem(9f, 86f, 2.5f, kelpGold);
                    DrawStem(16f, 96f, -2.2f, leafBase);
                    DrawStem(23f, 78f, 2.0f, kelpGold);
                    break;
                default: // thin swaying grass
                    DrawBlade(9f, 78f, -7f, 1.7f, leafBase, 1);
                    DrawBlade(15f, 88f, 3f, 1.6f, leafBase, 5);
                    DrawBlade(21f, 72f, 7f, 1.5f, leafBase, 10);
                    break;
            }

            // Small dark base clump hides plant roots in the gravel.
            for (int y = 0; y < 7; y++)
            for (int x = 6; x < W - 5; x++)
            {
                if (((x + y) & 1) == 0)
                    SetPlantPixel(x, y, y < 2 ? leafShade : leafBase);
            }
            return px;
        }

        static Color[] DrawCoral(int variant)
        {
            const int W = 48;
            const int H = 56;
            Color[] px = new Color[W * H];
            Color main = variant switch
            {
                0 => new Color(0.92f, 0.24f, 0.30f, 1f),
                1 => new Color(0.90f, 0.42f, 0.72f, 1f),
                2 => new Color(0.95f, 0.55f, 0.25f, 1f),
                3 => new Color(0.55f, 0.32f, 0.75f, 1f),
                _ => new Color(0.92f, 0.30f, 0.42f, 1f),
            };
            Color shade = main * 0.58f; shade.a = 1f;
            Color light = Color.Lerp(main, Color.white, 0.35f); light.a = 1f;

            void DrawBranch(float x0, float y0, float x1, float y1, float radius)
            {
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float vx = x1 - x0;
                    float vy = y1 - y0;
                    float len2 = vx * vx + vy * vy;
                    float u = len2 <= 0.001f ? 0f : Mathf.Clamp01(((x - x0) * vx + (y - y0) * vy) / len2);
                    float px0 = x0 + vx * u;
                    float py0 = y0 + vy * u;
                    float d = Mathf.Sqrt((x - px0) * (x - px0) + (y - py0) * (y - py0));
                    if (d > radius) continue;
                    Color c = d > radius - 1.2f ? shade : (x < px0 ? light : main);
                    px[y * W + x] = c;
                }
            }

            float baseX = W * 0.5f;
            DrawBranch(baseX, 0f, baseX, H * 0.68f, 4.5f);
            DrawBranch(baseX, H * 0.22f, baseX - 14f, H * 0.52f, 3.2f);
            DrawBranch(baseX, H * 0.35f, baseX + 16f, H * 0.70f, 3.0f);
            DrawBranch(baseX - 4f, H * 0.46f, baseX - 20f, H * 0.82f, 2.5f);
            if ((variant & 1) == 0)
                DrawBranch(baseX + 2f, H * 0.18f, baseX + 18f, H * 0.45f, 2.7f);
            else
                DrawBranch(baseX - 2f, H * 0.58f, baseX + 10f, H * 0.92f, 2.2f);
            return px;
        }

        static Color[] DrawDistantReef(int variant)
        {
            const int W = 96;
            const int H = 64;
            Color[] px = new Color[W * H];
            Color c = variant switch
            {
                0 => new Color(0.06f, 0.23f, 0.36f, 0.38f),
                1 => new Color(0.04f, 0.18f, 0.32f, 0.32f),
                _ => new Color(0.08f, 0.28f, 0.34f, 0.30f),
            };

            for (int x = 0; x < W; x++)
            {
                float n = Mathf.Sin((x + variant * 17f) * 0.11f) * 9f
                    + Mathf.Sin((x + variant * 31f) * 0.27f) * 4f;
                int h = Mathf.RoundToInt(18f + n + (variant == 1 ? 8f : 0f));
                for (int y = 0; y <= h && y < H; y++)
                    px[y * W + x] = c;
            }
            return px;
        }

        // ------------------------------------------------------------------
        // I/O
        // ------------------------------------------------------------------

        static float Hash01(int x, int y, int salt)
        {
            unchecked
            {
                int h = x * 73856093 ^ y * 19349663 ^ salt * 83492791;
                h ^= h >> 13;
                h *= 1274126177;
                return (h & 0x00FFFFFF) / 16777215f;
            }
        }

        static float ValueNoiseTileable(int x, int y, int width, int height, int cellsX, int cellsY, int salt)
        {
            float gx = (x + 0.5f) * cellsX / width;
            float gy = (y + 0.5f) * cellsY / height;
            int x0 = Mathf.FloorToInt(gx);
            int y0 = Mathf.FloorToInt(gy);
            float tx = SmoothStep(gx - x0);
            float ty = SmoothStep(gy - y0);

            int x1 = (x0 + 1) % cellsX;
            int y1 = (y0 + 1) % cellsY;
            x0 %= cellsX;
            y0 %= cellsY;

            float a = Hash01(x0, y0, salt);
            float b = Hash01(x1, y0, salt);
            float c = Hash01(x0, y1, salt);
            float d = Hash01(x1, y1, salt);

            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        static float SmoothStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        static Sprite WriteSprite(string path, Color[] pixels, int width, int height, int ppu, SpriteAlignment alignment)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            tex.SetPixels(pixels);
            tex.Apply(false);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureImport(path, ppu, alignment);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void ConfigureImport(string path, int ppu, SpriteAlignment alignment)
        {
            TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            TextureImporterSettings s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.textureType = TextureImporterType.Sprite;
            s.spriteMode = (int)SpriteImportMode.Single;
            s.alphaIsTransparency = true;
            s.spriteAlignment = (int)alignment;
            s.spritePixelsPerUnit = ppu;
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteExtrude = 0;
            s.filterMode = FilterMode.Point;
            s.wrapMode = TextureWrapMode.Repeat; // tileable for water / gravel / glass
            s.mipmapEnabled = false;
            imp.SetTextureSettings(s);
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
    }
}
