using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using Unity.Services.Authentication;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
partial struct GoInGameClientSystem : ISystem
{
    private bool _hasEntitiesReference;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkId>();
        _hasEntitiesReference = false;
    }

    public void OnUpdate(ref SystemState state)
    {
        // EntitiesReference가 있는지 확인 (SubScene 로드 완료)
        bool hasEntitiesRef = SystemAPI.HasSingleton<EntitiesReference>();

        // EntitiesReference가 새로 로드되었을 때 InGame이지만 RPC를 다시 보내야 할 수 있음
        if (hasEntitiesRef && !_hasEntitiesReference)
        {
            _hasEntitiesReference = true;
            Debug.Log("[GoInGameClient] EntitiesReference detected (SubScene loaded)");
        }

        EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach ((
                     RefRO<NetworkId> networkId,
                     Entity entity)
                 in SystemAPI.Query<
                     RefRO<NetworkId>>().WithNone<NetworkStreamInGame>().WithEntityAccess())
        {
            // EntitiesReference가 있을 때만 InGame 설정 및 RPC 전송
            if (!hasEntitiesRef)
            {
                Debug.Log("[GoInGameClient] Waiting for EntitiesReference (SubScene)...");
                continue;
            }

            entityCommandBuffer.AddComponent<NetworkStreamInGame>(entity);
            Debug.Log($"[GoInGameClient] Setting Client as InGame, NetworkId: {networkId.ValueRO.Value}");

            // AuthId를 포함한 RPC 전송
            string authId = "";
            if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
            {
                authId = AuthenticationService.Instance.PlayerId;
            }

            Entity rpcEntity = entityCommandBuffer.CreateEntity();
            entityCommandBuffer.AddComponent(rpcEntity, new GoInGameRequestRpc
            {
                AuthId = authId
            });
            entityCommandBuffer.AddComponent(rpcEntity, new SendRpcCommandRequest());

            Debug.Log($"[GoInGameClient] Sending GoInGame RPC with AuthId: {authId}");
        }
        entityCommandBuffer.Playback(state.EntityManager);
    }

    public struct GoInGameRequestRpc : IRpcCommand
    {
        public FixedString64Bytes AuthId;
    }
}
