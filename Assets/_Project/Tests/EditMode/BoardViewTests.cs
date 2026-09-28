using NUnit.Framework;
using ProjectVoid.Combat;
using ProjectVoid.View;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class BoardViewTests
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

        private (BattleState state, Board3D board) BuildBoard(Texture2D allyTexture = null, Texture2D enemyTexture = null)
        {
            var encounter = Make.Encounter(new Vector2Int(3, 3), new Vector2Int(2, 2),
                new[] { Make.Place(Make.Ally("a", maxHp: 20, speed: 5, deck: new[] { Make.Card("c", damage: 1), Make.Card("c2", damage: 1), Make.Card("c3", damage: 1), Make.Card("c4", damage: 1) }), 0, 1) },
                new[] { Make.Place(Make.Enemy("e", maxHp: 20, speed: 1), 0, 0), Make.Place(Make.Enemy("f", maxHp: 20, speed: 1), 1, 1) });
            BattleState state = Make.State(encounter, 3);
            _root = new GameObject("BoardTestRoot");
            var board = _root.AddComponent<Board3D>();
            board.Build(state, TestAssets.Load(), allyTexture, enemyTexture);
            return (state, board);
        }

        [Test]
        public void BuildCreatesTilesForBothSides()
        {
            (_, Board3D board) = BuildBoard();
            Assert.AreEqual(13, board.TileCount, "3x3 + 2x2 tiles");
        }

        [Test]
        public void SyncMarksCurrentOccupiedAndEmpty()
        {
            (BattleState state, Board3D board) = BuildBoard();
            state.StartBattle();
            board.SyncFromState(state);
            Assert.AreEqual(Board3D.TileState.Current, board.GetTileState(Team.Ally, new Vector2Int(0, 1)), "acting ally glows");
            Assert.AreEqual(Board3D.TileState.Base, board.GetTileState(Team.Enemy, new Vector2Int(0, 0)), "occupied enemy tile");
            Assert.AreEqual(Board3D.TileState.Empty, board.GetTileState(Team.Enemy, new Vector2Int(1, 0)), "empty tile");
        }

        [Test]
        public void DeadUnitIsNotPickable()
        {
            (BattleState state, Board3D board) = BuildBoard();
            Unit enemy = state.LivingUnits(Team.Enemy)[0];
            enemy.Hp = 0;
            board.SyncFromState(state);
            Assert.IsFalse(board.ViewFor(enemy).gameObject.activeSelf, "dead view hidden");
            Assert.AreEqual(Board3D.TileState.Empty, board.GetTileState(Team.Enemy, enemy.Cell), "its tile looks empty");
            Vector3 top = board.Layout.CellPosition(Team.Enemy, enemy.Cell);
            bool hit = board.PickAt(new Ray(top + Vector3.up * 5f, Vector3.down), out Team team, out Vector2Int cell);
            Assert.IsTrue(hit, "the tile under the dead unit is still pickable");
            Assert.AreEqual(Team.Enemy, team);
            Assert.AreEqual(enemy.Cell, cell);
        }

        [Test]
        public void MissingSpriteFallsBackToPlaceholder()
        {
            (BattleState state, Board3D board) = BuildBoard();
            UnitView view = board.ViewFor(state.Units[0]);
            Assert.AreSame(TestAssets.Load().placeholderSprite.texture, view.BodyMaterial.GetTexture("_BaseMap"));
        }

        // 종잇장처럼 보이던 문제: 스프라이트가 조명을 받지 않고 그림자도 없었다.
        [Test]
        public void UnitBodyIsLitAndCastsAShadow()
        {
            (BattleState state, Board3D board) = BuildBoard();
            UnitView view = board.ViewFor(state.Units[0]);
            Assert.AreEqual("Universal Render Pipeline/Lit", view.BodyMaterial.shader.name, "body reacts to scene lights");
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.TwoSided, view.BodyRenderer.shadowCastingMode, "body drops a real shadow");
            Assert.IsTrue(view.BodyMaterial.IsKeywordEnabled("_ALPHATEST_ON"), "transparent pixels are cut out, not drawn as a quad");
        }

        // 수직 판을 44° 로 내려다보면 위아래로 눌려 보였다. 발을 축으로 카메라 쪽으로 살짝 기울인다.
        [Test]
        public void UnitLeansTowardTheCamera()
        {
            (BattleState state, Board3D board) = BuildBoard();
            Billboard billboard = board.ViewFor(state.Units[0]).GetComponentInChildren<Billboard>();
            Assert.IsTrue(billboard.yAxisOnly);
            Assert.AreEqual(UnitView.BodyTiltDeg, billboard.tiltDeg, 1e-4f);
            Assert.GreaterOrEqual(UnitView.BodyTiltDeg, 15f);
        }

        [Test]
        public void ContactShadowIsDarkAndTeamRingShowsTheSide()
        {
            (BattleState state, Board3D board) = BuildBoard();
            UnitView ally = board.ViewFor(state.Units[0]);
            UnitView enemy = board.ViewFor(state.Units[1]);
            Assert.Less(ally.ShadowColor.maxColorComponent, 0.2f, "shadow is dark, not team coloured");
            Assert.Greater(ally.RingColor.b, ally.RingColor.r, "ally ring is blue");
            Assert.Greater(enemy.RingColor.r, enemy.RingColor.b, "enemy ring is red");
        }

        [Test]
        public void StatTextShowsHpAndBlock()
        {
            (BattleState state, Board3D board) = BuildBoard();
            UnitView view = board.ViewFor(state.Units[0]);
            view.SetStats(12, 30, 4);
            Assert.AreEqual("12/30  방4", view.StatText);
            view.SetStats(10, 20, 0);
            Assert.AreEqual("10/20", view.StatText);
            Assert.AreEqual(UnitView.HpBarWidth * 0.5f, view.HpFillWidth, 1e-4f, "hp bar shrinks with hp");
        }

        // 리뷰 지적: UI 위에서 누르고 보드 위에서 떼면 보드 클릭으로 처리돼 카드가 쓰이거나 SP 가 빠졌다.
        [Test]
        public void PressOnUiReleasedOverBoardIsNotAClick()
        {
            (_, Board3D board) = BuildBoard();
            board.HandlePointer(Vector2.zero, true, false, true);
            board.HandlePointer(Vector2.zero, false, true, false);
            Assert.IsFalse(board.HasPendingPick, "a press that began on the HUD must not pick a tile");
        }

        [Test]
        public void PressAndReleaseOnBoardIsAClick()
        {
            (_, Board3D board) = BuildBoard();
            board.HandlePointer(Vector2.zero, true, false, false);
            board.HandlePointer(Vector2.zero, false, true, false);
            Assert.IsTrue(board.HasPendingPick, "a press and release on the board picks a tile");
        }

        [Test]
        public void MoveViewWithoutAnimationJumpsHome()
        {
            (BattleState state, Board3D board) = BuildBoard();
            Unit ally = state.Units[0];
            Coroutines.Drain(board.MoveView(ally, new Vector2Int(0, 1), new Vector2Int(1, 1), false));
            Assert.AreEqual(board.Layout.CellPosition(Team.Ally, new Vector2Int(1, 1)), board.ViewFor(ally).HomePosition);
            Assert.AreEqual(Board3D.TileState.Empty, board.GetTileState(Team.Ally, new Vector2Int(0, 1)));
            Assert.AreEqual(Board3D.TileState.Current, board.GetTileState(Team.Ally, new Vector2Int(1, 1)));
        }

        private static Color BaseColorOf(Board3D board, Team team, Vector2Int cell)
            => board.TileRenderer(team, cell).sharedMaterial.GetColor("_BaseColor");

        // 무늬 없는 회색 판이 바닥에 붙인 UI 처럼 보였다. 아군은 콘크리트, 적은 금속 패널 그림을 입힌다.
        [Test]
        public void TilesShowTheirSideTexture()
        {
            var ally = new Texture2D(4, 4);
            var enemy = new Texture2D(4, 4);
            try
            {
                (_, Board3D board) = BuildBoard(ally, enemy);
                Assert.AreSame(ally, board.TileRenderer(Team.Ally, Vector2Int.zero).sharedMaterial.mainTexture);
                Assert.AreSame(enemy, board.TileRenderer(Team.Enemy, Vector2Int.zero).sharedMaterial.mainTexture);
                Assert.Greater(BaseColorOf(board, Team.Enemy, new Vector2Int(0, 0)).maxColorComponent, 0.7f, "textured tiles are not tinted dark");
            }
            finally
            {
                Object.DestroyImmediate(ally);
                Object.DestroyImmediate(enemy);
            }
        }

        // 현재 턴 칸이 노랗게 꽉 칠해져 판 무늬를 덮었다. 은은한 발광이어야 한다.
        [Test]
        public void HighlightGlowsWithoutHidingTheTile()
        {
            (BattleState state, Board3D board) = BuildBoard();
            state.StartBattle();
            board.SyncFromState(state);
            var current = new Vector2Int(0, 1);
            Material material = board.TileRenderer(Team.Ally, current).sharedMaterial;
            Color emission = material.GetColor("_EmissionColor");
            Assert.Greater(emission.maxColorComponent, 0f, "still glows");
            Assert.LessOrEqual(emission.maxColorComponent, 0.45f, "faint enough to keep the texture visible");
        }

        // 빈 칸이 거의 검게 꺼져 바닥 구멍처럼 보였다.
        [Test]
        public void EmptyTileIsOnlySlightlyDimmed()
        {
            (BattleState state, Board3D board) = BuildBoard();
            state.StartBattle();
            board.SyncFromState(state);
            Color empty = BaseColorOf(board, Team.Enemy, new Vector2Int(1, 0));
            Color occupied = BaseColorOf(board, Team.Enemy, new Vector2Int(0, 0));
            Assert.GreaterOrEqual(empty.maxColorComponent, occupied.maxColorComponent * 0.75f);
        }

        [Test]
        public void TilesAreSlabsThickEnoughToReadAsPlates()
        {
            Assert.GreaterOrEqual(Board3D.TileThickness, 0.08f);
        }
    }
}
