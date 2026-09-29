using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.View
{
    /// <summary>전투 효과음 묶음. 공격 종류·피해·처치에 맞는 클립을 고른다. 비어 있는 칸은 null 이라 무음이다.</summary>
    [CreateAssetMenu(menuName = "Project Void/Battle Sounds")]
    public sealed class BattleSounds : ScriptableObject
    {
        public AudioClip meleeSwing;
        public AudioClip meleeHit;
        public AudioClip rangedShot;
        public AudioClip rangedHit;
        public AudioClip blocked;
        public AudioClip kill;

        public AudioClip ForAttack(AttackType type) => type == AttackType.Ranged ? rangedShot : meleeSwing;

        public AudioClip ForImpact(AttackType type, int amount, bool died)
        {
            if (died)
            {
                return kill;
            }
            if (amount <= 0)
            {
                return blocked;
            }
            return type == AttackType.Ranged ? rangedHit : meleeHit;
        }
    }
}
