using System;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace RealmForge.Session
{
    /// <summary>
    /// NFE(Netcode for Entities) 연결 이벤트와 GameSession 간의 브릿지
    /// NFE 연결 상태 변경을 GameSession에 반영
    /// </summary>
    public class NetcodeSessionAdapter : IDisposable
    {
        private readonly GameSession _session;
        private readonly PlayerIdMapper _idMapper;

        private World _serverWorld;
        private World _clientWorld;
        private EntityQuery _connectionQuery;
        private EntityQuery _networkIdQuery;

        private bool _isServer;
        private bool _isInitialized;

        public bool IsServer => _isServer;
        public bool IsConnected { get; private set; }

        public event Action OnServerStarted;
        public event Action OnClientConnected;
        public event Action<int> OnClientDisconnected;

        public NetcodeSessionAdapter(GameSession session, PlayerIdMapper idMapper)
        {
            _session = session;
            _idMapper = idMapper;
        }

        /// <summary>
        /// 서버 월드 초기화 (호스트용)
        /// </summary>
        public void InitializeAsServer(World serverWorld)
        {
            _serverWorld = serverWorld;
            _isServer = true;
            _isInitialized = true;

            SetupServerQueries();

            // 호스트 자신의 NetworkId 바인딩
            if (_session.IsHost)
            {
                // 서버의 로컬 플레이어는 NetworkId 0 또는 별도 처리
                _idMapper.BindNetworkId(_session.LocalPlayerId, 0);
                _session.SetConnectionId(_session.LocalPlayerId, 0);
                _session.UpdatePlayerState(_session.LocalPlayerId, PlayerConnectionState.Connected);
            }

            OnServerStarted?.Invoke();
            Debug.Log("[NetcodeSessionAdapter] Server initialized");
        }

        /// <summary>
        /// 클라이언트 월드 초기화
        /// </summary>
        public void InitializeAsClient(World clientWorld)
        {
            _clientWorld = clientWorld;
            _isServer = false;
            _isInitialized = true;

            SetupClientQueries();
            Debug.Log("[NetcodeSessionAdapter] Client initialized");
        }

        /// <summary>
        /// 매 프레임 연결 상태 폴링 (MonoBehaviour.Update에서 호출)
        /// </summary>
        public void UpdateConnectionState()
        {
            if (!_isInitialized) return;

            if (_isServer)
            {
                PollServerConnections();
            }
            else
            {
                PollClientConnection();
            }
        }

        /// <summary>
        /// 서버 연결 상태 폴링
        /// </summary>
        private void PollServerConnections()
        {
            if (_serverWorld == null || !_serverWorld.IsCreated) return;

            var entityManager = _serverWorld.EntityManager;

            // NetworkId가 있는 연결된 클라이언트 조회
            using var connectedEntities = _networkIdQuery.ToEntityArray(Allocator.Temp);
            using var networkIds = _networkIdQuery.ToComponentDataArray<NetworkId>(Allocator.Temp);

            for (int i = 0; i < connectedEntities.Length; i++)
            {
                var entity = connectedEntities[i];
                var networkId = networkIds[i].Value;

                // 이미 매핑된 NetworkId인지 확인
                if (!_idMapper.TryGetPlayerId(networkId, out _))
                {
                    // 새 연결 - Lobby에서 이미 등록된 플레이어와 매칭 필요
                    // 실제로는 RPC를 통해 AuthId를 받아서 매핑해야 함
                    HandleNewConnection(networkId);
                }

                // 연결 상태 업데이트
                if (entityManager.HasComponent<NetworkStreamInGame>(entity))
                {
                    if (_idMapper.TryGetPlayerId(networkId, out var playerId))
                    {
                        _session.UpdatePlayerState(playerId, PlayerConnectionState.Connected);
                        _session.UpdateHeartbeat(playerId);
                    }
                }
            }

            // 연결 끊김 감지 (ConnectionState 컴포넌트 체크)
            CheckForDisconnections();
        }

        /// <summary>
        /// 클라이언트 연결 상태 폴링
        /// </summary>
        private void PollClientConnection()
        {
            if (_clientWorld == null || !_clientWorld.IsCreated) return;

            var entityManager = _clientWorld.EntityManager;

            using var entities = _connectionQuery.ToEntityArray(Allocator.Temp);

            if (entities.Length == 0)
            {
                if (IsConnected)
                {
                    IsConnected = false;
                    _session.UpdatePlayerState(_session.LocalPlayerId, PlayerConnectionState.Disconnected);
                    Debug.Log("[NetcodeSessionAdapter] Client disconnected");
                }
                return;
            }

            var connectionEntity = entities[0];

            // NetworkId 획득 확인
            if (entityManager.HasComponent<NetworkId>(connectionEntity))
            {
                var networkId = entityManager.GetComponentData<NetworkId>(connectionEntity).Value;

                if (!IsConnected)
                {
                    IsConnected = true;
                    _idMapper.BindNetworkId(_session.LocalPlayerId, networkId);
                    _session.SetConnectionId(_session.LocalPlayerId, networkId);
                    _session.UpdatePlayerState(_session.LocalPlayerId, PlayerConnectionState.Connected);

                    OnClientConnected?.Invoke();
                    Debug.Log($"[NetcodeSessionAdapter] Client connected with NetworkId: {networkId}");
                }

                _session.UpdateHeartbeat(_session.LocalPlayerId);
            }
            else
            {
                // 아직 연결 중
                _session.UpdatePlayerState(_session.LocalPlayerId, PlayerConnectionState.Connecting);
            }
        }

        /// <summary>
        /// 새 클라이언트 연결 처리 (서버측)
        /// </summary>
        private void HandleNewConnection(int networkId)
        {
            // 실제 구현에서는 클라이언트가 보낸 AuthId RPC를 통해 매핑
            // 여기서는 임시로 새 플레이어 생성
            Debug.Log($"[NetcodeSessionAdapter] New connection with NetworkId: {networkId}");

            // 이 시점에서는 아직 어떤 플레이어인지 모름
            // 클라이언트가 접속 후 자신의 AuthId를 RPC로 보내야 함
        }

        /// <summary>
        /// 클라이언트의 AuthId를 NetworkId와 바인딩 (RPC 핸들러에서 호출)
        /// </summary>
        public void BindClientAuthId(int networkId, string authId)
        {
            if (!_idMapper.TryGetPlayerId(authId, out var playerId))
            {
                Debug.LogWarning($"[NetcodeSessionAdapter] Unknown AuthId: {authId}");
                return;
            }

            _idMapper.BindNetworkId(playerId, networkId);
            _session.SetConnectionId(playerId, networkId);
            _session.UpdatePlayerState(playerId, PlayerConnectionState.Connected);

            Debug.Log($"[NetcodeSessionAdapter] Bound AuthId {authId} to NetworkId {networkId}");
        }

        /// <summary>
        /// 연결 끊김 감지
        /// </summary>
        private void CheckForDisconnections()
        {
            if (_serverWorld == null || !_serverWorld.IsCreated) return;

            // GameSession의 플레이어 중 NetworkId가 더 이상 존재하지 않는 경우 체크
            foreach (var kvp in _session.Players)
            {
                var playerId = kvp.Key;
                if (_idMapper.TryGetNetworkId(playerId, out var networkId))
                {
                    if (networkId > 0 && !IsNetworkIdConnected(networkId))
                    {
                        _session.UpdatePlayerState(playerId, PlayerConnectionState.Disconnected);
                        OnClientDisconnected?.Invoke(networkId);
                    }
                }
            }
        }

        /// <summary>
        /// NetworkId가 아직 연결되어 있는지 확인
        /// </summary>
        private bool IsNetworkIdConnected(int networkId)
        {
            if (_serverWorld == null || !_serverWorld.IsCreated) return false;

            using var networkIds = _networkIdQuery.ToComponentDataArray<NetworkId>(Allocator.Temp);

            foreach (var id in networkIds)
            {
                if (id.Value == networkId) return true;
            }

            return false;
        }

        /// <summary>
        /// 클라이언트 강제 연결 해제 (서버측)
        /// </summary>
        public void DisconnectClient(ulong playerId)
        {
            if (!_isServer || _serverWorld == null) return;

            if (!_idMapper.TryGetNetworkId(playerId, out var networkId)) return;

            var entityManager = _serverWorld.EntityManager;
            using var entities = _networkIdQuery.ToEntityArray(Allocator.Temp);
            using var networkIds = _networkIdQuery.ToComponentDataArray<NetworkId>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                if (networkIds[i].Value == networkId)
                {
                    entityManager.AddComponentData(entities[i], new NetworkStreamRequestDisconnect
                    {
                        Reason = NetworkStreamDisconnectReason.ConnectionClose
                    });
                    break;
                }
            }

            _idMapper.UnbindNetworkId(playerId);
        }

        private void SetupServerQueries()
        {
            if (_serverWorld == null) return;

            var entityManager = _serverWorld.EntityManager;

            _connectionQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NetworkStreamConnection>()
            );

            _networkIdQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NetworkId>(),
                ComponentType.ReadOnly<NetworkStreamConnection>()
            );
        }

        private void SetupClientQueries()
        {
            if (_clientWorld == null) return;

            var entityManager = _clientWorld.EntityManager;

            _connectionQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NetworkStreamConnection>()
            );
        }

        public void Dispose()
        {
            _connectionQuery.Dispose();
            _networkIdQuery.Dispose();
            _serverWorld = null;
            _clientWorld = null;
            _isInitialized = false;
        }
    }
}
