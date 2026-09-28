using System;
using UnityEngine;

namespace ProjectVoid.Combat
{
    /// <summary>전투 방에 놓는 3D 소품 하나. position 은 월드 좌표의 바닥 위치(y 무시), height 는 맞출 높이(월드 단위).</summary>
    [Serializable]
    public sealed class PropPlacement
    {
        public GameObject prefab;
        public Vector3 position;
        public float yaw;
        public float height = 1f;
    }
}
