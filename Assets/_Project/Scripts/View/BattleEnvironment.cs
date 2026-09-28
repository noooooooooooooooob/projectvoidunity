using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.View
{
    /// <summary>
    /// 보드 아래 반복 바닥과 보드 뒤에 세운 배경 그림. 둘 다 3D 평면이라 카메라 원근을 받아 타일이 실제 바닥 위에 선 것처럼 보인다.
    /// </summary>
    public sealed class BattleEnvironment : MonoBehaviour
    {
        public const float GroundWidth = 40f;
        public const float GroundDepth = 24f;
        // 바닥 텍스처 한 장이 덮는 월드 크기.
        public const float GroundTileSize = 5f;
        // 바닥이 유닛보다 튀지 않게 어둡게 입힌다.
        public static readonly Color GroundTint = new Color(0.45f, 0.45f, 0.5f);
        // 뒷벽은 가장 먼 행에서 이만큼 뒤에 선다.
        public const float BackdropGap = 2.5f;
        // 카메라 거리에서 화면 위쪽을 넉넉히 덮는 크기.
        public const float BackdropHeight = 22f;

        public GameObject Ground { get; private set; }
        public GameObject Backdrop { get; private set; }

        /// <summary>인카운터에 바닥·뒷벽 텍스처가 하나도 없으면 null.</summary>
        public static BattleEnvironment Build(Transform parent, BoardLayout layout, EncounterData encounter, Material baseMaterial)
        {
            if (encounter.groundTexture == null && encounter.backdrop == null)
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
                GameObject ground = Plane("Ground", root.transform, encounter.groundTexture, baseMaterial);
                ground.transform.position = new Vector3(center.x, floorY, center.z);
                ground.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                ground.transform.localScale = new Vector3(GroundWidth, GroundDepth, 1f);
                Material material = ground.GetComponent<Renderer>().sharedMaterial;
                material.mainTextureScale = new Vector2(GroundWidth / GroundTileSize, GroundDepth / GroundTileSize);
                material.SetColor("_BaseColor", GroundTint);
                environment.Ground = ground;
            }

            if (encounter.backdrop != null)
            {
                float aspect = (float)encounter.backdrop.width / Mathf.Max(encounter.backdrop.height, 1);
                float width = BackdropHeight * aspect;
                GameObject backdrop = Plane("Backdrop", root.transform, encounter.backdrop, baseMaterial);
                // 0행이 +z 쪽(화면 안쪽)이다. 가장 먼 행 너머 바닥에 밑변을 대고,
                // 내려다보는 카메라를 정면으로 보게 뒤로 기울인다 (수직으로 세우면 그림 아랫부분만 납작하게 보인다).
                float z = layout.Depth() / 2f + BackdropGap;
                backdrop.transform.rotation = Quaternion.Euler(BattleRoot.CameraPitchDeg, 0f, 0f);
                Vector3 bottomEdge = new Vector3(center.x, floorY, z);
                backdrop.transform.position = bottomEdge + backdrop.transform.up * (BackdropHeight / 2f);
                backdrop.transform.localScale = new Vector3(width, BackdropHeight, 1f);
                environment.Backdrop = backdrop;
            }
            return environment;
        }

        private static GameObject Plane(string name, Transform parent, Texture2D texture, Material baseMaterial)
        {
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plane.name = name;
            plane.transform.SetParent(parent, false);
            // 배경은 클릭 판정에 끼면 안 된다.
            DestroyCollider(plane.GetComponent<Collider>());
            var material = new Material(baseMaterial) { mainTexture = texture };
            material.SetColor("_BaseColor", Color.white);
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
