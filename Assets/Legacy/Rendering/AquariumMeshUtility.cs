using UnityEngine;

/// <summary>
/// Built‑in meshes (primitive Quad asset) may be unreadable → vertex copies come back empty → invisible fish.
/// This mesh is guaranteed readable/runnable morph source.
/// </summary>
public static class AquariumMeshUtility
{
    static Mesh TemplateUnitQuad;

    /// <summary>Creates a XY quad in front of a +Z camera (classic 2D setup); shares topology for lerps.</summary>
    public static Mesh CreateUnitQuadReadable()
    {
        var mesh = new Mesh
        {
            name = "Aquarium_ReadableUnitQuad",
        };

        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
        };

        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
        };

        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Single shared topology copy for morph slots (same ref in all eight slots ⇒ safe lerp).</summary>
    public static Mesh SharedTopologyQuad()
    {
        if (TemplateUnitQuad == null)
            TemplateUnitQuad = CreateUnitQuadReadable();
        return TemplateUnitQuad;
    }

    /// <summary>New instance for assignment to MeshFilter.mesh (writable instance).</summary>
    public static Mesh CloneReadableUnitQuad()
    {
        return Object.Instantiate(SharedTopologyQuad());
    }
}
