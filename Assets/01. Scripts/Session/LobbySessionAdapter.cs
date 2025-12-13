using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace RealmForge.Session
{
    /// <summary>
    /// Unity Lobby 서비스와 GameSession 간의 브릿지
    /// Lobby 이벤트를 GameSession 호출로 변환
    /// </summary>
    public class LobbySessionAdapter : IDisposable
    {
        private readonly GameSession _session;
        private readonly PlayerIdMapper _idMapper;

        private Lobby _currentLobby;
        private ILobbyEvents _lobbyEvents;
        private float _heartbeatTimer;

        private const float HeartbeatInterval = 15f;
        private const string RelayJoinCodeKey = "RelayJoinCode";

        public Lobby CurrentLobby => _currentLobby;
        public bool IsHost => _currentLobby?.HostId == AuthenticationService.Instance.PlayerId;

        public event Action<string> OnRelayJoinCodeReceived;
        public event Action<Lobby> OnLobbyUpdated;
        public event Action OnLobbyDeleted;

        public LobbySessionAdapter(GameSession session, PlayerIdMapper idMapper)
        {
            _session = session;
            _idMapper = idMapper;
        }

        /// <summary>
        /// 새 Lobby 생성 및 GameSession 초기화
        /// </summary>
        public async Task<bool> CreateLobbyAsync(string lobbyName, int maxPlayers, bool isPrivate = false)
        {
            try
            {
                var authId = AuthenticationService.Instance.PlayerId;
                var playerName = GetPlayerName();

                var options = new CreateLobbyOptions
                {
                    IsPrivate = isPrivate,
                    Player = CreateLobbyPlayer(playerName),
                    Data = new Dictionary<string, DataObject>
                    {
                        { RelayJoinCodeKey, new DataObject(DataObject.VisibilityOptions.Member, "") }
                    }
                };

                _currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);

                // GameSession 초기화
                var playerId = _idMapper.RegisterFromLobby(authId);
                var config = new SessionConfig
                {
                    SessionName = lobbyName,
                    MaxPlayers = maxPlayers,
                    IsPrivate = isPrivate,
                    GameMode = "Default",
                    CustomData = new Dictionary<string, string>()
                };

                _session.Create(config, playerId, playerName);

                await SubscribeToLobbyEventsAsync();

                return true;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[LobbySessionAdapter] CreateLobby failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lobby Code로 참가
        /// </summary>
        public async Task<bool> JoinLobbyByCodeAsync(string lobbyCode)
        {
            try
            {
                var authId = AuthenticationService.Instance.PlayerId;
                var playerName = GetPlayerName();

                var options = new JoinLobbyByCodeOptions
                {
                    Player = CreateLobbyPlayer(playerName)
                };

                _currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, options);

                // GameSession 참가
                var playerId = _idMapper.RegisterFromLobby(authId);
                _session.Join(_currentLobby.Id, playerId, playerName);

                // 기존 플레이어들 동기화
                SyncExistingPlayers();

                await SubscribeToLobbyEventsAsync();

                // Relay Join Code가 이미 있으면 알림
                CheckForRelayJoinCode();

                return true;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[LobbySessionAdapter] JoinLobbyByCode failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lobby ID로 참가
        /// </summary>
        public async Task<bool> JoinLobbyByIdAsync(string lobbyId)
        {
            try
            {
                var authId = AuthenticationService.Instance.PlayerId;
                var playerName = GetPlayerName();

                var options = new JoinLobbyByIdOptions
                {
                    Player = CreateLobbyPlayer(playerName)
                };

                _currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, options);

                var playerId = _idMapper.RegisterFromLobby(authId);
                _session.Join(_currentLobby.Id, playerId, playerName);

                SyncExistingPlayers();
                await SubscribeToLobbyEventsAsync();
                CheckForRelayJoinCode();

                return true;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[LobbySessionAdapter] JoinLobbyById failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 퀵 조인
        /// </summary>
        public async Task<bool> QuickJoinAsync()
        {
            try
            {
                var authId = AuthenticationService.Instance.PlayerId;
                var playerName = GetPlayerName();

                var options = new QuickJoinLobbyOptions
                {
                    Player = CreateLobbyPlayer(playerName)
                };

                _currentLobby = await LobbyService.Instance.QuickJoinLobbyAsync(options);

                var playerId = _idMapper.RegisterFromLobby(authId);
                _session.Join(_currentLobby.Id, playerId, playerName);

                SyncExistingPlayers();
                await SubscribeToLobbyEventsAsync();
                CheckForRelayJoinCode();

                return true;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[LobbySessionAdapter] QuickJoin failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lobby 나가기
        /// </summary>
        public async Task LeaveLobbyAsync()
        {
            if (_currentLobby == null) return;

            try
            {
                var authId = AuthenticationService.Instance.PlayerId;

                if (_lobbyEvents != null)
                {
                    await _lobbyEvents.UnsubscribeAsync();
                    _lobbyEvents = null;
                }

                await LobbyService.Instance.RemovePlayerAsync(_currentLobby.Id, authId);

                // GameSession 정리
                if (_idMapper.TryGetPlayerId(authId, out var playerId))
                {
                    _session.RemovePlayer(playerId);
                    _idMapper.RemovePlayer(playerId);
                }

                _currentLobby = null;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[LobbySessionAdapter] LeaveLobby failed: {e.Message}");
            }
        }

        /// <summary>
        /// Lobby 삭제 (호스트 전용)
        /// </summary>
        public async Task DeleteLobbyAsync()
        {
            if (_currentLobby == null || !IsHost) return;

            try
            {
                if (_lobbyEvents != null)
                {
                    await _lobbyEvents.UnsubscribeAsync();
                    _lobbyEvents = null;
                }

                await LobbyService.Instance.DeleteLobbyAsync(_currentLobby.Id);

                _session.EndSession();
                _currentLobby = null;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[LobbySessionAdapter] DeleteLobby failed: {e.Message}");
            }
        }

        /// <summary>
        /// Relay Join Code 설정 (호스트가 NFE 서버 시작 후 호출)
        /// </summary>
        public async Task SetRelayJoinCodeAsync(string joinCode)
        {
            if (_currentLobby == null || !IsHost) return;

            try
            {
                var options = new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        { RelayJoinCodeKey, new DataObject(DataObject.VisibilityOptions.Member, joinCode) }
                    }
                };

                _currentLobby = await LobbyService.Instance.UpdateLobbyAsync(_currentLobby.Id, options);
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[LobbySessionAdapter] SetRelayJoinCode failed: {e.Message}");
            }
        }

        /// <summary>
        /// Lobby 잠금 (게임 시작 시)
        /// </summary>
        public async Task LockLobbyAsync()
        {
            if (_currentLobby == null || !IsHost) return;

            try
            {
                var options = new UpdateLobbyOptions
                {
                    IsLocked = true
                };

                _currentLobby = await LobbyService.Instance.UpdateLobbyAsync(_currentLobby.Id, options);
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[LobbySessionAdapter] LockLobby failed: {e.Message}");
            }
        }

        /// <summary>
        /// 하트비트 업데이트 (MonoBehaviour.Update에서 호출)
        /// </summary>
        public async void UpdateHeartbeat(float deltaTime)
        {
            if (_currentLobby == null || !IsHost) return;

            _heartbeatTimer += deltaTime;
            if (_heartbeatTimer >= HeartbeatInterval)
            {
                _heartbeatTimer = 0f;
                try
                {
                    await LobbyService.Instance.SendHeartbeatPingAsync(_currentLobby.Id);
                }
                catch (LobbyServiceException e)
                {
                    Debug.LogWarning($"[LobbySessionAdapter] Heartbeat failed: {e.Message}");
                }
            }
        }

        private async Task SubscribeToLobbyEventsAsync()
        {
            if (_currentLobby == null) return;

            var callbacks = new LobbyEventCallbacks();
            callbacks.LobbyChanged += OnLobbyChanged;
            callbacks.KickedFromLobby += OnKickedFromLobby;
            callbacks.LobbyEventConnectionStateChanged += OnConnectionStateChanged;

            _lobbyEvents = await LobbyService.Instance.SubscribeToLobbyEventsAsync(_currentLobby.Id, callbacks);
        }

        private void OnLobbyChanged(ILobbyChanges changes)
        {
            if (_currentLobby == null) return;

            changes.ApplyToLobby(_currentLobby);

            // 플레이어 변경 처리
            if (changes.PlayerJoined.Changed)
            {
                foreach (var joinedPlayer in changes.PlayerJoined.Value)
                {
                    HandlePlayerJoined(joinedPlayer.Player);
                }
            }

            if (changes.PlayerLeft.Changed)
            {
                foreach (var leftPlayerId in changes.PlayerLeft.Value)
                {
                    HandlePlayerLeft(leftPlayerId);
                }
            }

            // 호스트 변경 처리
            if (changes.HostId.Changed)
            {
                HandleHostChanged(changes.HostId.Value);
            }

            // Relay Join Code 변경 확인
            if (changes.Data.Changed)
            {
                CheckForRelayJoinCode();
            }

            OnLobbyUpdated?.Invoke(_currentLobby);
        }

        private void OnKickedFromLobby()
        {
            Debug.Log("[LobbySessionAdapter] Kicked from lobby");
            _session.EndSession();
            _currentLobby = null;
            OnLobbyDeleted?.Invoke();
        }

        private void OnConnectionStateChanged(LobbyEventConnectionState state)
        {
            Debug.Log($"[LobbySessionAdapter] Connection state: {state}");
        }

        private void HandlePlayerJoined(Player lobbyPlayer)
        {
            var playerId = _idMapper.RegisterFromLobby(lobbyPlayer.Id);
            var playerName = lobbyPlayer.Data?.TryGetValue("PlayerName", out var nameData) == true
                ? nameData.Value
                : "Unknown";

            var sessionPlayer = new SessionPlayer
            {
                PlayerId = playerId,
                DisplayName = playerName,
                ConnectionId = -1,
                State = PlayerConnectionState.Connecting,
                IsHost = lobbyPlayer.Id == _currentLobby.HostId,
                LastHeartbeat = Time.realtimeSinceStartup
            };

            _session.AddPlayer(sessionPlayer);
        }

        private void HandlePlayerLeft(int playerIndex)
        {
            // Lobby는 index로 알려주므로 현재 플레이어 목록과 비교 필요
            // 이미 ApplyToLobby가 호출되었으므로 _currentLobby.Players는 업데이트됨
            // 제거된 플레이어를 찾기 위해 GameSession의 플레이어와 비교

            var currentAuthIds = new HashSet<string>();
            foreach (var player in _currentLobby.Players)
            {
                currentAuthIds.Add(player.Id);
            }

            foreach (var kvp in _session.Players)
            {
                if (_idMapper.TryGetAuthId(kvp.Key, out var authId))
                {
                    if (!currentAuthIds.Contains(authId))
                    {
                        _session.RemovePlayer(kvp.Key);
                        _idMapper.RemovePlayer(kvp.Key);
                        break;
                    }
                }
            }
        }

        private void HandleHostChanged(string newHostAuthId)
        {
            if (_idMapper.TryGetPlayerId(newHostAuthId, out var newHostPlayerId))
            {
                // GameSession의 호스트 마이그레이션은 내부적으로 처리됨
                Debug.Log($"[LobbySessionAdapter] Host migrated to {newHostAuthId}");
            }
        }

        private void SyncExistingPlayers()
        {
            if (_currentLobby == null) return;

            foreach (var lobbyPlayer in _currentLobby.Players)
            {
                var authId = lobbyPlayer.Id;

                // 자기 자신은 이미 Join에서 처리됨
                if (authId == AuthenticationService.Instance.PlayerId) continue;

                var playerId = _idMapper.RegisterFromLobby(authId);
                var playerName = lobbyPlayer.Data?.TryGetValue("PlayerName", out var nameData) == true
                    ? nameData.Value
                    : "Unknown";

                var sessionPlayer = new SessionPlayer
                {
                    PlayerId = playerId,
                    DisplayName = playerName,
                    ConnectionId = -1,
                    State = PlayerConnectionState.Connected,
                    IsHost = authId == _currentLobby.HostId,
                    LastHeartbeat = Time.realtimeSinceStartup
                };

                _session.AddPlayer(sessionPlayer);
            }
        }

        private void CheckForRelayJoinCode()
        {
            if (_currentLobby?.Data == null) return;

            if (_currentLobby.Data.TryGetValue(RelayJoinCodeKey, out var joinCodeData))
            {
                if (!string.IsNullOrEmpty(joinCodeData.Value))
                {
                    OnRelayJoinCodeReceived?.Invoke(joinCodeData.Value);
                }
            }
        }

        private Player CreateLobbyPlayer(string playerName)
        {
            return new Player
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) }
                }
            };
        }

        private string GetPlayerName()
        {
            // TODO: 실제 구현에서는 PlayerPrefs나 프로필 시스템에서 가져오기
            return $"Player_{AuthenticationService.Instance.PlayerId[..6]}";
        }

        public void Dispose()
        {
            _lobbyEvents?.UnsubscribeAsync();
            _lobbyEvents = null;
            _currentLobby = null;
        }
    }
}
