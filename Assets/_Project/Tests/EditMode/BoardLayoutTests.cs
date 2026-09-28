using NUnit.Framework;
using ProjectVoid.Combat;
using ProjectVoid.View;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class BoardLayoutTests
    {
        private const float Eps = 1e-4f;

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
