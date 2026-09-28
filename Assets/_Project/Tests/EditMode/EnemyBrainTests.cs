using System.Collections.Generic;
using NUnit.Framework;
using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class EnemyBrainTests
    {
        [TearDown]
        public void TearDown() => Make.Cleanup();

        // 원본 _ally: 속도 1, SP 1.
        private static AllyData Ally(string id, int hp) => Make.Ally(id, maxHp: hp, speed: 1, maxSp: 1);

        // 원본 _enemy: 체력 20, 속도 99, 원거리 단일, 이동 확률 0.
        private static EnemyData Enemy(int attackRange, int damage, int blockAmount, int restHeal)
        {
            EnemyData data = Make.Enemy("foe", maxHp: 20, speed: 99, attackDamage: damage, attackRange: attackRange,
                attackType: AttackType.Ranged, attackShape: Shape.Single, blockAmount: blockAmount, restHeal: restHeal, moveChance: 0f);
            data.displayName = "적";
            return data;
        }

        private static BattleState NewState(UnitPlacement[] allies, EnemyData enemy, Vector2Int? enemyGrid = null, Vector2Int? enemyCell = null)
        {
            Vector2Int cell = enemyCell ?? new Vector2Int(0, 1);
            EncounterData encounter = Make.Encounter(new Vector2Int(3, 3), enemyGrid ?? new Vector2Int(3, 3),
                allies, new[] { new UnitPlacement(enemy, cell) });
            return Make.State(encounter, 4242);
        }

        private static Unit Foe(BattleState state) => state.LivingUnits(Team.Enemy)[0];

        [Test]
        public void AttacksLowestHpTarget()
        {
            BattleState state = NewState(new[] { Make.Place(Ally("healthy", 30), 0, 0), Make.Place(Ally("wounded", 8), 0, 1) }, Enemy(5, 6, 5, 4));
            Unit foe = Foe(state);
            Assert.AreEqual(EnemyAction.Attack, EnemyBrain.Decide(state, foe), "picks attack");
            Assert.AreEqual("wounded", EnemyBrain.FindTarget(state, foe).Data.id, "targets the lower hp ally");
            EnemyBrain.TakeTurn(state, foe);
            Assert.AreEqual(2, state.LivingUnits(Team.Ally)[1].Hp, "wounded ally took damage");
        }

        [Test]
        public void RestsWhenBadlyHurt()
        {
            BattleState state = NewState(new[] { Make.Place(Ally("a", 30), 0, 1) }, Enemy(5, 6, 5, 4));
            Unit foe = Foe(state);
            foe.TakeDamage(15);
            Assert.AreEqual(EnemyAction.Rest, EnemyBrain.Decide(state, foe), "rests at or below 30% hp");
            EnemyBrain.TakeTurn(state, foe);
            Assert.AreEqual(9, foe.Hp, "healed by rest_heal");
        }

        [Test]
        public void DefendsWhenNoTargetInRange()
        {
            BattleState state = NewState(new[] { Make.Place(Ally("far", 30), 2, 1) }, Enemy(1, 6, 7, 4));
            Unit foe = Foe(state);
            Assert.IsNull(EnemyBrain.FindTarget(state, foe), "no reachable target");
            Assert.AreEqual(EnemyAction.Defend, EnemyBrain.Decide(state, foe), "falls back to defend");
            EnemyBrain.TakeTurn(state, foe);
            Assert.AreEqual(7, foe.Block, "gained block");
        }

        [Test]
        public void AttackRespectsMeleeBlocking()
        {
            EnemyData melee = Enemy(4, 6, 5, 4);
            melee.attackType = AttackType.Melee;
            BattleState state = NewState(new[] { Make.Place(Ally("front", 30), 0, 1), Make.Place(Ally("back", 5), 1, 1) }, melee);
            Assert.AreEqual("front", EnemyBrain.FindTarget(state, Foe(state)).Data.id, "melee must hit the front rank");
        }

        [Test]
        public void DeterministicAcrossRuns()
        {
            var picked = new List<string>();
            for (int i = 0; i < 2; i++)
            {
                BattleState state = NewState(new[] { Make.Place(Ally("x", 20), 0, 0), Make.Place(Ally("y", 20), 0, 2) }, Enemy(5, 6, 5, 4));
                picked.Add(EnemyBrain.FindTarget(state, Foe(state)).Data.id);
            }
            Assert.AreEqual(picked[0], picked[1], "same situation yields the same target");
        }

        [Test]
        public void MovesWhenTheRollHits()
        {
            EnemyData mover = Enemy(5, 6, 5, 4);
            mover.moveChance = 1f;
            BattleState state = NewState(new[] { Make.Place(Ally("a", 30), 0, 1) }, mover);
            Unit foe = Foe(state);
            Assert.AreEqual(EnemyAction.Move, EnemyBrain.Decide(state, foe), "certain roll picks move");
            EnemyBrain.TakeTurn(state, foe);
            Assert.AreNotEqual(new Vector2Int(0, 1), foe.Cell, "moved off the starting cell");
            CollectionAssert.Contains(new[] { new Vector2Int(0, 0), new Vector2Int(0, 2), new Vector2Int(1, 1) }, foe.Cell, "moved one step up, down or back");
            Assert.AreEqual(30, state.LivingUnits(Team.Ally)[0].Hp, "moving is the whole action");
        }

        [Test]
        public void BlockedMovePicksAnotherAction()
        {
            EnemyData mover = Enemy(5, 6, 5, 4);
            mover.moveChance = 1f;
            BattleState state = NewState(new[] { Make.Place(Ally("a", 30), 0, 1) }, mover, new Vector2Int(1, 1), new Vector2Int(0, 0));
            Unit foe = Foe(state);
            var picks = new List<EnemyAction>();
            for (int i = 0; i < 30; i++)
            {
                picks.Add(EnemyBrain.Decide(state, foe));
            }
            Assert.IsFalse(picks.Contains(EnemyAction.Move), "blocked mover never picks MOVE");
            Assert.IsTrue(picks.Contains(EnemyAction.Attack), "blocked mover can attack a reachable target");
            Assert.IsTrue(picks.Contains(EnemyAction.Defend) || picks.Contains(EnemyAction.Rest), "blocked mover can also defend or rest");

            EnemyData shy = Enemy(1, 6, 5, 4);
            shy.moveChance = 1f;
            BattleState farState = NewState(new[] { Make.Place(Ally("far", 30), 2, 1) }, shy, new Vector2Int(1, 1), new Vector2Int(0, 0));
            Unit farFoe = Foe(farState);
            var farPicks = new List<EnemyAction>();
            for (int i = 0; i < 30; i++)
            {
                farPicks.Add(EnemyBrain.Decide(farState, farFoe));
            }
            Assert.IsFalse(farPicks.Contains(EnemyAction.Attack), "no attack without a target");
            Assert.IsTrue(farPicks.Contains(EnemyAction.Defend) && farPicks.Contains(EnemyAction.Rest), "falls back to defend and rest");
        }

        [Test]
        public void ZeroChanceUsesNoRandomness()
        {
            BattleState state = NewState(new[] { Make.Place(Ally("a", 30), 0, 1) }, Enemy(5, 6, 5, 4));
            Unit foe = Foe(state);
            int before = state.AiRng.Draws;
            Assert.AreEqual(EnemyAction.Attack, EnemyBrain.Decide(state, foe), "zero chance keeps the old choice");
            Assert.AreEqual(before, state.AiRng.Draws, "zero chance draws no random number");
        }

        [Test]
        public void SameSeedSameMoves()
        {
            var cells = new List<Vector2Int>();
            for (int i = 0; i < 2; i++)
            {
                EnemyData mover = Enemy(5, 6, 5, 4);
                mover.moveChance = 1f;
                BattleState state = NewState(new[] { Make.Place(Ally("a", 30), 0, 1) }, mover);
                Unit foe = Foe(state);
                EnemyBrain.TakeTurn(state, foe);
                cells.Add(foe.Cell);
            }
            Assert.AreEqual(cells[0], cells[1], "same seed moves to the same cell");
        }
    }
}
