using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.Networking.Transport.Relay;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace RealmForge.Session
{
    /// <summary>
    /// 인게임 세션을 관리하는 매니저
    /// Relay + Netcode 연결 및 플레이어 동기화 담당
    /// Lobby/Room 단계는 LobbyManager/RoomManager가 담당
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }

        // Core Components
        public GameSession Session { get; private set; }
        public PlayerIdMapper IdMapper { get; private set; }
        public NetcodeSessionAdapter NetcodeAdapter { get; private set; }

        // NFE Worlds
        private World _serverWorld;
        private World _clientWorld;
        private RelayServerData? _serverRelayData;
        private string _currentRelayJoinCode;

        // Lobby reference (Room에서 전달받음)
        private Lobby _currentLobby;

        // State
        public bool IsInGame => Session?.State == SessionState.InProgress;
        public bool IsHost => Session?.IsHost ?? false;
        public string CurrentRelayJoinCode => _currentRelayJoinCode;

        // Events
        public event Action<SessionState> OnSessionStateChanged;
        public event Action<SessionPlayer> OnPlayerJoined;
        public event Action<SessionPlayer> OnPlayerLeft;
        public event Action OnGameStarting;
        public event Action OnGameStarted;
        public event Action<string> OnGameStartFailed;

        // Session Data (ScriptableObject for persistence)
        private SessionDataSO _sessionData;
        public SessionDataSO SessionData => _sessionData;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Load SessionDataSO from Resources
            _sessionData = SessionDataSO.Instance;

            InitializeComponents();

            // 씬 전환 후 데이터 복원 시도
            TryRestoreSessionFromData();
        }

        private void InitializeComponents()
        {
            Session = new GameSession();
            IdMapper = new PlayerIdMapper();
            NetcodeAdapter = new NetcodeSessionAdapter(Session, IdMapper);

            // 이벤트 구독
            Session.OnStateChanged += HandleSessionStateChanged;
            Session.OnPlayerJoined += player => OnPlayerJoined?.Invoke(player);
            Session.OnPlayerLeft += player => OnPlayerLeft?.Invoke(player);
        }

        private void Update()
        {
            NetcodeAdapter?.UpdateConnectionState();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Cleanup();
                Instance = null;
            }
        }

        #region Session Initialization

        /// <summary>
        /// Room에서 게임 시작 시 호출 - Lobby 플레이어들을 Session에 등록
        /// </summary>
        public void InitializeSessionFromLobby(Lobby lobby, string localPlayerName, bool isHost)
        {
            _currentLobby = lobby;

            // SessionConfig 설정
            var config = new SessionConfig
            {
                SessionName = lobby.Name,
                MaxPlayers = lobby.MaxPlayers,
                IsPrivate = lobby.IsPrivate,
                GameMode = "Default",
                CustomData = new Dictionary<string, string>()
            };

            // 로컬 플레이어 등록
            var localAuthId = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
            var localPlayerId = IdMapper.RegisterFromLobby(localAuthId);

            if (isHost)
            {
                Session.Create(config, localPlayerId, localPlayerName);
            }
            else
            {
                Session.Join(lobby.Id, localPlayerId, localPlayerName);
            }

            // Lobby의 다른 플레이어들을 Session에 등록
            foreach (var lobbyPlayer in lobby.Players)
            {
                // 자기 자신은 이미 등록됨
                if (lobbyPlayer.Id == localAuthId) continue;

                var playerId = IdMapper.RegisterFromLobby(lobbyPlayer.Id);
                var playerName = GetPlayerNameFromLobby(lobbyPlayer);

                var sessionPlayer = new SessionPlayer
                {
                    PlayerId = playerId,
                    DisplayName = playerName,
                    ConnectionId = -1,
                    State = PlayerConnectionState.Connecting,
                    IsHost = lobbyPlayer.Id == lobby.HostId,
                    LastHeartbeat = Time.realtimeSinceStartup
                };

                Session.AddPlayer(sessionPlayer);
            }

            Debug.Log($"[SessionManager] Session initialized with {Session.PlayerCount} players");
        }

        private string GetPlayerNameFromLobby(Player lobbyPlayer)
        {
            if (lobbyPlayer.Data != null && lobbyPlayer.Data.TryGetValue("PlayerName", out var nameData))
            {
                return nameData.Value;
            }
            return "Unknown";
        }

        #endregion

        #region Game Start Operations

        /// <summary>
        /// 게임 시작 (호스트 전용)
        /// 1. Relay 할당
        /// 2. NFE 서버 월드 생성 및 Relay 설정
        /// 3. 호스트 클라이언트 연결
        /// 4. Relay Join Code 반환 (RoomManager가 Lobby에 공유)
        /// </summary>
        public async Task<string> StartGameAsHostAsync()
        {
            if (!IsHost)
            {
                Debug.LogError("[SessionManager] Only host can start the game");
                OnGameStartFailed?.Invoke("Only host can start the game");
                return null;
            }

            if (Session.State != SessionState.Waiting)
            {
                Debug.LogError("[SessionManager] Cannot start game in current state");
                OnGameStartFailed?.Invoke("Invalid session state");
                return null;
            }

            OnGameStarting?.Invoke();

            try
            {
                // 1. Relay 할당 및 Join Code 획득
                var maxConnections = Session.Config.MaxPlayers - 1; // 호스트 제외
                var relayResult = await RelayServiceHelper.AllocateRelayServerAsync(maxConnections);

                if (!relayResult.HasValue)
                {
                    Debug.LogError("[SessionManager] Failed to allocate relay");
                    OnGameStartFailed?.Invoke("Failed to allocate relay server");
                    return null;
                }

                _serverRelayData = relayResult.Value.serverData;
                _currentRelayJoinCode = relayResult.Value.joinCode;
                Debug.Log($"[SessionManager] Relay allocated, join code: {_currentRelayJoinCode}");

                // 2. NFE 서버 월드 생성
                _serverWorld = NetcodeWorldHelper.CreateServerWorld("SessionServerWorld");
                if (_serverWorld == null)
                {
                    Debug.LogError("[SessionManager] Failed to create server world");
                    OnGameStartFailed?.Invoke("Failed to create server world");
                    return null;
                }

                // 3. 서버에 Relay 설정 적용
                if (!NetcodeWorldHelper.SetupServerWithRelay(_serverWorld, _serverRelayData.Value))
                {
                    Debug.LogError("[SessionManager] Failed to setup server with relay");
                    OnGameStartFailed?.Invoke("Failed to setup server with relay");
                    NetcodeWorldHelper.DisposeWorld(_serverWorld);
                    _serverWorld = null;
                    return null;
                }

                NetcodeAdapter.InitializeAsServer(_serverWorld);
                Debug.Log("[SessionManager] Server world created and listening");

                // 4. 호스트도 클라이언트 월드 생성하여 연결
                await ConnectHostAsClient(_currentRelayJoinCode);

                // 5. GameSession 시작
                Session.StartSession();

                OnGameStarted?.Invoke();
                Debug.Log("[SessionManager] Game started successfully as host");

                // Relay Join Code 반환 (RoomManager가 Lobby에 공유)
                return _currentRelayJoinCode;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SessionManager] StartGameAsHost failed: {e.Message}");
                OnGameStartFailed?.Invoke(e.Message);

                // 정리
                if (_serverWorld != null)
                {
                    NetcodeWorldHelper.DisposeWorld(_serverWorld);
                    _serverWorld = null;
                }

                return null;
            }
        }

        /// <summary>
        /// 호스트가 자신의 서버에 클라이언트로 연결
        /// </summary>
        private async Task ConnectHostAsClient(string relayJoinCode)
        {
            // 호스트용 클라이언트 Relay 데이터 획득
            var clientRelayData = await RelayServiceHelper.JoinRelayAsync(relayJoinCode);
            if (!clientRelayData.HasValue)
            {
                Debug.LogWarning("[SessionManager] Host client relay join failed, using local connection");
                // 로컬 IPC 연결로 폴백 가능
                return;
            }

            _clientWorld = NetcodeWorldHelper.CreateClientWorld("SessionClientWorld");
            if (_clientWorld == null)
            {
                Debug.LogError("[SessionManager] Failed to create host client world");
                return;
            }

            NetcodeWorldHelper.SetupClientWithRelay(_clientWorld, clientRelayData.Value);
            Debug.Log("[SessionManager] Host connected as client");
        }

        /// <summary>
        /// 클라이언트로서 게임 연결 (비호스트 플레이어용)
        /// </summary>
        public async Task<bool> ConnectToGameAsClientAsync(string relayJoinCode)
        {
            try
            {
                // Relay Join
                var clientRelayData = await RelayServiceHelper.JoinRelayAsync(relayJoinCode);
                if (!clientRelayData.HasValue)
                {
                    Debug.LogError("[SessionManager] Failed to join relay");
                    return false;
                }

                // 클라이언트 월드 생성
                _clientWorld = NetcodeWorldHelper.CreateClientWorld("SessionClientWorld");
                if (_clientWorld == null)
                {
                    Debug.LogError("[SessionManager] Failed to create client world");
                    return false;
                }

                // Relay 설정 및 연결
                if (!NetcodeWorldHelper.SetupClientWithRelay(_clientWorld, clientRelayData.Value))
                {
                    Debug.LogError("[SessionManager] Failed to setup client with relay");
                    NetcodeWorldHelper.DisposeWorld(_clientWorld);
                    _clientWorld = null;
                    return false;
                }

                NetcodeAdapter.InitializeAsClient(_clientWorld);
                Debug.Log("[SessionManager] Connected to game as client");

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SessionManager] ConnectToGameAsClient failed: {e.Message}");
                return false;
            }
        }

        #endregion

        #region Event Handlers

        private void HandleSessionStateChanged(SessionState newState)
        {
            Debug.Log($"[SessionManager] Session state changed to: {newState}");
            OnSessionStateChanged?.Invoke(newState);
        }

        #endregion

        #region Session Data Persistence

        /// <summary>
        /// 씬 전환 전에 세션 데이터를 ScriptableObject에 저장
        /// 에디터에서는 동작하지 않음 (빌드 전용)
        /// </summary>
        public void SaveSessionToData()
        {
#if UNITY_EDITOR
            Debug.Log("[SessionManager] SaveSessionToData skipped in Editor");
            return;
#else
            if (_sessionData == null)
            {
                Debug.LogError("[SessionManager] SessionDataSO not found!");
                return;
            }

            _sessionData.SaveSessionData(
                relayCode: _currentRelayJoinCode,
                lobbyId: _currentLobby?.Id,
                sessionId: Session.SessionId,
                isHost: IsHost,
                localPlayerId: Session.LocalPlayerId,
                localPlayerName: Session.GetPlayer(Session.LocalPlayerId)?.DisplayName ?? "Unknown",
                config: Session.Config,
                sessionPlayers: Session.Players.Values,
                state: Session.State
            );

            Debug.Log("[SessionManager] Session data saved to ScriptableObject");
#endif
        }

        /// <summary>
        /// 씬 전환 후 ScriptableObject에서 세션 데이터 복원 시도
        /// 에디터에서는 동작하지 않음 (빌드 전용)
        /// </summary>
        private void TryRestoreSessionFromData()
        {
#if UNITY_EDITOR
            Debug.Log("[SessionManager] TryRestoreSessionFromData skipped in Editor");
            return;
#else
            if (_sessionData == null || !_sessionData.HasValidData)
            {
                Debug.Log("[SessionManager] No session data to restore");
                return;
            }

            // PendingRestore 플래그 체크 - 씬 전환 직후에만 복원
            if (!_sessionData.PendingRestore)
            {
                Debug.Log("[SessionManager] No pending restore, skipping");
                return;
            }

            Debug.Log("[SessionManager] Restoring session from saved data...");

            // 이미 세션이 진행 중이면 복원하지 않음
            if (Session.State != SessionState.None)
            {
                Debug.Log("[SessionManager] Session already active, skipping restore");
                _sessionData.MarkRestoreComplete();
                return;
            }

            _currentRelayJoinCode = _sessionData.RelayJoinCode;

            // SessionConfig 복원
            var config = _sessionData.GetSessionConfig();

            // 세션 재생성
            if (_sessionData.IsHost)
            {
                Session.Create(config, _sessionData.LocalPlayerId, _sessionData.LocalPlayerName);
            }
            else
            {
                Session.Join(_sessionData.SessionId, _sessionData.LocalPlayerId, _sessionData.LocalPlayerName);
            }

            // 다른 플레이어들 복원
            foreach (var playerData in _sessionData.Players)
            {
                if (playerData.playerId == _sessionData.LocalPlayerId) continue;

                var player = playerData.ToSessionPlayer();
                Session.AddPlayer(player);
            }

            // 세션 상태가 InProgress였다면 다시 시작
            if (_sessionData.SessionState == SessionState.InProgress)
            {
                Session.StartSession();
            }

            // 복원 완료 플래그 해제
            _sessionData.MarkRestoreComplete();

            Debug.Log($"[SessionManager] Session restored - IsHost: {IsHost}, Players: {Session.PlayerCount}, RelayCode: {_currentRelayJoinCode}");
#endif
        }

        /// <summary>
        /// 세션 데이터 클리어 (게임 종료 시)
        /// 에디터에서는 동작하지 않음 (빌드 전용)
        /// </summary>
        public void ClearSessionData()
        {
#if !UNITY_EDITOR
            _sessionData?.Clear();
#endif
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// 세션 종료 및 정리 (Lobby 정리는 RoomManager에서 처리)
        /// </summary>
        public void EndSession()
        {
            Debug.Log("[SessionManager] Ending session...");

            // GameSession 종료
            Session.EndSession();

            // ID 매핑 정리
            IdMapper.Clear();

            // NetcodeAdapter 정리
            NetcodeAdapter?.Dispose();

            // NFE 월드 정리
            if (_clientWorld != null)
            {
                NetcodeWorldHelper.DisposeWorld(_clientWorld);
                _clientWorld = null;
            }

            if (_serverWorld != null)
            {
                NetcodeWorldHelper.DisposeWorld(_serverWorld);
                _serverWorld = null;
            }

            _serverRelayData = null;
            _currentRelayJoinCode = null;
            _currentLobby = null;

            // 세션 데이터 클리어
            ClearSessionData();

            Debug.Log("[SessionManager] Session ended");
        }

        private void Cleanup()
        {
            Session.OnStateChanged -= HandleSessionStateChanged;

            NetcodeAdapter?.Dispose();

            // 월드 정리
            if (_clientWorld != null && _clientWorld.IsCreated)
            {
                _clientWorld.Dispose();
            }

            if (_serverWorld != null && _serverWorld.IsCreated)
            {
                _serverWorld.Dispose();
            }
        }

        #endregion

        #region Debug Helpers

        /// <summary>
        /// 현재 세션 상태 디버그 출력
        /// </summary>
        [ContextMenu("Debug Session State")]
        public void DebugSessionState()
        {
            Debug.Log($"=== Session Manager State ===");
            Debug.Log($"IsInGame: {IsInGame}");
            Debug.Log($"IsHost: {IsHost}");
            Debug.Log($"Session State: {Session?.State}");
            Debug.Log($"Player Count: {Session?.PlayerCount}");
            Debug.Log($"Relay Join Code: {_currentRelayJoinCode}");
            Debug.Log($"Server World: {_serverWorld?.Name ?? "null"}");
            Debug.Log($"Client World: {_clientWorld?.Name ?? "null"}");

            if (Session?.Players != null)
            {
                Debug.Log("--- Players ---");
                foreach (var player in Session.Players)
                {
                    Debug.Log($"  {player.Value.DisplayName} (ID: {player.Key}, NetworkId: {player.Value.ConnectionId}, Host: {player.Value.IsHost})");
                }
            }
        }

        #endregion
    }
}
