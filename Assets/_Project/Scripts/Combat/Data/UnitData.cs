using UnityEngine;

namespace ProjectVoid.Combat
{
    /// <summary>아군·적군 공통 설계 데이터. AllyData / EnemyData 가 상속한다.</summary>
    public abstract class UnitData : ScriptableObject
    {
        public string id = "";
        public string displayName = "";
        public int maxHp = 10;
        public int speed = 10;
        // Godot 에는 없는 표현용 필드. 비어 있으면 임시 실루엣을 쓴다.
        public Sprite sprite;
    }
}
