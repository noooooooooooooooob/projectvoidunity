using UnityEngine;

namespace ProjectVoid.Combat
{
    [CreateAssetMenu(menuName = "Project Void/Enemy", fileName = "Enemy")]
    public sealed class EnemyData : UnitData
    {
        public int attackDamage = 5;
        public AttackType attackType = AttackType.Melee;
        public Shape attackShape = Shape.Single;
        public int attackRange = 1;
        public int blockAmount = 5;
        public int restHeal = 4;
        // 0 이면 이동하지 않고 난수도 쓰지 않는다.
        public float moveChance = 0.25f;
    }
}
