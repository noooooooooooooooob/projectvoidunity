using System.Collections.Generic;
using NUnit.Framework;
using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class CardZoneSignalsTests
    {
        private const int Seed = 2024;

        [TearDown]
        public void TearDown() => Make.Cleanup();

        private static AllyData Ally(int deckSize)
        {
            var deck = new CardData[deckSize];
            for (int i = 0; i < deckSize; i++)
            {
                deck[i] = Make.Card($"c{i}");
            }
            return Make.Ally("a", maxHp: 30, speed: 10, maxSp: 3, deck: deck);
        }

        private static BattleState NewState(AllyData ally, float moveChance = 0f)
            => Make.State(Make.Encounter(new Vector2Int(3, 3), new Vector2Int(3, 3),
                new[] { Make.Place(ally, 0, 1) },
                new[] { Make.Place(Make.Enemy("e", maxHp: 20, speed: 1, moveChance: moveChance), 0, 1) }), Seed);

        private static List<string> Record(BattleState state)
        {
            var seen = new List<string>();
            state.CardDrawn += (unit, card, deck, discard) => seen.Add($"drawn:{deck}:{discard}");
            state.DeckReshuffled += (unit, count) => seen.Add($"reshuffled:{count}");
            state.HandDiscarded += (unit, cards, discard) => seen.Add($"discarded:{cards.Count}:{discard}");
            return seen;
        }

        private static List<string> Ids(List<CardData> cards) => cards.ConvertAll(c => c.id);

        [Test]
        public void DrawReportsCounts()
        {
            BattleState state = NewState(Ally(6));
            List<string> seen = Record(state);
            state.StartBattle();
            CollectionAssert.AreEqual(new[] { "drawn:5:0", "drawn:4:0", "drawn:3:0", "drawn:2:0" }, seen, "four draws with shrinking deck");
            Assert.AreEqual(4, state.LivingUnits(Team.Ally)[0].Hand.Count, "hand holds four cards");
        }

        [Test]
        public void ReshuffleHappensMidDraw()
        {
            BattleState state = NewState(Ally(6));
            state.StartBattle();
            List<string> seen = Record(state);
            state.EndTurn();
            CollectionAssert.AreEqual(new[] { "discarded:4:4", "drawn:1:4", "drawn:0:4", "reshuffled:4", "drawn:3:0", "drawn:2:0" }, seen,
                "discard, two draws, reshuffle, two draws");
            Assert.AreEqual(4, state.LivingUnits(Team.Ally)[0].Hand.Count, "hand refilled to four");
        }

        [Test]
        public void DrawStopsWhenDeckAndDiscardAreEmpty()
        {
            BattleState state = NewState(Ally(2));
            List<string> seen = Record(state);
            state.StartBattle();
            CollectionAssert.AreEqual(new[] { "drawn:1:0", "drawn:0:0" }, seen, "only two draws, no reshuffle");
            Assert.AreEqual(2, state.LivingUnits(Team.Ally)[0].Hand.Count, "hand holds two cards");
        }

        [Test]
        public void HandDiscardedCarriesCards()
        {
            BattleState state = NewState(Ally(6));
            state.StartBattle();
            Unit ally = state.LivingUnits(Team.Ally)[0];
            var before = new List<CardData>(ally.Hand);
            var discarded = new List<(List<CardData> cards, int count)>();
            state.HandDiscarded += (unit, cards, count) => discarded.Add((new List<CardData>(cards), count));
            state.EndTurn();
            CollectionAssert.AreEqual(before, discarded[0].cards, "discarded cards in hand order");
            Assert.AreEqual(4, discarded[0].count, "discard count right after discarding");
        }

        [Test]
        public void StateDrawMatchesUnitDraw()
        {
            AllyData data = Ally(6);
            BattleState state = NewState(data);
            state.StartBattle();
            Unit ally = state.LivingUnits(Team.Ally)[0];
            List<string> firstHand = Ids(ally.Hand);
            state.EndTurn();
            List<string> secondHand = Ids(ally.Hand);

            var rng = new Rng(Seed);
            var reference = new Unit(0, data, Team.Ally, new Vector2Int(0, 1));
            reference.ShuffleDeck(rng);
            reference.Draw(BattleState.DrawPerTurn, rng);
            CollectionAssert.AreEqual(Ids(reference.Hand), firstHand, "first hand matches Unit.draw");
            reference.DiscardHand();
            reference.Draw(BattleState.DrawPerTurn, rng);
            CollectionAssert.AreEqual(Ids(reference.Hand), secondHand, "second hand, across a reshuffle, matches Unit.draw");
        }

        [Test]
        public void EnemyMovesDoNotChangeDraws()
        {
            BattleState still = NewState(Ally(6), 0f);
            BattleState moving = NewState(Ally(6), 1f);
            still.StartBattle();
            moving.StartBattle();
            still.EndTurn();
            moving.EndTurn();
            CollectionAssert.AreEqual(Ids(still.LivingUnits(Team.Ally)[0].Hand), Ids(moving.LivingUnits(Team.Ally)[0].Hand),
                "enemy randomness leaves the draw order alone");
            Assert.AreNotEqual(new Vector2Int(0, 1), moving.LivingUnits(Team.Enemy)[0].Cell, "the moving enemy left its cell");
        }
    }
}
