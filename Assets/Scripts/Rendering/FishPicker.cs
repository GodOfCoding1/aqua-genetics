using Aquarium.PixelArt;
using UnityEngine;

/// <summary>
/// Owns the click-target collider for a fish. Auto-fits a <see cref="BoxCollider2D"/>
/// to the current visible silhouette (composite bounds of every active
/// <see cref="FishCompositor"/> sprite renderer) and exposes the bound
/// <see cref="FishData"/> for UI consumers like <c>FishInspector</c>.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class FishPicker : MonoBehaviour
{
    [SerializeField] BoxCollider2D boxCollider;
    [SerializeField] FishCompositor compositor;
    [SerializeField] FishRenderer fishRenderer;
    [SerializeField] FishAnimator fishAnimator;

    [Tooltip("Multiplier applied to the composite bounds — slightly bigger than the silhouette to make picking forgiving.")]
    [SerializeField] float padding = 1.15f;

    [Tooltip("Minimum collider extent on each axis (avoids zero-size colliders during init).")]
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
        FitToBounds();
    }

    void OnEnable()
    {
        CacheRefs();
        FitToBounds();
    }

    void LateUpdate()
    {
        FitToBounds();
    }

    void CacheRefs()
    {
        if (boxCollider == null)
            boxCollider = GetComponent<BoxCollider2D>();
        if (compositor == null)
            compositor = GetComponent<FishCompositor>();
        if (fishRenderer == null)
            fishRenderer = GetComponent<FishRenderer>();
        if (fishAnimator == null)
            fishAnimator = GetComponent<FishAnimator>();
    }

    public void FitToBounds()
    {
        if (boxCollider == null || compositor == null)
            return;

        Bounds world = compositor.GetCompositeBounds();
        if (world.size == Vector3.zero)
            return;

        // Collider lives on the fish root, so its space is local. Convert
        // world bounds → local by accounting for the parent's lossy scale.
        Vector3 lossy = transform.lossyScale;
        Vector2 localSize = new Vector2(
            world.size.x / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)),
            world.size.y / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)));
        Vector2 localCenter = transform.InverseTransformPoint(world.center);

        Vector2 size = localSize * Mathf.Max(padding, 1f);
        size.x = Mathf.Max(size.x, minExtent);
        size.y = Mathf.Max(size.y, minExtent);
        boxCollider.size = size;
        boxCollider.offset = localCenter;
        boxCollider.isTrigger = true;
    }
}
