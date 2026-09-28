using System.Collections.Generic;
using NUnit.Framework;
using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class TurnOrderTests
    {
        [TearDown]
        public void TearDown() => Make.Cleanup();

        private static AllyData Ally(string id, int speed, int deckSize)
        {
            var deck = new CardData[deckSize];
            for (int i = 0; i < deckSize; i++)
            {
                deck[i] = Make.Card($"{id}_c{i}");
            }
            return Make.Ally(id, maxHp: 30, speed: speed, maxSp: 3, deck: deck);
        }

        private static EnemyData Enemy(string id, int speed) => Make.Enemy(id, maxHp: 10, speed: speed, moveChance: 0f);

        // 원본 _state: 각 편을 (0,0), (0,1)... 에 차례로 놓는다. 시드 999.
        private static BattleState NewState(UnitData[] allies, UnitData[] enemies)
        {
            var allyPlacements = new List<UnitPlacement>();
            for (int i = 0; i < allies.Length; i++)
            {
                allyPlacements.Add(Make.Place(allies[i], 0, i));
            }
            var enemyPlacements = new List<UnitPlacement>();
            for (int i = 0; i < enemies.Length; i++)
            {
                enemyPlacements.Add(Make.Place(enemies[i], 0, i));
            }
            return Make.State(Make.Encounter(new Vector2Int(3, 3), new Vector2Int(3, 3), allyPlacements, enemyPlacements), 999);
        }

        [Test]
        public void InitiativeSortedBySpeed()
        {
            BattleState state = NewState(new UnitData[] { Ally("slow", 5, 6), Ally("fast", 20, 6) }, new UnitData[] { Enemy("mid", 10) });
            state.StartBattle();
            Assert.AreEqual("fast", state.Initiative[0].Data.id, "fastest acts first");
            Assert.AreEqual("mid", state.Initiative[1].Data.id, "enemy in the middle");
            Assert.AreEqual("slow", state.Initiative[2].Data.id, "slowest acts last");
        }

        [Test]
        public void InitiativeTiesBrokenById()
        {
            BattleState state = NewState(new UnitData[] { Ally("a", 10, 6), Ally("b", 10, 6) }, new UnitData[] { Enemy("e", 10) });
            state.StartBattle();
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, state.Initiative.ConvertAll(u => u.UnitId), "ties resolve by ascending unit_id");
        }

        [Test]
        public void TurnStartRefillsSpAndDraws()
        {
            BattleState state = NewState(new UnitData[] { Ally("a", 20, 6) }, new UnitData[] { Enemy("e", 1) });
            state.StartBattle();
            Unit actor = state.CurrentUnit();
            Assert.AreEqual("a", actor.Data.id, "stopped on the ally");
            Assert.AreEqual(3, actor.Sp, "sp refilled");
            Assert.AreEqual(BattleState.DrawPerTurn, actor.Hand.Count, "drew four cards");
            Assert.AreEqual(0, actor.Block, "block reset");
        }

        [Test]
        public void EndTurnDiscardsHand()
        {
            BattleState state = NewState(new UnitData[] { Ally("first", 20, 6), Ally("second", 15, 6) }, new UnitData[] { Enemy("e", 1) });
            state.StartBattle();
            Unit actor = state.CurrentUnit();
            Assert.AreEqual("first", actor.Data.id, "faster ally goes first");
            state.EndTurn();
            Assert.AreEqual(0, actor.Hand.Count, "hand emptied at end of turn");
            Assert.AreEqual(BattleState.DrawPerTurn, actor.Discard.Count, "hand moved to discard");
            Assert.AreEqual("second", state.CurrentUnit().Data.id, "turn passed to the next ally");
        }

        [Test]
        public void DeadUnitsAreSkipped()
        {
            BattleState state = NewState(new UnitData[] { Ally("fast", 20, 6), Ally("slow", 5, 6) }, new UnitData[] { Enemy("e", 10) });
            state.StartBattle();
            Unit slow = state.Initiative[2];
            slow.TakeDamage(999);
            state.EndTurn();
            Assert.AreNotSame(slow, state.CurrentUnit(), "dead ally never becomes current");
        }

        [Test]
        public void NewRoundAfterEveryoneActed()
        {
            BattleState state = NewState(new UnitData[] { Ally("a", 20, 8) }, new UnitData[] { Enemy("e", 1) });
            state.StartBattle();
            Assert.AreEqual(1, state.RoundIndex, "first round");
            state.EndTurn();
            Assert.AreEqual(2, state.RoundIndex, "second round begins");
            Assert.AreEqual("a", state.CurrentUnit().Data.id, "ally acts again");
        }

        [Test]
        public void EmptyEncounterFinishesWithoutHanging()
        {
            BattleState state = NewState(new UnitData[0], new UnitData[0]);
            state.StartBattle();
            Assert.IsTrue(state.Finished, "battle already finished with no units");
        }
    }
}
