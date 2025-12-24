using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using RealmForge.Session;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
partial struct GoInGameServerSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EntitiesReference>();
        state.RequireForUpdate<NetworkId>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        EntitiesReference entitiesReference = SystemAPI.GetSingleton<EntitiesReference>();
        foreach ((
                     RefRO<ReceiveRpcCommandRequest> receiveRpcCommandRequest,
                     RefRO<GoInGameClientSystem.GoInGameRequestRpc> rpc,
                     Entity entity)
                 in SystemAPI.Query<
                     RefRO<ReceiveRpcCommandRequest>,
                     RefRO<GoInGameClientSystem.GoInGameRequestRpc>>().WithEntityAccess())
        {
            var sourceConnection = receiveRpcCommandRequest.ValueRO.SourceConnection;
            entityCommandBuffer.AddComponent<NetworkStreamInGame>(sourceConnection);

            NetworkId networkId = SystemAPI.GetComponent<NetworkId>(sourceConnection);
            string authId = rpc.ValueRO.AuthId.ToString();

            Debug.Log($"[GoInGameServer] Client connected! NetworkId: {networkId.Value}, AuthId: {authId}");

            // SessionManager를 통해 플레이어 정보 매칭
            string playerName = "Unknown";
            if (SessionManager.Instance != null)
            {
                var idMapper = SessionManager.Instance.IdMapper;
                var session = SessionManager.Instance.Session;

                // AuthId로 PlayerId 찾기
                if (idMapper.TryGetPlayerId(authId, out var playerId))
                {
                    // NetworkId 바인딩
                    idMapper.BindNetworkId(playerId, networkId.Value);
                    session.SetConnectionId(playerId, networkId.Value);
                    session.UpdatePlayerState(playerId, PlayerConnectionState.Connected);

                    // 플레이어 이름 가져오기
                    var player = session.GetPlayer(playerId);
                    if (player.HasValue)
                    {
                        playerName = player.Value.DisplayName;
                    }

                    Debug.Log($"[GoInGameServer] Matched player: {playerName} (PlayerId: {playerId})");
                }
                else
                {
                    Debug.LogWarning($"[GoInGameServer] Could not find player with AuthId: {authId}");
                }
            }

            entityCommandBuffer.DestroyEntity(entity);

            // 플레이어 엔티티 스폰
            Entity playerEntity = entityCommandBuffer.Instantiate(entitiesReference.playerPrefabEntity);
            entityCommandBuffer.SetComponent(playerEntity, LocalTransform.FromPosition(new float3(0, 3, 0)));

            entityCommandBuffer.AddComponent(playerEntity, new GhostOwner
            {
                NetworkId = networkId.Value,
            });

            Debug.Log($"[GoInGameServer] Spawned player entity for: {playerName}");
        }
        entityCommandBuffer.Playback(state.EntityManager);
    }
}
