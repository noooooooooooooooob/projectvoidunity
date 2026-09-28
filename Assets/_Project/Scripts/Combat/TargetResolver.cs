using System.Collections.Generic;
using UnityEngine;

namespace ProjectVoid.Combat
{
    /// <summary>
    /// "누가 누구를 칠 수 있는가 / 누가 맞는가"를 계산하는 순수 계산기. 상태를 바꾸지 않는다.
    /// 좌표: 각 편이 자기 격자를 가진다. cell.x = 열 (0 이 상대와 가장 가까운 앞줄), cell.y = 행.
    /// </summary>
    public sealed class TargetResolver
    {
        // 위, 아래, 앞(적 쪽), 뒤 순서로 고정해 무작위 선택이 시드마다 재현되게 한다.
        public static readonly Vector2Int[] MoveDirections =
        {
            new Vector2Int(0, -1), new Vector2Int(0, 1), new Vector2Int(-1, 0), new Vector2Int(1, 0),
        };

        public TargetResolver(Vector2Int allyGrid, Vector2Int enemyGrid)
        {
            AllyGrid = allyGrid;
            EnemyGrid = enemyGrid;
        }

        public Vector2Int AllyGrid { get; }
        public Vector2Int EnemyGrid { get; }

        public int RowsFor(Team team) => team == Team.Ally ? AllyGrid.y : EnemyGrid.y;

        public Vector2Int GridFor(Team team) => team == Team.Ally ? AllyGrid : EnemyGrid;

        /// <summary>행 번호를 격자 가운데로부터의 거리로 바꾼다. 3행이면 0 → -1, 1 → 0, 2 → 1.</summary>
        public static float CenterOffset(int row, int rows) => row - (rows - 1) / 2f;

        public int Reach(Unit attacker, Unit target) => ReachCell(attacker, target.Team, target.Cell);

        /// <summary>거리 = (공격자 열 + 1 + 대상 열) + 내림(가운데 기준 행 차이). 칸에 유닛이 없어도 된다.</summary>
        public int ReachCell(Unit attacker, Team targetTeam, Vector2Int targetCell)
        {
            int colDistance = attacker.Cell.x + 1 + targetCell.x;
            float attackerOffset = CenterOffset(attacker.Cell.y, RowsFor(attacker.Team));
            float targetOffset = CenterOffset(targetCell.y, RowsFor(targetTeam));
            return colDistance + Mathf.FloorToInt(Mathf.Abs(attackerOffset - targetOffset));
        }

        public bool IsBlocked(Unit target, IReadOnlyList<Unit> allUnits) => IsCellBlocked(target.Team, target.Cell, allUnits);

