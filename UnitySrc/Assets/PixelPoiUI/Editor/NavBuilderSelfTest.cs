// TEMPORARY batchmode self-test for MainNavigationBuilder. Delete after verification.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DefaultNamespace.BLE;
using UI;
using UI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class NavBuilderSelfTest
{
    private static int _failures;

    private static void Check(bool ok, string what)
    {
        if (!ok) _failures++;
        Debug.Log((ok ? "[SELFTEST PASS] " : "[SELFTEST FAIL] ") + what);
    }

    private static List<string> Snapshot(Scene scene)
    {
        var list = new List<string>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var path = t.name;
                for (var p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
                var comps = string.Join(",", t.GetComponents<Component>().Select(c => c == null ? "MISSING" : c.GetType().Name));
                list.Add($"{path} active={t.gameObject.activeSelf} [{comps}]");
            }
        list.Sort();
        return list;
    }

    private static string Diff(List<string> a, List<string> b)
    {
        var onlyA = a.Except(b).Take(15);
        var onlyB = b.Except(a).Take(15);
        return "only-before:\n  " + string.Join("\n  ", onlyA) + "\nonly-after:\n  " + string.Join("\n  ", onlyB);
    }

    private static void CallPrivate(object target, string method)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Invoke(target, null);
    }

    public static void Run()
    {
        try
        {
            RunInner();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            _failures++;
        }
        Debug.Log($"[SELFTEST] DONE failures={_failures}");
        EditorApplication.Exit(_failures == 0 ? 0 : 1);
    }

    private static void RunInner()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity", OpenSceneMode.Single);
        var original = Snapshot(scene);
        Debug.Log("[SELFTEST] objects before: " + original.Count);

        // ---- build #1
        MainNavigationBuilder.BuildNavigation();
        var built = Snapshot(scene);
        Debug.Log("[SELFTEST] objects after build: " + built.Count + "\n" + Diff(original, built));

        var canvas = GameObject.Find("Canvas");
        var appRoot = canvas.transform.Find("AppRoot");
        Check(appRoot != null, "AppRoot exists under Canvas");
        var safe = appRoot.Find("SafeArea");
        Check(safe != null && safe.GetComponent<SafeAreaFitter>() != null, "SafeArea with SafeAreaFitter");
        var windowsRoot = (RectTransform)safe.Find("WindowsRoot");
        var tabBar = (RectTransform)safe.Find("TabBar");
        Check(windowsRoot.anchorMin == new Vector2(0, 0.1f) && windowsRoot.anchorMax == Vector2.one, "WindowsRoot anchors (0,0.1)-(1,1)");
        Check(tabBar.anchorMin == Vector2.zero && tabBar.anchorMax == new Vector2(1, 0.1f), "TabBar anchors (0,0)-(1,0.1)");
        var names = windowsRoot.Cast<Transform>().Select(t => t.name).ToArray();
        Check(names.SequenceEqual(new[] { "ControlWindow", "BLEConnection", "ImageGalleryWindow", "EffectsWindow" }), "4 windows in order: " + string.Join(",", names));
        foreach (Transform w in windowsRoot)
            Check(!w.gameObject.activeSelf && w.localScale == Vector3.zero, $"{w.name} starts inactive, scale 0");
        Check(canvas.transform.Find("Main") == null, "Main removed");
        var control = windowsRoot.Find("ControlWindow");
        Check(control.GetComponentInChildren<BrightnessWindow>(true)?.transform.parent == control, "Brightness under ControlWindow");
        Check(control.GetComponentInChildren<FlexibleColorPicker>(true) != null, "FlexibleColorPicker carried into ControlWindow");
        var ble = Object.FindFirstObjectByType<BLEWindow>(FindObjectsInactive.Include);
        Check(ble.transform.Find("Content/CloseBtn") == null, "BLE CloseBtn removed");
        Check(ble.transform.Find("Content/BlePanel/DeviceList") != null, "BLE content intact");
        var gallery = Object.FindFirstObjectByType<ImageGalleryWindow>(FindObjectsInactive.Include);
        Check(gallery.transform.Find("SafeArea/closeBtn") == null, "Gallery closeBtn removed");
        var cell = gallery.transform.Find("ImageSlotCell");
        Check(cell != null && !cell.gameObject.activeSelf && gallery.slotPrefab != null && gallery.slotPrefab.transform == cell, "ImageSlotCell template intact + referenced");
        Check(gallery.detailWindow != null && gallery.detailWindow.transform.parent == gallery.transform, "PhotoDetailWindow still child + referenced");
        Check(gallery.slotsContainer != null && gallery.slotsContainer.IsChildOf(gallery.transform), "slotsContainer intact");
        var nav = tabBar.GetComponent<TabBarNavigation>();
        var so = new SerializedObject(nav);
        var tabs = so.FindProperty("_tabs");
        Check(tabs.arraySize == 4, "4 tab entries");
        for (int i = 0; i < tabs.arraySize; i++)
        {
            var e = tabs.GetArrayElementAtIndex(i);
            Check(e.FindPropertyRelative("Button").objectReferenceValue != null && e.FindPropertyRelative("Window").objectReferenceValue != null,
                $"tab {i} '{e.FindPropertyRelative("Id").stringValue}' wired");
        }
        Check(tabBar.GetComponentsInChildren<TabButtonView>(true).Length == 4, "4 TabButtonViews");
        Check(new SerializedObject(ble).FindProperty("_navigation").objectReferenceValue == nav, "BLEWindow._navigation wired");
        Check(new SerializedObject(ble).FindProperty("_hideBtn").objectReferenceValue == null, "BLEWindow._hideBtn cleared");

        // ---- build #2: idempotent
        MainNavigationBuilder.BuildNavigation();
        var rebuilt = Snapshot(scene);
        Check(rebuilt.SequenceEqual(built), "re-run produces identical hierarchy\n" + Diff(built, rebuilt));

        // ---- undo both builds -> original
        Undo.PerformUndo();
        var afterUndo1 = Snapshot(scene);
        Check(afterUndo1.SequenceEqual(built), "undo #1 returns to state after build #1\n" + Diff(built, afterUndo1));
        Undo.PerformUndo();
        var afterUndo2 = Snapshot(scene);
        Check(afterUndo2.SequenceEqual(original), "undo #2 returns to original scene\n" + Diff(original, afterUndo2));
        var bleAfter = Object.FindFirstObjectByType<BLEWindow>(FindObjectsInactive.Include);
        Check(new SerializedObject(bleAfter).FindProperty("_hideBtn").objectReferenceValue != null, "undo restored BLEWindow._hideBtn reference");

        // ---- redo and runtime-logic smoke test (edit mode, manual lifecycle)
        MainNavigationBuilder.BuildNavigation();
        nav = Object.FindFirstObjectByType<TabBarNavigation>(FindObjectsInactive.Include);
        RuntimeSmoke(nav);
    }

    private static void RuntimeSmoke(TabBarNavigation nav)
    {
        // Swap the real windows for stubs to avoid BLE/permissions side effects in edit mode.
        var root = new GameObject("SmokeRoot", typeof(RectTransform));
        var windows = new List<StubWindow>();
        var buttons = new List<TabButtonView>();
        for (int i = 0; i < 4; i++)
        {
            var w = new GameObject("W" + i, typeof(RectTransform)).AddComponent<StubWindow>();
            w.transform.SetParent(root.transform, false);
            windows.Add(w);
        }
        var bar = Object.Instantiate(nav.gameObject, root.transform);
        var smokeNav = bar.GetComponent<TabBarNavigation>();
        buttons.AddRange(bar.GetComponentsInChildren<TabButtonView>(true));
        var so = new SerializedObject(smokeNav);
        var tabs = so.FindProperty("_tabs");
        for (int i = 0; i < 4; i++)
        {
            tabs.GetArrayElementAtIndex(i).FindPropertyRelative("Window").objectReferenceValue = windows[i];
            tabs.GetArrayElementAtIndex(i).FindPropertyRelative("Button").objectReferenceValue = buttons[i];
        }
        so.FindProperty("_windowsRoot").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();

        foreach (var b in buttons) CallPrivate(b, "Awake");
        CallPrivate(smokeNav, "Awake");
        CallPrivate(smokeNav, "Start");

        int ActiveCount() => windows.Count(w => w.gameObject.activeSelf);
        Check(smokeNav.CurrentIndex == 0 && ActiveCount() == 1 && windows[0].gameObject.activeSelf, "start: default tab 0 open, only one window active");
        Check(windows[0].IsOpen, "start: window 0 IsOpen");

        int changed = 0;
        smokeNav.TabChanged += _ => changed++;
        var rng = new System.Random(1);
        bool ok = true;
        for (int k = 0; k < 200; k++)
        {
            int idx = rng.Next(4);
            buttons[idx].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            if (ActiveCount() != 1 || !windows[idx].gameObject.activeSelf || smokeNav.CurrentIndex != idx
                || windows.Where((w, j) => j != idx).Any(w => w.IsOpen))
            {
                ok = false;
                Debug.LogError($"[SELFTEST] invariant broken at click {k} idx {idx}");
                break;
            }
        }
        Check(ok, "200 random tab clicks: exactly one window active, matches CurrentIndex");

        int before = changed;
        int cur = smokeNav.CurrentIndex;
        buttons[cur].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Check(changed == before, "clicking selected tab does not fire TabChanged");

        smokeNav.Select("effects");
        Check(smokeNav.CurrentIndex == 3, "Select(string id)");
        smokeNav.SelectDefault();
        Check(smokeNav.CurrentIndex == 0, "SelectDefault");

        // Null-safe entry
        tabs.GetArrayElementAtIndex(2).FindPropertyRelative("Window").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
        LogAssertIgnore(() => smokeNav.Select(2));
        Check(smokeNav.CurrentIndex == 0, "invalid tab is skipped without NRE");

        Object.DestroyImmediate(root);

        // Gallery OnHidden closes popups
        var gallery = Object.FindFirstObjectByType<ImageGalleryWindow>(FindObjectsInactive.Include);
        gallery.detailWindow.gameObject.SetActive(true);
        gallery.detailWindow.warningPopup.gameObject.SetActive(true);
        gallery.HideInstant();
        Check(!gallery.detailWindow.gameObject.activeSelf && !gallery.detailWindow.warningPopup.gameObject.activeSelf,
            "gallery HideInstant closes PhotoDetailWindow + WarningPopup");
    }

    private static void LogAssertIgnore(Action a)
    {
        try { a(); }
        catch (Exception e) { Debug.LogException(e); _failures++; }
    }
}
