using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class GalleryStorage
{
    private const string FolderName = "PixelPoiImages";
    private const string IndexFileName = "slots.json";

    public static string FolderPath => Path.Combine(Application.persistentDataPath, FolderName);

    private static string IndexPath => Path.Combine(FolderPath, IndexFileName);

    [System.Serializable]
    private class SlotIndex
    {
        public List<string> files = new List<string>();
    }

    public static void EnsureFolder()
    {
        if (!Directory.Exists(FolderPath))
            Directory.CreateDirectory(FolderPath);
    }

    public static List<string> LoadSlotFiles(int slotCount)
    {
        EnsureFolder();
        var result = new List<string>();
        for (int i = 0; i < slotCount; i++) result.Add(null);

        if (!File.Exists(IndexPath))
            return result;

        var json = File.ReadAllText(IndexPath);
        var index = JsonUtility.FromJson<SlotIndex>(json);
        if (index == null || index.files == null)
            return result;

        for (int i = 0; i < slotCount && i < index.files.Count; i++)
        {
            var fileName = index.files[i];
            if (string.IsNullOrEmpty(fileName))
                continue;

            var fullPath = Path.Combine(FolderPath, fileName);
            if (File.Exists(fullPath))
                result[i] = fullPath;
            else
                result[i] = null;
        }

        return result;
    }

    public static void SaveSlotFiles(List<string> fullPaths)
    {
        EnsureFolder();
        var index = new SlotIndex();
        foreach (var p in fullPaths)
        {
            if (string.IsNullOrEmpty(p))
                index.files.Add("");
            else
                index.files.Add(Path.GetFileName(p));
        }

        File.WriteAllText(IndexPath, JsonUtility.ToJson(index));
    }

    public static string CopyImageToLocal(string sourcePath, int slotIndex)
    {
        EnsureFolder();
        var ext = Path.GetExtension(sourcePath);
        if (string.IsNullOrEmpty(ext)) ext = ".png";

        var fileName = "slot_" + slotIndex + "_" + System.DateTime.Now.Ticks + ext;
        var destPath = Path.Combine(FolderPath, fileName);

        File.Copy(sourcePath, destPath, true);
        return destPath;
    }

    public static string SaveTextureToLocal(Texture2D tex, int slotIndex)
    {
        EnsureFolder();
        var fileName = "slot_" + slotIndex + "_" + System.DateTime.Now.Ticks + ".png";
        var destPath = Path.Combine(FolderPath, fileName);

        File.WriteAllBytes(destPath, tex.EncodeToPNG());
        return destPath;
    }

    public static void DeleteLocalFile(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return;
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }

    public static Texture2D LoadTexture(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            return null;

        var bytes = File.ReadAllBytes(fullPath);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(bytes);
        return tex;
    }
}
