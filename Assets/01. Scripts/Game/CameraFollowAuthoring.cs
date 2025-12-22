using Unity.Entities;
using UnityEngine;

public struct CameraFollowComponent : IComponentData
{
    public float EyeHeight;
    public float Distance;
    public float MouseSensitivity;
    public float PitchAngle; // 카메라 상하 각도
}

public class CameraFollowAuthoring : MonoBehaviour
{
    [Tooltip("플레이어 눈 높이")]
    public float eyeHeight = 1.7f;

    [Tooltip("플레이어로부터의 거리 (0 = 1인칭, 5 = 3인칭)")]
    [Range(0f, 15f)]
    public float distance = 0f;

    [Tooltip("마우스 감도")]
    [Range(0.1f, 10f)]
    public float mouseSensitivity = 2f;

    class Baker : Baker<CameraFollowAuthoring>
    {
        public override void Bake(CameraFollowAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new CameraFollowComponent
            {
                EyeHeight = authoring.eyeHeight,
                Distance = authoring.distance,
                MouseSensitivity = authoring.mouseSensitivity,
                PitchAngle = 0f
            });
        }
    }
}
