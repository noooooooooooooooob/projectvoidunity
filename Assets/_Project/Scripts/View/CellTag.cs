using ProjectVoid.Combat;
using UnityEngine;

namespace ProjectVoid.View
{
    /// <summary>클릭 판정용 콜라이더에 붙여 "어느 편의 어느 칸인가"를 알려 준다 (Godot set_meta 대응).</summary>
    public sealed class CellTag : MonoBehaviour
    {
        public Team team;
        public Vector2Int cell;
    }
}
