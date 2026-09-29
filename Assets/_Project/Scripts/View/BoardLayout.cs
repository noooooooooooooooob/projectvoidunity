using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.View
{
    /// <summary>
    /// 격자 칸 → 월드 좌표. 아군은 왼쪽(-x), 적은 오른쪽(+x), 양쪽 모두 col 0 이 가운데 틈 쪽.
    /// 행은 격자 가운데를 z=0 에 맞추고, 0행이 화면 안쪽(+z)이다 (카메라가 -Z 쪽에서 본다). 미러링은 여기서만 한다.
    /// </summary>
    public sealed class BoardLayout
    {
        public const float TileSize = 1f;
        public const float CellPitch = 1.1f;
        public const float SideGap = 1.5f;
        public const float FlatRowScale = 0.6f;
        public const float FlatDepthPerUnit = 0.1f;
        // 2D 바닥 원근: 앞 가장자리에서 이만큼(레이아웃 단위) 들어가면 크기가 절반이 되는 정도의 세기,
        // 그리고 소실점이 앞 가장자리보다 얼마나 위(화면 단위)에 있는지. 배경 바닥 원근선에 맞춘 값.
        public const float FlatPerspectiveDepth = 10f;
        public const float FlatVanishHeight = 9f;

        /// <summary>2D 에서 레이아웃 깊이 z 의 바닥이 그려지는 크기 (앞 가장자리 = 1, 뒤로 갈수록 작다).</summary>
        public float FlatScaleAt(float layoutZ) => FlatPerspectiveDepth / (FlatPerspectiveDepth + layoutZ + Depth() / 2f);

        /// <summary>
        /// 2D(XY 화면 평면) 바닥 위의 점: 보드 가운데 위쪽 소실점을 향해 모이는 한 점 원근.
        /// 먼 곳(+z)일수록 화면 위로 오르고 가운데로 모이며, 조금 더 깊이 둬서 앞 행 유닛이 그 위에 그려지게 한다.
        /// </summary>
        public Vector3 FlatFloorPoint(float layoutX, float layoutZ)
        {
            float scale = FlatScaleAt(layoutZ);
            float center = Center().x;
            float nearY = -Depth() / 2f * FlatRowScale;
            return new Vector3(center + (layoutX - center) * scale, nearY + FlatVanishHeight * (1f - scale), layoutZ * FlatDepthPerUnit);
        }

        /// <summary>보드 폭·높이가 margin 배 여유를 두고 들어오는 정사영 카메라 크기 (화면 세로의 절반).</summary>
        public static float FlatOrthoSize(float width, float height, float aspect, float margin)
            => Mathf.Max(height * margin / 2f, width * margin / 2f / aspect);

        public Vector3 WorldCell(Team team, Vector2Int cell, BoardProjection projection)
        {
            Vector3 position = CellPosition(team, cell);
            return projection == BoardProjection.Flat2D ? FlatFloorPoint(position.x, position.z) : position;
        }

        public BoardLayout(Vector2Int allyGrid, Vector2Int enemyGrid)
        {
            AllyGrid = allyGrid;
            EnemyGrid = enemyGrid;
        }

        public Vector2Int AllyGrid { get; }
        public Vector2Int EnemyGrid { get; }

        /// <summary>칸 윗면 중앙 (y = 0 이 바닥).</summary>
        public Vector3 CellPosition(Team team, Vector2Int cell)
        {
            float side = team == Team.Ally ? -1f : 1f;
            int rows = team == Team.Ally ? AllyGrid.y : EnemyGrid.y;
            float x = side * (SideGap / 2f + CellPitch / 2f + cell.x * CellPitch);
            float z = -TargetResolver.CenterOffset(cell.y, rows) * CellPitch;
            return new Vector3(x, 0f, z);
        }

        public float MinX() => -(SideGap / 2f + AllyGrid.x * CellPitch);

        public float MaxX() => SideGap / 2f + EnemyGrid.x * CellPitch;

        public float Width() => MaxX() - MinX();

        public float Depth() => Mathf.Max(AllyGrid.y, EnemyGrid.y) * CellPitch;

        public Vector3 Center() => new Vector3((MinX() + MaxX()) / 2f, 0f, 0f);

        /// <summary>보드 폭과 깊이가 margin 배 여유를 두고 화면에 들어오는 카메라 거리 (둘 중 먼 쪽).</summary>
        public static float CameraDistance(float boardWidth, float boardDepth, float verticalFovDeg, float aspect, float margin)
        {
            float halfVertical = verticalFovDeg * Mathf.Deg2Rad / 2f;
            float halfHorizontal = Mathf.Atan(Mathf.Tan(halfVertical) * aspect);
            float forWidth = boardWidth * margin / 2f / Mathf.Tan(halfHorizontal);
            float forDepth = boardDepth * margin / 2f / Mathf.Tan(halfVertical);
            return Mathf.Max(forWidth, forDepth);
        }
    }
}
