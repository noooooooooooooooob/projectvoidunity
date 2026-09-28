using System.Collections.Generic;
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
        // 벽 텍스처 아래쪽에 그려진 바닥 줄을 잘라낸다 (바닥이 두 번 보이지 않게).
        public const float WallFloorCrop = 0.12f;
        // 벽 위로 갈수록 어두워지는 그늘의 최대 불투명도 (천장 아래 어둠).
        public const float WallShadeAlpha = 0.85f;
        // 빛기둥은 뒤의 유닛을 가리지 않을 만큼만 옅게.
        private static readonly Color BeamColor = new Color(1f, 0.9f, 0.7f, 0.06f);
        private static readonly Color DustColor = new Color(1f, 0.92f, 0.8f, 0.3f);
        private static readonly Color LightColor = new Color(1f, 0.9f, 0.75f);

        public GameObject Ground { get; private set; }
        public GameObject BackWall { get; private set; }
        public GameObject LeftWall { get; private set; }
        public GameObject RightWall { get; private set; }
        public List<GameObject> Props { get; } = new List<GameObject>();
        public List<SpriteRenderer> WallShades { get; } = new List<SpriteRenderer>();
        public List<SpriteRenderer> LightShafts { get; } = new List<SpriteRenderer>();

        /// <summary>인카운터에 바닥·벽 텍스처와 소품이 하나도 없으면 null.</summary>
        public static BattleEnvironment Build(Transform parent, BoardLayout layout, EncounterData encounter, Material baseMaterial)
        {
            if (encounter.groundTexture == null && encounter.wallTexture == null && encounter.props.Count == 0)
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

                environment.BackWall = environment.Wall("BackWall", root.transform, encounter.wallTexture, baseMaterial,
                    new Vector3(center.x, floorY, backZ), Quaternion.identity, rightX - leftX);
                // 옆벽은 안쪽(보드 쪽)을 향하게 돌린다.
                environment.LeftWall = environment.Wall("LeftWall", root.transform, encounter.wallTexture, baseMaterial,
                    new Vector3(leftX, floorY, frontZ + sideLength / 2f), Quaternion.Euler(0f, -90f, 0f), sideLength);
                environment.RightWall = environment.Wall("RightWall", root.transform, encounter.wallTexture, baseMaterial,
                    new Vector3(rightX, floorY, frontZ + sideLength / 2f), Quaternion.Euler(0f, 90f, 0f), sideLength);

                // 뒷벽 위쪽 창문에서 비스듬히 들어오는 빛줄기 두 개 (레퍼런스의 바닥 빛 조각).
                environment.LightShaft(root.transform, new Vector3(leftX + (rightX - leftX) * 0.3f, WallHeight, backZ - 0.5f), center + new Vector3(-1.5f, floorY, 0.5f));
                environment.LightShaft(root.transform, new Vector3(leftX + (rightX - leftX) * 0.75f, WallHeight, backZ - 0.5f), center + new Vector3(2.5f, floorY, -0.5f));
            }

            foreach (PropPlacement placement in encounter.props)
            {
                if (placement.prefab != null)
                {
                    environment.Props.Add(Prop(root.transform, placement, floorY));
                }
            }
            return environment;
        }

        private static GameObject Prop(Transform parent, PropPlacement placement, float floorY)
        {
            GameObject prop = Instantiate(placement.prefab, parent);
            prop.name = placement.prefab.name;
            // AI 모델 루트에는 세우는 회전이 들어 있다. 그 위에 yaw 를 더해야 눕지 않는다.
            prop.transform.rotation = Quaternion.Euler(0f, placement.yaw, 0f) * placement.prefab.transform.rotation;
            prop.transform.position = new Vector3(placement.position.x, floorY, placement.position.z);
            // AI 로 만든 모델은 원본 크기가 제각각이라 지정한 높이로 맞춘다.
            Bounds bounds = RendererBounds(prop);
            if (bounds.size.y > 0f)
            {
                prop.transform.localScale *= placement.height / bounds.size.y;
            }
            bounds = RendererBounds(prop);
            prop.transform.position += Vector3.up * (floorY - bounds.min.y);
            foreach (Collider collider in prop.GetComponentsInChildren<Collider>())
            {
                DestroyCollider(collider);
            }
            foreach (Renderer renderer in prop.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            return prop;
        }

        private static Bounds RendererBounds(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(target.transform.position, Vector3.zero);
            }
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        private GameObject Wall(string name, Transform parent, Texture2D texture, Material baseMaterial,
            Vector3 bottomCenter, Quaternion rotation, float length)
        {
            GameObject wall = Plane(name, parent, texture, baseMaterial, WallTint);
            wall.transform.rotation = rotation;
            wall.transform.position = bottomCenter + Vector3.up * (WallHeight / 2f);
            wall.transform.localScale = new Vector3(length, WallHeight, 1f);
            Material material = wall.GetComponent<Renderer>().sharedMaterial;
            material.mainTextureScale = new Vector2(length / WallTileSize, WallHeight / WallTileSize * (1f - WallFloorCrop));
            material.mainTextureOffset = new Vector2(0f, WallFloorCrop);

            // 벽 바로 앞(보드 쪽)에 위로 갈수록 짙어지는 그늘을 덮어 천장 아래 어둠을 흉내 낸다.
            var shadeObject = new GameObject(name + "Shade");
            shadeObject.transform.SetParent(parent, false);
            shadeObject.transform.rotation = rotation;
            shadeObject.transform.position = wall.transform.position - wall.transform.forward * 0.02f;
            shadeObject.transform.localScale = new Vector3(length, WallHeight, 1f);
            var shade = shadeObject.AddComponent<SpriteRenderer>();
            shade.sprite = GradientSprite(t => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, t)) * WallShadeAlpha);
            shade.color = Color.black;
            WallShades.Add(shade);
            return wall;
        }

        private void LightShaft(Transform parent, Vector3 position, Vector3 target)
        {
            SpotLight(parent, position, target);

            // 빛기둥: 빛에서 바닥까지 이어지는 옅은 판. 빛 쪽이 진하고 바닥 쪽으로 옅어진다.
            Vector3 up = (position - target).normalized;
            var beamObject = new GameObject("LightBeam");
            beamObject.transform.SetParent(parent, false);
            beamObject.transform.position = (position + target) / 2f;
            Vector3 facing = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
            beamObject.transform.rotation = Quaternion.LookRotation(facing, up);
            beamObject.transform.localScale = new Vector3(1.8f, Vector3.Distance(position, target), 1f);
            var beam = beamObject.AddComponent<SpriteRenderer>();
            // 좌우로 부드럽게 사라져야 딱딱한 직사각형으로 보이지 않는다.
            beam.sprite = GradientSprite(t => Mathf.Lerp(0.15f, 1f, t), softSides: true);
            beam.color = BeamColor;
            LightShafts.Add(beam);

            Dust(parent, target + Vector3.up * 1.5f);
        }

        // 빛 속에 천천히 떠다니는 먼지.
        private static void Dust(Transform parent, Vector3 center)
        {
            var dustObject = new GameObject("Dust");
            dustObject.transform.SetParent(parent, false);
            dustObject.transform.position = center;
            var particles = dustObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = 6f;
            main.startSpeed = 0.05f;
            main.startSize = 0.02f;
            main.startColor = DustColor;
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 8f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2f, 3f, 2f);
            var particleRenderer = dustObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = DustMaterial();
        }

        private static Material _dustMaterial;

        private static Material DustMaterial()
        {
            if (_dustMaterial != null)
            {
                return _dustMaterial;
            }
            _dustMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            // 반투명으로 섞는다 (기본값은 불투명 사각형).
            _dustMaterial.SetFloat("_Surface", 1f);
            _dustMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _dustMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            _dustMaterial.SetFloat("_ZWrite", 0f);
            _dustMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            _dustMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return _dustMaterial;
        }

        /// <summary>세로 그라데이션 흰 스프라이트 (1 유닛 정사각). alphaAt(0=아래..1=위) 로 알파를 정한다. 색은 SpriteRenderer.color 로 곱한다.</summary>
        private static Sprite GradientSprite(System.Func<float, float> alphaAt, bool softSides = false)
        {
            // 정사각이어야 스프라이트가 1×1 유닛이 되어 scale 이 곧 월드 크기가 된다.
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                float alpha = alphaAt(y / (size - 1f));
                for (int x = 0; x < size; x++)
                {
                    // 가운데 1 → 가장자리 0 으로 매끄럽게 줄어드는 가로 감쇠.
                    float side = softSides ? Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(x / (size - 1f) * 2f - 1f)) : 1f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * side));
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
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
