using System.Collections.Generic;
using NUnit.Framework;
using ProjectVoid.View;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class AimArrowTests
    {
        [Test]
        public void CurveRunsFromCardToCursor()
        {
            var from = new Vector2(100f, 50f);
            var to = new Vector2(400f, 300f);
            List<Vector2> points = AimArrow.CurvePoints(from, to, 20, 120f);
            Assert.AreEqual(21, points.Count, "segments + 1 points");
            Assert.AreEqual(from, points[0], "starts at the card");
            Assert.AreEqual(to, points[20], "ends at the cursor");
        }

        // UI 좌표는 y 가 위로 커진다 (Godot 은 아래로). 곡선은 두 끝보다 위로 솟아야 한다.
        [Test]
        public void CurveArcsAboveBothEnds()
        {
            List<Vector2> points = AimArrow.CurvePoints(new Vector2(0f, 0f), new Vector2(200f, 100f), 20, 120f);
            Assert.Greater(points[10].y, 100f, "middle of the curve rises above the higher end");
        }
    }
}
