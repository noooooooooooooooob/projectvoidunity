using System;
using UnityEngine;

namespace ProjectVoid.Combat
{
    /// <summary>
    /// 전투 방에 놓는 소품 하나. sprite 가 있으면 유닛처럼 카메라를 향한 2D 판으로, 없으면 prefab 3D 모델로 놓는다.
    /// position 은 월드 좌표의 바닥 위치(y 무시), height 는 맞출 높이(월드 단위). yaw 는 3D 모델에만 쓴다.
    /// </summary>
    [Serializable]
    public sealed class PropPlacement
    {
        public Sprite sprite;
        public GameObject prefab;
        public Vector3 position;
        public float yaw;
        public float height = 1f;
    }
}
