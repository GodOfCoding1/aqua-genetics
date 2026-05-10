using UnityEngine;

/// <summary>
/// Owns the click-target collider for a fish. Auto-fits a <see cref="BoxCollider2D"/>
/// to the current body mesh bounds (which change every time
/// <see cref="FishRenderer.ApplyGenome"/> is called) and exposes the bound
/// <see cref="FishData"/> for UI consumers like <c>FishInspector</c>.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class FishPicker : MonoBehaviour
{
    [SerializeField] BoxCollider2D boxCollider;
    [SerializeField] MeshFilter bodyMeshFilter;
    [SerializeField] FishRenderer fishRenderer;
    [SerializeField] FishAnimator fishAnimator;

    [Tooltip("Multiplier applied to the mesh bounds — slightly bigger than the silhouette to make picking forgiving.")]
    [SerializeField] float padding = 1.15f;

    [Tooltip("Minimum collider extent on each axis (avoids zero-size colliders during morph init).")]
    [SerializeField] float minExtent = 0.18f;

    /// <summary>
    /// The fish this picker represents. Pulled from the animator (preferred,
    /// because it's set by every spawn pipeline) and falls back to the
    /// renderer's last-applied fish.
    /// </summary>
    public FishData BoundFishData
    {
        get
        {
            if (fishAnimator != null && fishAnimator.BoundFish != null)
                return fishAnimator.BoundFish;
            if (fishRenderer != null)
                return fishRenderer.AppliedFish;
            return null;
        }
    }

    void Reset()
    {
        CacheRefs();
        FitToMesh();
    }

    void OnEnable()
    {
        CacheRefs();
        FitToMesh();
    }

    void LateUpdate()
    {
        FitToMesh();
    }

    void CacheRefs()
    {
        if (boxCollider == null)
            boxCollider = GetComponent<BoxCollider2D>();
        if (bodyMeshFilter == null)
            bodyMeshFilter = GetComponent<MeshFilter>();
        if (fishRenderer == null)
            fishRenderer = GetComponent<FishRenderer>();
        if (fishAnimator == null)
            fishAnimator = GetComponent<FishAnimator>();
    }

    public void FitToMesh()
    {
        if (boxCollider == null || bodyMeshFilter == null)
            return;

        Mesh m = bodyMeshFilter.sharedMesh;
        if (m == null)
            return;

        Bounds b = m.bounds;
        Vector2 size = (Vector2)b.size * Mathf.Max(padding, 1f);
        size.x = Mathf.Max(size.x, minExtent);
        size.y = Mathf.Max(size.y, minExtent);
        boxCollider.size = size;
        boxCollider.offset = (Vector2)b.center;
        boxCollider.isTrigger = true;
    }
}
