using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Hover-to-inspect with click-to-pin: every frame, the inspector raycasts
/// against 2D colliders under the mouse and previews the hovered fish in the
/// side panel. Left-clicking a fish "pins" it so the panel persists when the
/// mouse moves away; clicking empty space or the close button unpins.
/// </summary>
public class FishInspector : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] GameObject panelRoot;
    [SerializeField] Text titleText;
    [SerializeField] Text bodyText;
    [SerializeField] Button closeButton;
    [SerializeField] Image accentSwatch;

    [Header("References")]
    [SerializeField] GeneLibrary geneLibrary;
    [SerializeField] Camera worldCamera;

    [Header("Hover")]
    [Tooltip("If true, mousing over a fish previews its genetics. Click to pin.")]
    [SerializeField] bool hoverEnabled = true;

    FishPicker _pinned;
    FishPicker _hovered;
    CanvasGroup _panelGroup;
    bool _visible;

    void Awake()
    {
        // We must keep panelRoot's GameObject ACTIVE so this MonoBehaviour's
        // Update() keeps polling for hover even while the panel is "hidden".
        // Use a CanvasGroup to toggle visibility + raycasts instead.
        EnsureCanvasGroup();
        SetVisible(false);
    }

    void OnEnable()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Unpin);
        EnsureCanvasGroup();
        SetVisible(false);
    }

    void OnDisable()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Unpin);
    }

    void Update()
    {
        // Refresh hover target every frame (cheap: a single OverlapPoint).
        _hovered = hoverEnabled ? PickFishUnderMouse() : null;

        if (LeftMousePressedThisFrame())
            HandleLeftClick();

        // Resolve which fish (if any) the panel should display: pinned wins,
        // then the live hover preview, then nothing.
        FishData display = ResolveDisplayFish();
        if (display != null)
        {
            SetVisible(true);
            UpdatePanel(display);
        }
        else
        {
            SetVisible(false);
        }
    }

    void EnsureCanvasGroup()
    {
        if (_panelGroup != null || panelRoot == null)
            return;
        _panelGroup = panelRoot.GetComponent<CanvasGroup>();
        if (_panelGroup == null)
            _panelGroup = panelRoot.AddComponent<CanvasGroup>();
        // Defensive: if the panel was saved inactive in a scene authored
        // before the CanvasGroup-based hiding, re-activate it so Update runs.
        if (!panelRoot.activeSelf)
            panelRoot.SetActive(true);
    }

    void SetVisible(bool visible)
    {
        if (_visible == visible && _panelGroup != null)
            return;
        _visible = visible;
        if (_panelGroup == null)
            EnsureCanvasGroup();
        if (_panelGroup == null)
            return;
        _panelGroup.alpha = visible ? 1f : 0f;
        _panelGroup.blocksRaycasts = visible;
        _panelGroup.interactable = visible;
    }

    FishData ResolveDisplayFish()
    {
        if (_pinned != null && _pinned.BoundFishData != null)
            return _pinned.BoundFishData;
        if (_hovered != null && _hovered.BoundFishData != null)
            return _hovered.BoundFishData;
        return null;
    }

    void HandleLeftClick()
    {
        // Clicks on the inspector itself shouldn't change selection; just let
        // the close button (or its own UI) handle it.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        FishPicker picker = PickFishUnderMouse();
        if (picker != null && picker.BoundFishData != null)
        {
            _pinned = picker;
        }
        else
        {
            // Empty-space click → unpin (and the next Update() will hide if
            // nothing is hovered either).
            _pinned = null;
        }
    }

    /// <summary>
    /// Casts a single point against 2D colliders at the current mouse position
    /// and returns the first <see cref="FishPicker"/> on or above the hit.
    /// </summary>
    FishPicker PickFishUnderMouse()
    {
        // Don't try to pick through UI panels — but the caller may still keep
        // the pinned fish on display.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return null;

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null)
            return null;

        Vector3 mouse = ReadMousePosition();
        mouse.z = 10f;
        Vector3 worldPos = cam.ScreenToWorldPoint(mouse);
        Vector2 worldPos2D = new Vector2(worldPos.x, worldPos.y);

        Collider2D hit = Physics2D.OverlapPoint(worldPos2D);
        if (hit == null)
            return null;
        return hit.GetComponentInParent<FishPicker>();
    }

    static bool LeftMousePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
            return Mouse.current.leftButton.wasPressedThisFrame;
        return false;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    static Vector3 ReadMousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            Vector2 p = Mouse.current.position.ReadValue();
            return new Vector3(p.x, p.y, 0f);
        }
        return Vector3.zero;
#else
        return Input.mousePosition;
