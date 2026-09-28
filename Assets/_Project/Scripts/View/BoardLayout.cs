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
