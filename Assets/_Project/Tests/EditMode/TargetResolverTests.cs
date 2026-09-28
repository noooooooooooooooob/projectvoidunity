using System.Collections.Generic;
using NUnit.Framework;
using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class TargetResolverTests
    {
        [TearDown]
        public void TearDown() => Make.Cleanup();

        // 원본 _unit(id, team, cell, hp=10). UnitData 는 C# 에서 추상이라 편에 맞는 구체 타입을 쓴다.
        private static Unit NewUnit(int id, Team team, Vector2Int cell, int hp = 10)
        {
            UnitData data = team == Team.Ally ? Make.Ally($"u{id}", maxHp: hp) : (UnitData)Make.Enemy($"u{id}", maxHp: hp);
            return new Unit(id, data, team, cell);
        }

        private static TargetResolver Resolver(int allyCols, int allyRows, int enemyCols, int enemyRows)
            => new TargetResolver(new Vector2Int(allyCols, allyRows), new Vector2Int(enemyCols, enemyRows));

        private static Vector2Int C(int x, int y) => new Vector2Int(x, y);

        [Test]
        public void ColDistance()
        {
            TargetResolver resolver = Resolver(3, 3, 3, 3);
            Unit a = NewUnit(1, Team.Ally, C(0, 1));
            Unit e = NewUnit(2, Team.Enemy, C(0, 1));
            Assert.AreEqual(1, resolver.Reach(a, e), "front row vs front row is 1");
            Unit back = NewUnit(3, Team.Enemy, C(2, 1));
            Assert.AreEqual(3, resolver.Reach(a, back), "front vs enemy col2 is 3");
        }

        [Test]
        public void RowDistanceSameSize()
        {
            TargetResolver resolver = Resolver(3, 3, 3, 3);
            Unit a = NewUnit(1, Team.Ally, C(0, 0));
            Unit e = NewUnit(2, Team.Enemy, C(0, 2));
            Assert.AreEqual(3, resolver.Reach(a, e), "two rows apart adds 2");
        }

        [Test]
        public void RowDistanceDifferentSize()
        {
            TargetResolver resolver = Resolver(3, 3, 3, 5);
            Unit a = NewUnit(1, Team.Ally, C(0, 1));
            Unit e = NewUnit(2, Team.Enemy, C(0, 2));
            Assert.AreEqual(1, resolver.Reach(a, e), "3-row centre faces 5-row centre");
            Unit off = NewUnit(3, Team.Enemy, C(0, 3));
            Assert.AreEqual(2, resolver.Reach(a, off), "one row off centre adds 1");
        }

        [Test]
        public void RowDistanceOddEvenRoundsDown()
        {
            TargetResolver resolver = Resolver(3, 3, 3, 4);
            Unit a = NewUnit(1, Team.Ally, C(0, 1));
            Unit e = NewUnit(2, Team.Enemy, C(0, 1));
            Assert.AreEqual(1, resolver.Reach(a, e), "half-cell offset counts as facing");
            Unit far = NewUnit(3, Team.Enemy, C(0, 3));
            Assert.AreEqual(2, resolver.Reach(a, far), "one and a half rows off rounds down to 1");
        }

        [Test]
        public void MeleeBlockedByFront()
        {
            TargetResolver resolver = Resolver(3, 3, 3, 3);
            Unit a = NewUnit(1, Team.Ally, C(0, 1));
            Unit front = NewUnit(2, Team.Enemy, C(0, 1));
            Unit back = NewUnit(3, Team.Enemy, C(1, 1));
            var all = new List<Unit> { a, front, back };
            Assert.IsTrue(resolver.IsValidTarget(a, front, AttackType.Melee, 1, all), "front is reachable");
            Assert.IsFalse(resolver.IsValidTarget(a, back, AttackType.Melee, 4, all), "back is blocked");
        }

        [Test]
        public void MeleeUnblockedAfterFrontDies()
        {
            TargetResolver resolver = Resolver(3, 3, 3, 3);
            Unit a = NewUnit(1, Team.Ally, C(0, 1));
            Unit front = NewUnit(2, Team.Enemy, C(0, 1));
            Unit back = NewUnit(3, Team.Enemy, C(1, 1));
            var all = new List<Unit> { a, front, back };
            front.TakeDamage(999);
            Assert.IsTrue(resolver.IsValidTarget(a, back, AttackType.Melee, 4, all), "dead front no longer blocks");
        }

        [Test]
        public void RangedIgnoresBlocking()
        {
            TargetResolver resolver = Resolver(3, 3, 3, 3);
            Unit a = NewUnit(1, Team.Ally, C(0, 1));
            Unit front = NewUnit(2, Team.Enemy, C(0, 1));
            Unit back = NewUnit(3, Team.Enemy, C(1, 1));
            var all = new List<Unit> { a, front, back };
            Assert.IsTrue(resolver.IsValidTarget(a, back, AttackType.Ranged, 3, all), "ranged reaches behind the front");
        }

        [Test]
        public void OutOfRangeRejected()
        {
            TargetResolver resolver = Resolver(3, 3, 3, 3);
            Unit a = NewUnit(1, Team.Ally, C(0, 0));
            Unit far = NewUnit(2, Team.Enemy, C(2, 2));
            var all = new List<Unit> { a, far };
            Assert.IsFalse(resolver.IsValidTarget(a, far, AttackType.Ranged, 3, all), "reach 5 exceeds range 3");
        }

        [Test]
        public void ExpandPierce()
        {
            TargetResolver resolver = Resolver(3, 3, 3, 3);
            Unit primary = NewUnit(2, Team.Enemy, C(0, 1));
            Unit sameRow = NewUnit(3, Team.Enemy, C(2, 1));
            Unit otherRow = NewUnit(4, Team.Enemy, C(0, 2));
            Unit ally = NewUnit(1, Team.Ally, C(0, 1));
            var all = new List<Unit> { ally, primary, sameRow, otherRow };
            List<Unit> hit = resolver.ExpandShape(primary, Shape.Pierce, all);
            Assert.AreEqual(2, hit.Count, "pierce hits the whole row");
            Assert.IsFalse(hit.Contains(otherRow), "pierce excludes other rows");
            Assert.IsFalse(hit.Contains(ally), "pierce never hits the attacker camp");
        }

        [Test]
        public void ExpandSweep()
        {
            TargetResolver resolver = Resolver(3, 3, 3, 3);
            Unit primary = NewUnit(2, Team.Enemy, C(0, 0));
            Unit sameCol = NewUnit(3, Team.Enemy, C(0, 2));
            Unit otherCol = NewUnit(4, Team.Enemy, C(1, 0));
            var all = new List<Unit> { primary, sameCol, otherCol };
            List<Unit> hit = resolver.ExpandShape(primary, Shape.Sweep, all);
            Assert.AreEqual(2, hit.Count, "sweep hits the whole column");
            Assert.IsFalse(hit.Contains(otherCol), "sweep excludes other columns");
        }

        [Test]
        public void ExpandArea()
        {
            TargetResolver resolver = Resolver(3, 3, 3, 3);
            Unit primary = NewUnit(2, Team.Enemy, C(0, 0));
            Unit right = NewUnit(3, Team.Enemy, C(1, 0));
            Unit below = NewUnit(4, Team.Enemy, C(0, 1));
            Unit diagonal = NewUnit(5, Team.Enemy, C(1, 1));
            Unit outside = NewUnit(6, Team.Enemy, C(2, 0));
            Unit ally = NewUnit(1, Team.Ally, C(1, 0));
            var all = new List<Unit> { ally, primary, right, below, diagonal, outside };
            List<Unit> hit = resolver.ExpandShape(primary, Shape.Area, all);
            Assert.AreEqual(4, hit.Count, "area hits the whole 2x2 block");
            Assert.IsFalse(hit.Contains(outside), "area excludes cells outside the block");
            Assert.IsFalse(hit.Contains(ally), "area never hits the attacker camp");
        }

        [Test]
        public void ExpandLine()
        {
            TargetResolver resolver = Resolver(3, 3, 3, 3);
            Unit primary = NewUnit(2, Team.Enemy, C(1, 1));
            Unit front = NewUnit(3, Team.Enemy, C(0, 1));
            Unit behind = NewUnit(4, Team.Enemy, C(2, 1));
            Unit otherRow = NewUnit(5, Team.Enemy, C(0, 0));
            var all = new List<Unit> { primary, front, behind, otherRow };
            List<Unit> hit = resolver.ExpandShape(primary, Shape.Line, all);
            Assert.AreEqual(2, hit.Count, "line hits the path up to the target");
            Assert.IsFalse(hit.Contains(behind), "line excludes cells behind the target");
            Assert.IsFalse(hit.Contains(otherRow), "line excludes other rows");
        }

        [Test]
        public void IsValidCellAllowsEmptyCell()
        {
            TargetResolver resolver = Resolver(3, 3, 3, 3);
            Unit a = NewUnit(1, Team.Ally, C(0, 0));
            var all = new List<Unit> { a };
            Assert.IsTrue(resolver.IsValidCell(a, Team.Enemy, C(0, 0), AttackType.Ranged, 3, all), "empty cell in range is valid");
            Assert.IsFalse(resolver.IsValidCell(a, Team.Enemy, C(2, 2), AttackType.Ranged, 3, all), "empty cell out of range is invalid");
        }

        [Test]
        public void ExpandShapeCellHitsNeighborsFromEmptyAnchor()
        {
            TargetResolver resolver = Resolver(3, 3, 3, 3);
            Unit neighbor = NewUnit(1, Team.Enemy, C(1, 0));
            Unit outside = NewUnit(2, Team.Enemy, C(2, 0));
            var all = new List<Unit> { neighbor, outside };
            List<Unit> hit = resolver.ExpandShapeCell(Team.Enemy, C(0, 0), Shape.Area, all);
            CollectionAssert.AreEqual(new[] { neighbor }, hit, "empty-anchored area still hits the neighbor");
        }

        [Test]
        public void ShapeCellsIncludesEmptyCellsAndClipsToGrid()
        {
            TargetResolver resolver = Resolver(3, 3, 3, 3);
            CollectionAssert.AreEqual(new[] { C(0, 1), C(1, 1), C(2, 1) },
                resolver.ShapeCells(C(1, 1), Shape.Pierce, C(3, 3)), "pierce covers the whole row regardless of units");
            CollectionAssert.AreEqual(new[] { C(2, 2) },
                resolver.ShapeCells(C(2, 2), Shape.Area, C(3, 3)), "area clips to the grid at a corner");
        }

        [Test]
        public void MovableCellsInTheMiddle()
        {
            TargetResolver resolver = Resolver(3, 3, 3, 3);
            Unit a = NewUnit(1, Team.Ally, C(1, 1));
            CollectionAssert.AreEqual(new[] { C(1, 0), C(1, 2), C(0, 1), C(2, 1) },
                resolver.MovableCells(a, new List<Unit> { a }), "middle cell has four moves in fixed order");
        }

        [Test]
        public void MovableCellsAtACorner()
        {
            TargetResolver resolver = Resolver(3, 3, 3, 3);
            Unit a = NewUnit(1, Team.Ally, C(0, 0));
            CollectionAssert.AreEqual(new[] { C(0, 1), C(1, 0) },
                resolver.MovableCells(a, new List<Unit> { a }), "corner cell has two moves");
        }

        [Test]
        public void MovableCellsSkipLivingUnits()
        {
            TargetResolver resolver = Resolver(3, 3, 3, 3);
            Unit a = NewUnit(1, Team.Ally, C(1, 1));
            Unit blocker = NewUnit(2, Team.Ally, C(1, 0));
            Unit fallen = NewUnit(3, Team.Ally, C(1, 2));
            fallen.TakeDamage(999);
            Unit foe = NewUnit(4, Team.Enemy, C(0, 1));
            var all = new List<Unit> { a, blocker, fallen, foe };
            CollectionAssert.AreEqual(new[] { C(1, 2), C(0, 1), C(2, 1) },
                resolver.MovableCells(a, all), "living ally blocks, fallen ally and enemy side do not");
        }

        [Test]
        public void MovableCellsStayInOwnGrid()
        {
            TargetResolver resolver = Resolver(3, 3, 2, 2);
            Unit e = NewUnit(1, Team.Enemy, C(1, 1));
            CollectionAssert.AreEqual(new[] { C(1, 0), C(0, 1) },
                resolver.MovableCells(e, new List<Unit> { e }), "moves stay inside the unit's own grid");
        }
    }
}
