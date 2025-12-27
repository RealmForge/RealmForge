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

            // 현재 transform의 up 방향을 유지하면서 카메라를 향하도록 회전
            Vector3 toCamera = mainCamera.transform.position - transform.position;

            // 현재 up 방향(플레이어의 로컬 up)을 기준으로 카메라 방향 투영
            Vector3 currentUp = transform.up;
            Vector3 projectedDirection = toCamera - Vector3.Dot(toCamera, currentUp) * currentUp;

            if (projectedDirection.sqrMagnitude > 0.001f)
            {
                // up 방향을 유지하면서 카메라를 향하도록 회전
                transform.rotation = Quaternion.LookRotation(projectedDirection, currentUp);
            }
        }
    }
}
