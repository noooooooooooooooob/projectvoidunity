using NUnit.Framework;
using ProjectVoid.Combat;
using ProjectVoid.View;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class PlaybackTests
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

        [Test]
        public void InstantPlaybackMirrorsTheRules()
        {
            CardData Hit(string id) => Make.Card(id, damage: 50, attackRange: 5, attackType: AttackType.Ranged);
            var encounter = Make.Encounter(new Vector2Int(3, 3), new Vector2Int(2, 2),
                new[] { Make.Place(Make.Ally("a", maxHp: 30, speed: 9, deck: new[] { Hit("hit"), Hit("hit2"), Hit("hit3"), Hit("hit4") }), 0, 1) },
                new[] { Make.Place(Make.Enemy("e", maxHp: 10, speed: 1), 0, 0) });
            BattleState state = Make.State(encounter, 8);
            var recorder = new BattleEventRecorder(state);

            _root = new GameObject("PlaybackTestRoot");
            var board = new GameObject("Board").AddComponent<Board3D>();
            board.transform.SetParent(_root.transform);
            board.Build(state, TestAssets.Load());
            var hud = new GameObject("Hud").AddComponent<BattleHud>();
            hud.transform.SetParent(_root.transform);
            hud.Build(TestAssets.Load());
            var playback = _root.AddComponent<BattlePlayback>();
            playback.Board = board;
            playback.Hud = hud;
            playback.Instant = true;

            state.StartBattle();
            Coroutines.Drain(playback.Play(recorder.TakeEvents()));
            Assert.AreEqual(4, hud.HandCount, "drawn cards appear");

            Assert.IsTrue(state.PlayCard(0, Team.Enemy, new Vector2Int(0, 0)));
            Coroutines.Drain(playback.Play(recorder.TakeEvents()));
            Unit enemy = state.Units[1];
            Assert.IsFalse(board.ViewFor(enemy).gameObject.activeSelf, "killed enemy hidden");
            Assert.IsTrue(hud.BannerVisible, "victory banner");
            StringAssert.Contains("전투 종료 — 승리", hud.LogText);
        }
    }
}
