using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the in-scene aquarium environment: gradient water background, gravel
/// floor, glass-frame border, ambient bubble particles, and adjusts the camera.
/// All textures are generated procedurally into <c>Assets/Textures/Aquarium/</c>.
/// </summary>
public static class AquariumSceneSetup
{
    public const string EnvFolder = "Assets/Textures/Aquarium";
    public const string TankRootName = "Aquarium_Tank";

    public const float TankHalfWidth = 7f;
    public const float TankHalfHeight = 3.5f;

    [MenuItem("Tools/Aquarium/Legacy/Setup Aquarium Scene", false, 915)]
    public static void Setup()
    {
        Undo.IncrementCurrentGroup();

        EnsureFolder(EnvFolder);

        Sprite waterSprite = LoadOrBakeWaterGradient();
        Sprite gravelSprite = LoadOrBakeGravel();
        Sprite glassSprite = LoadOrBakeWhitePixel();
        Sprite bubbleSprite = LoadOrBakeBubble();

        Transform existing = ResolveTankRoot();
        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);

        var tankRoot = new GameObject(TankRootName);
        Undo.RegisterCreatedObjectUndo(tankRoot, "Setup Aquarium Tank");
        tankRoot.transform.position = Vector3.zero;

        BuildBackground(tankRoot.transform, waterSprite);
        BuildGravelFloor(tankRoot.transform, gravelSprite);
        BuildGlassFrame(tankRoot.transform, glassSprite);
        BuildBubbleEmitter(tankRoot.transform, bubbleSprite);

        ConfigureCamera();

