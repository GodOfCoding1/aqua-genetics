using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Builds the runtime UI: Canvas + EventSystem, top-left fish counter +
/// "Spawn Random Fish" button, and a right-side <see cref="FishInspector"/>
/// panel. Wires references to the prototype prefab and gene library.
/// </summary>
public static class AquariumUiSetup
{
    public const string CanvasRootName = "Aquarium_HUD_Canvas";
    public const string LifecycleManagerName = "FishLifecycleManager";

    const string GeneLibraryAssetPath = "Assets/ScriptableObjects/Genes/DefaultGeneLibrary.asset";
    const string PrefabAssetPath = "Assets/Prefabs/Fish/PrototypeFish.prefab";

    static readonly Color PanelBg = new Color(0.06f, 0.10f, 0.14f, 0.85f);
    static readonly Color ButtonBg = new Color(0.18f, 0.45f, 0.55f, 1f);
    static readonly Color ButtonHover = new Color(0.28f, 0.6f, 0.7f, 1f);
    static readonly Color ButtonPressed = new Color(0.12f, 0.32f, 0.42f, 1f);
    static readonly Color TextColor = new Color(0.92f, 0.96f, 1f, 1f);
    static readonly Color SubTextColor = new Color(0.72f, 0.85f, 0.92f, 1f);

    [MenuItem("Tools/Aquarium/Setup Aquarium UI", false, 220)]
    public static void Setup()
    {
        Undo.IncrementCurrentGroup();

        EnsureLifecycleManager();
        EnsureEventSystem();

        Transform existing = ResolveCanvasRoot();
        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);

        GameObject canvasGo = new GameObject(CanvasRootName,
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGo, "Aquarium UI Canvas");

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GeneLibrary lib = AssetDatabase.LoadAssetAtPath<GeneLibrary>(GeneLibraryAssetPath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);

        BuildHud(canvasGo.transform, lib, prefab);
        BuildInspector(canvasGo.transform, lib);

