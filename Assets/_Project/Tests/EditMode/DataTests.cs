using NUnit.Framework;
using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class DataTests
    {
        [TearDown]
        public void TearDown() => Make.Cleanup();

        [Test]
        public void CardDefaults()
        {
            var card = Make.Asset<CardData>();
            Assert.AreEqual(1, card.spCost, "sp_cost default");
            Assert.AreEqual(AttackType.Melee, card.attackType, "attack_type default");
            Assert.AreEqual(Shape.Single, card.shape, "shape default");
            Assert.AreEqual(1, card.attackRange, "attack_range default");
            Assert.AreEqual(0, card.damage, "damage default");
        }

        [Test]
        public void AllyExtendsUnitData()
        {
            var ally = Make.Asset<AllyData>();
            Assert.IsInstanceOf<UnitData>(ally, "AllyData is UnitData");
            Assert.AreEqual(3, ally.maxSp, "max_sp default");
            Assert.AreEqual(0, ally.deck.Count, "deck starts empty");
            Assert.AreEqual(10, ally.maxHp, "max_hp default");
            Assert.AreEqual(10, ally.speed, "speed default");
        }

        [Test]
        public void EnemyDefaults()
        {
            var enemy = Make.Asset<EnemyData>();
            Assert.AreEqual(5, enemy.attackDamage);
            Assert.AreEqual(1, enemy.attackRange);
            Assert.AreEqual(5, enemy.blockAmount);
            Assert.AreEqual(4, enemy.restHeal);
            Assert.AreEqual(0.25f, enemy.moveChance);
        }

        [Test]
        public void EncounterDefaults()
        {
            var encounter = Make.Asset<EncounterData>();
            Assert.AreEqual(new Vector2Int(3, 3), encounter.allyGrid);
            Assert.AreEqual(new Vector2Int(3, 3), encounter.enemyGrid);
        }
    }
}
