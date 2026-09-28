using System.Collections.Generic;
using UnityEngine;

namespace ProjectVoid.Combat
{
    [CreateAssetMenu(menuName = "Project Void/Ally", fileName = "Ally")]
    public sealed class AllyData : UnitData
    {
        public int maxSp = 3;
        public List<CardData> deck = new List<CardData>();
    }
}
