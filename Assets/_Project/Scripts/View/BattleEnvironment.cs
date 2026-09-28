using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.View
{
    /// <summary>
    /// 보드를 둘러싼 방 디오라마: 반복 바닥, 뒷벽·좌우 벽, 비스듬한 스포트라이트.
    /// 모두 3D 라서 카메라 원근과 조명을 받아 타일이 실제 방 바닥 위에 놓인 것처럼 보인다.
    /// </summary>
    public sealed class BattleEnvironment : MonoBehaviour
    {
        public const float GroundWidth = 40f;
        public const float GroundDepth = 24f;
        // 바닥 텍스처 한 장이 덮는 월드 크기.
        public const float GroundTileSize = 5f;
        // 바닥이 유닛보다 튀지 않게 어둡게 입힌다.
        public static readonly Color GroundTint = new Color(0.45f, 0.45f, 0.5f);
        // 44° 카메라에는 벽 아래쪽 약 4 단위만 보인다. 창문(텍스처 윗부분)이 화면에 들어오도록 낮게 둔다.
        public const float WallHeight = 4f;
        // 벽 텍스처 한 장이 덮는 월드 크기 (정사각).
        public const float WallTileSize = 4f;
        // 뒷벽은 가장 먼 행에서, 옆벽은 보드 좌우 끝에서 이만큼 떨어진다.
        public const float BackWallGap = 2f;
        public const float SideWallGap = 3f;
        public static readonly Color WallTint = new Color(0.8f, 0.8f, 0.85f);
        private static readonly Color LightColor = new Color(1f, 0.9f, 0.75f);

        public GameObject Ground { get; private set; }
        public GameObject BackWall { get; private set; }
        public GameObject LeftWall { get; private set; }
        public GameObject RightWall { get; private set; }

        /// <summary>인카운터에 바닥·벽 텍스처가 하나도 없으면 null.</summary>
        public static BattleEnvironment Build(Transform parent, BoardLayout layout, EncounterData encounter, Material baseMaterial)
        {
            if (encounter.groundTexture == null && encounter.wallTexture == null)
            {
                return null;
            }
            var root = new GameObject("Environment");
            root.transform.SetParent(parent, false);
            var environment = root.AddComponent<BattleEnvironment>();
            Vector3 center = layout.Center();
            float floorY = -Board3D.TileThickness;

            if (encounter.groundTexture != null)
            {
                GameObject ground = Plane("Ground", root.transform, encounter.groundTexture, baseMaterial, GroundTint);
                ground.transform.position = new Vector3(center.x, floorY, center.z);
                ground.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                ground.transform.localScale = new Vector3(GroundWidth, GroundDepth, 1f);
                ground.GetComponent<Renderer>().sharedMaterial.mainTextureScale =
                    new Vector2(GroundWidth / GroundTileSize, GroundDepth / GroundTileSize);
                environment.Ground = ground;
            }

            if (encounter.wallTexture != null)
            {
                // 0행이 +z 쪽(화면 안쪽)이다. 뒷벽은 가장 먼 행 너머, 옆벽은 보드 좌우 끝 너머에 선다.
                float backZ = layout.Depth() / 2f + BackWallGap;
                float leftX = layout.MinX() - SideWallGap;
                float rightX = layout.MaxX() + SideWallGap;
                float frontZ = center.z - GroundDepth / 2f;
                float sideLength = backZ - frontZ;

                environment.BackWall = Wall("BackWall", root.transform, encounter.wallTexture, baseMaterial,
                    new Vector3(center.x, floorY, backZ), Quaternion.identity, rightX - leftX);
                // 옆벽은 안쪽(보드 쪽)을 향하게 돌린다.
                environment.LeftWall = Wall("LeftWall", root.transform, encounter.wallTexture, baseMaterial,
                    new Vector3(leftX, floorY, frontZ + sideLength / 2f), Quaternion.Euler(0f, -90f, 0f), sideLength);
                environment.RightWall = Wall("RightWall", root.transform, encounter.wallTexture, baseMaterial,
                    new Vector3(rightX, floorY, frontZ + sideLength / 2f), Quaternion.Euler(0f, 90f, 0f), sideLength);

                // 뒷벽 위쪽 창문에서 비스듬히 들어오는 빛줄기 두 개 (레퍼런스의 바닥 빛 조각).
                SpotLight(root.transform, new Vector3(leftX + (rightX - leftX) * 0.3f, WallHeight, backZ - 0.5f), center + new Vector3(-1.5f, 0f, 0.5f));
                SpotLight(root.transform, new Vector3(leftX + (rightX - leftX) * 0.75f, WallHeight, backZ - 0.5f), center + new Vector3(2.5f, 0f, -0.5f));
            }
            return environment;
        }

        private static GameObject Wall(string name, Transform parent, Texture2D texture, Material baseMaterial,
            Vector3 bottomCenter, Quaternion rotation, float length)
        {
            GameObject wall = Plane(name, parent, texture, baseMaterial, WallTint);
            wall.transform.rotation = rotation;
            wall.transform.position = bottomCenter + Vector3.up * (WallHeight / 2f);
            wall.transform.localScale = new Vector3(length, WallHeight, 1f);
            wall.GetComponent<Renderer>().sharedMaterial.mainTextureScale = new Vector2(length / WallTileSize, WallHeight / WallTileSize);
            return wall;
        }

        private static void SpotLight(Transform parent, Vector3 position, Vector3 target)
        {
            var lightObject = new GameObject("LightShaft");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.position = position;
            lightObject.transform.LookAt(target);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = LightColor;
            light.intensity = 25f;
            light.range = 20f;
            light.spotAngle = 35f;
            light.innerSpotAngle = 15f;
            light.shadows = LightShadows.Soft;
        }

        private static GameObject Plane(string name, Transform parent, Texture2D texture, Material baseMaterial, Color tint)
        {
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plane.name = name;
            plane.transform.SetParent(parent, false);
            // 배경은 클릭 판정에 끼면 안 된다.
            DestroyCollider(plane.GetComponent<Collider>());
            var material = new Material(baseMaterial) { mainTexture = texture };
            material.SetColor("_BaseColor", tint);
            material.SetColor("_EmissionColor", Color.black);
            plane.GetComponent<Renderer>().sharedMaterial = material;
            return plane;
        }

        private static void DestroyCollider(Collider collider)
        {
            if (Application.isPlaying)
            {
                // Destroy 는 프레임 끝에 지워지므로 그 전에 판정에서 빠지도록 꺼 둔다.
                collider.enabled = false;
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }
        }
    }
}