#endif
    }

    void Hide()
    {
        _pinned = null;
        _hovered = null;
        SetVisible(false);
    }

    /// <summary>Close-button handler: unpin the panel; hover preview can still re-show it next frame.</summary>
    void Unpin()
    {
        _pinned = null;
        SetVisible(false);
    }

    void UpdatePanel(FishData fish)
    {
        if (fish == null)
            return;

        if (titleText != null)
            titleText.text = string.IsNullOrEmpty(fish.lineageTag) ? "Unnamed Fish" : fish.lineageTag;

        if (accentSwatch != null && geneLibrary != null)
            accentSwatch.color = ResolveAccentColor(fish);

        if (bodyText != null)
            bodyText.text = ComposeBody(fish);
    }

    Color ResolveAccentColor(FishData fish)
    {
        if (fish?.genome == null || geneLibrary == null)
            return Color.white;

        GeneDefinition hueDef = geneLibrary.GetGene("base_hue");
        GeneDefinition satDef = geneLibrary.GetGene("base_saturation");
        GeneDefinition valDef = geneLibrary.GetGene("base_value");

        float h = hueDef != null ? fish.genome.GetPhenotype("base_hue", hueDef) : 0f;
        float s = satDef != null ? Mathf.Clamp01(fish.genome.GetPhenotype("base_saturation", satDef)) : 1f;
        float v = valDef != null ? Mathf.Clamp01(fish.genome.GetPhenotype("base_value", valDef)) : 1f;

        return Color.HSVToRGB(Mathf.Repeat(h, 360f) / 360f, s, v);
    }

    string ComposeBody(FishData fish)
    {
        var sb = new StringBuilder();

        sb.Append("Stage: ").Append(fish.stage)
            .Append("  ·  Age: ").Append(Mathf.RoundToInt(fish.currentAge * 100f)).Append("%\n");
        sb.Append("Generation: ").Append(fish.generationNumber).Append('\n');

        if (fish.isHypermutant)
            sb.Append("Hypermutant: yes\n");

        if (geneLibrary == null || fish.genome == null)
            return sb.ToString();

        AppendDiscrete(sb, fish, "body_shape", "Body", BodyShapeNames);
        AppendDiscrete(sb, fish, "tail_type", "Tail", TailTypeNames);
        AppendDiscrete(sb, fish, "fin_shape", "Fins", FinShapeNames);
        AppendDiscrete(sb, fish, "pattern_type", "Pattern", PatternNames);
        AppendDiscrete(sb, fish, "swim_style", "Swim", SwimStyleNames);

        AppendContinuous(sb, fish, "body_size", "Size", "{0:0.00}x");
        AppendContinuous(sb, fish, "lifespan", "Lifespan", "{0:0.00}x");
        AppendContinuous(sb, fish, "hardiness", "Hardiness", "{0:P0}");
        AppendContinuous(sb, fish, "fertility", "Fertility", "{0:P0}");
        AppendContinuous(sb, fish, "iridescence", "Iridescence", "{0:P0}");
        AppendContinuous(sb, fish, "bioluminescence", "Bioluminescence", "{0:P0}");

        return sb.ToString();
    }

    void AppendDiscrete(StringBuilder sb, FishData fish, string geneId, string label, string[] names)
    {
        GeneDefinition def = geneLibrary.GetGene(geneId);
        if (def == null)
            return;
        int idx = Mathf.Clamp(Mathf.RoundToInt(fish.genome.GetPhenotype(geneId, def)), 0, names.Length - 1);
        sb.Append(label).Append(": ").Append(names[idx]).Append('\n');
    }

    void AppendContinuous(StringBuilder sb, FishData fish, string geneId, string label, string format)
    {
        GeneDefinition def = geneLibrary.GetGene(geneId);
        if (def == null)
            return;
        float v = fish.GetPhenotypeForDisplay(geneId, def);
        sb.Append(label).Append(": ").AppendFormat(format, v).Append('\n');
    }

    static readonly string[] BodyShapeNames =
        { "Oval", "Elongated", "Deep", "Flat", "Round", "Torpedo", "Ribbon", "Diamond" };

    static readonly string[] TailTypeNames =
        { "Fan", "Veil", "Lyre", "Rounded", "Forked", "Halfmoon" };

    static readonly string[] FinShapeNames =
        { "Round", "Pointed", "Spike", "Sail", "Whisker" };

    static readonly string[] PatternNames =
        { "Solid", "Stripes", "Spots", "Marble", "Gradient", "Iridescent", "Outlined", "Reticulated", "Banded" };

    static readonly string[] SwimStyleNames =
        { "Cruiser", "Wiggler", "Darter", "Patroller", "Acrobat" };

    public void SetGeneLibrary(GeneLibrary lib) => geneLibrary = lib;
    public void SetWorldCamera(Camera cam) => worldCamera = cam;
    public void SetWiring(GameObject panel, Text title, Text body, Button close, Image swatch)
    {
        panelRoot = panel;
        titleText = title;
        bodyText = body;
        closeButton = close;
        accentSwatch = swatch;
    }
}
