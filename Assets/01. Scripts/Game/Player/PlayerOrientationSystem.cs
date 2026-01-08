using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

// 플레이어를 지면에 맞춰 회전시키는 시스템 (위치는 물리 엔진이 처리)
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerMovementSystem))]
public partial struct PlayerOrientationSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlanetComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 행성 찾기
        PlanetComponent planet = default;
        bool foundPlanet = false;
        foreach (var p in SystemAPI.Query<RefRO<PlanetComponent>>())
        {
            planet = p.ValueRO;
            foundPlanet = true;
            break;
        }

        if (!foundPlanet)
            return;

        // Physics World 가져오기
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;

        // 모든 플레이어의 회전 처리
        foreach (var (transform, groundState, velocity, playerComp)
                 in SystemAPI.Query<RefRW<LocalTransform>, RefRW<PlayerGroundState>, RefRO<PlayerVelocity>, RefRO<PlayerComponent>>()
                     .WithAll<Simulate>())
        {
            var playerPos = transform.ValueRO.Position;
            var playerHeight = playerComp.ValueRO.Height;

            // 행성 중심 방향 (대략적인 아래 방향)
            var toPlanet = planet.Center - playerPos;
            var approximateDown = math.normalize(toPlanet);
            var approximateUp = -approximateDown;

            // 지면 감지용 Raycast (플레이어 아래로)
            var rayStart = playerPos; // 플레이어 중심에서 시작
            var rayEnd = playerPos + approximateDown * (playerHeight * 0.6f); // 아래로 캐스트

            var rayInput = new RaycastInput
            {
                Start = rayStart,
                End = rayEnd,
                Filter = CollisionFilter.Default
            };

            // Raycast로 지면 감지
            if (physicsWorld.CastRay(rayInput, out var hit))
            {
                // 지면 노말 업데이트
                var surfaceNormal = hit.SurfaceNormal;
                groundState.ValueRW.GroundNormal = surfaceNormal;

                // 위쪽 속도 확인 - 점프 중이 아닐 때만 IsGrounded = true
                var upwardVelocity = math.dot(velocity.ValueRO.Value, surfaceNormal);
                groundState.ValueRW.IsGrounded = upwardVelocity < 1f; // 위로 빠르게 이동 중이면 공중 상태

                // 플레이어 회전 (표면 노말에 맞춤)
                var upDirection = surfaceNormal;
                var currentForward = math.mul(transform.ValueRO.Rotation, new float3(0, 0, 1));

                // forward가 up과 평행하지 않도록 조정
                if (math.abs(math.dot(currentForward, upDirection)) > 0.99f)
                {
                    currentForward = math.mul(transform.ValueRO.Rotation, new float3(1, 0, 0));
                }

                // up 방향을 기준으로 회전 계산
                var targetRotation = quaternion.LookRotationSafe(
                    math.cross(upDirection, math.cross(currentForward, upDirection)),
                    upDirection
                );

                // 부드럽게 회전
                transform.ValueRW.Rotation = math.slerp(
                    transform.ValueRO.Rotation,
                    targetRotation,
                    math.min(1f, SystemAPI.Time.DeltaTime * 10f)
                );
            }
            else
            {
                // 지면을 감지하지 못함 (공중)
                groundState.ValueRW.IsGrounded = false;
                groundState.ValueRW.GroundNormal = approximateUp; // 행성 중심 기준 up 사용
            }
        }
    }
}
