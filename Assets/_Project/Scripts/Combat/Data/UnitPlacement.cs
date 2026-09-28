using System;
using UnityEngine;

namespace ProjectVoid.Combat
{
    /// <summary>"어떤 유닛이 자기 편 격자의 어느 칸에 서는가" 한 줄. cell.x = 열(0 이 앞줄), cell.y = 행.</summary>
    [Serializable]
    public sealed class UnitPlacement
    {
        public UnitData unitData;
        public Vector2Int cell;

        public UnitPlacement() { }

        public UnitPlacement(UnitData unitData, Vector2Int cell)
        {
            this.unitData = unitData;
            this.cell = cell;
        }
    }
}
