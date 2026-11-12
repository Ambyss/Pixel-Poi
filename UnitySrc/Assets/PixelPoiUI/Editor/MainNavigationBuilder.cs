using System;
using System.Collections.Generic;
using System.Text;
using DefaultNamespace.BLE;
using TMPro;
using UI;
using UI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Builds Canvas/AppRoot/SafeArea/{WindowsRoot, TabBar} in the open scene.
/// Re-runnable: existing windows are kept as they are, only the TabBar is rebuilt.
/// The whole run is a single undo step.
/// </summary>
public static class MainNavigationBuilder
{
    private const string UndoName = "Build Main Navigation";
    private const string TabIconPath = "Assets/PixelPoiGallery/Sprites/Placeholders/circle.png";

    private const string AppRootName = "AppRoot";
    private const string SafeAreaName = "SafeArea";
    private const string WindowsRootName = "WindowsRoot";
    private const string TabBarName = "TabBar";
    private const string ControlWindowName = "ControlWindow";
    private const string EffectsWindowName = "EffectsWindow";
    private const string LegacyMainName = "Main";
    private const string LegacyContentName = "Content";
    private const string CloseButtonName = "closeBtn"; // CloseBtn / closeBtn, compared ignoring case

    private static readonly string[] LegacyMainButtons = { "ConnectionBtn", "ImgBtn" };

    private static readonly Color ColBackground = new Color32(0x1A, 0x1A, 0x2E, 0xFF);
    private static readonly Color ColPanel = new Color32(0x24, 0x24, 0x3E, 0xFF);
    private static readonly Color ColAccent = new Color32(0xF5, 0xA6, 0x23, 0xFF);
    private static readonly Color ColTabNormal = new Color32(0x8A, 0x8A, 0xA3, 0xFF);
    private static readonly Color ColDivider = new Color32(0x2E, 0x2E, 0x4A, 0xFF);

    // Order = tab order = TabBarNavigation._tabs order.
    private static readonly (string Id, string Label)[] Tabs =
    {
        ("control", "Control"),
        ("connect", "Connect"),
        ("images", "Images"),
        ("effects", "Effects"),
    };

