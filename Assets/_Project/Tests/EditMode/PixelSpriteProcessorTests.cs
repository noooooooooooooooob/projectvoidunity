using NUnit.Framework;
using ProjectVoid.EditorTools;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class PixelSpriteProcessorTests
    {
        private static Color32[] Canvas(int w, int h) => new Color32[w * h];

        private static void Fill(Color32[] px, int w, int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    px[y * w + x] = c;
                }
            }
        }

        [Test]
        public void CropsScalesAndAnchorsBottomCenter()
        {
            // 100x200 캔버스에 20x100 불투명 막대 → 긴 변 100 이 64 로. 폭은 round(20 * 64/100) = 13.
            Color32[] src = Canvas(100, 200);
            Fill(src, 100, 40, 50, 59, 149, new Color32(200, 30, 30, 255));
            Color32[] outPx = PixelSpriteProcessor.Pixelize(src, 100, 200, 64, 128);
            Assert.AreEqual(64 * 64, outPx.Length);
            int offset = (64 - 13) / 2;
            Assert.AreEqual(255, outPx[0 * 64 + offset].a, "bottom-left of the sprite sits on row 0");
            Assert.AreEqual(0, outPx[0 * 64 + offset - 1].a, "left of the sprite is clear");
            Assert.AreEqual(255, outPx[0 * 64 + offset + 12].a, "13 pixels wide");
            Assert.AreEqual(0, outPx[0 * 64 + offset + 13].a, "no wider than 13");
            Assert.AreEqual(255, outPx[63 * 64 + offset + 6].a, "fills the full height");
        }

        [Test]
        public void BinarizesAlpha()
        {
            Color32[] src = Canvas(64, 64);
            Fill(src, 64, 0, 0, 63, 63, new Color32(10, 10, 10, 200));
            src[10 * 64 + 10] = new Color32(10, 10, 10, 100);
            Color32[] outPx = PixelSpriteProcessor.Pixelize(src, 64, 64, 64, 128);
            Assert.AreEqual(255, outPx[5 * 64 + 5].a, "opaque enough becomes fully opaque");
            Assert.AreEqual(0, outPx[10 * 64 + 10].a, "semi-transparent becomes clear");
        }

        [Test]
        public void EmptySourceGivesClearCanvas()
        {
            Color32[] outPx = PixelSpriteProcessor.Pixelize(Canvas(32, 32), 32, 32, 64, 128);
            foreach (Color32 c in outPx)
            {
                Assert.AreEqual(0, c.a);
            }
        }

        // 스프라이트시트 배경(회색)은 테두리에서 이어진 부분만 지운다. 갑옷처럼 배경과 비슷한 색이 캐릭터 안에 있어도 남아야 한다.
        [Test]
        public void KeyOutBackgroundOnlyRemovesColorConnectedToTheEdges()
        {
            var gray = new Color32(187, 187, 187, 255);
            var dark = new Color32(20, 20, 20, 255);
            Color32[] px = Canvas(8, 8);
            Fill(px, 8, 0, 0, 7, 7, gray);
            Fill(px, 8, 2, 2, 5, 5, dark);
            px[3 * 8 + 3] = new Color32(190, 185, 188, 255);
            PixelSpriteProcessor.KeyOutBackground(px, 8, 8, 30);
            Assert.AreEqual(0, px[0].a, "edge background removed");
            Assert.AreEqual(0, px[1 * 8 + 1].a, "background next to the character removed");
            Assert.AreEqual(255, px[2 * 8 + 2].a, "character kept");
            Assert.AreEqual(255, px[3 * 8 + 3].a, "gray inside the character kept");
        }

        [Test]
        public void SplitGridReadsFramesLeftToRightTopToBottom()
        {
            // 4x4 시트, 2x2 칸. GetPixels32 는 0 행이 아래이므로 위쪽 칸이 큰 y.
            Color32[] sheet = Canvas(4, 4);
            Fill(sheet, 4, 0, 2, 1, 3, new Color32(1, 0, 0, 255));
            Fill(sheet, 4, 2, 2, 3, 3, new Color32(2, 0, 0, 255));
            Fill(sheet, 4, 0, 0, 1, 1, new Color32(3, 0, 0, 255));
            Fill(sheet, 4, 2, 0, 3, 1, new Color32(4, 0, 0, 255));
            var frames = PixelSpriteProcessor.SplitGrid(sheet, 4, 4, 2, 2);
            Assert.AreEqual(4, frames.Count);
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(2 * 2, frames[i].Length);
                Assert.AreEqual(i + 1, frames[i][0].r, $"frame {i}");
            }
        }

        // 프레임마다 따로 잘라 맞추면 무기를 뻗는 프레임에서 캐릭터가 작아져 출렁인다. 모든 프레임이 같은 틀과 배율을 쓴다.
        [Test]
        public void PixelizeFramesUsesOneBoxForAllFrames()
        {
            var red = new Color32(200, 30, 30, 255);
            Color32[] small = Canvas(100, 100);
            Fill(small, 100, 45, 0, 54, 49, red);
            Color32[] tall = Canvas(100, 100);
            Fill(tall, 100, 45, 0, 54, 99, red);
            Color32[] strip = PixelSpriteProcessor.PixelizeFrames(new System.Collections.Generic.List<Color32[]> { small, tall }, 100, 100, 64, 128);
            Assert.AreEqual(64 * 2 * 64, strip.Length, "two 64x64 frames side by side");
            int stripWidth = 128;
            int Height(int frame)
            {
                int h = 0;
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        if (strip[y * stripWidth + frame * 64 + x].a > 0)
                        {
                            h = y + 1;
                            break;
                        }
                    }
                }
                return h;
            }
            Assert.AreEqual(64, Height(1), "the tallest frame fills the height");
            Assert.AreEqual(32, Height(0), 1, "the shorter frame keeps its relative size");
            Assert.Greater(strip[0 * stripWidth + 0 * 64 + 32].a, 0, "feet on the bottom row");
        }

        [Test]
        public void KeyOutRemovesColorsNearTheKey()
        {
            var px = new[] { new Color32(0, 255, 0, 255), new Color32(10, 240, 12, 255), new Color32(200, 30, 30, 255) };
            PixelSpriteProcessor.KeyOut(px, new Color32(0, 255, 0, 255), 60);
            Assert.AreEqual(0, px[0].a, "exact key removed");
            Assert.AreEqual(0, px[1].a, "near key removed");
            Assert.AreEqual(255, px[2].a, "other colors kept");
        }
    }
}
