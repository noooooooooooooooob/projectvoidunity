using NUnit.Framework;
using ProjectVoid.View;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class BattleCameraTests
    {
        private static readonly Vector3 BasePosition = new Vector3(0f, 8f, -8f);
        private static readonly Quaternion BaseRotation = Quaternion.Euler(44f, 0f, 0f);
        private GameObject _object;
        private BattleCamera _camera;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("CameraTest");
            _camera = _object.AddComponent<BattleCamera>();
            _camera.SetBase(BasePosition, BaseRotation);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
            Time.timeScale = 1f;
        }

        private void Run(float seconds)
        {
            const float dt = 1f / 60f;
            for (float t = 0f; t < seconds; t += dt)
            {
                _camera.Tick(dt);
            }
        }

        [Test]
        public void SetBasePlacesTheCamera()
        {
            Assert.Less(Vector3.Distance(BasePosition, _object.transform.position), 1e-4f);
            Assert.Less(Quaternion.Angle(BaseRotation, _object.transform.rotation), 0.01f);
        }

        [Test]
        public void PushMovesTowardTheHitPointButOnlyPartway()
        {
            Vector3 hit = new Vector3(2f, 0f, 1f);
            _camera.PushToward(hit);
            Run(1f);
            float before = Vector3.Distance(BasePosition, hit);
            float after = Vector3.Distance(_object.transform.position, hit);
            Assert.Less(after, before - 0.5f, "moved closer to the hit");
            Assert.Greater(after, before * 0.5f, "only a nudge, not a close-up");
        }

        // 연출이 끝나면 기본 구도에서 조금이라도 어긋나 있으면 안 된다.
        [Test]
        public void ReturnsExactlyToBaseAfterEffects()
        {
            _camera.PushToward(new Vector3(2f, 0f, 1f));
            _camera.Shake(1f);
            Run(0.3f);
            _camera.Release();
            Run(3f);
            Assert.Less(Vector3.Distance(BasePosition, _object.transform.position), 1e-4f);
            Assert.Less(Quaternion.Angle(BaseRotation, _object.transform.rotation), 0.01f);
        }

        [Test]
        public void ShakeDisplacesTheCameraThenFades()
        {
            _camera.Shake(1f);
            _camera.Tick(1f / 60f);
            Assert.Greater(Vector3.Distance(BasePosition, _object.transform.position), 0.01f, "visibly shakes");
            Run(2f);
            Assert.Less(Vector3.Distance(BasePosition, _object.transform.position), 1e-4f, "settles");
        }

        [Test]
        public void ShakeGrowsWithDamageUpToACap()
        {
            Assert.Less(BattleCamera.ShakeForDamage(2, false), BattleCamera.ShakeForDamage(8, false));
            Assert.AreEqual(BattleCamera.ShakeForDamage(100, false), BattleCamera.ShakeForDamage(1000, false), 1e-5f, "capped");
            Assert.Greater(BattleCamera.ShakeForDamage(1, true), BattleCamera.ShakeForDamage(1000, false), "a kill hits harder");
        }

        [Test]
        public void HitStopFreezesTimeThenRestores()
        {
            _camera.HitStop(0.06f);
            Assert.Less(Time.timeScale, 0.1f, "frozen");
            Run(0.1f);
            Assert.AreEqual(1f, Time.timeScale, "restored");
        }

        // 처치하면 히트스톱이 끝난 뒤 잠깐 느리게 흐르다가 정상 속도로 돌아온다.
        [Test]
        public void KillSlowMoFollowsTheHitStop()
        {
            _camera.HitStop(0.06f);
            _camera.KillSlowMo();
            _camera.Tick(0.1f);
            Assert.AreEqual(BattleCamera.HitStopScale, Time.timeScale, 1e-5f, "hitstop first");
            _camera.Tick(0.1f);
            Assert.AreEqual(BattleCamera.SlowMoScale, Time.timeScale, 1e-5f, "then slow motion");
            _camera.Tick(0.2f);
            Assert.AreEqual(BattleCamera.SlowMoScale, Time.timeScale, 1e-5f, "still slow");
            _camera.Tick(0.2f);
            Assert.AreEqual(1f, Time.timeScale, "back to normal");
        }

        [Test]
        public void KillSlowMoWorksWithoutHitStop()
        {
            _camera.KillSlowMo();
            _camera.Tick(0.01f);
            Assert.AreEqual(BattleCamera.SlowMoScale, Time.timeScale, 1e-5f);
            _camera.Tick(1f);
            Assert.AreEqual(1f, Time.timeScale);
        }

        // 버그: 히트스톱을 건 프레임의 LateUpdate 가 그 프레임 시간까지 빼서, 낮은 FPS 에선 멈춤이 아예 보이지 않았다.
        [Test]
        public void HitStopIgnoresTheFrameItStartedIn()
        {
            _camera.HitStop(0.06f);
            _camera.Tick(0.1f);
            Assert.Less(Time.timeScale, 0.1f, "still frozen for the next frame");
            _camera.Tick(0.1f);
            Assert.AreEqual(1f, Time.timeScale, "restored");
        }
    }
}
