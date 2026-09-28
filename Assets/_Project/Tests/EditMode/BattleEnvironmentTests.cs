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
        private Texture2D _backdrop;

        [SetUp]
        public void SetUp()
        {
            _parent = new GameObject("EnvTestRoot");
            _ground = new Texture2D(4, 4);
            _backdrop = new Texture2D(4, 4);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_parent);
            Object.DestroyImmediate(_ground);
            Object.DestroyImmediate(_backdrop);
            Make.Cleanup();
        }

        private static BoardLayout Layout() => new BoardLayout(new Vector2Int(3, 3), new Vector2Int(2, 2));

        private EncounterData Encounter(Texture2D ground, Texture2D backdrop)
        {
            EncounterData encounter = Make.Asset<EncounterData>();
            encounter.groundTexture = ground;
            encounter.backdrop = backdrop;
            return encounter;
        }

        [Test]
        public void BuildsNothingWithoutTextures()
        {
            Assert.IsNull(BattleEnvironment.Build(_parent.transform, Layout(), Encounter(null, null), TestAssets.Load().tileMaterial));
        }

        [Test]
        public void GroundLiesUnderTheTilesAndCoversTheBoard()
        {
            BoardLayout layout = Layout();
            BattleEnvironment env = BattleEnvironment.Build(_parent.transform, layout, Encounter(_ground, null), TestAssets.Load().tileMaterial);
            Bounds bounds = env.Ground.GetComponent<Renderer>().bounds;
            Assert.LessOrEqual(bounds.max.y, -Board3D.TileThickness + 1e-3f, "ground is below the tile bottoms");
            Assert.Less(bounds.min.x, layout.MinX(), "covers the left edge");
            Assert.Greater(bounds.max.x, layout.MaxX(), "covers the right edge");
            Assert.Greater(bounds.max.z, layout.Depth() / 2f, "covers the far rows");
            Assert.AreSame(_ground, env.Ground.GetComponent<Renderer>().sharedMaterial.mainTexture, "ground shows its texture");
            Assert.IsNull(env.Ground.GetComponent<Collider>(), "ground must not catch board clicks");
            Assert.IsNull(env.Backdrop, "no backdrop without its texture");
        }

        [Test]
        public void BackdropStandsBehindTheFarRowOnTheGround()
        {
            BoardLayout layout = Layout();
            BattleEnvironment env = BattleEnvironment.Build(_parent.transform, layout, Encounter(_ground, _backdrop), TestAssets.Load().tileMaterial);
            Bounds bounds = env.Backdrop.GetComponent<Renderer>().bounds;
            Assert.Greater(bounds.min.z, layout.Depth() / 2f, "behind the far row (row 0 is +z)");
            Assert.AreEqual(-Board3D.TileThickness, bounds.min.y, 0.05f, "stands on the ground");
            Assert.Greater(bounds.size.x, layout.Width(), "wider than the board");
            Assert.AreSame(_backdrop, env.Backdrop.GetComponent<Renderer>().sharedMaterial.mainTexture, "backdrop shows its picture");
            Assert.IsNull(env.Backdrop.GetComponent<Collider>(), "backdrop must not catch clicks");
        }

        // 수직으로 세우면 44° 로 내려다보는 카메라에 그림 아랫부분만 납작하게 보였다. 카메라를 정면으로 보게 기울인다.
        [Test]
        public void BackdropFacesTheCamera()
        {
            BattleEnvironment env = BattleEnvironment.Build(_parent.transform, Layout(), Encounter(null, _backdrop), TestAssets.Load().tileMaterial);
            float pitch = BattleRoot.CameraPitchDeg * Mathf.Deg2Rad;
            var cameraForward = new Vector3(0f, -Mathf.Sin(pitch), Mathf.Cos(pitch));
            Assert.Less(Vector3.Angle(env.Backdrop.transform.forward, cameraForward), 1f, "picture plane is perpendicular to the view");
        }

        [Test]
        public void GroundIsDimmedSoUnitsStandOut()
        {
            BattleEnvironment env = BattleEnvironment.Build(_parent.transform, Layout(), Encounter(_ground, null), TestAssets.Load().tileMaterial);
            Color tint = env.Ground.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");
            Assert.Less(tint.maxColorComponent, 0.7f, "ground texture is darkened");
        }
    }
}
