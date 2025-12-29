using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using RealmForge.Session;
using RealmForge.Game.UI;

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

            // 스폰 위치 계산 (행성 표면 위로)
            float3 spawnPosition = CalculateSafeSpawnPosition(ref state, new float3(0, 0, 0));

            // 플레이어 엔티티 스폰
            Entity playerEntity = entityCommandBuffer.Instantiate(entitiesReference.playerPrefabEntity);
            entityCommandBuffer.SetComponent(playerEntity, LocalTransform.FromPosition(spawnPosition));

            entityCommandBuffer.AddComponent(playerEntity, new GhostOwner
            {
                NetworkId = networkId.Value,
            });

            // 플레이어 이름 컴포넌트 설정 (prefab에 이미 있으므로 SetComponent 사용)
            entityCommandBuffer.SetComponent(playerEntity, new PlayerNameComponent
            {
                DisplayName = new Unity.Collections.FixedString64Bytes(playerName),
                NetworkId = networkId.Value
            });

            Debug.Log($"[GoInGameServer] Spawned player entity for: {playerName}");
        }
        entityCommandBuffer.Playback(state.EntityManager);
    }

    /// <summary>
    /// 안전한 스폰 위치 계산 - 행성 표면 위로
    /// </summary>
    private float3 CalculateSafeSpawnPosition(ref SystemState state, float3 desiredPosition)
    {
        const float SPAWN_HEIGHT_OFFSET = 1f; // 최대 높이 위 1만큼 오프셋

        // PlanetTag를 가진 엔티티에서 PlanetData와 NoiseSettings 찾기
        PlanetData planetData = default;
        NoiseSettings noiseSettings = default;
        bool foundPlanet = false;

        foreach (var (pd, ns) in SystemAPI.Query<RefRO<PlanetData>, RefRO<NoiseSettings>>())
        {
            planetData = pd.ValueRO;
            noiseSettings = ns.ValueRO;
            foundPlanet = true;
            break;
        }

        if (!foundPlanet)
        {
            Debug.LogWarning("[GoInGameServer] Planet not found! Using default spawn position.");
            return new float3(0, 88f, 0);
        }

        // 최대 높이 계산: Radius + HeightMultiplier + 1
        float maxHeight = planetData.Radius + noiseSettings.HeightMultiplier + SPAWN_HEIGHT_OFFSET;

        // 스폰 방향 결정 (기본: 위쪽)
        float3 spawnDirection = math.normalizesafe(desiredPosition - planetData.Center);
        if (math.lengthsq(spawnDirection) < 0.01f)
        {
            spawnDirection = new float3(0, 1, 0);
        }

        // 최대 높이 위치에 스폰
        float3 spawnPosition = planetData.Center + spawnDirection * maxHeight;

        Debug.Log($"[GoInGameServer] Spawn position: {spawnPosition}, max height: {maxHeight} (Radius: {planetData.Radius}, HeightMultiplier: {noiseSettings.HeightMultiplier})");

        return spawnPosition;
    }
}
