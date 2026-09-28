using System.Collections.Generic;
using NUnit.Framework;
using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class BattleSignalsTests
    {
        [TearDown]
        public void TearDown() => Make.Cleanup();

        private static UnitPlacement AllyAt(Vector2Int cell)
        {
            CardData zap = Make.Card("zap", damage: 3, spCost: 1, attackRange: 9, attackType: AttackType.Ranged, shape: Shape.Single);
            return new UnitPlacement(Make.Ally("a", maxHp: 30, speed: 10, maxSp: 3, deck: zap), cell);
        }

        private static EnemyData Enemy(int attackRange, int restHeal)
            => Make.Enemy("e", maxHp: 20, speed: 1, attackDamage: 6, attackRange: attackRange, attackType: AttackType.Ranged,
                attackShape: Shape.Single, blockAmount: 7, restHeal: restHeal, moveChance: 0f);

        private static BattleState NewState(Vector2Int allyCell, EnemyData enemy)
            => Make.State(Make.Encounter(new Vector2Int(3, 3), new Vector2Int(3, 3), new[] { AllyAt(allyCell) }, new[] { Make.Place(enemy, 0, 1) }), 777);

        private static Unit UnitAt(BattleState state, Team team, Vector2Int cell)
            => state.Units.Find(u => u.Team == team && u.Cell == cell && u.IsAlive);

        // 원본은 행동을 enum 정수(%d)로 기록했다. 같은 뜻으로 (int) 를 쓴다.
        private static List<string> Record(BattleState state)
        {
            var seen = new List<string>();
            state.CardPlayed += (actor, card, team, cell) =>
            {
                Unit hit = UnitAt(state, team, cell);
                seen.Add($"card:{card.id}:{(hit == null ? "empty" : hit.Data.id)}");
            };
            state.EnemyActed += (actor, action, target) => seen.Add($"acted:{(int)action}:{(target == null ? "null" : target.Data.id)}");
            state.UnitDamaged += (unit, amount) => seen.Add($"damaged:{amount}");
            state.UnitHealed += (unit, amount) => seen.Add($"healed:{amount}");
            state.BlockGained += (unit, amount) => seen.Add($"block:{amount}");
            return seen;
        }

        [Test]
        public void CardPlayedPrecedesDamage()
        {
            BattleState state = NewState(new Vector2Int(0, 1), Enemy(9, 4));
            state.StartBattle();
            List<string> seen = Record(state);
            Unit foe = state.LivingUnits(Team.Enemy)[0];
            Assert.IsTrue(state.PlayCard(0, foe.Team, foe.Cell), "card play accepted");
            CollectionAssert.AreEqual(new[] { "card:zap:e", "damaged:3" }, seen, "card_played comes before damage");
        }

        [Test]
        public void EnemyAttackSignals()
        {
            BattleState state = NewState(new Vector2Int(0, 1), Enemy(9, 4));
            List<string> seen = Record(state);
            EnemyBrain.TakeTurn(state, state.LivingUnits(Team.Enemy)[0]);
            CollectionAssert.AreEqual(new[] { $"acted:{(int)EnemyAction.Attack}:a", "damaged:6" }, seen, "attack reports action then damage");
        }

        [Test]
        public void EnemyDefendSignals()
        {
            BattleState state = NewState(new Vector2Int(2, 1), Enemy(1, 4));
            List<string> seen = Record(state);
            Unit foe = state.LivingUnits(Team.Enemy)[0];
            EnemyBrain.TakeTurn(state, foe);
            CollectionAssert.AreEqual(new[] { $"acted:{(int)EnemyAction.Defend}:null", "block:7" }, seen, "defend reports action then block");
            Assert.AreEqual(7, foe.Block, "block actually applied");
        }

        [Test]
        public void EnemyRestSignals()
        {
            BattleState state = NewState(new Vector2Int(0, 1), Enemy(9, 4));
            Unit foe = state.LivingUnits(Team.Enemy)[0];
            foe.TakeDamage(15);
            List<string> seen = Record(state);
            EnemyBrain.TakeTurn(state, foe);
            CollectionAssert.AreEqual(new[] { $"acted:{(int)EnemyAction.Rest}:null", "healed:4" }, seen, "rest reports action then heal");
            Assert.AreEqual(9, foe.Hp, "heal actually applied");
        }

        [Test]
        public void EnemyRestReportsCappedAmount()
        {
            BattleState state = NewState(new Vector2Int(0, 1), Enemy(9, 30));
            Unit foe = state.LivingUnits(Team.Enemy)[0];
            foe.TakeDamage(15);
            List<string> seen = Record(state);
            EnemyBrain.TakeTurn(state, foe);
            CollectionAssert.AreEqual(new[] { $"acted:{(int)EnemyAction.Rest}:null", "healed:15" }, seen, "heal amount is what was restored, not rest_heal");
            Assert.AreEqual(20, foe.Hp, "hp capped at max");
        }
    }
}
