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
        // 64×64 프레임을 가로로 이어 붙인 애니메이션 띠 (프레임 수 = 너비 / 높이). 비어 있으면 sprite 한 장 + 코드 모션.
        public Texture2D idleSheet;
        public Texture2D attackSheet;
        public Texture2D hitSheet;
    }
}
