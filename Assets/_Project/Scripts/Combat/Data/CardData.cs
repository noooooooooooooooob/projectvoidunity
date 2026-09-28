using UnityEngine;

namespace ProjectVoid.Combat
{
    /// <summary>카드 한 종류의 설계 데이터. 전투 중에는 이 에셋을 그대로 덱·손패·묘지에 넣어 돌려 쓰고 값은 바꾸지 않는다.</summary>
    [CreateAssetMenu(menuName = "Project Void/Card", fileName = "Card")]
    public sealed class CardData : ScriptableObject
    {
        public string id = "";
        public string displayName = "";
        public int spCost = 1;
        public AttackType attackType = AttackType.Melee;
        public Shape shape = Shape.Single;
        public int attackRange = 1;
        public int damage;
    }
}
