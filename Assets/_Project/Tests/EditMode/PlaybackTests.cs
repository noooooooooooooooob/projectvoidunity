using System.Collections.Generic;
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

        [Test]
        public void InstantPlaybackLeavesTheCameraAlone()
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
            var camera = new GameObject("Camera").AddComponent<BattleCamera>();
            camera.transform.SetParent(_root.transform);
            var basePosition = new Vector3(0f, 8f, -8f);
            camera.SetBase(basePosition, Quaternion.identity);
            var playback = _root.AddComponent<BattlePlayback>();
            playback.Board = board;
            playback.Hud = hud;
            playback.CameraFx = camera;
            var audio = camera.gameObject.AddComponent<BattleAudio>();
            // 클립이 있어야 Instant 가 소리를 막는지 확인할 수 있다 (없으면 원래 무음이다).
            var sounds = ScriptableObject.CreateInstance<BattleSounds>();
            AudioClip clip = AudioClip.Create("hit", 10, 1, 44100, false);
            sounds.meleeSwing = sounds.meleeHit = sounds.rangedShot = sounds.rangedHit = sounds.blocked = sounds.kill = clip;
            audio.Sounds = sounds;
            playback.Audio = audio;
            playback.Instant = true;

            state.StartBattle();
            Coroutines.Drain(playback.Play(recorder.TakeEvents()));
            Assert.IsTrue(state.PlayCard(0, Team.Enemy, new Vector2Int(0, 0)));
            Coroutines.Drain(playback.Play(recorder.TakeEvents()));
            Assert.AreEqual(1f, Time.timeScale, "no hitstop in instant playback");
            Assert.Less(Vector3.Distance(basePosition, camera.transform.position), 1e-4f, "no push or shake");
            Assert.AreEqual(0, audio.PlayCount, "no sound in instant playback");
            Object.DestroyImmediate(sounds);
        }

        // 버그: 원거리 발사음이 예비동작 시작에 나서 화살보다 0.5초 먼저 들렸다. 원거리는 화살이 나가는 순간에 낸다.
        [Test]
        public void RangedShotSoundWaitsForTheRelease()
        {
            Assert.IsTrue(BattlePlayback.AttackSoundAtStrike(AttackType.Ranged));
            Assert.IsFalse(BattlePlayback.AttackSoundAtStrike(AttackType.Melee), "the swing whoosh leads the hit");
        }

        private static BattleEvent Ev(BattleEventKind kind) => new BattleEvent(kind);

        [Test]
        public void KnockbackGrowsWithDamageUpToACap()
        {
            Assert.AreEqual(0f, BattlePlayback.KnockbackDistance(0), "blocked hits do not shove");
            Assert.Less(BattlePlayback.KnockbackDistance(2), BattlePlayback.KnockbackDistance(6));
            Assert.AreEqual(BattlePlayback.MaxKnockback, BattlePlayback.KnockbackDistance(100), 1e-5f);
        }

        [Test]
        public void ImpactStartFindsTheHitBatchAfterAnAttack()
        {
            var hit = new List<BattleEvent> { Ev(BattleEventKind.CardPlayed), Ev(BattleEventKind.Damaged) };
            Assert.AreEqual(1, BattlePlayback.ImpactStart(hit, 0));
            var logged = new List<BattleEvent> { Ev(BattleEventKind.CardPlayed), Ev(BattleEventKind.Log), Ev(BattleEventKind.Died) };
            Assert.AreEqual(2, BattlePlayback.ImpactStart(logged, 0), "logs between attack and hit are skipped");
            var miss = new List<BattleEvent> { Ev(BattleEventKind.CardPlayed), Ev(BattleEventKind.Log) };
            Assert.AreEqual(-1, BattlePlayback.ImpactStart(miss, 0), "no hit");
            var other = new List<BattleEvent> { Ev(BattleEventKind.EnemyActed), Ev(BattleEventKind.BlockGained), Ev(BattleEventKind.Damaged) };
            Assert.AreEqual(-1, BattlePlayback.ImpactStart(other, 0), "another event breaks the attack");
        }

        // 버그: 빗나가 피해가 없는 공격 뒤에도 푸시인이 풀리지 않아 다음 차례까지 확대된 채였다.
        // 푸시는 돌진한 공격의 피해·사망(사이의 로그 포함)까지만 유지된다.
        [Test]
        public void CameraPushLastsOnlyThroughTheHit()
        {
            Assert.IsTrue(BattlePlayback.KeepsCameraPush(BattleEventKind.Damaged));
            Assert.IsTrue(BattlePlayback.KeepsCameraPush(BattleEventKind.Died));
            Assert.IsTrue(BattlePlayback.KeepsCameraPush(BattleEventKind.Log));
            Assert.IsFalse(BattlePlayback.KeepsCameraPush(BattleEventKind.TurnStarted));
            Assert.IsFalse(BattlePlayback.KeepsCameraPush(BattleEventKind.Healed));
            Assert.IsFalse(BattlePlayback.KeepsCameraPush(BattleEventKind.BlockGained));
            Assert.IsFalse(BattlePlayback.KeepsCameraPush(BattleEventKind.EnemyActed));
        }
    }
}
