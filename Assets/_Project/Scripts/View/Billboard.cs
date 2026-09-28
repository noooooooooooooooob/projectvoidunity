using UnityEngine;

namespace ProjectVoid.View
{
    /// <summary>
    /// 카메라를 향해 돈다. yAxisOnly 면 세로축으로만 돌아 캐릭터가 뒤로 눕지 않는다 (Godot BILLBOARD_FIXED_Y).
    /// tiltDeg 만큼은 윗부분을 카메라 반대쪽으로 눕혀, 내려다보는 카메라에 판이 덜 눌려 보이게 한다 (발이 축).
    /// </summary>
    public sealed class Billboard : MonoBehaviour
    {
        public bool yAxisOnly;
        public float tiltDeg;

        private void LateUpdate()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }
            if (!yAxisOnly)
            {
                transform.rotation = camera.transform.rotation;
                return;
            }
            Vector3 forward = camera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0f)
            {
                transform.rotation = Quaternion.LookRotation(forward) * Quaternion.Euler(tiltDeg, 0f, 0f);
            }
        }
    }
}
