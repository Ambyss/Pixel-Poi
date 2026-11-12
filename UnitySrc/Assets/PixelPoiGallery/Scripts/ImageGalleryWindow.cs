using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UI.Navigation;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ImageGalleryWindow : AppWindow
{
    [Header("Slots")]
    public int slotCount = 12;
    public ImageSlotCell slotPrefab;
    public Transform slotsContainer;

    [Header("Optional")]
    public TextMeshProUGUI titleLabel;
    [Tooltip("Switches back to the default tab when pressed.")]
    public Button closeButton;

    [Header("Detail")]
    public PhotoDetailWindow detailWindow;

    private readonly List<ImageSlotCell> _cells = new List<ImageSlotCell>();
    private readonly List<string> _slotFiles = new List<string>();

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(ReturnToDefaultTab);
    }

    private void Start()
    {
        BuildSlots();
        LoadFromStorage();
    }

    private void BuildSlots()
    {
        for (int i = 0; i < slotCount; i++)
        {
            var cell = Instantiate(slotPrefab, slotsContainer);
            cell.gameObject.SetActive(true);
            cell.Init(i, OnSlotPick, OnSlotRemove, OnSlotOpen);
            _cells.Add(cell);
            _slotFiles.Add(null);
        }
    }

    private void LoadFromStorage()
    {
        var files = GalleryStorage.LoadSlotFiles(slotCount);
        for (int i = 0; i < slotCount; i++)
        {
            _slotFiles[i] = files[i];
            if (!string.IsNullOrEmpty(files[i]))
                _cells[i].SetImageFromFile(files[i]);
            else
                _cells[i].SetEmpty();
        }
    }

    private void OnSlotPick(int slotIndex)
    {
        PickImageNative(slotIndex);
    }

    private void OnSlotOpen(int slotIndex)
    {
        var path = _slotFiles[slotIndex];
        if (string.IsNullOrEmpty(path))
            return;

        detailWindow.Open(slotIndex, path, OnSlotPick, OnSlotRemove);
    }

    private void OnSlotRemove(int slotIndex)
    {
        var old = _slotFiles[slotIndex];
        GalleryStorage.DeleteLocalFile(old);
        _slotFiles[slotIndex] = null;
        _cells[slotIndex].SetEmpty();
        GalleryStorage.SaveSlotFiles(_slotFiles);
    }

    private void ApplyPickedPath(int slotIndex, string pickedPath)
    {
        if (string.IsNullOrEmpty(pickedPath) || !File.Exists(pickedPath))
        {
            Debug.LogWarning("Picked path is empty or missing: " + pickedPath);
            return;
        }

        var old = _slotFiles[slotIndex];
        GalleryStorage.DeleteLocalFile(old);

        var localPath = GalleryStorage.CopyImageToLocal(pickedPath, slotIndex);
        _slotFiles[slotIndex] = localPath;
        _cells[slotIndex].SetImageFromFile(localPath);
        GalleryStorage.SaveSlotFiles(_slotFiles);
    }

    private void ApplyPickedTexture(int slotIndex, Texture2D tex)
    {
        if (tex == null)
        {
            Debug.LogWarning("Picked texture is null");
            return;
        }

        var old = _slotFiles[slotIndex];
        GalleryStorage.DeleteLocalFile(old);

        var localPath = GalleryStorage.SaveTextureToLocal(tex, slotIndex);
        _slotFiles[slotIndex] = localPath;
        _cells[slotIndex].SetImageFromTexture(tex);
        GalleryStorage.SaveSlotFiles(_slotFiles);
    }

    private void PickImageNative(int slotIndex)
    {
#if UNITY_EDITOR
        var path = UnityEditor.EditorUtility.OpenFilePanel("Select image", "", "png,jpg,jpeg");
        if (!string.IsNullOrEmpty(path))
            ApplyPickedPath(slotIndex, path);
        return;
#else
        if (TryPickWithNativeGallery(slotIndex))
            return;

        Debug.LogError(
            "NativeGallery not found. Import yasirkula NativeGallery " +
            "(https://github.com/yasirkula/UnityNativeGallery) to enable gallery picking on device.");
#endif
    }

    private bool TryPickWithNativeGallery(int slotIndex)
    {
        var nativeGalleryType = System.Type.GetType("NativeGallery, Assembly-CSharp")
                                ?? FindTypeByName("NativeGallery");
        if (nativeGalleryType == null)
            return false;

        var permissionType = nativeGalleryType.GetNestedType("MediaPickCallback");
        if (permissionType == null)
            return false;

        var getImageMethod = nativeGalleryType.GetMethod(
            "GetImageFromGallery",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { permissionType, typeof(string), typeof(string) },
            null);

        if (getImageMethod == null)
            return false;

        System.Action<string> callback = pickedPath =>
        {
            if (string.IsNullOrEmpty(pickedPath))
                return;

            var tex = LoadTextureFromNative(nativeGalleryType, pickedPath);
            if (tex != null)
                ApplyPickedTexture(slotIndex, tex);
            else
                ApplyPickedPath(slotIndex, pickedPath);
        };

        var del = System.Delegate.CreateDelegate(permissionType, callback.Target, callback.Method);
        getImageMethod.Invoke(null, new object[] { del, "Select image", "image/*" });
        return true;
    }

    private Texture2D LoadTextureFromNative(System.Type nativeGalleryType, string path)
    {
        var loadMethod = nativeGalleryType.GetMethod(
            "LoadImageAtPath",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(string), typeof(int), typeof(bool), typeof(bool), typeof(bool) },
            null);

        if (loadMethod == null)
            return GalleryStorage.LoadTexture(path);

        var tex = loadMethod.Invoke(null, new object[] { path, 2048, false, false, false }) as Texture2D;
        return tex;
    }

    private static System.Type FindTypeByName(string name)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(name);
            if (t != null) return t;
        }
        return null;
    }

    protected override void OnHidden()
    {
        // The popups are children of this window: close them so the tab reopens clean.
        if (detailWindow == null)
            return;

        detailWindow.HideImmediate();
        if (detailWindow.warningPopup != null)
            detailWindow.warningPopup.HideImmediate();
    }
}
