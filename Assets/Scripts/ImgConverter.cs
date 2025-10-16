using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Color = System.Drawing.Color;
using Graphics = System.Drawing.Graphics; // For .Select and .Chunk in modern .NET

/// <summary>
/// Утиліта для конвертації зображень PNG/BMP/JPG в масив байтів у форматі RGB565 
/// для використання з мікроконтролерами Arduino (наприклад, для LED-матриць чи TFT-дисплеїв).
/// </summary>
public static class ImageConverter
{
    /// <summary>
    /// Конвертує 24-бітний колір RGB (8 біт на канал) у 16-бітний формат RGB565.
    /// 
    /// R: 8-біт -> 5-біт (зсув вправо 3)
    /// G: 8-біт -> 6-біт (зсув вправо 2)
    /// B: 8-біт -> 5-біт (зсув вправо 3)
    /// 
    /// Формат: [R4 R3 R2 R1 R0 G5 G4 G3] [G2 G1 G0 B4 B3 B2 B1 B0]
    /// </summary>
    /// <param name="r">Червоний компонент (0-255).</param>
    /// <param name="g">Зелений компонент (0-255).</param>
    /// <param name="b">Синій компонент (0-255).</param>
    /// <returns>16-бітне значення RGB565 (ushort).</returns>
    private static ushort ConvertRgb888ToRgb565(byte r, byte g, byte b)
    {
        // Усічення до 5, 6, 5 біт відповідно
        int r5 = r >> 3;
        int g6 = g >> 2;
        int b5 = b >> 3;

        // Комбінування бітів в єдине 16-бітне значення
        // RRRRRGGGGGGBBBBB
        ushort rgb565 = (ushort)((r5 << 11) | (g6 << 5) | b5);

        return rgb565;
    }

    /// <summary>
    /// Конвертує зображення у масив байтів у форматі RGB565 та записує його 
    /// у файл заголовка C-стилю (.h) для Arduino.
    /// </summary>
    /// <param name="inputPath">Шлях до вхідного файлу зображення.</param>
    /// <param name="outputPath">Шлях до вихідного файлу заголовка (.h).</param>
    /// <param name="width">Бажана ширина зображення.</param>
    /// <param name="height">Бажана висота зображення.</param>
    /// <param name="arrayName">Назва масиву в C-заголовку.</param>
    public static void ImageToRgb565Array(string inputPath, string outputPath, int height, string arrayName = "myColorBitmap")
    {
        var originalImage = new Bitmap(inputPath);
        var width = (int)(((float)height / originalImage.Height) * originalImage.Width);
        try
        {
            Debug.Log($"🔍 Обробка зображення: '{inputPath}'...");
            // 1. Відкриття та обробка зображення
            using (var resizedImage = new Bitmap(originalImage, new Size(width, height))) // Зміна розміру
            {
                var rgb565Bytes = new List<byte>();

                // Прохід по пікселях зображення
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        Color pixel = resizedImage.GetPixel(x, y);

                        // Конвертація 24-бітного кольору в 16-бітний RGB565
                        ushort color565 = ConvertRgb888ToRgb565(pixel.R, pixel.G, pixel.B);

                        // Виділення молодшого (LSB) і старшого (MSB) байтів 
                        // (Little-Endian формат для зручного читання на Arduino)
                        // Arduino зазвичай використовує Little-Endian для простих типів, 
                        // тому ми записуємо LSB, а потім MSB.
                        byte lsb = (byte)(color565 & 0xFF);         // 0x00FF (молодші 8 біт)
                        byte msb = (byte)(color565 >> 8);           // 0xFF00 (старші 8 біт)

                        // Зберігання в масиві як пара LSB, MSB
                        rgb565Bytes.Add(lsb);
                        rgb565Bytes.Add(msb);
                    }
                }

                // 2. Форматування у файл заголовка C
                using (var sw = new StreamWriter(outputPath, false, Encoding.ASCII))
                {
                    sw.WriteLine($"// Image: {Path.GetFileName(inputPath)}");
                    sw.WriteLine($"// Dimensions: {width}x{height} pixels, RGB565 (2 bytes/pixel)");
                    sw.WriteLine("#include <avr/pgmspace.h>");
                    sw.WriteLine();
                    sw.WriteLine($"const unsigned char {arrayName}[] PROGMEM = {{");

                    // Форматування байтів у шістнадцятковий рядок (0xXX)
                    var hexBytes = rgb565Bytes.Select(b => $"0x{b:X2}").ToArray();

                    // Запис 16 байтів на рядок для читабельності
                    // Примітка: C# .NET Core 3.0+ має метод .Chunk(), але для сумісності 
                    // з ширшим діапазоном версій можна використати звичайний цикл.
                    for (int i = 0; i < hexBytes.Length; i += 16)
                    {
                        // Вибираємо до 16 елементів
                        string[] lineChunk = hexBytes.Skip(i).Take(16).ToArray();
                        string line = string.Join(", ", lineChunk);
                        sw.WriteLine($"  {line},");
                    }

                    sw.WriteLine("};");
                    sw.WriteLine();
                    sw.WriteLine($"#define BITMAP_WIDTH  {width}");
                    sw.WriteLine($"#define BITMAP_HEIGHT {height}");
                }

                Debug.Log($"✅ Успішно конвертовано '{inputPath}' у '{outputPath}'");
                Debug.Log($"   Розмір масиву: {rgb565Bytes.Count} байт.");
            }
        }
        catch (FileNotFoundException)
        {
            Debug.Log($"❌ Помилка: Файл не знайдено за шляхом {inputPath}");
        }
        catch (Exception e)
        {
            Debug.Log($"❌ Виникла помилка: {e.Message}");
            Debug.Log(e.StackTrace);
        }
    }
}

