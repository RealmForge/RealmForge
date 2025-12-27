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
        state.RequireForUpdate<PlanetComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var deltaTime = SystemAPI.Time.DeltaTime;

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
