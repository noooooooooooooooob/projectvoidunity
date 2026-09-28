namespace ProjectVoid.Combat
{
    // 순서는 Godot 원본 enum 과 같다 (데이터 이식 시 숫자 값이 맞아야 한다).
    public enum Team { Ally, Enemy }
    public enum AttackType { Melee, Ranged }
    public enum Shape { Single, Pierce, Sweep, Area, Line }
    public enum Phase { Standby, Draw, Action, BeforeEnd, AfterEnd }
    public enum EnemyAction { Attack, Defend, Rest, Move }
}
