using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bakes the procedural eye / tail (×6) / side-fin (×5) / glow PNGs and configures
/// their import settings as Sprites with sensible pivots and pixels-per-unit.
/// </summary>
public static class FishSpriteGenerator
{
    public const string SpriteFolder = "Assets/Textures/Fish";

    public const int TailCount = 6;
    public const int FinCount = 5;

    const int EyeSize = 128;
    const int OverlaySize = 256;
    const int GlowSize = 256;

    const int EyePpu = 400;
    const int TailPpu = 600;
    const int FinPpu = 600;
    const int GlowPpu = 128;

    [MenuItem("Tools/Aquarium/Legacy/Bake Fish Sprites", false, 902)]
    public static void GenerateAll()
    {
        EnsureFolder(SpriteFolder);

        WriteSprite($"{SpriteFolder}/Eye.png", DrawEye(EyeSize), EyeSize, EyeSize, EyePpu, SpriteAlignment.Center);

        for (int i = 0; i < TailCount; i++)
        {
            string p = $"{SpriteFolder}/Tail_{i}.png";
            WriteSprite(p, DrawTail(i, OverlaySize), OverlaySize, OverlaySize, TailPpu, SpriteAlignment.RightCenter);
        }

        for (int i = 0; i < FinCount; i++)
        {
            string p = $"{SpriteFolder}/Fin_{i}.png";
            WriteSprite(p, DrawFin(i, OverlaySize), OverlaySize, OverlaySize, FinPpu, SpriteAlignment.TopCenter);
        }

        WriteSprite($"{SpriteFolder}/Glow.png", DrawGlow(GlowSize), GlowSize, GlowSize, GlowPpu, SpriteAlignment.Center);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Aquarium: baked fish sprites into '{SpriteFolder}/' (1 eye, {TailCount} tails, {FinCount} side fins, 1 glow).");
    }

    public static Sprite LoadOrBakeEye()
    {
        return LoadOrBake($"{SpriteFolder}/Eye.png", () => GenerateAll());
    }

    public static Sprite LoadOrBakeTail(int type)
    {
        return LoadOrBake($"{SpriteFolder}/Tail_{Mathf.Clamp(type, 0, TailCount - 1)}.png", () => GenerateAll());
    }

    public static Sprite LoadOrBakeFin(int shape)
    {
        return LoadOrBake($"{SpriteFolder}/Fin_{Mathf.Clamp(shape, 0, FinCount - 1)}.png", () => GenerateAll());
    }

    public static Sprite LoadOrBakeGlow()
    {
        return LoadOrBake($"{SpriteFolder}/Glow.png", () => GenerateAll());
    }

