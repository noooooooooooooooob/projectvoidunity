using NUnit.Framework;
using ProjectVoid.Combat;
using ProjectVoid.View;
using UnityEngine;

namespace ProjectVoid.Tests
{
    public class BattleEnvironmentTests
    {
        private GameObject _parent;
        private Texture2D _ground;
        private Texture2D _wall;

        [SetUp]
        public void SetUp()
        {
            _parent = new GameObject("EnvTestRoot");
            _ground = new Texture2D(4, 4);
            _wall = new Texture2D(4, 4);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_parent);
            Object.DestroyImmediate(_ground);
            Object.DestroyImmediate(_wall);
            Make.Cleanup();
        }

        private static BoardLayout Layout() => new BoardLayout(new Vector2Int(3, 3), new Vector2Int(2, 2));

        private BattleEnvironment Build(Texture2D ground, Texture2D wall)
        {
            EncounterData encounter = Make.Asset<EncounterData>();
            encounter.groundTexture = ground;
            encounter.wallTexture = wall;
            return BattleEnvironment.Build(_parent.transform, Layout(), encounter, TestAssets.Load().tileMaterial);
        }

        [Test]
        public void BuildsNothingWithoutTextures()
        {
            Assert.IsNull(Build(null, null));
        }

        [Test]
        public void GroundLiesUnderTheTilesAndCoversTheBoard()
        {
            BoardLayout layout = Layout();
            BattleEnvironment env = Build(_ground, null);
            Bounds bounds = env.Ground.GetComponent<Renderer>().bounds;
            Assert.LessOrEqual(bounds.max.y, -Board3D.TileThickness + 1e-3f, "ground is below the tile bottoms");
            Assert.Less(bounds.min.x, layout.MinX(), "covers the left edge");
            Assert.Greater(bounds.max.x, layout.MaxX(), "covers the right edge");
            Assert.Greater(bounds.max.z, layout.Depth() / 2f, "covers the far rows");
            Assert.AreSame(_ground, env.Ground.GetComponent<Renderer>().sharedMaterial.mainTexture, "ground shows its texture");
            Assert.IsNull(env.Ground.GetComponent<Collider>(), "ground must not catch board clicks");
            Assert.IsNull(env.BackWall, "no walls without a wall texture");
        }

        [Test]
        public void GroundIsDimmedSoUnitsStandOut()
        {
            Color tint = Build(_ground, null).Ground.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");
            Assert.Less(tint.maxColorComponent, 0.7f, "ground texture is darkened");
        }

        // 레퍼런스(무기미도)처럼 그림 한 장이 아니라 방: 가장 먼 행 뒤에 수직 벽이 선다.
        [Test]
        public void BackWallStandsUprightBehindTheFarRow()
        {
            BoardLayout layout = Layout();
            BattleEnvironment env = Build(_ground, _wall);
            Renderer wall = env.BackWall.GetComponent<Renderer>();
            Assert.Greater(wall.bounds.min.z, layout.Depth() / 2f, "behind the far row (row 0 is +z)");
            Assert.AreEqual(-Board3D.TileThickness, wall.bounds.min.y, 0.05f, "stands on the ground");
            Assert.Less(Vector3.Angle(env.BackWall.transform.forward, Vector3.forward), 1f, "upright, facing the board");
            Assert.Greater(wall.bounds.size.x, layout.Width(), "wider than the board");
            Assert.AreSame(_wall, wall.sharedMaterial.mainTexture, "wall shows its texture");
            Assert.Greater(wall.sharedMaterial.mainTextureScale.x, 1f, "texture repeats along the wall");
            Assert.IsNull(env.BackWall.GetComponent<Collider>(), "walls must not catch clicks");
        }

        [Test]
        public void SideWallsFlankTheBoard()
        {
            BoardLayout layout = Layout();
            BattleEnvironment env = Build(_ground, _wall);
            Assert.Less(env.LeftWall.GetComponent<Renderer>().bounds.max.x, layout.MinX(), "left wall is left of the ally side");
            Assert.Greater(env.RightWall.GetComponent<Renderer>().bounds.min.x, layout.MaxX(), "right wall is right of the enemy side");
        }

        // 벽이 고르게 밝아 평면적으로 보였다. 비스듬한 스포트라이트로 바닥에 빛 조각과 명암을 만든다.
        [Test]
        public void SpotLightsCastPoolsOfLight()
        {
            Light[] lights = Build(_ground, _wall).GetComponentsInChildren<Light>();
            Assert.GreaterOrEqual(lights.Length, 1, "at least one light shaft");
            foreach (Light light in lights)
            {
                Assert.AreEqual(LightType.Spot, light.type);
                Assert.Less(light.transform.forward.y, -0.3f, "shines down onto the floor");
            }
        }
    }
}
