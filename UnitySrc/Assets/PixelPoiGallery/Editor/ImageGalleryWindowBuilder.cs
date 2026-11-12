using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

public static class ImageGalleryWindowBuilder
{
    private const string PlaceholderDir = "Assets/PixelPoiGallery/Sprites/Placeholders";

    private static readonly Color ColBackground = new Color32(0x1A, 0x1A, 0x2E, 0xFF);
    private static readonly Color ColPanel = new Color32(0x24, 0x24, 0x3E, 0xFF);
    private static readonly Color ColSlotEmpty = new Color32(0x2E, 0x2E, 0x4A, 0xFF);
    private static readonly Color ColAccent = new Color32(0xF5, 0xA6, 0x23, 0xFF);
    private static readonly Color ColPrimary = new Color32(0x4A, 0x90, 0xE2, 0xFF);
    private static readonly Color ColRemove = new Color32(0xE2, 0x4A, 0x4A, 0xFF);

    [MenuItem("Tools/PixelPoi/Build Image Gallery Window")]
    public static void BuildWindow()
    {
        EnsurePlaceholders();

        var canvas = GetOrCreateCanvas();
        var existing = Object.FindObjectOfType<ImageGalleryWindow>();
        if (existing != null)
        {
            Debug.LogWarning("ImageGalleryWindow already exists in scene. Delete it first to rebuild.");
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        var roundedPanel = Load("panel_rounded.png");
        var roundedSlot = Load("slot_rounded.png");
        var plusSprite = Load("plus_icon.png");
        var circleSprite = Load("circle.png");

        var windowGo = new GameObject("ImageGalleryWindow", typeof(RectTransform), typeof(Image));
        windowGo.transform.SetParent(canvas.transform, false);
        var windowRt = windowGo.GetComponent<RectTransform>();
        Stretch(windowRt);
        windowGo.GetComponent<Image>().color = ColBackground;

        var safeArea = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
        safeArea.transform.SetParent(windowGo.transform, false);
        var safeRt = safeArea.GetComponent<RectTransform>();
        Stretch(safeRt);

        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(safeArea.transform, false);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -24f);
        titleRt.sizeDelta = new Vector2(-48f, 70f);
        var title = titleGo.AddComponent<TextMeshProUGUI>();
        title.text = "IMAGES";
        title.fontSize = 44;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;
        title.fontStyle = FontStyles.Bold;

        var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollGo.transform.SetParent(safeArea.transform, false);
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0f, 0f);
        scrollRt.anchorMax = new Vector2(1f, 1f);
        scrollRt.offsetMin = new Vector2(24f, 24f);
        scrollRt.offsetMax = new Vector2(-24f, -110f);
        var scrollBg = scrollGo.GetComponent<Image>();
        scrollBg.sprite = roundedPanel;
        scrollBg.type = Image.Type.Sliced;
        scrollBg.color = ColPanel;

        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        var viewportRt = viewportGo.GetComponent<RectTransform>();
        Stretch(viewportRt);
        viewportRt.offsetMin = new Vector2(12f, 12f);
        viewportRt.offsetMax = new Vector2(-12f, -12f);
        viewportGo.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
        viewportGo.GetComponent<Mask>().showMaskGraphic = false;

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewportGo.transform, false);
        var contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);

        var grid = contentGo.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(300f, 300f);
        grid.spacing = new Vector2(20f, 20f);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.UpperCenter;

        var fitter = contentGo.GetComponent<ContentSizeFitter>();
        //fitter.verticalFit = ContentSizeFitter.Fit.PreferredSize;

        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.content = contentRt;
        scroll.viewport = viewportRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 25f;

        var prefab = BuildSlotPrefab(roundedSlot, plusSprite, circleSprite);

        var window = windowGo.AddComponent<ImageGalleryWindow>();
        window.slotCount = 12;
        window.slotPrefab = prefab.GetComponent<ImageSlotCell>();
        window.slotsContainer = contentGo.transform;
        window.titleLabel = title;

        var warning = BuildWarningPopup(canvas, roundedPanel);
        var detail = BuildDetailWindow(canvas, roundedPanel, circleSprite, warning);
        window.detailWindow = detail;

        prefab.SetActive(false);
        prefab.transform.SetParent(windowGo.transform, false);

        Selection.activeGameObject = windowGo;
        EditorUtility.SetDirty(windowGo);
        Debug.Log("Image Gallery Window built. Press Play to test (Editor uses file panel; device uses gallery).");
    }

    private static PhotoDetailWindow BuildDetailWindow(Canvas canvas, Sprite panelSprite, Sprite circle, WarningPopup warning)
    {
        var root = new GameObject("PhotoDetailWindow", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        var rootRt = root.GetComponent<RectTransform>();
        Stretch(rootRt);

        var backdropGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        backdropGo.transform.SetParent(root.transform, false);
        var backdropRt = backdropGo.GetComponent<RectTransform>();
        Stretch(backdropRt);
        var backdrop = backdropGo.GetComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0f);
        var backdropBtn = backdropGo.GetComponent<Button>();

        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(root.transform, false);
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(760f, 1000f);
        var panelImg = panelGo.GetComponent<Image>();
        panelImg.sprite = panelSprite;
        panelImg.type = Image.Type.Sliced;
        panelImg.color = ColPanel;

        var numberGo = new GameObject("Number", typeof(RectTransform));
        numberGo.transform.SetParent(panelGo.transform, false);
        var numberRt = numberGo.GetComponent<RectTransform>();
        numberRt.anchorMin = new Vector2(0.5f, 1f);
        numberRt.anchorMax = new Vector2(0.5f, 1f);
        numberRt.pivot = new Vector2(0.5f, 1f);
        numberRt.anchoredPosition = new Vector2(0f, -30f);
        numberRt.sizeDelta = new Vector2(400f, 70f);
        var number = numberGo.AddComponent<TextMeshProUGUI>();
        number.text = "#1";
        number.fontSize = 48;
        number.alignment = TextAlignmentOptions.Center;
        number.color = Color.white;
        number.fontStyle = FontStyles.Bold;

        var photoGo = new GameObject("Photo", typeof(RectTransform), typeof(Image));
        photoGo.transform.SetParent(panelGo.transform, false);
        var photoRt = photoGo.GetComponent<RectTransform>();
        photoRt.anchorMin = new Vector2(0.5f, 0.5f);
        photoRt.anchorMax = new Vector2(0.5f, 0.5f);
        photoRt.anchoredPosition = new Vector2(0f, 40f);
        photoRt.sizeDelta = new Vector2(640f, 640f);
        var photo = photoGo.GetComponent<Image>();
        photo.preserveAspect = true;

        var closeGo = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGo.transform.SetParent(panelGo.transform, false);
        var closeRt = closeGo.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1f, 1f);
        closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-20f, -20f);
        closeRt.sizeDelta = new Vector2(64f, 64f);
        var closeImg = closeGo.GetComponent<Image>();
        closeImg.sprite = circle;
        closeImg.color = ColRemove;
        var closeBtn = closeGo.GetComponent<Button>();
        AddButtonLabel(closeGo.transform, "X");

        var changeBtn = BuildActionButton(panelGo.transform, "Change", ColPrimary, panelSprite,
            new Vector2(-250f, 70f));
        var removeBtn = BuildActionButton(panelGo.transform, "Remove", ColRemove, panelSprite,
            new Vector2(0f, 70f));
        var uploadBtn = BuildActionButton(panelGo.transform, "Upload", ColAccent, panelSprite,
            new Vector2(250f, 70f));

        var detail = root.AddComponent<PhotoDetailWindow>();
        detail.backdrop = backdrop;
        detail.panel = panelRt;
        detail.photoImage = photo;
        detail.numberLabel = number;
        detail.changeButton = changeBtn;
        detail.removeButton = removeBtn;
        detail.uploadButton = uploadBtn;
        detail.closeButton = closeBtn;
        detail.warningPopup = warning;

        backdropBtn.onClick.AddListener(closeBtn.onClick.Invoke);

        root.SetActive(false);
        return detail;
    }

    private static Button BuildActionButton(Transform parent, string label, Color color, Sprite bg, Vector2 bottomOffset)
    {
        var go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(bottomOffset.x, bottomOffset.y);
        rt.sizeDelta = new Vector2(220f, 110f);

        var img = go.GetComponent<Image>();
        img.sprite = bg;
        img.type = Image.Type.Sliced;
        img.color = color;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        AddButtonLabel(go.transform, label);
        return btn;
    }

    private static void AddButtonLabel(Transform parent, string text)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        Stretch(rt);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = 32;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.fontStyle = FontStyles.Bold;
    }

    private static WarningPopup BuildWarningPopup(Canvas canvas, Sprite panelSprite)
    {
        var root = new GameObject("WarningPopup", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        var rootRt = root.GetComponent<RectTransform>();
        Stretch(rootRt);

        var backdropGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        backdropGo.transform.SetParent(root.transform, false);
        Stretch(backdropGo.GetComponent<RectTransform>());
        var backdrop = backdropGo.GetComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0f);

        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(root.transform, false);
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(640f, 380f);
        var panelImg = panelGo.GetComponent<Image>();
        panelImg.sprite = panelSprite;
        panelImg.type = Image.Type.Sliced;
        panelImg.color = ColPanel;

        var msgGo = new GameObject("Message", typeof(RectTransform));
        msgGo.transform.SetParent(panelGo.transform, false);
        var msgRt = msgGo.GetComponent<RectTransform>();
        msgRt.anchorMin = new Vector2(0f, 0f);
        msgRt.anchorMax = new Vector2(1f, 1f);
        msgRt.offsetMin = new Vector2(40f, 140f);
        msgRt.offsetMax = new Vector2(-40f, -40f);
        var msg = msgGo.AddComponent<TextMeshProUGUI>();
        msg.text = "Warning";
        msg.fontSize = 36;
        msg.alignment = TextAlignmentOptions.Center;
        msg.color = Color.white;

        var okGo = new GameObject("OkButton", typeof(RectTransform), typeof(Image), typeof(Button));
        okGo.transform.SetParent(panelGo.transform, false);
        var okRt = okGo.GetComponent<RectTransform>();
        okRt.anchorMin = new Vector2(0.5f, 0f);
        okRt.anchorMax = new Vector2(0.5f, 0f);
        okRt.pivot = new Vector2(0.5f, 0f);
        okRt.anchoredPosition = new Vector2(0f, 40f);
        okRt.sizeDelta = new Vector2(300f, 90f);
        var okImg = okGo.GetComponent<Image>();
        okImg.sprite = panelSprite;
        okImg.type = Image.Type.Sliced;
        okImg.color = ColPrimary;
        var okBtn = okGo.GetComponent<Button>();
        okBtn.targetGraphic = okImg;
        AddButtonLabel(okGo.transform, "OK");

        var popup = root.AddComponent<WarningPopup>();
        popup.backdrop = backdrop;
        popup.panel = panelRt;
        popup.messageLabel = msg;
        popup.okButton = okBtn;

        root.SetActive(false);
        return popup;
    }

    private static GameObject BuildSlotPrefab(Sprite slotBg, Sprite plus, Sprite circle)
    {
        var go = new GameObject("ImageSlotCell", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300f, 300f);

        var bg = go.GetComponent<Image>();
        bg.sprite = slotBg;
        bg.type = Image.Type.Sliced;
        bg.color = ColSlotEmpty;

        var button = go.GetComponent<Button>();
        button.targetGraphic = bg;
        var colors = button.colors;
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        button.colors = colors;

        var photoGo = new GameObject("Photo", typeof(RectTransform), typeof(Image));
        photoGo.transform.SetParent(go.transform, false);
        var photoRt = photoGo.GetComponent<RectTransform>();
        Stretch(photoRt);
        photoRt.offsetMin = new Vector2(10f, 10f);
        photoRt.offsetMax = new Vector2(-10f, -10f);
        var photo = photoGo.GetComponent<Image>();
        photo.preserveAspect = true;
        photo.enabled = false;

        var plusGo = new GameObject("Plus", typeof(RectTransform), typeof(Image));
        plusGo.transform.SetParent(go.transform, false);
        var plusRt = plusGo.GetComponent<RectTransform>();
        plusRt.anchorMin = new Vector2(0.5f, 0.5f);
        plusRt.anchorMax = new Vector2(0.5f, 0.5f);
        plusRt.sizeDelta = new Vector2(90f, 90f);
        var plusImg = plusGo.GetComponent<Image>();
        plusImg.sprite = plus;
        plusImg.color = ColAccent;

        var idxGo = new GameObject("IndexLabel", typeof(RectTransform), typeof(Image));
        idxGo.transform.SetParent(go.transform, false);
        var idxRt = idxGo.GetComponent<RectTransform>();
        idxRt.anchorMin = new Vector2(0f, 1f);
        idxRt.anchorMax = new Vector2(0f, 1f);
        idxRt.pivot = new Vector2(0f, 1f);
        idxRt.anchoredPosition = new Vector2(10f, -10f);
        idxRt.sizeDelta = new Vector2(56f, 56f);
        var idxBg = idxGo.GetComponent<Image>();
        idxBg.sprite = circle;
        idxBg.color = ColPrimary;

        var idxTextGo = new GameObject("Text", typeof(RectTransform));
        idxTextGo.transform.SetParent(idxGo.transform, false);
        var idxTextRt = idxTextGo.GetComponent<RectTransform>();
        Stretch(idxTextRt);
        var idxText = idxTextGo.AddComponent<TextMeshProUGUI>();
        idxText.text = "1";
        idxText.fontSize = 30;
        idxText.alignment = TextAlignmentOptions.Center;
        idxText.color = Color.white;
        idxText.fontStyle = FontStyles.Bold;

        var removeGo = new GameObject("RemoveButton", typeof(RectTransform), typeof(Image), typeof(Button));
        removeGo.transform.SetParent(go.transform, false);
        var removeRt = removeGo.GetComponent<RectTransform>();
        removeRt.anchorMin = new Vector2(1f, 1f);
        removeRt.anchorMax = new Vector2(1f, 1f);
        removeRt.pivot = new Vector2(1f, 1f);
        removeRt.anchoredPosition = new Vector2(-10f, -10f);
        removeRt.sizeDelta = new Vector2(56f, 56f);
        var removeImg = removeGo.GetComponent<Image>();
        removeImg.sprite = circle;
        removeImg.color = ColRemove;
        var removeButton = removeGo.GetComponent<Button>();
        removeButton.targetGraphic = removeImg;

        var xGo = new GameObject("X", typeof(RectTransform));
        xGo.transform.SetParent(removeGo.transform, false);
        var xRt = xGo.GetComponent<RectTransform>();
        Stretch(xRt);
        var xText = xGo.AddComponent<TextMeshProUGUI>();
        xText.text = "X";
        xText.fontSize = 30;
        xText.alignment = TextAlignmentOptions.Center;
        xText.color = Color.white;
        xText.fontStyle = FontStyles.Bold;

        var cell = go.AddComponent<ImageSlotCell>();
        cell.button = button;
        cell.photoImage = photo;
        cell.plusIcon = plusImg;
        cell.indexLabel = idxText;
        cell.removeButton = removeButton;

        return go;
    }

    private static Canvas GetOrCreateCanvas()
    {
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas != null) return canvas;

        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        return canvas;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Sprite Load(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderDir + "/" + fileName);
    }

    private static void EnsurePlaceholders()
    {
        if (!Directory.Exists(PlaceholderDir))
            Directory.CreateDirectory(PlaceholderDir);

        CreateRoundedIfMissing("panel_rounded.png", 64, 16, Color.white);
        CreateRoundedIfMissing("slot_rounded.png", 64, 18, Color.white);
        CreateCircleIfMissing("circle.png", 64, Color.white);
        CreatePlusIfMissing("plus_icon.png", 64, Color.white);

        AssetDatabase.Refresh();
    }

    private static void CreateRoundedIfMissing(string name, int size, int radius, Color color)
    {
        var path = PlaceholderDir + "/" + name;
        if (File.Exists(path)) return;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, InsideRoundedRect(x, y, size, radius) ? color : Color.clear);
            }
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        ConfigureSlicedSprite(path, radius);
    }

    private static void CreateCircleIfMissing(string name, int size, Color color)
    {
        var path = PlaceholderDir + "/" + name;
        if (File.Exists(path)) return;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var c = (size - 1) / 2f;
        var r = size / 2f - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var dx = x - c;
                var dy = y - c;
                tex.SetPixel(x, y, (dx * dx + dy * dy) <= r * r ? color : Color.clear);
            }
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        ConfigureSimpleSprite(path);
    }

    private static void CreatePlusIfMissing(string name, int size, Color color)
    {
        var path = PlaceholderDir + "/" + name;
        if (File.Exists(path)) return;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int i = 0; i < size * size; i++)
            tex.SetPixel(i % size, i / size, Color.clear);

        int thick = size / 6;
        int lo = (size - thick) / 2;
        int hi = lo + thick;
        int margin = size / 6;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool vert = x >= lo && x < hi && y >= margin && y < size - margin;
                bool horiz = y >= lo && y < hi && x >= margin && x < size - margin;
                if (vert || horiz)
                    tex.SetPixel(x, y, color);
            }
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        ConfigureSimpleSprite(path);
    }

    private static bool InsideRoundedRect(int x, int y, int size, int radius)
    {
        int minX = radius;
        int maxX = size - 1 - radius;
        int minY = radius;
        int maxY = size - 1 - radius;

        if (x >= minX && x <= maxX) return true;
        if (y >= minY && y <= maxY) return true;

        int cx = x < minX ? minX : maxX;
        int cy = y < minY ? minY : maxY;
        int dx = x - cx;
        int dy = y - cy;
        return (dx * dx + dy * dy) <= radius * radius;
    }

    private static void ConfigureSlicedSprite(string path, int border)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.filterMode = FilterMode.Bilinear;
        importer.spriteBorder = new Vector4(border, border, border, border);
        importer.SaveAndReimport();
    }

    private static void ConfigureSimpleSprite(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
    }
}
