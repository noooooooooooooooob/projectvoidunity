using System.Collections;
using UnityEngine;

namespace ProjectVoid.View
{
    /// <summary>원거리 공격의 화살. 쏜 쪽 가슴에서 맞을 쪽 가슴까지 낮은 포물선으로 날아가고, 그림은 화면에서 진행 방향을 가리킨다.</summary>
    public sealed class Projectile : MonoBehaviour
    {
        public const float MinFlight = 0.12f;
        public const float MaxFlight = 0.35f;
        public const float Speed = 16f;
        public const float ArcPerDistance = 0.06f;
        public const float Length = 0.8f;
        // 그림이 없을 때 쓰는 흰 줄무늬의 세로/가로 비.
        private const float FallbackAspect = 0.2f;
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static Texture2D _fallback;

        private Vector3 _from;
        private Vector3 _to;
        private float _arc;

        public float Duration { get; private set; }

        public static float FlightTime(float distance) => Mathf.Clamp(distance / Speed, MinFlight, MaxFlight);

        public static Vector3 PositionAt(Vector3 from, Vector3 to, float arcHeight, float t)
            => Vector3.Lerp(from, to, t) + Vector3.up * (4f * arcHeight * t * (1f - t));

        /// <summary>sprite 가 null 이면 코드로 만든 흰 줄무늬로 그린다.</summary>
        public static Projectile Spawn(Sprite sprite, Material material, Vector3 from, Vector3 to)
        {
            var root = new GameObject("Projectile");
            var projectile = root.AddComponent<Projectile>();
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Sprite";
            quad.transform.SetParent(root.transform, false);
            Collider quadCollider = quad.GetComponent<Collider>();
            quadCollider.enabled = false;
            if (Application.isPlaying)
            {
                Destroy(quadCollider);
            }
            else
            {
                DestroyImmediate(quadCollider);
            }
            Texture texture = sprite != null ? sprite.texture : Fallback();
            float aspect = sprite != null ? sprite.rect.height / sprite.rect.width : FallbackAspect;
            quad.transform.localScale = new Vector3(Length, Length * aspect, 1f);
            var quadMaterial = new Material(material);
            quadMaterial.SetTexture(BaseMapId, texture);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = quadMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            float distance = Vector3.Distance(from, to);
            projectile._from = from;
            projectile._to = to;
            projectile._arc = distance * ArcPerDistance;
            projectile.Duration = FlightTime(distance);
            projectile.Place(0f);
            return projectile;
        }

        public IEnumerator Fly()
        {
            yield return Coroutines.Tween(Duration, Place);
            Destroy(gameObject);
        }

        private void Place(float t)
        {
            Vector3 position = PositionAt(_from, _to, _arc, t);
            transform.position = position;
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }
            // 판은 카메라를 보게 두고, 그 평면 안에서 화면상 진행 방향으로 돌린다.
            Vector3 direction = PositionAt(_from, _to, _arc, t + 0.02f) - position;
            Transform view = camera.transform;
            float angle = Mathf.Atan2(Vector3.Dot(direction, view.up), Vector3.Dot(direction, view.right)) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.LookRotation(view.forward, view.up) * Quaternion.Euler(0f, 0f, angle);
        }

        private static Texture2D Fallback()
        {
            if (_fallback != null)
            {
                return _fallback;
            }
            const int width = 16;
            const int height = 4;
            _fallback = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool inside = y == 1 || y == 2;
                    _fallback.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            }
            _fallback.Apply();
            return _fallback;
        }
    }
}
