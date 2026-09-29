using NUnit.Framework;
using ProjectVoid.View;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class FlatBackdropTests
    {
        // 배경의 벽·바닥 경계선을 보드 뒷줄 뒤에 맞춘다: 뒤 행 유닛이 벽에 붙어 보이지 않게.
        [Test]
        public void BackdropPutsItsFloorLineWhereAsked()
        {
            float center = BattleRoot.BackdropCenterY(floorLineY: 2f, spriteHeight: 10f, floorLineFromBottom: 0.6f);
            Assert.AreEqual(2f, center - 5f + 0.6f * 10f, 1e-4f);
        }

        // 캐릭터와 같은 픽셀 크기를 지키려고 원래 크기로 두되, 화면을 못 덮으면 그때만 키운다.
        [Test]
        public void BackdropKeepsPixelSizeUnlessItCannotCover()
        {
            Assert.AreEqual(1f, BattleRoot.BackdropScale(new Vector2(14f, 14f), new Vector2(13.6f, 7.7f)), 1e-4f, "native pixel size");
            Assert.AreEqual(2f, BattleRoot.BackdropScale(new Vector2(7f, 7f), new Vector2(14f, 7f)), 1e-4f, "grows only to cover");
        }
    }
}
