using UnityEngine;

/// <summary>
/// Builds the procedural side-view fish-silhouette meshes at RUNTIME (no
/// editor dependency). The editor-side <c>FishMeshGenerator</c> calls into
/// this to bake .asset files; <see cref="FishBodyMorpher"/> calls into it as
/// a runtime fallback when its <c>bodyMeshes</c> array is missing or
/// unreadable. All shapes share identical topology so morphing works.
/// </summary>
public static class FishBodyMeshBuilder
{
    public const int SamplesPerSide = 32;
    public const int OutlineVerts = SamplesPerSide * 2;
    public const int TotalVerts = OutlineVerts + 1;
    public const int ShapeCount = 8;

    public struct ShapeParams
    {
        public float bodyLen;
        public float bodyHt;
        public float headRound;
        public float tailRound;
        public float peak;
        public string label;
    }

    public static readonly ShapeParams[] Shapes =
    {
        new ShapeParams { bodyLen = 0.45f, bodyHt = 0.20f, headRound = 0.55f, tailRound = 0.22f, peak = 0.42f, label = "Oval" },
        new ShapeParams { bodyLen = 0.55f, bodyHt = 0.14f, headRound = 0.45f, tailRound = 0.20f, peak = 0.45f, label = "Elongated" },
        new ShapeParams { bodyLen = 0.35f, bodyHt = 0.32f, headRound = 0.70f, tailRound = 0.30f, peak = 0.45f, label = "Deep" },
        new ShapeParams { bodyLen = 0.50f, bodyHt = 0.08f, headRound = 0.50f, tailRound = 0.20f, peak = 0.50f, label = "Flat" },
        new ShapeParams { bodyLen = 0.36f, bodyHt = 0.34f, headRound = 0.95f, tailRound = 0.45f, peak = 0.50f, label = "Round" },
        new ShapeParams { bodyLen = 0.52f, bodyHt = 0.16f, headRound = 0.30f, tailRound = 0.12f, peak = 0.40f, label = "Torpedo" },
        new ShapeParams { bodyLen = 0.60f, bodyHt = 0.06f, headRound = 0.40f, tailRound = 0.15f, peak = 0.45f, label = "Ribbon" },
        new ShapeParams { bodyLen = 0.45f, bodyHt = 0.28f, headRound = 0.05f, tailRound = 0.05f, peak = 0.50f, label = "Diamond" },
    };

    static readonly Mesh[] _runtimeCache = new Mesh[ShapeCount];

    /// <summary>Returns a per-process shared mesh for the shape — built lazily.</summary>
    public static Mesh GetCached(int shapeIndex)
    {
        int i = Mathf.Clamp(shapeIndex, 0, ShapeCount - 1);
        if (_runtimeCache[i] == null)
        {
            _runtimeCache[i] = Build(i);
            if (_runtimeCache[i] != null)
                _runtimeCache[i].hideFlags = HideFlags.DontSave;
        }
        return _runtimeCache[i];
    }

    /// <summary>Builds a fresh mesh (caller owns it). Used by the editor baker.</summary>
    public static Mesh Build(int shapeIndex)
    {
        ShapeParams p = Shapes[Mathf.Clamp(shapeIndex, 0, ShapeCount - 1)];

        Vector3[] verts = new Vector3[TotalVerts];
        Vector2[] uvs = new Vector2[TotalVerts];
        Vector2[] uv2 = new Vector2[TotalVerts];

        verts[0] = Vector3.zero;
        uv2[0] = new Vector2(0f, 0f); // boundary distance: 0 at the centre vertex

        for (int i = 0; i < SamplesPerSide; i++)
        {
            Vector2 top = OutlinePoint(i, p, true);
            verts[1 + i] = new Vector3(top.x, top.y, 0f);
            uv2[1 + i] = new Vector2(1f, 0f); // outline -> 1
        }
        for (int i = 0; i < SamplesPerSide; i++)
        {
            int rev = SamplesPerSide - 1 - i;
            Vector2 bot = OutlinePoint(rev, p, false);
            verts[1 + SamplesPerSide + i] = new Vector3(bot.x, bot.y, 0f);
            uv2[1 + SamplesPerSide + i] = new Vector2(1f, 0f);
        }

        // Centre on bounding-box midpoint so transform.position is the fish centre.
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < TotalVerts; i++)
        {
            if (verts[i].x < minX) minX = verts[i].x;
            if (verts[i].x > maxX) maxX = verts[i].x;
            if (verts[i].y < minY) minY = verts[i].y;
            if (verts[i].y > maxY) maxY = verts[i].y;
        }

        Vector3 mid = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
        for (int i = 0; i < TotalVerts; i++)
            verts[i] -= mid;

        float w = Mathf.Max(maxX - minX, 1e-4f);
        float h = Mathf.Max(maxY - minY, 1e-4f);
        for (int i = 0; i < TotalVerts; i++)
        {
            float ux = (verts[i].x + (maxX - minX) * 0.5f) / w;
            float uy = (verts[i].y + (maxY - minY) * 0.5f) / h;
            uvs[i] = new Vector2(Mathf.Clamp01(ux), Mathf.Clamp01(uy));
        }

        int[] tris = new int[OutlineVerts * 3];
        for (int i = 0; i < OutlineVerts; i++)
        {
            int a = 1 + i;
            int b = 1 + ((i + 1) % OutlineVerts);
            tris[i * 3 + 0] = 0;
            tris[i * 3 + 1] = a;
            tris[i * 3 + 2] = b;
        }

        Mesh mesh = new Mesh
        {
            name = $"FishBody_{shapeIndex}_{p.label}",
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt16,
        };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetUVs(1, uv2);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Vector2 OutlinePoint(int sampleIdx, ShapeParams p, bool isTop)
    {
        float u = sampleIdx / (float)(SamplesPerSide - 1);
        float x = Mathf.Lerp(p.bodyLen, -p.bodyLen, u);

        float bodyMain;
        if (u < p.peak)
            bodyMain = Mathf.Sin(u / Mathf.Max(p.peak, 1e-3f) * Mathf.PI * 0.5f);
        else
            bodyMain = Mathf.Sin((1f - u) / Mathf.Max(1f - p.peak, 1e-3f) * Mathf.PI * 0.5f);

        float headThick = p.headRound * Mathf.Cos(u * Mathf.PI * 0.5f);
        float tailThick = p.tailRound * Mathf.Sin(u * Mathf.PI * 0.5f);
        float profile = Mathf.Max(bodyMain, Mathf.Max(headThick * 0.7f, tailThick * 0.55f));

        float halfHt = profile * p.bodyHt;
        return new Vector2(x, isTop ? halfHt : -halfHt);
    }
}
