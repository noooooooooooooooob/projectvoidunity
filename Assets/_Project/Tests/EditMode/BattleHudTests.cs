using System.Collections.Generic;
using NUnit.Framework;
using ProjectVoid.Combat;
using ProjectVoid.View;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class BattleHudTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
            Make.Cleanup();
        }

        private BattleHud NewHud()
        {
            _root = new GameObject("HudTestRoot");
            var hud = _root.AddComponent<BattleHud>();
            hud.Build(TestAssets.Load());
            return hud;
        }

        private static Unit NewUnit(int id, Team team) =>
            new Unit(id, team == Team.Ally ? Make.Ally($"u{id}") : (UnitData)Make.Enemy($"u{id}"), team, Vector2Int.zero);

        [Test]
        public void TurnBarMarksCurrentAndSkipsDead()
        {
            var order = new List<Unit> { NewUnit(0, Team.Ally), NewUnit(1, Team.Enemy), NewUnit(2, Team.Ally) };
            string text = BattleHud.TurnBarText(2, order, new List<bool> { true, false, true }, 2);
            StringAssert.StartsWith("R2  ", text);
            StringAssert.Contains("▶u2", text, "current unit marked");
            StringAssert.DoesNotContain("u1", text, "dead unit hidden");
            StringAssert.Contains("u0", text, "acted unit still listed");
        }

        [Test]
        public void DrawCardAddsButtonsAndDimsUnaffordable()
        {
            BattleHud hud = NewHud();
            Unit ally = NewUnit(0, Team.Ally);
            ally.Sp = 1;
            hud.DrawCard(new BattleEvent(BattleEventKind.CardDrawn) { Unit = ally, Card = Make.Card("cheap", spCost: 1) });
            hud.DrawCard(new BattleEvent(BattleEventKind.CardDrawn) { Unit = ally, Card = Make.Card("dear", spCost: 2) });
            Assert.AreEqual(2, hud.HandCount);
            Assert.IsTrue(hud.IsCardAffordable(0));
            Assert.IsFalse(hud.IsCardAffordable(1));
        }

        // 리뷰 지적: 드래그한 카드가 선택되지 않아 드래그 중 미리보기가 없거나 다른 카드 기준으로 나왔다.
        [Test]
        public void DraggingACardSelectsIt()
        {
            BattleHud hud = NewHud();
            Unit ally = NewUnit(0, Team.Ally);
            hud.DrawCard(new BattleEvent(BattleEventKind.CardDrawn) { Unit = ally, Card = Make.Card("a") });
            hud.DrawCard(new BattleEvent(BattleEventKind.CardDrawn) { Unit = ally, Card = Make.Card("b") });
            hud.SetInteractive(true);
            var selected = new List<int>();
            hud.CardSelected += index => selected.Add(index);
            CardButton second = hud.GetComponentsInChildren<CardButton>()[1];
            second.OnBeginDrag(new UnityEngine.EventSystems.PointerEventData(null));
            CollectionAssert.AreEqual(new[] { 1 }, selected, "drag start selects the dragged card");
        }

        [Test]
        public void DiscardHandClearsButtons()
        {
            BattleHud hud = NewHud();
            Unit ally = NewUnit(0, Team.Ally);
            hud.DrawCard(new BattleEvent(BattleEventKind.CardDrawn) { Unit = ally, Card = Make.Card("c") });
            hud.DiscardHand(new BattleEvent(BattleEventKind.HandDiscarded) { Unit = ally });
            Assert.AreEqual(0, hud.HandCount);
        }

        [Test]
        public void SyncShowsHandAndSpOfCurrentAlly()
        {
            var encounter = Make.Encounter(new Vector2Int(3, 3), new Vector2Int(3, 3),
                new[] { Make.Place(Make.Ally("a", speed: 9, deck: new[] { Make.Card("c1"), Make.Card("c2"), Make.Card("c3"), Make.Card("c4"), Make.Card("c5") }), 0, 1) },
                new[] { Make.Place(Make.Enemy("e", speed: 1), 0, 1) });
            BattleState state = Make.State(encounter, 3);
            state.StartBattle();
            BattleHud hud = NewHud();
            hud.SyncFromState(state, -1);
            Assert.AreEqual(4, hud.HandCount, "draws four");
            StringAssert.Contains("3 / 3", hud.SpText);
            StringAssert.Contains("▶a", hud.TurnText);
        }

        [Test]
        public void InteractiveTogglesButtons()
        {
            BattleHud hud = NewHud();
            hud.SetInteractive(false);
            Assert.IsFalse(hud.EndTurnEnabled);
            hud.SetInteractive(true);
            Assert.IsTrue(hud.EndTurnEnabled);
            hud.SetMoveAvailable(false);
            Assert.IsFalse(hud.MoveEnabled);
            hud.SetMoveAvailable(true);
            Assert.IsTrue(hud.MoveEnabled);
        }

        [Test]
        public void BannerShowsResult()
        {
            BattleHud hud = NewHud();
            Assert.IsFalse(hud.BannerVisible);
            hud.ShowBanner(true);
            Assert.IsTrue(hud.BannerVisible);
            Assert.AreEqual("승리!", hud.BannerText);
        }

        [Test]
        public void LogAppendsLines()
        {
            BattleHud hud = NewHud();
            hud.AppendLog("첫 줄");
            hud.AppendLog("둘째 줄");
            Assert.AreEqual("첫 줄\n둘째 줄\n", hud.LogText);
        }
    }
}
