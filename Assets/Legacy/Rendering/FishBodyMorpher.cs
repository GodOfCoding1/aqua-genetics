using UnityEngine;

/// <summary>
/// Phase 4.2 — Morphs between authored body-shape meshes and scales by <c>body_size</c>.
/// Assigned meshes must share identical vertex ordering and triangles (same topology).
/// If the inspector slots are empty / unreadable (e.g. someone left Unity's
/// primitive Quad in there) we fall back to the runtime
/// <see cref="FishBodyMeshBuilder"/> so we always render fish-shaped fish.
/// </summary>
public class FishBodyMorpher : MonoBehaviour
{
    [Tooltip("Meshes for body_shape states 0..7 — same topology required for lerping.")]
    [SerializeField] Mesh[] bodyMeshes = new Mesh[8];

    public void PrepareMorphMeshesForRuntime()
    {
        if (bodyMeshes == null || bodyMeshes.Length != 8)
        {
            var resized = new Mesh[8];
            if (bodyMeshes != null)
            {
                for (int i = 0; i < bodyMeshes.Length && i < 8; i++)
                    resized[i] = bodyMeshes[i];
            }
            bodyMeshes = resized;
        }

        for (int i = 0; i < 8; i++)
        {
            if (bodyMeshes[i] == null || !MeshVertsAreAccessible(bodyMeshes[i]))
                bodyMeshes[i] = FishBodyMeshBuilder.GetCached(i);
        }
    }

    static bool MeshVertsAreAccessible(Mesh mesh)
    {
        if (mesh == null || mesh.vertexCount < 4)
            return false;
        // Reading mesh.vertices on a non-readable mesh (e.g. Unity's primitive
        // Quad which has isReadable=false) logs "Failed reading vertices..."
        // every frame. Pre-check isReadable to silently fall back instead.
        if (!mesh.isReadable)
            return false;
        var v = mesh.vertices;
        return v != null && v.Length == mesh.vertexCount;
    }

    public Mesh GetMorphedMesh(float bodyShapeGene, float bodySizeGene)
    {
        if (bodyMeshes == null || bodyMeshes.Length != 8)
        {
            Debug.LogWarning($"{nameof(FishBodyMorpher)}: expected 8 entries in bodyMeshes.", this);
            return null;
        }

        Mesh meshA = bodyMeshes[0];
        if (meshA == null)
        {
            Debug.LogWarning($"{nameof(FishBodyMorpher)}: bodyMeshes[0] missing.", this);
            return null;
        }

        int stateA = Mathf.FloorToInt(bodyShapeGene);
        int stateB = Mathf.CeilToInt(bodyShapeGene);
        stateA = Mathf.Clamp(stateA, 0, 7);
        stateB = Mathf.Clamp(stateB, 0, 7);

        float t = bodyShapeGene - Mathf.FloorToInt(bodyShapeGene);
        if (stateA == stateB)
            t = 0f;

        Mesh mA = bodyMeshes[stateA];
        Mesh mB = bodyMeshes[stateB];
        if (mA == null)
            mA = meshA;
        if (mB == null)
            mB = mA;

        int vc = meshA.vertexCount;
        if (mA.vertexCount != vc || mB.vertexCount != vc)
        {
            Debug.LogWarning($"{nameof(FishBodyMorpher)}: mesh vertex counts differ; returning base mesh.", this);
            Mesh flat = Instantiate(meshA);
            ScaleVertices(flat, bodySizeGene);
            flat.name = "FishBody_Runtime";
            return flat;
        }

        var vertsA = mA.vertices;
        var vertsB = mB.vertices;
        var vOut = new Vector3[vc];
        for (int i = 0; i < vc; i++)
            vOut[i] = Vector3.Lerp(vertsA[i], vertsB[i], t) * bodySizeGene;

        var normalsA = mA.normals;
        var normalsB = mB.normals;
        Vector3[] nOut = null;
        if (normalsA != null && normalsA.Length == vc && normalsB != null && normalsB.Length == vc)
        {
            nOut = new Vector3[vc];
            for (int i = 0; i < vc; i++)
                nOut[i] = Vector3.Normalize(Vector3.Lerp(normalsA[i], normalsB[i], t));
        }

        Mesh result = new Mesh
        {
            name = "FishBody_Runtime",
            indexFormat = meshA.indexFormat,
        };

        result.SetVertices(vOut);

        result.triangles = meshA.triangles;

        if (mA.uv != null && mA.uv.Length == vc && mB.uv != null && mB.uv.Length == vc)
        {
            var uvOut = new Vector2[vc];
            for (int i = 0; i < vc; i++)
                uvOut[i] = Vector2.Lerp(mA.uv[i], mB.uv[i], t);
            result.uv = uvOut;
        }
        else if (meshA.uv != null && meshA.uv.Length == vc)
            result.uv = meshA.uv;

        // Forward UV1 (boundary distance) so the fragment shader can darken
        // edges. Boundary distance is identical across all 8 shapes by design,
        // so we can just copy from mA.
        var uv2A = mA.uv2;
        if (uv2A != null && uv2A.Length == vc)
            result.uv2 = uv2A;

        if (nOut != null)
            result.normals = nOut;
        else
            result.RecalculateNormals();

        result.RecalculateBounds();

        return result;
    }

    static void ScaleVertices(Mesh mesh, float bodySizeGene)
    {
        if (mesh == null)
            return;
        var verts = mesh.vertices;
        for (int i = 0; i < verts.Length; i++)
            verts[i] *= bodySizeGene;
        mesh.vertices = verts;
        mesh.RecalculateBounds();
        if (mesh.normals == null || mesh.normals.Length == 0)
            mesh.RecalculateNormals();
    }
}
