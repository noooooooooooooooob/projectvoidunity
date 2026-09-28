using System.Collections.Generic;
using NUnit.Framework;
using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class MovementTests
    {
        [TearDown]
        public void TearDown() => Make.Cleanup();

        // 원본 _started_state: 아군 a(속도 10, 지정 칸) · b(속도 5, (0,0)), 적 e(체력 20, 속도 1, (0,1)), 시드 21, 전투 시작 후.
        private static BattleState StartedState(Vector2Int aCell)
        {
            EnemyData enemy = Make.Enemy("e", maxHp: 20, speed: 1, moveChance: 0.25f);
            EncounterData encounter = Make.Encounter(new Vector2Int(3, 3), new Vector2Int(3, 3),
                new[] { new UnitPlacement(Make.Ally("a", maxHp: 30, speed: 10, maxSp: 3), aCell), Make.Place(Make.Ally("b", maxHp: 30, speed: 5, maxSp: 3), 0, 0) },
                new[] { Make.Place(enemy, 0, 1) });
            BattleState state = Make.State(encounter, 21);
            state.StartBattle();
            return state;
        }

        private static Unit Find(BattleState state, string id) => state.Units.Find(u => u.Data.id == id);

        [Test]
        public void MoveSpendsSpAndMoves()
        {
            BattleState state = StartedState(new Vector2Int(1, 1));
            Unit a = Find(state, "a");
            var moved = new List<string>();
            state.UnitMoved += (unit, from, to) => moved.Add($"{unit.Data.id}:{from}:{to}");
            var logs = new List<string>();
            state.LogMessage += text => logs.Add(text);
            Assert.IsTrue(state.MoveUnit(new Vector2Int(2, 1)), "move accepted");
            Assert.AreEqual(new Vector2Int(2, 1), a.Cell, "unit stands on the new cell");
            Assert.AreEqual(2, a.Sp, "one sp spent");
            CollectionAssert.AreEqual(new[] { $"a:{new Vector2Int(1, 1)}:{new Vector2Int(2, 1)}" }, moved, "move signal carries both cells");
            CollectionAssert.AreEqual(new[] { "a 이동" }, logs, "move logged");
        }

        [Test]
        public void RejectsInvalidCells()
        {
            BattleState state = StartedState(new Vector2Int(0, 1));
            Unit a = Find(state, "a");
            Assert.IsFalse(state.MoveUnit(new Vector2Int(1, 2)), "diagonal rejected");
            Assert.IsFalse(state.MoveUnit(new Vector2Int(2, 1)), "two cells rejected");
            Assert.IsFalse(state.MoveUnit(new Vector2Int(0, 0)), "occupied cell rejected");
            Assert.IsFalse(state.MoveUnit(new Vector2Int(-1, 1)), "outside the grid rejected");
            Assert.AreEqual(new Vector2Int(0, 1), a.Cell, "cell unchanged");
            Assert.AreEqual(3, a.Sp, "sp unchanged");
            a.Sp = 0;
            Assert.IsFalse(state.MoveUnit(new Vector2Int(0, 2)), "no sp rejected");
        }

        [Test]
        public void RejectsOutsideAnAllyTurn()
        {
            BattleState finishedState = StartedState(new Vector2Int(0, 1));
            finishedState.Finished = true;
            Assert.IsFalse(finishedState.MoveUnit(new Vector2Int(0, 2)), "finished battle rejects moves");
            BattleState enemyTurn = StartedState(new Vector2Int(0, 1));
            enemyTurn.Initiative = new List<Unit> { Find(enemyTurn, "e") };
            enemyTurn.TurnIndex = 0;
            Assert.IsFalse(enemyTurn.MoveUnit(new Vector2Int(0, 2)), "enemy turn rejects moves");
        }

        [Test]
        public void MovesWhileSpLasts()
        {
            BattleState state = StartedState(new Vector2Int(0, 1));
            Unit a = Find(state, "a");
            var accepted = new[]
            {
                state.MoveUnit(new Vector2Int(1, 1)), state.MoveUnit(new Vector2Int(2, 1)),
                state.MoveUnit(new Vector2Int(2, 2)), state.MoveUnit(new Vector2Int(2, 1)),
            };
            CollectionAssert.AreEqual(new[] { true, true, true, false }, accepted, "three moves then out of sp");
            Assert.AreEqual(0, a.Sp, "sp used up");
            Assert.AreEqual(new Vector2Int(2, 2), a.Cell, "stopped on the third cell");
        }

        [Test]
        public void ReachFollowsTheNewCell()
        {
            BattleState state = StartedState(new Vector2Int(0, 1));
            Unit a = Find(state, "a");
            Unit e = Find(state, "e");
            Assert.AreEqual(1, state.Resolver.Reach(a, e), "front rank reach");
            state.MoveUnit(new Vector2Int(1, 1));
            Assert.AreEqual(2, state.Resolver.Reach(a, e), "one step back adds one");
        }
    }
}
