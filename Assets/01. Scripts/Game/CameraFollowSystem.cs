using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct CameraFollowSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CameraFollowComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var camera = Camera.main;
        if (camera == null)
            return;

        // 카메라 설정
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

        // 로컬 플레이어 찾기 (읽기만)
        foreach (var (transform, groundState) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<PlayerGroundState>>()
            .WithAll<GhostOwnerIsLocal, PlayerComponent>())
        {
            var playerUp = groundState.ValueRO.GroundNormal;
            var playerPosition = transform.ValueRO.Position;
            var playerRotation = transform.ValueRO.Rotation;

            // 플레이어가 바라보는 방향
            var playerForward = math.mul(playerRotation, new float3(0, 0, 1));

            // 카메라 위치: 플레이어 눈 높이 + 뒤쪽으로 Distance만큼
            var eyePosition = playerPosition + playerUp * cameraSettings.ValueRO.EyeHeight;
            var cameraBack = -playerForward;
            var cameraPosition = eyePosition + cameraBack * cameraSettings.ValueRO.Distance;
            camera.transform.position = cameraPosition;

            // 카메라 회전: 플레이어 방향 + Pitch
            var right = math.normalize(math.cross(playerUp, playerForward));
            var pitchRotation = quaternion.AxisAngle(right, math.radians(cameraSettings.ValueRO.PitchAngle));
            camera.transform.rotation = math.mul(pitchRotation, playerRotation);

            break;
        }
    }
}