        Selection.activeGameObject = tankRoot;
        EditorGUIUtility.PingObject(tankRoot);
        Undo.SetCurrentGroupName("Setup Aquarium Scene");
        Debug.Log("Aquarium: tank environment built — background, gravel, glass frame, bubbles, camera tuned.");
    }

    static Transform ResolveTankRoot()
    {
        GameObject go = GameObject.Find(TankRootName);
        return go != null ? go.transform : null;
    }

    static void BuildBackground(Transform parent, Sprite waterSprite)
    {
        var bg = new GameObject("Aquarium_Background");
        Undo.RegisterCreatedObjectUndo(bg, "Aquarium Background");
        Undo.SetTransformParent(bg.transform, parent, "BG parent");
        bg.transform.position = new Vector3(0f, 0f, 5f);
        bg.transform.localScale = Vector3.one;

        SpriteRenderer sr = bg.AddComponent<SpriteRenderer>();
        sr.sprite = waterSprite;
        // SpriteDrawMode.Sliced + size gives a stretch-to-size that's
        // independent of the sprite's native pixel dimensions, so an
        // 8x256 gradient still fills the tank both ways.
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(TankHalfWidth * 2f * 1.05f, TankHalfHeight * 2f * 1.05f);
        sr.color = new Color(1f, 1f, 1f, 1f);
        sr.sortingLayerName = "Default";
        sr.sortingOrder = -200;
    }

    static void BuildGravelFloor(Transform parent, Sprite gravelSprite)
    {
        var floor = new GameObject("Aquarium_Floor");
        Undo.RegisterCreatedObjectUndo(floor, "Aquarium Floor");
        Undo.SetTransformParent(floor.transform, parent, "Floor parent");
        floor.transform.position = new Vector3(0f, -TankHalfHeight + 0.35f, 4.5f);
        floor.transform.localScale = Vector3.one;

        SpriteRenderer sr = floor.AddComponent<SpriteRenderer>();
        sr.sprite = gravelSprite;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(TankHalfWidth * 2f * 1.05f, 0.7f);
        sr.sortingLayerName = "Default";
        sr.sortingOrder = -180;
    }

    static void BuildGlassFrame(Transform parent, Sprite whitePixel)
    {
        Color glass = new Color(0.7f, 0.95f, 1f, 0.45f);
        const float thickness = 0.18f;
        const int order = 80;

        // Top
        CreateGlassEdge(parent, "Glass_Top", whitePixel, glass, order,
            new Vector3(0f, TankHalfHeight, 0f),
            new Vector3(TankHalfWidth * 2f, thickness, 1f));
        // Bottom
        CreateGlassEdge(parent, "Glass_Bottom", whitePixel, glass, order,
            new Vector3(0f, -TankHalfHeight, 0f),
            new Vector3(TankHalfWidth * 2f, thickness, 1f));
        // Left
        CreateGlassEdge(parent, "Glass_Left", whitePixel, glass, order,
            new Vector3(-TankHalfWidth, 0f, 0f),
            new Vector3(thickness, TankHalfHeight * 2f, 1f));
        // Right
        CreateGlassEdge(parent, "Glass_Right", whitePixel, glass, order,
            new Vector3(TankHalfWidth, 0f, 0f),
            new Vector3(thickness, TankHalfHeight * 2f, 1f));
    }

    static void CreateGlassEdge(Transform parent, string name, Sprite white, Color tint,
        int order, Vector3 localPos, Vector3 scale)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Glass Edge");
        Undo.SetTransformParent(go.transform, parent, "Glass parent");
        go.transform.localPosition = localPos;
        go.transform.localScale = Vector3.one;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = white;
        // The "scale" parameter here is in world units (frame thickness x length).
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(scale.x, scale.y);
        sr.color = tint;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = order;
    }

    static void BuildBubbleEmitter(Transform parent, Sprite bubble)
    {
        var go = new GameObject("Aquarium_Bubbles");
        Undo.RegisterCreatedObjectUndo(go, "Bubbles");
        Undo.SetTransformParent(go.transform, parent, "Bubbles parent");
        go.transform.position = new Vector3(0f, -TankHalfHeight + 0.3f, 3f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();

        // Use a simple unlit sprite material so bubbles render in URP 2D.
        Shader unlit = Shader.Find("Sprites/Default");
        if (unlit != null)
        {
            Material bubbleMat = new Material(unlit);
            bubbleMat.name = "M_BubbleParticle";
            psr.sharedMaterial = bubbleMat;
        }

        var main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = 4.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.22f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startColor = new Color(0.85f, 1f, 1f, 0.75f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.rateOverTime = 6f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(TankHalfWidth * 1.7f, 0.05f, 1f);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        // All three axes must use the same MinMaxCurveMode — leaving Z on its
        // default (Constant) while X/Y are TwoConstants raises
        // "Particle Velocity curves must all be in the same mode".
        velocity.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve sizeCurve = AnimationCurve.EaseInOut(0f, 0.25f, 1f, 1f);
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(0.85f, 1f, 1f), 0f), new GradientColorKey(new Color(0.95f, 1f, 1f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.65f, 0.2f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = new ParticleSystem.MinMaxGradient(g);

        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.sortingLayerName = "Default";
        psr.sortingOrder = -50;

        if (bubble != null)
        {
            var ta = ps.textureSheetAnimation;
            ta.mode = ParticleSystemAnimationMode.Sprites;
            ta.AddSprite(bubble);
        }
    }

    static void ConfigureCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        Undo.RecordObject(cam, "Camera Setup");
        cam.orthographic = true;
        cam.orthographicSize = TankHalfHeight + 0.4f;
        cam.backgroundColor = new Color(0.04f, 0.07f, 0.12f, 1f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.transform.position = new Vector3(0f, 0f, -10f);
    }

    // ---- texture baking ------------------------------------------------

    static Sprite LoadOrBakeWaterGradient()
    {
        string path = $"{EnvFolder}/WaterGradient.png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null)
            return existing;

        const int W = 8;
        const int H = 256;
        Color[] pixels = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            float t = y / (float)(H - 1);
            // Top of water lighter teal, bottom deeper navy.
            Color top = new Color(0.10f, 0.45f, 0.55f);
            Color bot = new Color(0.03f, 0.12f, 0.22f);
            Color c = Color.Lerp(bot, top, Mathf.Pow(t, 0.85f));
            for (int x = 0; x < W; x++)
                pixels[y * W + x] = new Color(c.r, c.g, c.b, 1f);
        }

        WriteSprite(path, pixels, W, H, 32, SpriteAlignment.Center);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Sprite LoadOrBakeGravel()
    {
        string path = $"{EnvFolder}/Gravel.png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null)
            return existing;

        const int W = 256;
        const int H = 64;
        Color[] pixels = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            float yt = y / (float)(H - 1);
            for (int x = 0; x < W; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.18f, y * 0.18f);
                float n2 = Mathf.PerlinNoise(x * 0.04f, y * 0.04f + 12.3f);
                float v = Mathf.Lerp(0.18f, 0.45f, n * 0.7f + n2 * 0.3f);
                // Darker and slightly bluer near top of gravel band.
                Color baseC = new Color(v * 0.85f, v, v * 0.65f);
                baseC = Color.Lerp(baseC * 0.6f, baseC, yt);
                pixels[y * W + x] = baseC;
            }
        }

        WriteSprite(path, pixels, W, H, 64, SpriteAlignment.Center);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Sprite LoadOrBakeWhitePixel()
    {
        string path = $"{EnvFolder}/White1px.png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null)
            return existing;

        Color[] pixels = new Color[16 * 16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;

        WriteSprite(path, pixels, 16, 16, 16, SpriteAlignment.Center);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Sprite LoadOrBakeBubble()
    {
        string path = $"{EnvFolder}/Bubble.png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null)
            return existing;

        const int S = 64;
        Color[] pixels = new Color[S * S];
        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float dx = x - c;
            float dy = y - c;
            float d = Mathf.Sqrt(dx * dx + dy * dy) / c;
            float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) * 4f);
            float core = Mathf.Clamp01(1f - d) * 0.25f;
            float a = Mathf.Clamp01(ring + core);
            pixels[y * S + x] = new Color(1f, 1f, 1f, a);
        }
        WriteSprite(path, pixels, S, S, 128, SpriteAlignment.Center);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

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

        TextureImporterSettings s = new TextureImporterSettings();
        imp.ReadTextureSettings(s);
        s.textureType = TextureImporterType.Sprite;
        s.spriteMode = (int)SpriteImportMode.Single;
        s.alphaIsTransparency = true;
        s.spriteAlignment = (int)alignment;
        s.spritePixelsPerUnit = ppu;
        s.spriteMeshType = SpriteMeshType.FullRect;
        s.spriteExtrude = 1;
        s.filterMode = FilterMode.Bilinear;
        s.wrapMode = TextureWrapMode.Clamp;
        s.mipmapEnabled = false;
        imp.SetTextureSettings(s);
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
