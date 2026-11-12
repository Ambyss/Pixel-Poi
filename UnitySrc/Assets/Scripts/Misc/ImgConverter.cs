using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ImageConverter
{
    /// <summary>
    /// Widest scan the poi can play back. The firmware loads the whole .bin into one
    /// heap buffer (SD_Card_Service::getImgData), and Bluedroid leaves an ESP32-C3 only
    /// a few tens of KB, so the width is capped: MAX_WIDTH columns = MAX_WIDTH * 36 * 2 bytes.
    /// Every ordinary photo is far below this (16:9 is 64 columns, 3:1 is 108).
    /// </summary>
    public const int MAX_WIDTH = 512;

    /// <summary>
    /// Why the last <see cref="ImageToBinaryFile"/> call returned no data. Read by
    /// PixelPoiUploadBridge (by name, through reflection) so the popup can show a real
    /// reason instead of a bare "Conversion failed".
    /// </summary>
    public static string LastError { get; private set; } = string.Empty;

    private static byte[] Fail(string message)
    {
        LastError = message;
        Debug.LogError("❌ " + message);
        return Array.Empty<byte>();
    }

    private static ushort ConvertRgb888ToRgb565(byte r, byte g, byte b)
    {
        int r5 = r >> 3;
        int g6 = g >> 2;
        int b5 = b >> 3;
        return (ushort)((r5 << 11) | (g6 << 5) | b5);
    }

    public static byte[] ImageToBinaryFile(string inputPath, int height, int delayMs)
    {
        LastError = string.Empty;

        if (!File.Exists(inputPath))
            return Fail($"Файл не знайдено: {inputPath}");

        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!source.LoadImage(File.ReadAllBytes(inputPath)))
        {
            Object.Destroy(source);
            return Fail($"Не вдалося декодувати зображення: {inputPath}");
        }

        // The LED strip is the image height; the width simply follows the aspect ratio,
        // so a rectangular image just becomes a longer or shorter scan.
        int width = Mathf.RoundToInt((float)height / source.height * source.width);

        if (width < 1)
        {
            int sourceWidth = source.width, sourceHeight = source.height;
            Object.Destroy(source);
            return Fail($"Зображення занадто вузьке: {sourceWidth}x{sourceHeight} дає " +
                        $"менше одного стовпця при висоті {height} px.");
        }

        if (width > MAX_WIDTH)
        {
            Object.Destroy(source);
            return Fail($"Зображення занадто широке: {width} стовпців при висоті {height} px, " +
                        $"максимум {MAX_WIDTH}. Обріжте до співвідношення не ширше " +
                        $"{MAX_WIDTH / height}:1.");
        }

        Color32[] src = source.GetPixels32();
        int sw = source.width, sh = source.height;
        Object.Destroy(source);
        Color32[] scaled = ResizeBilinear(src, sw, sh, width, height);

        var binData = new List<byte>(width * height * 2);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int unityY = height - 1 - y;
                Color32 p = scaled[unityY * width + x];
                ushort color565 = ConvertRgb888ToRgb565(p.r, p.g, p.b);
                binData.Add((byte)(color565 & 0xFF));
                binData.Add((byte)(color565 >> 8));
            }
        }

        Debug.Log($"✅ Успішно конвертовано! {width}x{height}, розмір даних: {binData.Count} байт.");
        return binData.ToArray();
    }

    private static Color32[] ResizeBilinear(Color32[] src, int sw, int sh, int dw, int dh)
    {
        var dst = new Color32[dw * dh];
        for (int y = 0; y < dh; y++)
        {
            float gy = dh > 1 ? (float)y / (dh - 1) * (sh - 1) : 0f;
            int y0 = (int)gy;
            int y1 = Mathf.Min(y0 + 1, sh - 1);
            float fy = gy - y0;

            for (int x = 0; x < dw; x++)
            {
                float gx = dw > 1 ? (float)x / (dw - 1) * (sw - 1) : 0f;
                int x0 = (int)gx;
                int x1 = Mathf.Min(x0 + 1, sw - 1);
                float fx = gx - x0;

                Color32 c00 = src[y0 * sw + x0];
                Color32 c10 = src[y0 * sw + x1];
                Color32 c01 = src[y1 * sw + x0];
                Color32 c11 = src[y1 * sw + x1];

                dst[y * dw + x] = new Color32(
                    BilerpByte(c00.r, c10.r, c01.r, c11.r, fx, fy),
                    BilerpByte(c00.g, c10.g, c01.g, c11.g, fx, fy),
                    BilerpByte(c00.b, c10.b, c01.b, c11.b, fx, fy),
                    255);
            }
        }

        return dst;
    }

    private static byte BilerpByte(byte c00, byte c10, byte c01, byte c11, float fx, float fy)
    {
        float top = c00 + (c10 - c00) * fx;
        float bot = c01 + (c11 - c01) * fx;
        return (byte)(top + (bot - top) * fy + 0.5f);
    }
}