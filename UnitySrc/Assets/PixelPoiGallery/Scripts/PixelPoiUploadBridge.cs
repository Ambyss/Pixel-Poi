using System.Reflection;
using UnityEngine;
using Zenject;

public static class PixelPoiUploadBridge
{
    [Inject]
    
    public const int PixelHeight = 36;
    public const int FrameDelayMs = 5;

    public static bool IsConnected()
    {
        var sender = ResolveBleSender();
        if (sender == null)
        {
            Debug.LogWarning("BLESender not found. Treating as not connected.");
            return false;
        }

        var prop = sender.GetType().GetProperty("IsConnected",
            BindingFlags.Public | BindingFlags.Instance);
        if (prop == null)
        {
            Debug.LogWarning("BLESender.IsConnected not found.");
            return false;
        }

        return (bool)prop.GetValue(sender);
    }

    public static bool Upload(
        string localImagePath,
        int slotIndex,
        System.Action onComplete = null,
        System.Action<string> onFailed = null,
        System.Action<float> onProgress = null)
    {
        var bytes = ConvertImage(localImagePath);
        if (bytes == null || bytes.Length == 0)
        {
            var reason = ConversionError() ?? "Conversion failed";
            onFailed?.Invoke(reason);
            Debug.LogError($"Conversion failed for {localImagePath}: {reason}");
            return false;
        }

        var imgSender = ResolveImgSender();
        if (imgSender == null)
        {
            onFailed?.Invoke("ImgSender not found");
            Debug.LogError("ImgSender not found. Cannot upload.");
            return false;
        }

        WireEvent(imgSender, "OnUploadComplete", onComplete);
        WireEventString(imgSender, "OnUploadFailed", onFailed);
        WireEventFloat(imgSender, "OnUploadProgress", onProgress);

        var method = imgSender.GetType().GetMethod("SendImage",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            new[] { typeof(byte[]), typeof(int) },
            null);

        if (method == null)
        {
            onFailed?.Invoke("SendImage not found");
            Debug.LogError("ImgSender.SendImage(byte[], int) not found.");
            return false;
        }

        method.Invoke(imgSender, new object[] { bytes, slotIndex });
        return true;
    }

    private static void WireEvent(object target, string fieldName, System.Action handler)
    {
        if (handler == null) return;
        var field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        if (field == null) return;
        field.SetValue(target, handler);
    }

    private static void WireEventString(object target, string fieldName, System.Action<string> handler)
    {
        if (handler == null) return;
        var field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        if (field == null) return;
        field.SetValue(target, handler);
    }

    private static void WireEventFloat(object target, string fieldName, System.Action<float> handler)
    {
        if (handler == null) return;
        var field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        if (field == null) return;
        field.SetValue(target, handler);
    }

    private static byte[] ConvertImage(string path)
    {
        var converterType = FindType("ImageConverter");
        if (converterType == null)
        {
            Debug.LogError("ImageConverter type not found.");
            return null;
        }

        var method = converterType.GetMethod("ImageToBinaryFile", // TODO
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(string), typeof(int), typeof(int) },
            null);

        if (method == null)
        {
            Debug.LogError("ImageConverter.ImageToBinaryFile(string,int,int) not found.");
            return null;
        }

        return method.Invoke(null, new object[] { path, PixelHeight, FrameDelayMs }) as byte[];
    }

    /// <summary>
    /// ImageConverter.LastError, if that property still exists. Null when it does not,
    /// so a rename degrades to the generic message instead of throwing.
    /// </summary>
    private static string ConversionError()
    {
        var converterType = FindType("ImageConverter");
        var prop = converterType?.GetProperty("LastError",
            BindingFlags.Public | BindingFlags.Static);
        var value = prop?.GetValue(null) as string;
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static object ResolveBleSender()
    {
        return ResolveFromZenject("BLESender");
    }

    private static object ResolveImgSender()
    {
        return ResolveFromZenject("ImgSender");
    }

    private static object ResolveFromZenject(string typeName)
    {
        var targetType = FindType(typeName);
        if (targetType == null) return null;

        var contextType = FindType("Zenject.SceneContext");
        if (contextType == null) return null;

        var contexts = Object.FindObjectsOfType(contextType);
        foreach (var ctx in contexts)
        {
            var containerProp = contextType.GetProperty("Container",
                BindingFlags.Public | BindingFlags.Instance);
            if (containerProp == null) continue;

            var container = containerProp.GetValue(ctx);
            if (container == null) continue;

            var tryResolve = container.GetType().GetMethod("TryResolve",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(System.Type) },
                null);

            if (tryResolve == null) continue;

            var resolved = tryResolve.Invoke(container, new object[] { targetType });
            if (resolved != null) return resolved;
        }

        return null;
    }

    private static System.Type FindType(string name)
    {
        var t = System.Type.GetType(name);
        if (t != null) return t;

        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            t = asm.GetType(name);
            if (t != null) return t;

            foreach (var candidate in asm.GetTypes())
            {
                if (candidate.Name == name)
                    return candidate;
            }
        }
        return null;
    }
}
