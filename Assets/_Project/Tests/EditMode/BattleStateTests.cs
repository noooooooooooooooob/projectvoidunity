using System.Collections.Generic;
using NUnit.Framework;
using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class BattleStateTests
    {
        [TearDown]
        public void TearDown() => Make.Cleanup();

        private static CardData Card(string id, int cost, AttackType type, Shape shape, int range, int damage)
            => Make.Card(id, damage: damage, spCost: cost, attackRange: range, attackType: type, shape: shape);

        // 원본 _state: 아군 1(테스터, 체력 30, 속도 10) vs 적 2(앞 (0,1) 속도 5, 뒤 (1,1) 속도 4), 시드 12345.
        private static BattleState NewState(CardData[] cards, int allySp = 5, int enemyHp = 10)
        {
            AllyData ally = Make.Ally("tester", maxHp: 30, speed: 10, maxSp: allySp, deck: cards);
            ally.displayName = "테스터";
            EnemyData front = Make.Enemy("front", maxHp: enemyHp, speed: 5, moveChance: 0.25f);
            EnemyData back = Make.Enemy("back", maxHp: enemyHp, speed: 4, moveChance: 0.25f);
            EncounterData encounter = Make.Encounter(new Vector2Int(3, 3), new Vector2Int(3, 3),
                new[] { Make.Place(ally, 0, 1) },
                new[] { Make.Place(front, 0, 1), Make.Place(back, 1, 1) });
            return Make.State(encounter, 12345);
        }

        private static void ActAs(BattleState state, Unit actor, params CardData[] hand)
        {
            state.TurnIndex = 0;
            state.Initiative = new List<Unit> { actor };
            actor.Hand.Clear();
            actor.Hand.AddRange(hand);
        }

        [Test]
        public void SetupPlacesUnits()
        {
            BattleState state = NewState(new CardData[0]);
            Assert.AreEqual(3, state.Units.Count, "three units total");
            Assert.AreEqual(1, state.LivingUnits(Team.Ally).Count, "one living ally");
            Assert.AreEqual(2, state.LivingUnits(Team.Enemy).Count, "two living enemies");
            Assert.AreEqual(new Vector2Int(3, 3), state.Resolver.AllyGrid, "resolver knows ally grid");
            Assert.AreNotEqual(state.Units[0].UnitId, state.Units[1].UnitId, "unit ids are unique");
        }

        [Test]
        public void PlayCardDamagesAndSpendsSp()
        {
            CardData strike = Card("strike", 1, AttackType.Melee, Shape.Single, 1, 6);
            BattleState state = NewState(new[] { strike });
            Unit ally = state.LivingUnits(Team.Ally)[0];
            Unit front = state.LivingUnits(Team.Enemy)[0];
            ActAs(state, ally, strike);
            Assert.IsTrue(state.PlayCard(0, front.Team, front.Cell), "card was played");
            Assert.AreEqual(4, front.Hp, "target lost hp");
            Assert.AreEqual(4, ally.Sp, "sp spent");
            Assert.AreEqual(0, ally.Hand.Count, "hand emptied");
            Assert.AreEqual(1, ally.Discard.Count, "card went to discard");
        }

        [Test]
        public void PlayCardRejectedWithoutSp()
        {
            CardData pricey = Card("pricey", 9, AttackType.Melee, Shape.Single, 1, 6);
            BattleState state = NewState(new[] { pricey }, 2);
            Unit ally = state.LivingUnits(Team.Ally)[0];
            Unit front = state.LivingUnits(Team.Enemy)[0];
            ActAs(state, ally, pricey);
            Assert.IsFalse(state.PlayCard(0, front.Team, front.Cell), "play rejected");
            Assert.AreEqual(10, front.Hp, "target untouched");
            Assert.AreEqual(2, ally.Sp, "sp untouched");
            Assert.AreEqual(1, ally.Hand.Count, "card stays in hand");
        }

        [Test]
        public void PlayCardRejectedOnBlockedTarget()
        {
            CardData strike = Card("strike", 1, AttackType.Melee, Shape.Single, 4, 6);
            BattleState state = NewState(new[] { strike });
            Unit ally = state.LivingUnits(Team.Ally)[0];
            Unit back = state.LivingUnits(Team.Enemy)[1];
            ActAs(state, ally, strike);
            Assert.IsFalse(state.PlayCard(0, back.Team, back.Cell), "melee cannot reach behind the front");
            Assert.AreEqual(10, back.Hp, "back rank untouched");
        }

        private static void AssertUntouched(Unit ally, Unit front)
        {
            Assert.AreEqual(10, front.Hp, "target untouched");
            Assert.AreEqual(5, ally.Sp, "sp untouched");
            Assert.AreEqual(1, ally.Hand.Count, "hand untouched");
            Assert.AreEqual(0, ally.Discard.Count, "discard untouched");
        }

        [Test]
        public void PlayCardRejectedWhenBattleFinished()
        {
            CardData strike = Card("strike", 1, AttackType.Melee, Shape.Single, 1, 6);
            BattleState state = NewState(new[] { strike });
            Unit ally = state.LivingUnits(Team.Ally)[0];
            Unit front = state.LivingUnits(Team.Enemy)[0];
            ActAs(state, ally, strike);
            state.Finished = true;
            Assert.IsFalse(state.PlayCard(0, front.Team, front.Cell), "play rejected once battle is finished");
            AssertUntouched(ally, front);
        }

        [Test]
        public void PlayCardRejectedWhenNoCurrentActor()
        {
            CardData strike = Card("strike", 1, AttackType.Melee, Shape.Single, 1, 6);
            BattleState state = NewState(new[] { strike });
            Unit ally = state.LivingUnits(Team.Ally)[0];
            Unit front = state.LivingUnits(Team.Enemy)[0];
            ActAs(state, ally, strike);
            state.Initiative = new List<Unit>();
            Assert.IsFalse(state.PlayCard(0, front.Team, front.Cell), "play rejected without a current actor");
            AssertUntouched(ally, front);
        }

        [Test]
        public void PlayCardRejectedWhenCurrentUnitIsEnemy()
        {
            CardData strike = Card("strike", 1, AttackType.Melee, Shape.Single, 1, 6);
            BattleState state = NewState(new[] { strike });
            Unit ally = state.LivingUnits(Team.Ally)[0];
            Unit front = state.LivingUnits(Team.Enemy)[0];
            ActAs(state, ally, strike);
            state.Initiative = new List<Unit> { front };
            Assert.IsFalse(state.PlayCard(0, front.Team, front.Cell), "play rejected when current unit is an enemy");
            AssertUntouched(ally, front);
        }

        [Test]
        public void PlayCardRejectedWhenCurrentUnitIsDead()
        {
            CardData strike = Card("strike", 1, AttackType.Melee, Shape.Single, 1, 6);
            BattleState state = NewState(new[] { strike });
            Unit ally = state.LivingUnits(Team.Ally)[0];
            Unit front = state.LivingUnits(Team.Enemy)[0];
            ActAs(state, ally, strike);
            ally.Hp = 0;
            Assert.IsFalse(state.PlayCard(0, front.Team, front.Cell), "play rejected when current unit is dead");
            AssertUntouched(ally, front);
        }

        [Test]
        public void PlayCardRejectedOnHandIndexOutOfBounds()
        {
            CardData strike = Card("strike", 1, AttackType.Melee, Shape.Single, 1, 6);
            BattleState state = NewState(new[] { strike });
            Unit ally = state.LivingUnits(Team.Ally)[0];
            Unit front = state.LivingUnits(Team.Enemy)[0];
            ActAs(state, ally, strike);
            Assert.IsFalse(state.PlayCard(-1, front.Team, front.Cell), "negative hand index rejected");
            Assert.IsFalse(state.PlayCard(5, front.Team, front.Cell), "too-large hand index rejected");
            AssertUntouched(ally, front);
        }

        [Test]
        public void SweepHitsMultiple()
        {
            CardData volley = Card("volley", 1, AttackType.Ranged, Shape.Sweep, 4, 3);
            BattleState state = NewState(new[] { volley });
            Unit ally = state.LivingUnits(Team.Ally)[0];
            Unit front = state.LivingUnits(Team.Enemy)[0];
            ActAs(state, ally, volley);
            Assert.IsTrue(state.PlayCard(0, front.Team, front.Cell), "sweep played");
            Assert.AreEqual(7, front.Hp, "front damaged");
            Assert.AreEqual(10, state.Units[2].Hp, "different column untouched");
        }

        [Test]
        public void BattleEndsWhenEnemiesWiped()
        {
            CardData nuke = Card("nuke", 1, AttackType.Ranged, Shape.Pierce, 5, 99);
            BattleState state = NewState(new[] { nuke });
            Unit ally = state.LivingUnits(Team.Ally)[0];
            Unit front = state.LivingUnits(Team.Enemy)[0];
            ActAs(state, ally, nuke);
            state.PlayCard(0, front.Team, front.Cell);
            Assert.IsTrue(state.Finished, "battle finished");
            Assert.IsTrue(state.AllyWon, "ally won");
        }
    }
}