// =========================================================================
// --- Приклад Використання ---
// =========================================================================

public class ImgConverter : MonoBehaviour
{
    [SerializeField] private int _pixelAmount = 36;
    
    private const string INPUT_FILENAME = "my_color_image.png"; 
    private const string OUTPUT_FILENAME = "color_bitmap.h";
    
    public void Start()
    {
        // Формування повних шляхів у папці Assets
        string assetsPath = Application.dataPath;
        string input_path = Path.Combine(assetsPath, INPUT_FILENAME);
        string output_path = Path.Combine(assetsPath, OUTPUT_FILENAME);

        Debug.Log($"Input Path: {input_path}");
        Debug.Log($"Output Path: {output_path}");

        // Перевірка, чи існує файл для прикладу
        if (!File.Exists(input_path))
        {
            Debug.Log($"*** СТВОРЕННЯ ФАЙЛУ-ЗАГЛУШКИ ДЛЯ ПРИКЛАДУ ***");
            Debug.Log($"*** Файл '{INPUT_FILENAME}' створюється в Assets/. Будь ласка, замініть його на ваше зображення. ***");
            CreateDummyImage(input_path, _pixelAmount, _pixelAmount);
        }
        
        // Виклик конвертера
        ImageConverter.ImageToRgb565Array(input_path, output_path, _pixelAmount);

        // Примітка: Оскільки цей код запускається в редакторі, 
        // можливо, знадобиться оновити базу даних AssetDatabase, 
        // щоб Unity одразу побачив створений .h файл.
        // Це вимагає використання 'using UnityEditor;' та 'UnityEditor.AssetDatabase.Refresh();', 
        // але це має бути в #if UNITY_EDITOR блоці і не є частиною основної логіки.
    }
    
    // Допоміжна функція для створення тестового зображення (потрібно System.Drawing)
    private static void CreateDummyImage(string path, int w, int h)
    {
        using (var bmp = new Bitmap(w, h))
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Black);
            // Червоний-Зелений-Синій
            g.FillRectangle(Brushes.Red, 0, 0, w / 3, h);
            g.FillRectangle(Brushes.Green, w / 3, 0, w / 3, h);
            g.FillRectangle(Brushes.Blue, 2 * w / 3, 0, w / 3, h);
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}