using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct ServerSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        foreach ((
                     RefRO<SimpleRPC> simpleRPC,
                     RefRO<ReceiveRpcCommandRequest> receiveRpcCommandRequest,
                     Entity entity)
                 in SystemAPI.Query<
                     RefRO<SimpleRPC>,
                     RefRO<ReceiveRpcCommandRequest>>().WithEntityAccess())
        {
            entityCommandBuffer.DestroyEntity((entity));
        }
        entityCommandBuffer.Playback((state.EntityManager));
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
