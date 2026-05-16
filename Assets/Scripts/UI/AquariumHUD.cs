using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight in-scene HUD: shows the fish count and provides a button that
/// spawns a freshly randomized fish at runtime via <see cref="FishSpawnService"/>.
/// Wired up automatically by the editor menu <c>AquariumUiSetup</c>; can also
/// be configured by hand in the inspector.
/// </summary>
public class AquariumHUD : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] Text countText;
    [SerializeField] Button spawnButton;

    [Header("Spawn settings")]
    [SerializeField] GameObject fishPrefab;
    [SerializeField] GeneLibrary geneLibrary;

    [Tooltip("If set, spawned fish are parented under this transform. " +
             "Otherwise a runtime root is created on demand.")]
    [SerializeField] Transform spawnParent;

    [SerializeField] Vector2 spawnAreaHalfExtents = new Vector2(7.5f, 3.75f);

    [Header("Refresh")]
    [Tooltip("Seconds between count text refreshes.")]
    [SerializeField] float refreshInterval = 0.4f;

    const string SpawnRootName = "Aquarium_RuntimeFishRoot";

    float _refreshTimer;

    void OnEnable()
    {
        if (spawnButton != null)
            spawnButton.onClick.AddListener(OnSpawnClicked);
        RefreshCount();
    }

    void OnDisable()
    {
        if (spawnButton != null)
            spawnButton.onClick.RemoveListener(OnSpawnClicked);
    }

    void Update()
    {
        _refreshTimer -= Time.unscaledDeltaTime;
        if (_refreshTimer <= 0f)
        {
            _refreshTimer = Mathf.Max(refreshInterval, 0.05f);
            RefreshCount();
        }
    }

    void RefreshCount()
    {
        if (countText == null)
            return;

        FishLifecycleManager mgr = FishLifecycleManager.Instance;
        if (mgr == null)
        {
            countText.text = "Fish: -- / --";
            return;
        }

        int alive = 0;
        int total = mgr.TankFish.Count;
        for (int i = 0; i < total; i++)
        {
            FishData f = mgr.TankFish[i];
            if (f != null && f.isAlive)
                alive++;
        }

        countText.text = $"Fish: {alive} alive / {total} total";
    }

    void OnSpawnClicked()
    {
        GeneLibrary lib = ResolveGeneLibrary();
        if (lib == null || fishPrefab == null)
        {
            Debug.LogWarning("AquariumHUD: missing fish prefab or gene library — cannot spawn at runtime.");
            return;
        }

        Transform parent = ResolveSpawnParent();
        Vector3 pos = new Vector3(
            Random.Range(-spawnAreaHalfExtents.x, spawnAreaHalfExtents.x),
            Random.Range(-spawnAreaHalfExtents.y, spawnAreaHalfExtents.y),
            0f);

        FishSpawnService.SpawnRandom(fishPrefab, lib, parent, pos);
        RefreshCount();
    }

    GeneLibrary ResolveGeneLibrary()
    {
        if (geneLibrary != null)
            return geneLibrary;
        if (FishLifecycleManager.Instance != null)
            return FishLifecycleManager.Instance.GeneLibrary;
        return null;
    }

    Transform ResolveSpawnParent()
    {
        if (spawnParent != null)
            return spawnParent;

        GameObject existing = GameObject.Find(SpawnRootName);
        if (existing != null)
        {
            spawnParent = existing.transform;
            return spawnParent;
        }

        var go = new GameObject(SpawnRootName);
        spawnParent = go.transform;
        return spawnParent;
    }

    public void SetFishPrefab(GameObject prefab) => fishPrefab = prefab;
    public void SetGeneLibrary(GeneLibrary lib) => geneLibrary = lib;
    public void SetCountText(Text txt) => countText = txt;
    public void SetSpawnButton(Button btn) => spawnButton = btn;
}
