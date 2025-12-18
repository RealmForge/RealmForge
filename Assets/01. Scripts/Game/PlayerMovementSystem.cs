using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
partial struct PlayerMovementSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach ((
                     RefRO<PlayerInput> playerInput, 
                     RefRW<LocalTransform> localTransform)
                 in SystemAPI.Query<
                    RefRO<PlayerInput>,
                    RefRW<LocalTransform>>().WithAll<Simulate>())
        {
            float moveSpeed = 10f;
            float3 moveVector = new float3(playerInput.ValueRO.inputVector.x, 0, playerInput.ValueRO.inputVector.y);
            localTransform.ValueRW.Position += moveVector * moveSpeed * SystemAPI.Time.DeltaTime;
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
