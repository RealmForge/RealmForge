using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

// 행성 중심으로 당기는 중력 시스템
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[UpdateBefore(typeof(PlayerMovementSystem))]
public partial struct GravitySystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // PlanetComponent와 Player 관련 컴포넌트가 모두 존재할 때만 실행
        state.RequireForUpdate<PlanetComponent>();
        state.RequireForUpdate<PlayerComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var deltaTime = SystemAPI.Time.DeltaTime;

        // 행성 싱글톤 가져오기 (안전한 방식)
        if (!SystemAPI.TryGetSingleton<PlanetComponent>(out var planet))
            return;

        // 모든 플레이어에게 중력 적용
        foreach (var (transform, physicsVelocity, customVelocity)
                 in SystemAPI.Query<RefRO<LocalTransform>, RefRW<PhysicsVelocity>, RefRW<PlayerVelocity>>()
                     .WithAll<Simulate, PlayerComponent>())
        {
            var playerPos = transform.ValueRO.Position;

            // 행성 중심 방향으로 중력 적용
            var toPlanet = planet.Center - playerPos;
            var distance = math.length(toPlanet);

            if (distance > 0.001f)
            {
                var gravityDirection = toPlanet / distance;
                var gravityForce = gravityDirection * planet.Gravity;

                // PhysicsVelocity와 PlayerVelocity 모두 업데이트
                physicsVelocity.ValueRW.Linear += gravityForce * deltaTime;
                customVelocity.ValueRW.Value += gravityForce * deltaTime;
            }
        }
    }
}