        Selection.activeGameObject = canvasGo;
        Undo.SetCurrentGroupName("Setup Aquarium UI");
        Debug.Log("Aquarium: HUD + inspector UI built. Gene library and fish prefab wired automatically.");
    }

    static Transform ResolveCanvasRoot()
    {
        GameObject go = GameObject.Find(CanvasRootName);
        return go != null ? go.transform : null;
    }

    static void EnsureEventSystem()
    {
        EventSystem existing = Object.FindFirstObjectByType<EventSystem>();
        if (existing != null)
        {
            UpgradeInputModuleIfNeeded(existing.gameObject);
            return;
        }

#if ENABLE_INPUT_SYSTEM
        // The legacy StandaloneInputModule throws under "Input System Package
        // (New)"-only projects because it reads UnityEngine.Input directly.
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
#else
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
        Undo.RegisterCreatedObjectUndo(go, "EventSystem");
    }

    static void UpgradeInputModuleIfNeeded(GameObject eventSystemGo)
    {
#if ENABLE_INPUT_SYSTEM
        StandaloneInputModule legacy = eventSystemGo.GetComponent<StandaloneInputModule>();
        if (legacy != null)
        {
            Undo.DestroyObjectImmediate(legacy);
        }

        if (eventSystemGo.GetComponent<InputSystemUIInputModule>() == null)
            Undo.AddComponent<InputSystemUIInputModule>(eventSystemGo);
#endif
    }

    static void EnsureLifecycleManager()
    {
        FishLifecycleManager existing = Object.FindFirstObjectByType<FishLifecycleManager>();
        if (existing != null)
            return;

        var go = new GameObject(LifecycleManagerName);
        FishLifecycleManager mgr = go.AddComponent<FishLifecycleManager>();
        Undo.RegisterCreatedObjectUndo(go, "Fish Lifecycle Manager");

        GeneLibrary lib = AssetDatabase.LoadAssetAtPath<GeneLibrary>(GeneLibraryAssetPath);
        if (lib != null)
        {
            SerializedObject so = new SerializedObject(mgr);
            SerializedProperty libProp = so.FindProperty("geneLibrary");
            if (libProp != null)
            {
                libProp.objectReferenceValue = lib;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    static void BuildHud(Transform parent, GeneLibrary lib, GameObject fishPrefab)
    {
        // Container in top-left corner.
        GameObject panel = CreateUiPanel(parent, "HUD_TopLeft",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(20f, -20f), new Vector2(360f, 80f),
            new Color(PanelBg.r, PanelBg.g, PanelBg.b, 0.6f));

        Text countText = CreateText(panel.transform, "CountText", "Fish: -- / --",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(320f, 60f),
            22, FontStyle.Bold, TextAnchor.MiddleLeft, TextColor);
        countText.rectTransform.anchoredPosition = new Vector2(0f, 0f);

        // Spawn button bottom-right.
        GameObject btnGo = CreateButton(parent, "SpawnButton", "Spawn Random Fish",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-20f, 20f), new Vector2(280f, 70f));

        Button button = btnGo.GetComponent<Button>();

        AquariumHUD hud = panel.AddComponent<AquariumHUD>();
        hud.SetCountText(countText);
        hud.SetSpawnButton(button);
        hud.SetGeneLibrary(lib);
        hud.SetFishPrefab(fishPrefab);
    }

    static void BuildInspector(Transform parent, GeneLibrary lib)
    {
        // Right-side panel (initially hidden).
        GameObject panel = CreateUiPanel(parent, "InspectorPanel",
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-20f, 0f), new Vector2(380f, 460f),
            PanelBg);

        // Top accent strip + title.
        GameObject titleRow = CreateUiPanel(panel.transform, "TitleRow",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -20f), new Vector2(-20f, 56f),
            new Color(0.10f, 0.18f, 0.24f, 1f));

        Image swatch = CreateSwatch(titleRow.transform, "AccentSwatch",
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(16f, 0f), new Vector2(28f, 28f), Color.white);

        Text title = CreateText(titleRow.transform, "TitleText", "—",
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(60f, 0f), new Vector2(-80f, 40f),
            22, FontStyle.Bold, TextAnchor.MiddleLeft, TextColor);

        // Close button.
        GameObject closeGo = CreateButton(titleRow.transform, "CloseButton", "X",
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-12f, 0f), new Vector2(36f, 36f));
        Button closeButton = closeGo.GetComponent<Button>();

        // Body text.
        Text body = CreateText(panel.transform, "BodyText", "",
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(20f, 20f), new Vector2(-20f, -90f),
            16, FontStyle.Normal, TextAnchor.UpperLeft, SubTextColor);
        // Pin under the title row.
        body.rectTransform.anchorMin = new Vector2(0f, 0f);
        body.rectTransform.anchorMax = new Vector2(1f, 1f);
        body.rectTransform.offsetMin = new Vector2(20f, 20f);
        body.rectTransform.offsetMax = new Vector2(-20f, -86f);

        // Use a CanvasGroup so the panel can be hidden without disabling the
        // GameObject — FishInspector polls hover every frame in Update and
        // therefore must stay active.
        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        if (group == null)
            group = panel.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        FishInspector inspector = panel.AddComponent<FishInspector>();
        inspector.SetGeneLibrary(lib);
        inspector.SetWorldCamera(Camera.main);
        inspector.SetWiring(panel, title, body, closeButton, swatch);

        panel.SetActive(true);
    }

    // -- UI primitives ----------------------------------------------------

    static GameObject CreateUiPanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Create Panel");
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = true;
        return go;
    }

    static Text CreateText(Transform parent, string name, string content,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size,
        int fontSize, FontStyle style, TextAnchor align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        Undo.RegisterCreatedObjectUndo(go, "Create Text");
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Text t = go.GetComponent<Text>();
        t.text = content;
        t.font = LegacyFont();
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static Image CreateSwatch(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Create Swatch");
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static GameObject CreateButton(Transform parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        Undo.RegisterCreatedObjectUndo(go, "Create Button");
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = go.GetComponent<Image>();
        img.color = ButtonBg;

        Button btn = go.GetComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = ButtonBg;
        cb.highlightedColor = ButtonHover;
        cb.pressedColor = ButtonPressed;
        cb.selectedColor = ButtonHover;
        cb.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
        btn.colors = cb;

        Text labelText = CreateText(go.transform, "Label", label,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero,
            22, FontStyle.Bold, TextAnchor.MiddleCenter, TextColor);
        labelText.rectTransform.anchorMin = Vector2.zero;
        labelText.rectTransform.anchorMax = Vector2.one;
        labelText.rectTransform.offsetMin = Vector2.zero;
        labelText.rectTransform.offsetMax = Vector2.zero;
        labelText.raycastTarget = false;

        return go;
    }

    static Font LegacyFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f != null)
            return f;
        // Older Unity versions ship "Arial.ttf" as the legacy builtin.
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
