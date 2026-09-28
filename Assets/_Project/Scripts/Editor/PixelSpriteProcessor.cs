using System.Collections.Generic;
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
        public const string AnimRawDir = "Assets/_Project/Art/Units/Anim/Raw";
        public const string AnimOutDir = "Assets/_Project/Art/Units/Anim";
        // AI 스프라이트시트는 4×4 칸 16프레임으로 나온다.
        public const int SheetGrid = 4;
        public const int BackgroundTolerance = 48;
        public static readonly string[] Motions = { "idle", "attack", "hit" };

        /// <summary>
        /// 불투명 영역만 잘라 긴 변이 size 가 되게 최근접 축소하고, 발밑 중앙에 맞춰 size×size 캔버스에 놓는다.
        /// 알파는 0/255 로 이진화한다. 픽셀 순서는 Texture2D.GetPixels32 와 같다 (0 행이 아래).
        /// </summary>
        public static Color32[] Pixelize(Color32[] source, int width, int height, int size, byte alphaThreshold)
        {
            var output = new Color32[size * size];
            var box = new BoxAccumulator();
            box.Add(source, width, height, alphaThreshold);
            if (!box.IsEmpty)
            {
                Blit(source, width, box, size, alphaThreshold, output, size, 0);
            }
            return output;
        }

        /// <summary>
        /// 여러 프레임을 같은 틀(모든 프레임의 불투명 영역 합)과 같은 배율로 줄여 size×size 칸을 가로로 이어 붙인다.
        /// 프레임마다 따로 맞추면 무기를 뻗는 프레임에서 캐릭터 크기가 출렁인다.
        /// </summary>
        public static Color32[] PixelizeFrames(List<Color32[]> frames, int width, int height, int size, byte alphaThreshold)
        {
            int stripWidth = size * frames.Count;
            var strip = new Color32[stripWidth * size];
            var box = new BoxAccumulator();
            foreach (Color32[] frame in frames)
            {
                box.Add(frame, width, height, alphaThreshold);
            }
            if (box.IsEmpty)
            {
                return strip;
            }
            for (int i = 0; i < frames.Count; i++)
            {
                Blit(frames[i], width, box, size, alphaThreshold, strip, stripWidth, i * size);
            }
            return strip;
        }

        /// <summary>cols×rows 격자 시트를 왼쪽 위부터 읽는 순서로 자른다.</summary>
        public static List<Color32[]> SplitGrid(Color32[] sheet, int width, int height, int cols, int rows)
        {
            int frameWidth = width / cols;
            int frameHeight = height / rows;
            var frames = new List<Color32[]>();
            for (int row = 0; row < rows; row++)
            {
                // 픽셀 0 행이 아래라서 위쪽 칸일수록 y 가 크다.
                int y0 = (rows - 1 - row) * frameHeight;
                for (int col = 0; col < cols; col++)
                {
                    var frame = new Color32[frameWidth * frameHeight];
                    for (int y = 0; y < frameHeight; y++)
                    {
                        System.Array.Copy(sheet, (y0 + y) * width + col * frameWidth, frame, y * frameWidth, frameWidth);
                    }
                    frames.Add(frame);
                }
            }
            return frames;
        }

        /// <summary>
        /// 모서리 색을 배경으로 보고, 테두리에서 이어진 비슷한 색만 투명하게 한다.
        /// 색만 보고 지우면 회색 갑옷처럼 배경과 비슷한 캐릭터 안쪽 색에 구멍이 난다.
        /// </summary>
        public static void KeyOutBackground(Color32[] pixels, int width, int height, int tolerance)
        {
            Color32 key = pixels[0];
            var visited = new bool[pixels.Length];
            var stack = new Stack<int>();
            for (int x = 0; x < width; x++)
            {
                stack.Push(x);
                stack.Push((height - 1) * width + x);
            }
            for (int y = 0; y < height; y++)
            {
                stack.Push(y * width);
                stack.Push(y * width + width - 1);
            }
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                if (visited[i])
                {
                    continue;
                }
                visited[i] = true;
                Color32 c = pixels[i];
                if (Mathf.Abs(c.r - key.r) + Mathf.Abs(c.g - key.g) + Mathf.Abs(c.b - key.b) > tolerance)
                {
                    continue;
                }
                pixels[i] = default;
                int x = i % width;
                int y = i / width;
                if (x > 0) stack.Push(i - 1);
                if (x < width - 1) stack.Push(i + 1);
                if (y > 0) stack.Push(i - width);
                if (y < height - 1) stack.Push(i + width);
            }
        }

        private sealed class BoxAccumulator
        {
            public int MinX = int.MaxValue, MinY = int.MaxValue, MaxX = -1, MaxY = -1;
            public bool IsEmpty => MaxX < 0;

            public void Add(Color32[] source, int width, int height, byte alphaThreshold)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (source[y * width + x].a < alphaThreshold)
                        {
                            continue;
                        }
                        MinX = Mathf.Min(MinX, x);
                        MinY = Mathf.Min(MinY, y);
                        MaxX = Mathf.Max(MaxX, x);
                        MaxY = Mathf.Max(MaxY, y);
                    }
                }
            }
        }

        // box 영역을 긴 변이 size 가 되게 최근접 축소해 dest 의 offsetX 칸 발밑 중앙에 놓는다.
        private static void Blit(Color32[] source, int width, BoxAccumulator box, int size, byte alphaThreshold,
            Color32[] dest, int destWidth, int offsetX)
        {
            int boxWidth = box.MaxX - box.MinX + 1;
            int boxHeight = box.MaxY - box.MinY + 1;
            float scale = Mathf.Max(boxWidth, boxHeight) / (float)size;
            int outWidth = Mathf.Clamp(Mathf.RoundToInt(boxWidth / scale), 1, size);
            int outHeight = Mathf.Clamp(Mathf.RoundToInt(boxHeight / scale), 1, size);
            int left = offsetX + (size - outWidth) / 2;

            for (int oy = 0; oy < outHeight; oy++)
            {
                for (int ox = 0; ox < outWidth; ox++)
                {
                    int sx = Mathf.Min(box.MinX + Mathf.FloorToInt((ox + 0.5f) * scale), box.MaxX);
                    int sy = Mathf.Min(box.MinY + Mathf.FloorToInt((oy + 0.5f) * scale), box.MaxY);
                    Color32 c = source[sy * width + sx];
                    dest[oy * destWidth + left + ox] = c.a >= alphaThreshold ? new Color32(c.r, c.g, c.b, 255) : default;
                }
            }
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

        /// <summary>
        /// Anim/Raw/{id}_{motion}.png 시트를 64×64 프레임 띠로 만들어 Anim/{id}_{motion}.png 에 쓰고 유닛 데이터에 연결한다.
        /// 한 유닛의 모든 동작이 같은 틀을 써야 동작이 바뀔 때 크기가 튀지 않는다.
        /// </summary>
        [MenuItem("Project Void/Process Unit Animations")]
        public static void ProcessAllAnimations()
        {
            var ids = new HashSet<string>();
            foreach (string path in Directory.GetFiles(AnimRawDir, "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                ids.Add(name.Substring(0, name.LastIndexOf('_')));
            }
            foreach (string id in ids)
            {
                ProcessUnitAnimations(id);
            }
            AssetDatabase.SaveAssets();
        }

        public static void ProcessUnitAnimations(string id)
        {
            var allFrames = new List<Color32[]>();
            var motionsFound = new List<string>();
            int frameWidth = 0, frameHeight = 0;
            foreach (string motion in Motions)
            {
                string rawPath = $"{AnimRawDir}/{id}_{motion}.png";
                if (!File.Exists(rawPath))
                {
                    continue;
                }
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                source.LoadImage(File.ReadAllBytes(rawPath));
                frameWidth = source.width / SheetGrid;
                frameHeight = source.height / SheetGrid;
                foreach (Color32[] frame in SplitGrid(source.GetPixels32(), source.width, source.height, SheetGrid, SheetGrid))
                {
                    KeyOutBackground(frame, frameWidth, frameHeight, BackgroundTolerance);
                    allFrames.Add(frame);
                }
                Object.DestroyImmediate(source);
                motionsFound.Add(motion);
            }
            if (motionsFound.Count == 0)
            {
                return;
            }

            Color32[] strip = PixelizeFrames(allFrames, frameWidth, frameHeight, Size, AlphaThreshold);
            int perMotion = SheetGrid * SheetGrid;
            int stripWidth = Size * allFrames.Count;
            var data = AssetDatabase.LoadAssetAtPath<ProjectVoid.Combat.UnitData>($"{DataImporter.UnitsDir}/{id}.asset");
            for (int m = 0; m < motionsFound.Count; m++)
            {
                int width = Size * perMotion;
                var pixels = new Color32[width * Size];
                for (int y = 0; y < Size; y++)
                {
                    System.Array.Copy(strip, y * stripWidth + m * width, pixels, y * width, width);
                }
                string outPath = $"{AnimOutDir}/{id}_{motionsFound[m]}.png";
                var texture = new Texture2D(width, Size, TextureFormat.RGBA32, false);
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(outPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(outPath);
                ApplyStripSettings(outPath);

                if (data != null)
                {
                    var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
                    switch (motionsFound[m])
                    {
                        case "idle": data.idleSheet = sheet; break;
                        case "attack": data.attackSheet = sheet; break;
                        case "hit": data.hitSheet = sheet; break;
                    }
                }
            }
            if (data != null)
            {
                EditorUtility.SetDirty(data);
            }
        }

        private static void ApplyStripSettings(string assetPath)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
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
