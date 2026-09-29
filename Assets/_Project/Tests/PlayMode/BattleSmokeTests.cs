using System.Collections;
using NUnit.Framework;
using ProjectVoid.Combat;
using ProjectVoid.View;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectVoid.Tests
{
    public class BattleSmokeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            BattleRoot.ForceInstantPlayback = true;
            SceneManager.LoadScene("Battle");
            yield return null;
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            BattleRoot.ForceInstantPlayback = false;
        }

        private static BattleRoot Root => Object.FindFirstObjectByType<BattleRoot>();

        private static IEnumerator WaitIdle(BattleRoot root)
        {
            for (int i = 0; i < 600 && root.IsPlaying; i++)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator BattleCameraHasHitEffects()
        {
            yield return WaitIdle(Root);
            Assert.IsNotNull(Camera.main.GetComponent<BattleCamera>());
        }

        [UnityTest]
        public IEnumerator PlaysSkirmishToTheEnd()
        {
            BattleRoot root = Root;
            yield return WaitIdle(root);
            for (int step = 0; step < 400 && !root.State.Finished; step++)
            {
                if (!TryPlayCard(root))
                {
                    root.OnEndTurnPressed();
                }
                yield return null;
                yield return WaitIdle(root);
            }
            Assert.IsTrue(root.State.Finished, "battle finishes");
            Assert.IsTrue(root.Hud.BannerVisible, "banner shown");
        }

        [UnityTest]
        public IEnumerator DropOnNothingCancelsSelection()
        {
            BattleRoot root = Root;
            yield return WaitIdle(root);
            Unit actor = root.State.CurrentUnit();
            int handBefore = actor.Hand.Count;
            root.OnCardDropped(0, new Vector2(-5000f, -5000f));
            yield return null;
            yield return null;
            Assert.AreEqual(-1, root.SelectedCard, "selection cleared");
            Assert.AreEqual(handBefore, actor.Hand.Count, "no card was used");
        }

        [UnityTest]
        public IEnumerator RestartStartsFreshBattle()
        {
            BattleRoot first = Root;
            yield return WaitIdle(first);
            first.OnEndTurnPressed();
            yield return WaitIdle(first);
            first.Restart();
            yield return null;
            yield return null;
            BattleRoot fresh = Root;
            Assert.AreNotSame(first, fresh, "scene reloaded");
            yield return WaitIdle(fresh);
            Assert.AreEqual(1, fresh.State.RoundIndex, "new battle starts at round 1");
        }

        internal static bool TryPlayCard(BattleRoot root)
        {
            BattleState state = root.State;
            Unit actor = state.CurrentUnit();
            if (actor == null || !actor.IsAlly)
            {
                return false;
            }
            for (int i = 0; i < actor.Hand.Count; i++)
            {
                CardData card = actor.Hand[i];
                if (card.spCost > actor.Sp)
                {
                    continue;
                }
                Vector2Int grid = state.Resolver.EnemyGrid;
                for (int col = 0; col < grid.x; col++)
                {
                    for (int row = 0; row < grid.y; row++)
                    {
                        var cell = new Vector2Int(col, row);
                        if (!state.Resolver.IsValidCell(actor, Team.Enemy, cell, card.attackType, card.attackRange, state.Units)
                            || state.Resolver.ExpandShapeCell(Team.Enemy, cell, card.shape, state.Units).Count == 0)
                        {
                            continue;
                        }
                        root.OnCardSelected(i);
                        root.OnCellClicked(Team.Enemy, cell);
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
