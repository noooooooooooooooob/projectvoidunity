using System.Collections.Generic;
using UnityEngine;

namespace ProjectVoid.Combat
{
    /// <summary>전투 한 판의 구성. 격자 x = 열 수, y = 행 수. 리스트 순서대로 유닛 id 가 매겨진다 (아군 먼저).</summary>
    [CreateAssetMenu(menuName = "Project Void/Encounter", fileName = "Encounter")]
    public sealed class EncounterData : ScriptableObject
    {
        public Vector2Int allyGrid = new Vector2Int(3, 3);
        public Vector2Int enemyGrid = new Vector2Int(3, 3);
        public List<UnitPlacement> allyUnits = new List<UnitPlacement>();
        public List<UnitPlacement> enemyUnits = new List<UnitPlacement>();
    }
}