        /// <summary>같은 편·같은 행에서 더 앞 열에 살아 있는 유닛이 있으면 근접 공격이 막힌다. 빈 칸도 막힐 수 있다.</summary>
        public bool IsCellBlocked(Team targetTeam, Vector2Int targetCell, IReadOnlyList<Unit> allUnits)
        {
            foreach (Unit unit in allUnits)
            {
                if (unit.Team != targetTeam || !unit.IsAlive)
                {
                    continue;
                }
                if (unit.Cell.y == targetCell.y && unit.Cell.x < targetCell.x)
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsValidTarget(Unit attacker, Unit target, AttackType attackType, int attackRange, IReadOnlyList<Unit> allUnits)
        {
            if (!target.IsAlive)
            {
                return false;
            }
            return IsValidCell(attacker, target.Team, target.Cell, attackType, attackRange, allUnits);
        }

        public bool IsValidCell(Unit attacker, Team targetTeam, Vector2Int targetCell, AttackType attackType, int attackRange, IReadOnlyList<Unit> allUnits)
        {
            if (targetTeam == attacker.Team)
            {
                return false;
            }
            if (ReachCell(attacker, targetTeam, targetCell) > attackRange)
            {
                return false;
            }
            if (attackType == AttackType.Melee && IsCellBlocked(targetTeam, targetCell, allUnits))
            {
                return false;
            }
            return true;
        }

        public List<Unit> ExpandShape(Unit primary, Shape shape, IReadOnlyList<Unit> allUnits)
            => ExpandShapeCell(primary.Team, primary.Cell, shape, allUnits);

        /// <summary>겨냥한 칸 기준으로 실제 맞는 살아 있는 유닛들. 겨냥한 칸이 비어 있어도 범위 안 다른 유닛은 맞는다.</summary>
        public List<Unit> ExpandShapeCell(Team anchorTeam, Vector2Int anchorCell, Shape shape, IReadOnlyList<Unit> allUnits)
        {
            var hit = new List<Unit>();
            if (shape == Shape.Single)
            {
                foreach (Unit unit in allUnits)
                {
                    if (unit.Team == anchorTeam && unit.Cell == anchorCell && unit.IsAlive)
                    {
                        hit.Add(unit);
                    }
                }
                return hit;
            }
            foreach (Unit unit in allUnits)
            {
                if (unit.Team != anchorTeam || !unit.IsAlive)
                {
                    continue;
                }
                bool inShape = shape switch
                {
                    Shape.Pierce => unit.Cell.y == anchorCell.y,
                    Shape.Sweep => unit.Cell.x == anchorCell.x,
                    Shape.Area => InArea(unit.Cell, anchorCell),
                    Shape.Line => unit.Cell.y == anchorCell.y && unit.Cell.x <= anchorCell.x,
                    _ => false,
                };
                if (inShape)
                {
                    hit.Add(unit);
                }
            }
            return hit;
        }

        /// <summary>범위가 덮는 칸 전부 (유닛 유무 무관, 격자 밖 제외). 호버 미리보기용.</summary>
        public List<Vector2Int> ShapeCells(Vector2Int anchor, Shape shape, Vector2Int grid)
        {
            var cells = new List<Vector2Int>();
            switch (shape)
            {
                case Shape.Single:
                    cells.Add(anchor);
                    break;
                case Shape.Pierce:
                    for (int x = 0; x < grid.x; x++)
                    {
                        cells.Add(new Vector2Int(x, anchor.y));
                    }
                    break;
                case Shape.Sweep:
                    for (int y = 0; y < grid.y; y++)
                    {
                        cells.Add(new Vector2Int(anchor.x, y));
                    }
                    break;
                case Shape.Area:
                    for (int dx = 0; dx < 2; dx++)
                    {
                        for (int dy = 0; dy < 2; dy++)
                        {
                            Vector2Int cell = anchor + new Vector2Int(dx, dy);
                            if (cell.x < grid.x && cell.y < grid.y)
                            {
                                cells.Add(cell);
                            }
                        }
                    }
                    break;
                case Shape.Line:
                    for (int x = 0; x < anchor.x + 1; x++)
                    {
                        cells.Add(new Vector2Int(x, anchor.y));
                    }
                    break;
            }
            return cells;
        }

        /// <summary>자기 편 격자 안, 살아 있는 유닛이 없는 상하좌우 칸 (MoveDirections 순서).</summary>
        public List<Vector2Int> MovableCells(Unit unit, IReadOnlyList<Unit> allUnits)
        {
            var cells = new List<Vector2Int>();
            Vector2Int grid = GridFor(unit.Team);
            foreach (Vector2Int direction in MoveDirections)
            {
                Vector2Int cell = unit.Cell + direction;
                if (cell.x < 0 || cell.y < 0 || cell.x >= grid.x || cell.y >= grid.y)
                {
                    continue;
                }
                if (Occupied(unit.Team, cell, allUnits))
                {
                    continue;
                }
                cells.Add(cell);
            }
            return cells;
        }

        private static bool InArea(Vector2Int cell, Vector2Int anchor)
            => cell.x >= anchor.x && cell.x <= anchor.x + 1 && cell.y >= anchor.y && cell.y <= anchor.y + 1;

        private static bool Occupied(Team team, Vector2Int cell, IReadOnlyList<Unit> allUnits)
        {
            foreach (Unit other in allUnits)
            {
                if (other.Team == team && other.Cell == cell && other.IsAlive)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
