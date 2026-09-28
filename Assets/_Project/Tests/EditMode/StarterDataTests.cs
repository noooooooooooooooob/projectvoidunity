using NUnit.Framework;
using ProjectVoid.Combat;
using UnityEditor;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class StarterDataTests
    {
        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        [Test]
        public void StarterCardsExist()
        {
            var strike = Load<CardData>("Assets/_Project/Data/Cards/strike.asset");
            Assert.IsNotNull(strike, "strike loads");
            Assert.AreEqual("strike", strike.id, "strike id");
            Assert.AreEqual(6, strike.damage, "strike damage");
            Assert.AreEqual(AttackType.Melee, strike.attackType, "strike is melee");
            var volley = Load<CardData>("Assets/_Project/Data/Cards/volley.asset");
            Assert.IsNotNull(volley, "volley loads");
            Assert.AreEqual(Shape.Sweep, volley.shape, "volley shape is SWEEP");
            Assert.AreEqual(4, volley.attackRange, "volley range");
            var blast = Load<CardData>("Assets/_Project/Data/Cards/blast.asset");
            Assert.IsNotNull(blast, "blast loads");
            Assert.AreEqual(Shape.Area, blast.shape, "blast shape is AREA");
            var skewer = Load<CardData>("Assets/_Project/Data/Cards/skewer.asset");
            Assert.IsNotNull(skewer, "skewer loads");
            Assert.AreEqual(Shape.Line, skewer.shape, "skewer shape is LINE");
        }

        [Test]
        public void UnitsMatchGodotValues()
        {
            var vanguard = Load<AllyData>("Assets/_Project/Data/Units/vanguard.asset");
            Assert.AreEqual("선봉", vanguard.displayName);
            Assert.AreEqual(30, vanguard.maxHp);
            Assert.AreEqual(12, vanguard.speed);
            Assert.AreEqual(8, vanguard.deck.Count);
            var scout = Load<AllyData>("Assets/_Project/Data/Units/scout.asset");
            Assert.AreEqual(6, scout.deck.Count);
            Assert.AreEqual(16, scout.speed);
            var sentry = Load<EnemyData>("Assets/_Project/Data/Units/sentry.asset");
            Assert.AreEqual(AttackType.Ranged, sentry.attackType);
            Assert.AreEqual(Shape.Sweep, sentry.attackShape);
            Assert.AreEqual(4, sentry.attackRange);
            Assert.AreEqual(8, sentry.blockAmount);
            Assert.AreEqual(3, sentry.restHeal);
        }

        [Test]
        public void SkirmishMatchesGodotLayout()
        {
            var skirmish = Load<EncounterData>("Assets/_Project/Data/Encounters/skirmish.asset");
            Assert.AreEqual(new Vector2Int(3, 3), skirmish.allyGrid);
            Assert.AreEqual(new Vector2Int(2, 2), skirmish.enemyGrid);
            Assert.AreEqual("vanguard", skirmish.allyUnits[0].unitData.id);
            Assert.AreEqual(new Vector2Int(0, 1), skirmish.allyUnits[0].cell);
            Assert.AreEqual(new Vector2Int(2, 0), skirmish.allyUnits[1].cell);
            Assert.AreEqual(new Vector2Int(1, 2), skirmish.allyUnits[2].cell);
            Assert.AreEqual("brute", skirmish.enemyUnits[0].unitData.id);
            Assert.AreEqual(new Vector2Int(0, 0), skirmish.enemyUnits[0].cell);
            Assert.AreEqual("stalker", skirmish.enemyUnits[1].unitData.id);
            Assert.AreEqual(new Vector2Int(1, 1), skirmish.enemyUnits[1].cell);
            Assert.AreEqual("sentry", skirmish.enemyUnits[2].unitData.id);
            Assert.AreEqual(new Vector2Int(1, 0), skirmish.enemyUnits[2].cell);
        }

        // 화면 없이 skirmish 를 끝까지 돌려 규칙 코어가 멈추지 않고 결판나는지 확인한다.
        [Test]
        public void SkirmishPlaysToAnEndHeadless([Values(1, 2, 3, 4, 5)] int seed)
        {
            var skirmish = Load<EncounterData>("Assets/_Project/Data/Encounters/skirmish.asset");
            var state = new BattleState(skirmish, new Rng(seed));
            state.StartBattle();
            for (int step = 0; step < 2000 && !state.Finished; step++)
            {
                if (!TryPlayAnyCard(state))
                {
                    state.EndTurn();
                }
            }
            Assert.IsTrue(state.Finished, "battle finishes");
        }

        private static bool TryPlayAnyCard(BattleState state)
        {
            Unit actor = state.CurrentUnit();
            for (int i = 0; i < actor.Hand.Count; i++)
            {
                Vector2Int grid = state.Resolver.EnemyGrid;
                for (int col = 0; col < grid.x; col++)
                {
                    for (int row = 0; row < grid.y; row++)
                    {
                        var cell = new Vector2Int(col, row);
                        if (state.Resolver.ExpandShapeCell(Team.Enemy, cell, actor.Hand[i].shape, state.Units).Count == 0)
                        {
                            continue;
                        }
                        if (state.PlayCard(i, Team.Enemy, cell))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}
