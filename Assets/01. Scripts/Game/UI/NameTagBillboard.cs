using UnityEngine;

namespace RealmForge.Game.UI
{
    public class NameTagBillboard : MonoBehaviour
    {
        private Camera mainCamera;

        private void Start()
        {
            mainCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            // 카메라를 향하도록 회전 (Y축만, 위아래 회전 제외)
            Vector3 lookDirection = mainCamera.transform.position - transform.position;
            lookDirection.y = 0; // Y축 성분 제거 (위아래 회전 방지)

            if (lookDirection.sqrMagnitude > 0.001f) // 거의 0이 아닐 때만 회전
            {
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }
    }
}
