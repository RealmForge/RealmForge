using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct GoInGameServerSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EntitiesReference>();
        state.RequireForUpdate<NetworkId>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        
        EntitiesReference entitiesReference = SystemAPI.GetSingleton<EntitiesReference>();
        foreach ((
                     RefRO<ReceiveRpcCommandRequest> receiveRpcCommandRequest,
                     Entity entity)
                 in SystemAPI.Query<
                     RefRO<ReceiveRpcCommandRequest>>().WithAll<GoInGameClientSystem.GoInGameRequestRpc>().WithEntityAccess())
        {
            entityCommandBuffer.AddComponent<NetworkStreamInGame>(receiveRpcCommandRequest.ValueRO.SourceConnection);
            Debug.Log("Client Connected to Server!");
            
            entityCommandBuffer.DestroyEntity(entity);

            Entity playerEntity = entityCommandBuffer.Instantiate(entitiesReference.playerPrefabEntity);
            entityCommandBuffer.SetComponent(playerEntity, LocalTransform.FromPosition(new float3(0, 3, 0)));
            
            NetworkId networkId = SystemAPI.GetComponent<NetworkId>(receiveRpcCommandRequest.ValueRO.SourceConnection);
            entityCommandBuffer.AddComponent(playerEntity, new GhostOwner
            {
                NetworkId = networkId.Value,
            });
        }
        entityCommandBuffer.Playback((state.EntityManager));
    }
}
