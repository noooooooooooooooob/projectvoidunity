using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectVoid.View
{
    /// <summary>카드를 끄는 동안 들린 카드에서 커서까지 위로 휘는 조준 화살표 (Godot aim_arrow.gd). 클릭은 통과시킨다.</summary>
    public sealed class AimArrow : MaskableGraphic
    {
        public const float Width = 6f;
        public const float HeadSize = 18f;
        public const int Segments = 20;
        public const float ArcHeight = 120f;

        private Vector2 _from;
        private Vector2 _to;
        private bool _aiming;

        public bool IsAiming => _aiming;

        /// <summary>from → to 2차 베지어 곡선 위의 점들. 조절점은 두 끝 중 높은 쪽보다 arcHeight 위 (UI 좌표는 y 가 위로 커진다).</summary>
        public static List<Vector2> CurvePoints(Vector2 from, Vector2 to, int segments, float arcHeight)
        {
            var control = new Vector2((from.x + to.x) / 2f, Mathf.Max(from.y, to.y) + arcHeight);
            var points = new List<Vector2>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                points.Add(Vector2.Lerp(Vector2.Lerp(from, control, t), Vector2.Lerp(control, to, t), t));
            }
            return points;
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            color = new Color(1f, 1f, 1f, 0.9f);
        }

        public void Show(Vector2 fromScreen, Vector2 toScreen)
        {
            _from = ToLocal(fromScreen);
            _to = ToLocal(toScreen);
            _aiming = true;
            SetVerticesDirty();
        }

        public void Hide()
        {
            _aiming = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!_aiming)
            {
                return;
            }
            List<Vector2> points = CurvePoints(_from, _to, Segments, ArcHeight);
            Vector2 direction = (points[Segments] - points[Segments - 1]).normalized;
            // 선은 화살촉 밑동에서 끝나야 뾰족한 끝이 선에 묻히지 않는다.
            points[Segments] = _to - direction * HeadSize;
            for (int i = 0; i < Segments; i++)
            {
                AddSegment(vh, points[i], points[i + 1]);
            }
            Vector2 normal = new Vector2(-direction.y, direction.x);
            Vector2 headBase = _to - direction * HeadSize;
            int start = vh.currentVertCount;
            vh.AddVert(_to, color, Vector2.zero);
            vh.AddVert(headBase + normal * HeadSize * 0.6f, color, Vector2.zero);
            vh.AddVert(headBase - normal * HeadSize * 0.6f, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
        }

        private void AddSegment(VertexHelper vh, Vector2 a, Vector2 b)
        {
            Vector2 along = b - a;
            if (along.sqrMagnitude <= 0f)
            {
                return;
            }
            Vector2 side = new Vector2(-along.y, along.x).normalized * (Width / 2f);
            int start = vh.currentVertCount;
            vh.AddVert(a + side, color, Vector2.zero);
            vh.AddVert(b + side, color, Vector2.zero);
            vh.AddVert(b - side, color, Vector2.zero);
            vh.AddVert(a - side, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private Vector2 ToLocal(Vector2 screenPosition)
        {
            Canvas root = canvas != null ? canvas.rootCanvas : null;
            Camera eventCamera = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPosition, eventCamera, out Vector2 local);
            return local;
        }
    }
}
