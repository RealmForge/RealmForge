using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

// 플레이어 회전 시스템 (마우스 입력)
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[UpdateBefore(typeof(PlayerMovementSystem))]
public partial struct PlayerRotationSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CameraFollowComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 카메라 설정 (감도, Pitch 각도 저장용)
        RefRW<CameraFollowComponent> cameraSettings = default;
        bool foundSettings = false;
        foreach (var settings in SystemAPI.Query<RefRW<CameraFollowComponent>>())
        {
            cameraSettings = settings;
            foundSettings = true;
            break;
        }
        if (!foundSettings)
            return;

        var sensitivity = cameraSettings.ValueRO.MouseSensitivity;

        // 로컬 플레이어만 회전
        foreach (var (transform, groundState, playerInput) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<PlayerGroundState>, RefRO<PlayerInput>>()
            .WithAll<Simulate, GhostOwnerIsLocal, PlayerComponent>())
        {
            var playerUp = groundState.ValueRO.GroundNormal;
            var mouseX = playerInput.ValueRO.mouseDelta.x;
            var mouseY = playerInput.ValueRO.mouseDelta.y;

            // 플레이어 좌우 회전 (Yaw)
            if (math.abs(mouseX) > 0.001f)
            {
                var yawRotation = quaternion.AxisAngle(playerUp, mouseX * sensitivity * 0.1f);
                transform.ValueRW.Rotation = math.mul(yawRotation, transform.ValueRO.Rotation);
            }

            // 카메라 상하 각도 (Pitch) - 카메라 컴포넌트에 저장
            if (math.abs(mouseY) > 0.001f)
            {
                cameraSettings.ValueRW.PitchAngle -= mouseY * sensitivity;
                cameraSettings.ValueRW.PitchAngle = math.clamp(cameraSettings.ValueRW.PitchAngle, -89f, 89f);
            }

            break;
        }
    }
}
