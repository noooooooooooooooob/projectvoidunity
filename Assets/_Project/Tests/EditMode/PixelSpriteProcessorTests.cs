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
