using NUnit.Framework;
using ProjectVoid.Combat;
using ProjectVoid.View;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class BoardLayoutTests
    {
        private const float Eps = 1e-4f;

        // 2D: 배경 바닥처럼 한 점 원근을 따른다. 뒤 행은 화면 위로, 보드 가운데 쪽으로 모이고, 앞 행 유닛이 그 위에 그려지도록 더 깊이 둔다.
        [Test]
        public void FlatProjectionFollowsTheFloorPerspective()
        {
            var layout = new BoardLayout(new Vector2Int(3, 3), new Vector2Int(2, 2));
            Vector3 far = layout.WorldCell(Team.Ally, new Vector2Int(2, 0), BoardProjection.Flat2D);
            Vector3 near = layout.WorldCell(Team.Ally, new Vector2Int(2, 2), BoardProjection.Flat2D);
            float center = layout.Center().x;
            Assert.Less(Mathf.Abs(far.x - center), Mathf.Abs(near.x - center), "far cells draw in toward the vanishing point");
            Assert.Less(layout.FlatScaleAt(layout.CellPosition(Team.Ally, new Vector2Int(0, 0)).z), 1f, "far rows are smaller");
            Assert.AreEqual(1f, layout.FlatScaleAt(-layout.Depth() / 2f), Eps, "the front edge keeps full size");
            Vector3 mid = layout.WorldCell(Team.Ally, new Vector2Int(2, 1), BoardProjection.Flat2D);
            Assert.Less(far.y - mid.y, mid.y - near.y, "rows get closer together further back");
            Assert.Greater(far.y, near.y, "far row is higher on screen");
            Assert.Greater(far.z, near.z, "far row is further from the camera");
            Assert.AreEqual(layout.CellPosition(Team.Enemy, new Vector2Int(1, 0)),
                layout.WorldCell(Team.Enemy, new Vector2Int(1, 0), BoardProjection.Perspective3D), "3D unchanged");
        }

        [Test]
        public void FlatOrthoSizeFitsTheBoard()
        {
            float size = BoardLayout.FlatOrthoSize(10f, 4f, 16f / 9f, 1.2f);
            Assert.GreaterOrEqual(size * 2f, 4f * 1.2f - Eps, "height fits");
            Assert.GreaterOrEqual(size * 2f * 16f / 9f, 10f * 1.2f - Eps, "width fits");
            Assert.Greater(BoardLayout.FlatOrthoSize(10f, 4f, 4f / 3f, 1.2f), size, "narrow screens need more room");
        }

        [Test]
        public void SidesAreMirrored()
        {
            var layout = new BoardLayout(new Vector2Int(3, 3), new Vector2Int(3, 3));
            Vector3 ally = layout.CellPosition(Team.Ally, new Vector2Int(0, 1));
            Vector3 enemy = layout.CellPosition(Team.Enemy, new Vector2Int(0, 1));
            Assert.Less(ally.x, 0f, "ally is on the left");
            Assert.Greater(enemy.x, 0f, "enemy is on the right");
            Assert.AreEqual(enemy.x, -ally.x, Eps, "same column mirrors");
            Assert.AreEqual(1.3f, enemy.x, Eps, "front column centre");
            Assert.AreEqual(0f, ally.y, "tile top is the floor");
        }

        [Test]
        public void ColZeroIsNearestTheGap()
        {
            var layout = new BoardLayout(new Vector2Int(3, 3), new Vector2Int(3, 3));
            Vector3 front = layout.CellPosition(Team.Ally, new Vector2Int(0, 1));
            Vector3 back = layout.CellPosition(Team.Ally, new Vector2Int(2, 1));
            Assert.Less(Mathf.Abs(front.x), Mathf.Abs(back.x), "col 0 is closer to the centre than col 2");
            Assert.AreEqual(2.2f, Mathf.Abs(back.x) - Mathf.Abs(front.x), Eps, "columns are one pitch apart");
        }

        // Godot 은 far.z < near.z 였다. 유니티는 카메라가 -Z 쪽이라 부호가 반대다.
        [Test]
        public void RowsGoIntoTheScreen()
        {
            var layout = new BoardLayout(new Vector2Int(3, 3), new Vector2Int(3, 3));
            Vector3 far = layout.CellPosition(Team.Ally, new Vector2Int(0, 0));
            Vector3 near = layout.CellPosition(Team.Ally, new Vector2Int(0, 2));
            Assert.Greater(far.z, near.z, "row 0 is farther from the camera");
            Assert.AreEqual(0f, layout.CellPosition(Team.Ally, new Vector2Int(0, 1)).z, Eps, "middle row is centred");
        }

        [Test]
        public void OddEvenRowsSitHalfACellApart()
        {
            var layout = new BoardLayout(new Vector2Int(3, 3), new Vector2Int(2, 2));
            Assert.AreEqual(0.55f, layout.CellPosition(Team.Enemy, new Vector2Int(0, 0)).z, Eps, "2-row side row 0");
            Assert.AreEqual(-0.55f, layout.CellPosition(Team.Enemy, new Vector2Int(0, 1)).z, Eps, "2-row side row 1");
        }

        [Test]
        public void Bounds()
        {
            var layout = new BoardLayout(new Vector2Int(3, 3), new Vector2Int(2, 2));
            Assert.AreEqual(-4.05f, layout.MinX(), Eps, "left edge");
            Assert.AreEqual(2.95f, layout.MaxX(), Eps, "right edge");
            Assert.AreEqual(7.0f, layout.Width(), Eps, "width");
            Assert.AreEqual(3.3f, layout.Depth(), Eps, "depth uses the taller side");
            Assert.AreEqual(-0.55f, layout.Center().x, Eps, "centre x");
        }

        [Test]
        public void CameraDistance()
        {
            Assert.AreEqual(1f, BoardLayout.CameraDistance(2f, 0f, 90f, 1f, 1f), Eps, "fits width exactly");
            float narrow = BoardLayout.CameraDistance(4f, 2f, 40f, 16f / 9f, 1.2f);
            float wide = BoardLayout.CameraDistance(8f, 2f, 40f, 16f / 9f, 1.2f);
            Assert.Greater(wide, narrow, "wider board needs a farther camera");
            float deep = BoardLayout.CameraDistance(1f, 6f, 40f, 16f / 9f, 1.2f);
            Assert.Greater(deep, BoardLayout.CameraDistance(1f, 0f, 40f, 16f / 9f, 1.2f), "a deep narrow board is framed by depth");
        }

        [Test]
        public void CameraDistanceGrowsForNarrowAspect()
        {
            float widescreen = BoardLayout.CameraDistance(7f, 3.3f, 40f, 16f / 9f, 1.8f);
            float portrait = BoardLayout.CameraDistance(7f, 3.3f, 40f, 9f / 16f, 1.8f);
            Assert.Greater(portrait, widescreen, "a narrow window pulls the camera back to keep the board in view");
        }
    }
}
