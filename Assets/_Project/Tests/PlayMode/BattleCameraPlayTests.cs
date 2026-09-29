using System.Collections;
using NUnit.Framework;
using ProjectVoid.View;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectVoid.Tests
{
    public class BattleCameraPlayTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
        }

        // 히트스톱 중에 씬이 바뀌거나 카메라가 사라져도 게임이 멈춘 채로 남으면 안 된다.
        [UnityTest]
        public IEnumerator DisablingRestoresTimeScale()
        {
            var camera = new GameObject("CameraTest").AddComponent<BattleCamera>();
            yield return null;
            camera.HitStop(10f);
            camera.enabled = false;
            Assert.AreEqual(1f, Time.timeScale);
            Object.Destroy(camera.gameObject);
        }
    }
}