    [MenuItem("Tools/PixelPoi/Build Main Navigation")]
    public static void BuildNavigation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[MainNavigationBuilder] Exit Play Mode before building the navigation.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);

        var report = new Report();

        var canvas = GetOrCreateCanvas(report);
        var appRoot = GetOrCreateContainer(canvas.transform, AppRootName, report);
        var safeArea = GetOrCreateContainer(appRoot, SafeAreaName, report, typeof(SafeAreaFitter));
        var windowsRoot = GetOrCreateContainer(safeArea, WindowsRootName, report);

        var control = GetOrCreateStub(windowsRoot, ControlWindowName, "CONTROL", "", report);
        var ble = AdoptWindow(FindInScene<BLEWindow>(), windowsRoot, "BLEConnection (BLEWindow)", report);
        var gallery = AdoptWindow(FindInScene<ImageGalleryWindow>(), windowsRoot, "ImageGalleryWindow", report);
        var effects = GetOrCreateStub(windowsRoot, EffectsWindowName, "EFFECTS", "Coming soon", report);

        MoveBrightness(control, report);
        RemoveLegacyMain(canvas.transform, control, report);
        if (ble != null)
            RemoveCloseButton(ble, "_hideBtn", report);
        if (gallery != null)
            RemoveCloseButton(gallery, "closeButton", report);

        var windows = new AppWindow[] { control, ble, gallery, effects };
        var navigation = RebuildTabBar(safeArea, windowsRoot, windows, report);

        foreach (var window in windows)
        {
            if (window == null)
                continue;
            SetNavigationRef(window, navigation);
            SetClosedState(window);
        }
        ReportUnmanagedWindows(windowsRoot, windows, report);

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = navigation.gameObject;
        Undo.CollapseUndoOperations(undoGroup);

        report.Log(navigation);
    }

    // ---------- hierarchy ----------

    private static Canvas GetOrCreateCanvas(Report report)
    {
        // Prefer the canvas that already hosts the app UI (the scene also holds the
        // IngameDebugConsole canvas, which must not be picked).
        Component anchor = FindInScene<BLEWindow>();
        if (anchor == null) anchor = FindInScene<ImageGalleryWindow>();
        if (anchor == null) anchor = FindInScene<BrightnessWindow>();
        if (anchor != null)
        {
            var parents = anchor.GetComponentsInParent<Canvas>(true);
            if (parents.Length > 0)
                return parents[parents.Length - 1];
        }

        Canvas fallback = null;
        foreach (var candidate in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.transform.parent != null)
                continue;
            if (candidate.name == "Canvas")
                return candidate;
            if (fallback == null)
                fallback = candidate;
        }
        if (fallback != null)
            return fallback;

        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(go, UndoName);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var eventSystem = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystem, UndoName);
        }

        report.Action("Created Canvas (none found in the scene)");
        return canvas;
    }

    private static RectTransform GetOrCreateContainer(Transform parent, string name, Report report, params Type[] extraComponents)
    {
        var existing = parent.Find(name);
        if (existing != null)
            return (RectTransform)existing;

        var types = new List<Type> { typeof(RectTransform) };
        types.AddRange(extraComponents);
        var rt = CreateUIObject(name, parent, types.ToArray());
        Stretch(rt);
        report.Action($"Created {name}");
        return rt;
    }

    private static StubWindow GetOrCreateStub(RectTransform windowsRoot, string name, string title, string body, Report report)
    {
        var existing = windowsRoot.Find(name);
        if (existing != null && existing.TryGetComponent(out StubWindow found))
        {
            report.WindowFound(name);
            return found;
        }

        var rt = CreateUIObject(name, windowsRoot, typeof(RectTransform), typeof(Image));
        Stretch(rt);
        rt.GetComponent<Image>().color = ColBackground;

        var titleLabel = CreateLabel(rt, "Title", 44, Color.white, FontStyles.Bold);
        var titleRt = titleLabel.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -24f);
        titleRt.sizeDelta = new Vector2(-48f, 70f);

        var bodyLabel = CreateLabel(rt, "Body", 32, ColTabNormal, FontStyles.Normal);
        Stretch(bodyLabel.rectTransform);
        bodyLabel.rectTransform.offsetMin = new Vector2(48f, 48f);
        bodyLabel.rectTransform.offsetMax = new Vector2(-48f, -140f);

        var stub = rt.gameObject.AddComponent<StubWindow>();
        var so = new SerializedObject(stub);
        so.FindProperty("_titleLabel").objectReferenceValue = titleLabel;
        so.FindProperty("_bodyLabel").objectReferenceValue = bodyLabel;
        so.ApplyModifiedPropertiesWithoutUndo();
        stub.Setup(title, body);

        report.WindowCreated(name);
        return stub;
    }

    private static T AdoptWindow<T>(T window, RectTransform windowsRoot, string label, Report report) where T : AppWindow
    {
        if (window == null)
        {
            report.Missing(label);
            return null;
        }

        report.WindowFound(label);
        if (window.transform.parent == windowsRoot)
            return window;

        // worldPositionStays: false — keep the local layout; serialized refs survive reparenting.
        Undo.SetTransformParent(window.transform, windowsRoot, false, UndoName);
        var rt = (RectTransform)window.transform;
        Undo.RecordObject(rt, UndoName);
        Stretch(rt);
        EditorUtility.SetDirty(rt);
        report.Action($"Moved {label} under {WindowsRootName}");
        return window;
    }

    private static void MoveBrightness(StubWindow control, Report report)
    {
        var brightness = FindInScene<BrightnessWindow>();
        if (brightness == null)
        {
            report.Missing("Brightness (BrightnessWindow)");
            return;
        }
        if (brightness.transform.parent == control.transform)
            return;

        Undo.SetTransformParent(brightness.transform, control.transform, false, UndoName);
        var rt = (RectTransform)brightness.transform;
        Undo.RecordObject(rt, UndoName);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -140f);
        rt.sizeDelta = new Vector2(-96f, 60f);
        EditorUtility.SetDirty(rt);
        report.Action($"Moved Brightness into {ControlWindowName}");
    }

    private static void RemoveLegacyMain(Transform canvas, StubWindow control, Report report)
    {
        var main = canvas.Find(LegacyMainName);
        if (main == null)
            return;

        if (main.GetComponentInChildren<BrightnessWindow>(true) != null)
        {
            report.Warning($"'{LegacyMainName}' still contains Brightness — left in place, remove it by hand.");
            return;
        }

        // Anything under Main that the old tab buttons don't cover (e.g. the FlexibleColorPicker
        // that MainSceneInstaller binds) is carried over into ControlWindow instead of deleted.
        var containers = new List<Transform> { main };
        var content = main.Find(LegacyContentName);
        if (content != null)
            containers.Add(content);

        var carryOver = new List<Transform>();
        foreach (var container in containers)
        {
            foreach (Transform child in container)
            {
                if (child == content || Array.IndexOf(LegacyMainButtons, child.name) >= 0)
                    continue;
                carryOver.Add(child);
            }
        }

        foreach (var child in carryOver)
        {
            Undo.SetTransformParent(child, control.transform, false, UndoName);
            report.Warning($"'{child.name}' was under '{LegacyMainName}' (not covered by the spec) — moved into {ControlWindowName}, check its layout.");
        }

        Undo.DestroyObjectImmediate(main.gameObject);
        report.Action($"Removed '{LegacyMainName}' ({string.Join(", ", LegacyMainButtons)})");
    }

    private static void RemoveCloseButton(AppWindow window, string fieldName, Report report)
    {
        var so = new SerializedObject(window);
        var prop = so.FindProperty(fieldName);

        GameObject target = null;
        if (prop != null && prop.objectReferenceValue is Component referenced)
            target = referenced.gameObject;
        if (target == null)
            target = FindDescendant(window.transform, CloseButtonName)?.gameObject;

        if (prop != null && prop.objectReferenceValue != null)
        {
            prop.objectReferenceValue = null;
            so.ApplyModifiedProperties();
        }

        // Never delete something outside the window, even if a field points at it.
        if (target == null || !target.transform.IsChildOf(window.transform))
            return;

        string buttonName = target.name;
        Undo.DestroyObjectImmediate(target);
        report.Action($"Removed {window.name}/{buttonName} (tabs replace it)");
    }

    // ---------- tab bar ----------

    private static TabBarNavigation RebuildTabBar(RectTransform safeArea, RectTransform windowsRoot, AppWindow[] windows, Report report)
    {
        float? barHeight = null;
        var old = safeArea.Find(TabBarName);
        if (old != null)
        {
            // Keep a hand-tuned height across rebuilds.
            if (old.TryGetComponent(out TabBarNavigation oldNavigation))
                barHeight = new SerializedObject(oldNavigation).FindProperty("_barHeight").floatValue;
            Undo.DestroyObjectImmediate(old.gameObject);
            report.Action("Removed old TabBar (rebuilding)");
        }

        var bar = CreateUIObject(TabBarName, safeArea, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        var barImage = bar.GetComponent<Image>();
        barImage.color = ColPanel;
        barImage.raycastTarget = true; // taps on the bar must not fall through to the window

        var layout = bar.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 0f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        var divider = CreateUIObject("Divider", bar, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        divider.GetComponent<LayoutElement>().ignoreLayout = true;
        divider.anchorMin = new Vector2(0f, 1f);
        divider.anchorMax = new Vector2(1f, 1f);
        divider.pivot = new Vector2(0.5f, 1f);
        divider.anchoredPosition = Vector2.zero;
        divider.sizeDelta = new Vector2(0f, 2f);
        var dividerImage = divider.GetComponent<Image>();
        dividerImage.color = ColDivider;
        dividerImage.raycastTarget = false;

        var icon = AssetDatabase.LoadAssetAtPath<Sprite>(TabIconPath);
        if (icon == null)
            report.Missing($"Tab icon {TabIconPath} (run Tools/PixelPoi/Build Image Gallery Window once to generate it)");

        var navigation = bar.gameObject.AddComponent<TabBarNavigation>();
        var so = new SerializedObject(navigation);
        var tabs = so.FindProperty("_tabs");
        tabs.arraySize = Tabs.Length;
        for (int i = 0; i < Tabs.Length; i++)
        {
            var view = BuildTab(bar, Tabs[i].Label, icon);
            var entry = tabs.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("Id").stringValue = Tabs[i].Id;
            entry.FindPropertyRelative("Button").objectReferenceValue = view;
            entry.FindPropertyRelative("Window").objectReferenceValue = windows[i];
            if (windows[i] == null)
                report.Warning($"Tab '{Tabs[i].Label}' has no window — it will be skipped at runtime.");
        }
        so.FindProperty("_defaultIndex").intValue = 0;
        if (barHeight.HasValue)
            so.FindProperty("_barHeight").floatValue = barHeight.Value;
        so.FindProperty("_windowsRoot").objectReferenceValue = windowsRoot;
        so.ApplyModifiedPropertiesWithoutUndo();

        Undo.RecordObject(windowsRoot, UndoName);
        navigation.ApplyLayout();
        EditorUtility.SetDirty(windowsRoot);
        EditorUtility.SetDirty(navigation);

        report.Action($"Built TabBar with {Tabs.Length} tabs");
        return navigation;
    }

    private static TabButtonView BuildTab(RectTransform bar, string label, Sprite icon)
    {
        var root = CreateUIObject("Tab_" + label, bar,
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        root.GetComponent<LayoutElement>().flexibleWidth = 1f;

        var background = root.GetComponent<Image>();
        background.raycastTarget = true;

        var button = root.GetComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None; // feedback is the punch + highlight

        var iconRt = CreateUIObject("Icon", root, typeof(RectTransform), typeof(Image));
        iconRt.anchorMin = new Vector2(0.5f, 1f);
        iconRt.anchorMax = new Vector2(0.5f, 1f);
        iconRt.pivot = new Vector2(0.5f, 1f);
        iconRt.anchoredPosition = new Vector2(0f, -8f);
        iconRt.sizeDelta = new Vector2(44f, 44f);
        var iconImage = iconRt.GetComponent<Image>();
        iconImage.sprite = icon;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        var text = CreateLabel(root, "Label", 22, ColTabNormal, FontStyles.Normal);
        var textRt = text.rectTransform;
        textRt.anchorMin = new Vector2(0.5f, 0f);
        textRt.anchorMax = new Vector2(0.5f, 0f);
        textRt.pivot = new Vector2(0.5f, 0f);
        textRt.anchoredPosition = new Vector2(0f, 6f);
        textRt.sizeDelta = new Vector2(200f, 30f);

        var view = root.gameObject.AddComponent<TabButtonView>();
        var so = new SerializedObject(view);
        so.FindProperty("_button").objectReferenceValue = button;
        so.FindProperty("_background").objectReferenceValue = background;
        so.FindProperty("_icon").objectReferenceValue = iconImage;
        so.FindProperty("_label").objectReferenceValue = text;
        so.FindProperty("_selectedColor").colorValue = ColAccent;
        so.FindProperty("_normalColor").colorValue = ColTabNormal;
        so.ApplyModifiedPropertiesWithoutUndo();

        view.SetLabel(label);
        view.SetSelected(false);
        return view;
    }

    // ---------- window state ----------

    private static void SetNavigationRef(AppWindow window, TabBarNavigation navigation)
    {
        var so = new SerializedObject(window);
        so.FindProperty("_navigation").objectReferenceValue = navigation;
        so.ApplyModifiedProperties();
    }

    private static void SetClosedState(AppWindow window)
    {
        // Start hidden so the first frame doesn't flash all windows before TabBarNavigation.Start.
        var go = window.gameObject;
        if (go.activeSelf)
        {
            Undo.RecordObject(go, UndoName);
            go.SetActive(false);
        }
        if (window.transform.localScale != Vector3.zero)
        {
            Undo.RecordObject(window.transform, UndoName);
            window.transform.localScale = Vector3.zero;
        }
        EditorUtility.SetDirty(window);
    }

    private static void ReportUnmanagedWindows(RectTransform windowsRoot, AppWindow[] managed, Report report)
    {
        foreach (Transform child in windowsRoot)
        {
            if (child.TryGetComponent(out AppWindow window) && Array.IndexOf(managed, window) < 0)
                report.Warning($"'{child.name}' is an AppWindow under {WindowsRootName} without a tab — left untouched.");
        }
    }

    // ---------- helpers ----------

    private static RectTransform CreateUIObject(string name, Transform parent, params Type[] components)
    {
        var go = new GameObject(name, components);
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, UndoName);
        return (RectTransform)go.transform;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, float fontSize, Color color, FontStyles style)
    {
        var rt = CreateUIObject(name, parent, typeof(RectTransform));
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.fontStyle = style;
        text.raycastTarget = false;
        return text;
    }

    private static T FindInScene<T>() where T : Object
    {
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        foreach (Transform child in root)
        {
            if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
                return child;
            var nested = FindDescendant(child, name);
            if (nested != null)
                return nested;
        }
        return null;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private sealed class Report
    {
        private readonly List<string> _found = new();
        private readonly List<string> _created = new();
        private readonly List<string> _missing = new();
        private readonly List<string> _actions = new();
        private readonly List<string> _warnings = new();

        public void WindowFound(string name) => _found.Add(name);
        public void WindowCreated(string name) => _created.Add(name);
        public void Missing(string name) => _missing.Add(name);
        public void Action(string text) => _actions.Add(text);
        public void Warning(string text) => _warnings.Add(text);

        public void Log(Object context)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[MainNavigationBuilder] Main navigation built.");
            sb.AppendLine($"Windows found: {_found.Count} ({Join(_found)})");
            sb.AppendLine($"Windows created: {_created.Count} ({Join(_created)})");
            sb.AppendLine($"Not found: {_missing.Count} ({Join(_missing)})");
            foreach (var action in _actions)
                sb.AppendLine("  • " + action);
            foreach (var warning in _warnings)
                sb.AppendLine("  ! " + warning);

            if (_missing.Count > 0 || _warnings.Count > 0)
                Debug.LogWarning(sb.ToString(), context);
            else
                Debug.Log(sb.ToString(), context);
        }

        private static string Join(List<string> items) => items.Count == 0 ? "—" : string.Join(", ", items);
    }
}
