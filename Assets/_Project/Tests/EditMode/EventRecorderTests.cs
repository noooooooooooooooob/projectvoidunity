using System.Collections.Generic;
using NUnit.Framework;
using ProjectVoid.Combat;
using ProjectVoid.View;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class EventRecorderTests
    {
        [TearDown]
        public void TearDown() => Make.Cleanup();

        private static EnemyData Enemy(string id, int speed, int damage)
            => Make.Enemy(id, maxHp: 20, speed: speed, attackDamage: damage, attackRange: 9, attackType: AttackType.Ranged,
                attackShape: Shape.Single, blockAmount: 5, restHeal: 0, moveChance: 0f);

        // 원본 _state: 아군 a(체력 30, 속도 10, SP 3, zap 1장) vs e1(속도 5, 피해 4, (0,0)) [+ e2(속도 4, 피해 3, (0,1))], 시드 99.
        private static BattleState NewState(int cardDamage, int enemyCount)
        {
            CardData zap = Make.Card("zap", damage: cardDamage, spCost: 1, attackRange: 9, attackType: AttackType.Ranged, shape: Shape.Single);
            AllyData ally = Make.Ally("a", maxHp: 30, speed: 10, maxSp: 3, deck: zap);
            var enemies = new List<UnitPlacement> { Make.Place(Enemy("e1", 5, 4), 0, 0) };
            if (enemyCount > 1)
            {
                enemies.Add(Make.Place(Enemy("e2", 4, 3), 0, 1));
            }
            return Make.State(Make.Encounter(new Vector2Int(3, 3), new Vector2Int(3, 3), new[] { Make.Place(ally, 0, 1) }, enemies), 99);
        }

        private static List<BattleEventKind> Kinds(List<BattleEvent> events) => events.ConvertAll(e => e.Kind);

        [Test]
        public void StartBattleRecordsFirstTurn()
        {
            BattleState state = NewState(1, 2);
            var recorder = new BattleEventRecorder(state);
            state.StartBattle();
            List<BattleEvent> events = recorder.TakeEvents();
            CollectionAssert.AreEqual(new[] { BattleEventKind.TurnStarted, BattleEventKind.CardDrawn }, Kinds(events), "turn start then the single draw");
            Assert.IsTrue(events[0].Unit.IsAlly, "subject is the ally");
            Assert.AreEqual(1, events[0].RoundIndex, "round snapshot");
            Assert.AreEqual(0, events[0].TurnIndex, "turn index snapshot");
            Assert.AreEqual(3, events[0].Order.Count, "order has every unit");
            CollectionAssert.AreEqual(new[] { true, true, true }, events[0].Alive, "alive flags");
            Assert.AreEqual(30, events[0].Hp, "hp snapshot");
            Assert.AreEqual(new Vector2Int(0, 1), events[0].Cell, "cell snapshot");
            Assert.AreEqual(1, events[0].DeckCount, "deck snapshot before the draw");
            Assert.AreEqual(0, events[0].DiscardCount, "discard snapshot before the draw");
            Assert.AreEqual(0, recorder.TakeEvents().Count, "take_events empties the queue");
        }

        [Test]
        public void EndTurnRecordsEnemiesInOrder()
        {
            BattleState state = NewState(1, 2);
            var recorder = new BattleEventRecorder(state);
            state.StartBattle();
            recorder.TakeEvents();
            Unit ally = state.LivingUnits(Team.Ally)[0];
            state.EndTurn();
            List<BattleEvent> events = recorder.TakeEvents();
            CollectionAssert.AreEqual(new[]
            {
                BattleEventKind.HandDiscarded,
                BattleEventKind.TurnStarted, BattleEventKind.EnemyActed, BattleEventKind.Log, BattleEventKind.Damaged,
                BattleEventKind.TurnStarted, BattleEventKind.EnemyActed, BattleEventKind.Log, BattleEventKind.Damaged,
                BattleEventKind.TurnStarted, BattleEventKind.DeckReshuffled, BattleEventKind.CardDrawn,
            }, Kinds(events), "event kinds in play order");
            Assert.AreEqual("e1", events[1].Unit.Data.id, "faster enemy acts first");
            Assert.AreEqual("a", events[2].Target.Data.id, "attack targets the ally");
            Assert.AreEqual(26, events[4].Hp, "first hit snapshot");
            Assert.AreEqual(23, events[8].Hp, "second hit snapshot");
            Assert.AreEqual(ally.Hp, events[8].Hp, "last snapshot matches state");
            Assert.AreEqual(2, events[9].RoundIndex, "ally's next turn is round 2");
        }

        [Test]
        public void CardPlayRecordsActionThenDamage()
        {
            BattleState state = NewState(1, 2);
            var recorder = new BattleEventRecorder(state);
            state.StartBattle();
            recorder.TakeEvents();
            Unit foe = state.LivingUnits(Team.Enemy)[0];
            state.PlayCard(0, foe.Team, foe.Cell);
            List<BattleEvent> events = recorder.TakeEvents();
            CollectionAssert.AreEqual(new[] { BattleEventKind.CardPlayed, BattleEventKind.Log, BattleEventKind.Damaged }, Kinds(events), "card play kinds");
            Assert.AreSame(foe, events[0].Target, "target is the clicked enemy");
            Assert.AreEqual("zap", events[0].Card.id, "card recorded");
            Assert.AreEqual(1, events[2].Amount, "damage amount");
            Assert.AreEqual(19, events[2].Hp, "damage hp snapshot");
            Assert.AreEqual(0, events[0].DeckCount, "deck after playing");
            Assert.AreEqual(1, events[0].DiscardCount, "discard after playing");
        }

        [Test]
        public void KillRecordsDeathAndBattleEnd()
        {
            BattleState state = NewState(50, 1);
            var recorder = new BattleEventRecorder(state);
            state.StartBattle();
            recorder.TakeEvents();
            Unit foe = state.LivingUnits(Team.Enemy)[0];
            state.PlayCard(0, foe.Team, foe.Cell);
            List<BattleEvent> events = recorder.TakeEvents();
            CollectionAssert.AreEqual(new[]
            {
                BattleEventKind.CardPlayed, BattleEventKind.Log, BattleEventKind.Damaged, BattleEventKind.Died, BattleEventKind.Log, BattleEventKind.BattleEnded,
            }, Kinds(events), "kill kinds");
            Assert.AreSame(foe, events[3].Unit, "died event names the foe");
            Assert.AreEqual(new Vector2Int(0, 0), events[3].Cell, "died cell snapshot");
            StringAssert.EndsWith("쓰러짐", events[4].Text, "death log follows");
            Assert.IsTrue(events[5].AllyWon, "ally won");
        }

        [Test]
        public void DefendAndRestRecordSnapshots()
        {
            BattleState state = NewState(1, 1);
            var recorder = new BattleEventRecorder(state);
            Unit foe = state.LivingUnits(Team.Enemy)[0];
            var data = (EnemyData)foe.Data;
            data.attackRange = 0;
            EnemyBrain.TakeTurn(state, foe);
            List<BattleEvent> defend = recorder.TakeEvents();
            CollectionAssert.AreEqual(new[] { BattleEventKind.EnemyActed, BattleEventKind.BlockGained, BattleEventKind.Log }, Kinds(defend), "defend kinds");
            Assert.AreEqual(EnemyAction.Defend, defend[0].Action, "defend action");
            Assert.IsNull(defend[0].Target, "defend has no target");
            Assert.AreEqual(5, defend[1].Amount, "block amount");
            Assert.AreEqual(5, defend[1].Block, "block snapshot");

            data.restHeal = 4;
            foe.TakeDamage(20);
            EnemyBrain.TakeTurn(state, foe);
            List<BattleEvent> rest = recorder.TakeEvents();
            CollectionAssert.AreEqual(new[] { BattleEventKind.EnemyActed, BattleEventKind.Healed, BattleEventKind.Log }, Kinds(rest), "rest kinds");
            Assert.AreEqual(EnemyAction.Rest, rest[0].Action, "rest action");
            Assert.AreEqual(4, rest[1].Amount, "heal amount");
            Assert.AreEqual(9, rest[1].Hp, "heal hp snapshot");
        }

        [Test]
        public void CardZoneEventsSnapshot()
        {
            BattleState state = NewState(1, 2);
            var recorder = new BattleEventRecorder(state);
            state.StartBattle();
            recorder.TakeEvents();
            state.EndTurn();
            List<BattleEvent> events = recorder.TakeEvents();
            BattleEvent discarded = events[0];
            BattleEvent reshuffled = events[10];
            BattleEvent drawn = events[11];
            Assert.AreEqual(1, discarded.Cards.Count, "discarded cards");
            Assert.AreEqual(1, discarded.DiscardCount, "discard pile after discarding");
            Assert.AreEqual(0, discarded.DeckCount, "deck when discarding");
            Assert.AreEqual(1, reshuffled.Amount, "reshuffled amount");
            Assert.AreEqual(1, reshuffled.DeckCount, "deck after reshuffle");
            Assert.AreEqual(0, reshuffled.DiscardCount, "discard after reshuffle");
            Assert.AreEqual("zap", drawn.Card.id, "drawn card");
            Assert.AreEqual(0, drawn.DeckCount, "deck after the draw");
        }

        [Test]
        public void AllyMoveRecordsMoveThenLog()
        {
            BattleState state = NewState(1, 1);
            var recorder = new BattleEventRecorder(state);
            state.StartBattle();
            recorder.TakeEvents();
            state.MoveUnit(new Vector2Int(1, 1));
            List<BattleEvent> events = recorder.TakeEvents();
            CollectionAssert.AreEqual(new[] { BattleEventKind.UnitMoved, BattleEventKind.Log }, Kinds(events), "ally move kinds");
            Assert.AreEqual(new Vector2Int(0, 1), events[0].FromCell, "from cell");
            Assert.AreEqual(new Vector2Int(1, 1), events[0].ToCell, "to cell");
            Assert.IsTrue(events[0].Unit.IsAlly, "moved unit is the ally");
            Assert.AreEqual("a 이동", events[1].Text, "move log text");
        }

        [Test]
        public void EnemyMoveRecordsActionMoveLog()
        {
            BattleState state = NewState(1, 1);
            var recorder = new BattleEventRecorder(state);
            Unit foe = state.LivingUnits(Team.Enemy)[0];
            ((EnemyData)foe.Data).moveChance = 1f;
            EnemyBrain.TakeTurn(state, foe);
            List<BattleEvent> events = recorder.TakeEvents();
            CollectionAssert.AreEqual(new[] { BattleEventKind.EnemyActed, BattleEventKind.UnitMoved, BattleEventKind.Log }, Kinds(events), "enemy move kinds");
            Assert.AreEqual(EnemyAction.Move, events[0].Action, "enemy action is move");
            Assert.AreEqual(foe.Cell, events[1].ToCell, "recorded destination matches the unit");
        }
    }
}