    static Sprite LoadOrBake(string path, System.Action regenerate)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null)
            return s;
        regenerate();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // -- Drawing ----------------------------------------------------------

    static Color[] DrawEye(int size)
    {
        Color[] data = new Color[size * size];
        float cx = (size - 1) * 0.5f;
        float cy = (size - 1) * 0.5f;
        float irisR = size * 0.36f;
        float pupilR = size * 0.16f;
        float highlightR = size * 0.06f;
        Vector2 highlightOffset = new Vector2(size * 0.10f, size * 0.10f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - cx;
            float dy = y - cy;
            float d = Mathf.Sqrt(dx * dx + dy * dy);

            float irisAlpha = SmoothStepAa(d, irisR, 1.2f);
            float pupilMask = SmoothStepAa(d, pupilR, 1.0f);

            float hx = x - (cx + highlightOffset.x);
            float hy = y - (cy + highlightOffset.y);
            float hd = Mathf.Sqrt(hx * hx + hy * hy);
            float highlightMask = SmoothStepAa(hd, highlightR, 0.8f);

            // Iris is white -> tinted at runtime; pupil overrides to black; highlight to bright white.
            float r = 1f, g = 1f, b = 1f;
            if (pupilMask > 0f)
            {
                r = Mathf.Lerp(r, 0.05f, pupilMask);
                g = Mathf.Lerp(g, 0.05f, pupilMask);
                b = Mathf.Lerp(b, 0.05f, pupilMask);
            }
            if (highlightMask > 0f)
            {
                r = Mathf.Lerp(r, 1f, highlightMask);
                g = Mathf.Lerp(g, 1f, highlightMask);
                b = Mathf.Lerp(b, 1f, highlightMask);
            }

            data[y * size + x] = new Color(r, g, b, irisAlpha);
        }

        return data;
    }

    static Color[] DrawTail(int type, int size)
    {
        Color[] data = new Color[size * size];
        // Pivot is right-center: attachment at (size-1, size/2). dx grows leftward.
        float ax = size - 1f;
        float ay = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = ax - x;
            float dy = y - ay;
            float a = TailAlpha(type, dx, dy, size);
            data[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        }
        return data;
    }

    static float TailAlpha(int type, float dx, float dy, int size)
    {
        if (dx < -1f) return 0f;

        switch (type)
        {
            case 0: // Fan
            {
                float maxX = size * 0.78f;
                float halfHt = size * 0.04f + dx * 0.45f;
                float yIn = halfHt - Mathf.Abs(dy);
                float xIn = Mathf.Min(dx, maxX - dx);
                return AaInside(Mathf.Min(yIn, xIn));
            }
            case 1: // Veil — long flowing
            {
                float maxX = size * 0.95f;
                float halfHt = size * 0.025f + dx * 0.18f;
                if (dx > maxX * 0.6f)
                    halfHt += (dx - maxX * 0.6f) * 0.15f;
                float yIn = halfHt - Mathf.Abs(dy);
                float xIn = Mathf.Min(dx + 1f, maxX - dx);
                return AaInside(Mathf.Min(yIn, xIn));
            }
            case 2: // Lyre — two prongs (top + bottom)
            {
                float maxX = size * 0.78f;
                float prongOffset = size * 0.22f;
                float prongHalfHt = size * 0.05f + dx * 0.16f;

                float topYIn = prongHalfHt - Mathf.Abs(dy - prongOffset);
                float botYIn = prongHalfHt - Mathf.Abs(dy + prongOffset);
                float xIn = Mathf.Min(dx, maxX - dx);

                float topAlpha = AaInside(Mathf.Min(topYIn, xIn));
                float botAlpha = AaInside(Mathf.Min(botYIn, xIn));

                // Thin connector at base
                float connectorHalfHt = size * 0.04f;
                float connectorXMax = size * 0.18f;
                float connectorAlpha = AaInside(Mathf.Min(
                    connectorHalfHt - Mathf.Abs(dy),
                    Mathf.Min(dx, connectorXMax - dx)));

                return Mathf.Max(Mathf.Max(topAlpha, botAlpha), connectorAlpha);
            }
            case 3: // Rounded
            {
                float r = size * 0.40f;
                float cx = size * 0.30f;
                float dist = Mathf.Sqrt((dx - cx) * (dx - cx) + dy * dy);
                float yIn = r - dist;
                float xIn = dx;
                return AaInside(Mathf.Min(yIn, xIn));
            }
            case 4: // Forked — strong V split
            {
                float maxX = size * 0.78f;
                float halfHt = size * 0.04f + dx * 0.50f;
                float yIn = halfHt - Mathf.Abs(dy);
                float xIn = Mathf.Min(dx, maxX - dx);
                float fanAlpha = AaInside(Mathf.Min(yIn, xIn));

                // Cut a triangular notch out of the back.
                float notchWidth = size * 0.42f;
                float notchDepth = (notchWidth - dx) * 0.95f;
                float notchYHalf = Mathf.Max(notchDepth, 0f);
                float notchInside = (dx > maxX - notchWidth ? 1f : 0f) * (notchYHalf - Mathf.Abs(dy));
                float notchAlpha = AaInside(notchInside);
                return Mathf.Clamp01(fanAlpha - notchAlpha);
            }
            case 5: // Halfmoon — large round fan
            {
                float r = size * 0.55f;
                float cx = size * 0.20f;
                float dist = Mathf.Sqrt((dx - cx) * (dx - cx) + dy * dy);
                float yIn = r - dist;
                float xIn = dx;
                return AaInside(Mathf.Min(yIn, xIn));
            }
        }

        return 0f;
    }

    static Color[] DrawFin(int shape, int size)
    {
        Color[] data = new Color[size * size];
        // Pivot is top-center: attachment at (size/2, size-1). dy grows downward (negative y from pivot).
        float ax = (size - 1) * 0.5f;
        float ay = size - 1f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - ax;
            float dy = ay - y; // grows downward from attachment
            float a = FinAlpha(shape, dx, dy, size);
            data[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        }
        return data;
    }

    static float FinAlpha(int shape, float dx, float dy, int size)
    {
        if (dy < -1f) return 0f;

        switch (shape)
        {
            case 0: // Round — small rounded oval pectoral
            {
                float rx = size * 0.18f;
                float ry = size * 0.30f;
                float ndx = dx / rx;
                float ndy = (dy - ry) / ry;
                float dist = Mathf.Sqrt(ndx * ndx + ndy * ndy);
                return AaInside(1f - dist) * 0.95f;
            }
            case 1: // Pointed — slim teardrop
            {
                float maxY = size * 0.40f;
                float halfWd = size * 0.10f * (1f - Mathf.Clamp01(dy / maxY));
                float yIn = Mathf.Min(dy, maxY - dy);
                float xIn = halfWd - Mathf.Abs(dx);
                return AaInside(Mathf.Min(yIn, xIn));
            }
            case 2: // Spike — long pointed
            {
                float maxY = size * 0.55f;
                float halfWd = size * 0.06f * (1f - Mathf.Clamp01(dy / maxY));
                float yIn = Mathf.Min(dy, maxY - dy);
                float xIn = halfWd - Mathf.Abs(dx);
                return AaInside(Mathf.Min(yIn, xIn));
            }
            case 3: // Sail — tall fan
            {
                float maxY = size * 0.45f;
                float halfWd = size * 0.07f + dy * 0.22f;
                if (dy > maxY) halfWd -= (dy - maxY) * 0.5f;
                float yIn = Mathf.Min(dy, maxY * 1.15f - dy);
                float xIn = halfWd - Mathf.Abs(dx);
                return AaInside(Mathf.Min(yIn, xIn));
            }
            case 4: // Whisker — thin wispy
            {
                float maxY = size * 0.50f;
                float curve = Mathf.Sin(dy / maxY * Mathf.PI * 0.5f) * size * 0.08f;
                float halfWd = size * 0.025f + (1f - dy / maxY) * size * 0.02f;
                float yIn = Mathf.Min(dy, maxY - dy);
                float xIn = halfWd - Mathf.Abs(dx - curve);
                return AaInside(Mathf.Min(yIn, xIn)) * 0.85f;
            }
        }
        return 0f;
    }

    static Color[] DrawGlow(int size)
    {
        Color[] data = new Color[size * size];
        float cx = (size - 1) * 0.5f;
        float cy = (size - 1) * 0.5f;
        float maxR = size * 0.5f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - cx;
            float dy = y - cy;
            float d = Mathf.Sqrt(dx * dx + dy * dy) / maxR;
            float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.4f);
            data[y * size + x] = new Color(1f, 1f, 1f, a);
        }
        return data;
    }

    static float SmoothStepAa(float distance, float radius, float aaPx)
    {
        return Mathf.Clamp01((radius - distance) / Mathf.Max(aaPx, 0.001f));
    }

    static float AaInside(float signedInside)
    {
        return Mathf.Clamp01(signedInside / 1.2f);
    }

    // -- I/O --------------------------------------------------------------

    static void WriteSprite(string path, Color[] pixels, int width, int height, int ppu, SpriteAlignment alignment)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
        tex.SetPixels(pixels);
        tex.Apply(false);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

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
        settings.spriteExtrude = 1;
        settings.filterMode = FilterMode.Bilinear;
        settings.wrapMode = TextureWrapMode.Clamp;
        settings.mipmapEnabled = false;

        imp.SetTextureSettings(settings);
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
    }

    static void EnsureFolder(string folder)
    {
        folder = folder.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent = "Assets";
        foreach (string part in folder.Split('/'))
        {
            if (part == "Assets")
                continue;
            string next = $"{parent}/{part}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(parent, part);
            parent = next;
        }
    }
}
