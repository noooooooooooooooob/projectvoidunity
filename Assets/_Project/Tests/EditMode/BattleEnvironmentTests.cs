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

        // AI 로 만든 3D 소품은 원본 크기가 제각각이다. 지정한 높이로 맞추고 바닥에 밑면을 붙인다.
        [Test]
        public void PropsAreScaledToHeightAndRestOnTheFloor()
        {
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab.transform.SetParent(_parent.transform);
            EncounterData encounter = Make.Asset<EncounterData>();
            encounter.props.Add(new PropPlacement { prefab = prefab, position = new Vector3(-5f, 0f, 3f), yaw = 30f, height = 2f });
            BattleEnvironment env = BattleEnvironment.Build(_parent.transform, Layout(), encounter, TestAssets.Load().tileMaterial);
            Assert.AreEqual(1, env.Props.Count);
            GameObject prop = env.Props[0];
            Bounds bounds = prop.GetComponentInChildren<Renderer>().bounds;
            Assert.AreEqual(2f, bounds.size.y, 0.01f, "scaled to the requested height");
            Assert.AreEqual(-Board3D.TileThickness, bounds.min.y, 0.01f, "rests on the floor");
            Assert.AreEqual(-5f, prop.transform.position.x, 0.01f);
            Assert.AreEqual(3f, prop.transform.position.z, 0.01f);
            Assert.AreEqual(30f, prop.transform.eulerAngles.y, 0.01f);
            Assert.AreEqual(0, prop.GetComponentsInChildren<Collider>().Length, "props must not catch board clicks");
            Assert.AreNotEqual(UnityEngine.Rendering.ShadowCastingMode.Off, prop.GetComponentInChildren<Renderer>().shadowCastingMode);
        }

        // 버그: AI 모델은 루트에 세우는 회전(270,90,0)이 들어 있는데, yaw 로 덮어써서 드럼통·선풍기가 누웠다.
        [Test]
        public void PropKeepsTheModelsOwnUprightRotation()
        {
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab.transform.SetParent(_parent.transform);
            prefab.transform.rotation = Quaternion.Euler(270f, 90f, 0f);
            EncounterData encounter = Make.Asset<EncounterData>();
            encounter.props.Add(new PropPlacement { prefab = prefab, position = Vector3.zero, yaw = 45f, height = 1f });
            GameObject prop = BattleEnvironment.Build(_parent.transform, Layout(), encounter, TestAssets.Load().tileMaterial).Props[0];
            Quaternion expected = Quaternion.Euler(0f, 45f, 0f) * Quaternion.Euler(270f, 90f, 0f);
            Assert.Less(Quaternion.Angle(expected, prop.transform.rotation), 0.1f, "yaw is applied on top of the model's own rotation");
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
