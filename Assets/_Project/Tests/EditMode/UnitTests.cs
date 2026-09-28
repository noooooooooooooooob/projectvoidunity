using NUnit.Framework;
using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class UnitTests
    {
        [TearDown]
        public void TearDown() => Make.Cleanup();

        private static Unit AllyWithDeck(int deckSize)
        {
            var deck = new CardData[deckSize];
            for (int i = 0; i < deckSize; i++)
            {
                deck[i] = Make.Card($"c{i}");
            }
            return new Unit(1, Make.Ally("a", maxHp: 20, maxSp: 3, deck: deck), Team.Ally, Vector2Int.zero);
        }

        [Test]
        public void BlockAbsorbsBeforeHp()
        {
            Unit unit = AllyWithDeck(0);
            unit.GainBlock(5);
            unit.TakeDamage(3);
            Assert.AreEqual(20, unit.Hp, "block absorbs damage, hp untouched");
            Assert.AreEqual(2, unit.Block, "block reduced by absorbed amount");
        }

        [Test]
        public void BlockDepletes()
        {
            Unit unit = AllyWithDeck(0);
            unit.GainBlock(4);
            unit.TakeDamage(10);
            Assert.AreEqual(14, unit.Hp, "leftover damage hits hp");
            Assert.AreEqual(0, unit.Block, "block fully spent");
        }

        [Test]
        public void HealCapsAtMax()
        {
            Unit unit = AllyWithDeck(0);
            unit.TakeDamage(5);
            unit.Heal(100);
            Assert.AreEqual(20, unit.Hp, "heal does not exceed max_hp");
        }

        [Test]
        public void DrawMovesCards()
        {
            Unit unit = AllyWithDeck(6);
            unit.Draw(4, new Rng(1));
            Assert.AreEqual(4, unit.Hand.Count, "hand has 4");
            Assert.AreEqual(2, unit.Deck.Count, "deck has 2 left");
        }

        [Test]
        public void DrawReshufflesDiscard()
        {
            Unit unit = AllyWithDeck(3);
            unit.Draw(3, new Rng(1));
            unit.DiscardHand();
            Assert.AreEqual(3, unit.Discard.Count, "discard holds 3 before redraw");
            unit.Draw(2, new Rng(1));
            Assert.AreEqual(2, unit.Hand.Count, "redraw pulls from reshuffled deck");
            Assert.AreEqual(0, unit.Discard.Count, "discard emptied by reshuffle");
            Assert.AreEqual(1, unit.Deck.Count, "deck keeps the remainder");
        }

        [Test]
        public void DrawStopsWhenBothEmpty()
        {
            Unit unit = AllyWithDeck(2);
            unit.Draw(5, new Rng(1));
            Assert.AreEqual(2, unit.Hand.Count, "draws only what exists");
        }

        [Test]
        public void DiscardHand()
        {
            Unit unit = AllyWithDeck(4);
            unit.Draw(4, new Rng(1));
            unit.DiscardHand();
            Assert.AreEqual(0, unit.Hand.Count, "hand cleared");
            Assert.AreEqual(4, unit.Discard.Count, "all cards moved to discard");
        }

        [Test]
        public void EnemyHasNoZones()
        {
            var unit = new Unit(2, Make.Enemy("e", maxHp: 15), Team.Enemy, Vector2Int.zero);
            Assert.AreEqual(0, unit.Deck.Count, "enemy deck empty");
            Assert.AreEqual(0, unit.Sp, "enemy sp zero");
            Assert.IsFalse(unit.IsAlly, "enemy is not ally");
        }

        [Test]
        public void DeckIsACopyOfTheAsset()
        {
            var card = Make.Card("x");
            AllyData data = Make.Ally("a", deck: new[] { card, card });
            var unit = new Unit(0, data, Team.Ally, Vector2Int.zero);
            unit.DrawOne();
            Assert.AreEqual(2, data.deck.Count, "drawing must not change the asset's deck");
        }
    }
}
