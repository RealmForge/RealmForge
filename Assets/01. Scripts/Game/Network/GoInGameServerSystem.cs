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
        const float DEFAULT_SPAWN_HEIGHT = 50f; // 기본 스폰 높이
        const float ESTIMATED_PLANET_RADIUS = 100f; // 예상 행성 반지름
        const float SURFACE_OFFSET = 2f; // 지면 위 여유 높이
        const float MAX_RAYCAST_DISTANCE = 500f; // Raycast 최대 거리

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
        {
            Debug.LogWarning("[GoInGameServer] Planet not found! Using default spawn position.");
            return new float3(0, DEFAULT_SPAWN_HEIGHT, 0);
        }

        // Physics World 가져오기
        if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorldSingleton))
        {
            Debug.LogWarning("[GoInGameServer] PhysicsWorld not available! Using estimated spawn height.");
            float3 spawnDir = math.normalizesafe(desiredPosition - planet.Center);
            if (math.lengthsq(spawnDir) < 0.01f)
            {
                spawnDir = new float3(0, 1, 0); // 위쪽 방향
            }
            return planet.Center + spawnDir * (ESTIMATED_PLANET_RADIUS + DEFAULT_SPAWN_HEIGHT);
        }

        var physicsWorld = physicsWorldSingleton.PhysicsWorld;

        // 스폰 방향 결정 (기본: 위쪽)
        float3 spawnDirection = math.normalizesafe(desiredPosition - planet.Center);
        if (math.lengthsq(spawnDirection) < 0.01f)
        {
            spawnDirection = new float3(0, 1, 0);
        }

        // 행성 중심에서 바깥쪽으로 Raycast (지면 찾기)
        float3 rayStart = planet.Center;
        float3 rayEnd = planet.Center + spawnDirection * MAX_RAYCAST_DISTANCE;

        var rayInput = new RaycastInput
        {
            Start = rayStart,
            End = rayEnd,
            Filter = CollisionFilter.Default
        };

        // Raycast로 지면 찾기
        if (physicsWorld.CastRay(rayInput, out var hit))
        {
            // 지면을 찾았으면 표면 위에 스폰
            float3 safePosition = hit.Position + hit.SurfaceNormal * SURFACE_OFFSET;
            Debug.Log($"[GoInGameServer] Spawn position found via raycast: {safePosition}, distance from center: {math.length(safePosition - planet.Center)}");
            return safePosition;
        }
        else
        {
            // 지면을 못 찾았으면 예상 반지름 + 기본 높이
            float3 fallbackPosition = planet.Center + spawnDirection * (ESTIMATED_PLANET_RADIUS + DEFAULT_SPAWN_HEIGHT);
            Debug.LogWarning($"[GoInGameServer] Raycast failed! Using fallback position: {fallbackPosition}");
            return fallbackPosition;
        }
    }
}
