using System.Collections;
using NUnit.Framework;
using ProjectVoid.View;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectVoid.Tests
{
    public class Battle2DSmokeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            BattleRoot.ForceInstantPlayback = true;
            SceneManager.LoadScene("Battle2D");
            yield return null;
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            BattleRoot.ForceInstantPlayback = false;
        }

        [UnityTest]
        public IEnumerator Battle2DPlaysToTheEndWithAnOrthographicCamera()
        {
            BattleRoot root = Object.FindFirstObjectByType<BattleRoot>();
            Assert.IsNotNull(root, "Battle2D scene has a battle");
            Assert.IsTrue(Camera.main.orthographic, "no perspective in 2D");
            Assert.AreEqual(BoardProjection.Flat2D, root.Board.Projection);
            for (int step = 0; step < 400 && !root.State.Finished; step++)
            {
                if (!BattleSmokeTests.TryPlayCard(root))
                {
                    root.OnEndTurnPressed();
                }
                yield return null;
                for (int i = 0; i < 600 && root.IsPlaying; i++)
                {
                    yield return null;
                }
            }
            Assert.IsTrue(root.State.Finished, "battle finishes");
            Assert.IsTrue(root.Hud.BannerVisible, "banner shown");
        }
    }
}
