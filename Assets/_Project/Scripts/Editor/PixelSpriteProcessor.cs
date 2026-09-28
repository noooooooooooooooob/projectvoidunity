using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectVoid.EditorTools
{
    /// <summary>AI 가 만든 큰 이미지를 레퍼런스(format3.png)와 같은 64×64 픽셀아트 스프라이트로 정리한다.</summary>
    public static class PixelSpriteProcessor
    {
        public const int Size = 64;
        public const byte AlphaThreshold = 128;
        public const string RawDir = "Assets/_Project/Art/Units/Raw";
        public const string OutDir = "Assets/_Project/Art/Units";

        /// <summary>
        /// 불투명 영역만 잘라 긴 변이 size 가 되게 최근접 축소하고, 발밑 중앙에 맞춰 size×size 캔버스에 놓는다.
        /// 알파는 0/255 로 이진화한다. 픽셀 순서는 Texture2D.GetPixels32 와 같다 (0 행이 아래).
        /// </summary>
        public static Color32[] Pixelize(Color32[] source, int width, int height, int size, byte alphaThreshold)
        {
            var output = new Color32[size * size];
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (source[y * width + x].a < alphaThreshold)
                    {
                        continue;
                    }
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }
            if (maxX < 0)
            {
                return output;
            }

            int boxWidth = maxX - minX + 1;
            int boxHeight = maxY - minY + 1;
            float scale = Mathf.Max(boxWidth, boxHeight) / (float)size;
            int outWidth = Mathf.Clamp(Mathf.RoundToInt(boxWidth / scale), 1, size);
            int outHeight = Mathf.Clamp(Mathf.RoundToInt(boxHeight / scale), 1, size);
            int offsetX = (size - outWidth) / 2;

            for (int oy = 0; oy < outHeight; oy++)
            {
                for (int ox = 0; ox < outWidth; ox++)
                {
                    int sx = Mathf.Min(minX + Mathf.FloorToInt((ox + 0.5f) * scale), maxX);
                    int sy = Mathf.Min(minY + Mathf.FloorToInt((oy + 0.5f) * scale), maxY);
                    Color32 c = source[sy * width + sx];
                    output[oy * size + offsetX + ox] = c.a >= alphaThreshold ? new Color32(c.r, c.g, c.b, 255) : default;
                }
            }
            return output;
        }

        /// <summary>key 색과의 RGB 차이 합이 tolerance 이하인 픽셀을 투명하게 (초록 배경 대안용).</summary>
        public static void KeyOut(Color32[] pixels, Color32 key, int tolerance)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 c = pixels[i];
                int diff = Mathf.Abs(c.r - key.r) + Mathf.Abs(c.g - key.g) + Mathf.Abs(c.b - key.b);
                if (diff <= tolerance)
                {
                    pixels[i] = default;
                }
            }
        }

        public static void ProcessFile(string rawAssetPath, string outAssetPath, bool keyOutGreen)
        {
            // 임포트 설정(Read/Write)에 기대지 않도록 PNG 를 직접 읽는다.
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(File.ReadAllBytes(rawAssetPath));
            Color32[] pixels = source.GetPixels32();
            if (keyOutGreen)
            {
                KeyOut(pixels, new Color32(0, 255, 0, 255), 120);
            }
            Color32[] result = Pixelize(pixels, source.width, source.height, Size, AlphaThreshold);
            Object.DestroyImmediate(source);

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.SetPixels32(result);
            texture.Apply();
            File.WriteAllBytes(outAssetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(outAssetPath);
            ApplySpriteSettings(outAssetPath);
        }

        public static void ApplySpriteSettings(string assetPath)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = Size;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        [MenuItem("Project Void/Pixelize Raw Unit Sprites")]
        public static void ProcessAllRaw()
        {
            foreach (string path in Directory.GetFiles(RawDir, "*.png"))
            {
                string rawPath = path.Replace('\\', '/');
                ProcessFile(rawPath, $"{OutDir}/{Path.GetFileName(rawPath)}", false);
            }
        }
    }
}
