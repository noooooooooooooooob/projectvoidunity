using UnityEngine;

namespace ProjectVoid.View
{
    /// <summary>카메라를 향해 돈다. yAxisOnly 면 세로축으로만 돌아 캐릭터가 뒤로 눕지 않는다 (Godot BILLBOARD_FIXED_Y).</summary>
    public sealed class Billboard : MonoBehaviour
    {
        public bool yAxisOnly;

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
                transform.rotation = Quaternion.LookRotation(forward);
            }
        }
    }
}
