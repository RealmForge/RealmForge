using System;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.Networking.Transport.Relay;
using UnityEngine;

namespace RealmForge.Session
{
    /// <summary>
    /// GameSession, LobbySessionAdapter, NetcodeSessionAdapter를 통합 관리
    /// 세션 라이프사이클 전체를 조율하는 최상위 매니저
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private bool _autoHeartbeat = true;

        // Core Components
        public GameSession Session { get; private set; }
        public PlayerIdMapper IdMapper { get; private set; }
        public LobbySessionAdapter LobbyAdapter { get; private set; }
        public NetcodeSessionAdapter NetcodeAdapter { get; private set; }

        // NFE Worlds
        private World _serverWorld;
        private World _clientWorld;
        private RelayServerData? _serverRelayData;
        private string _currentRelayJoinCode;

        // State
        public bool IsInLobby => LobbyAdapter?.CurrentLobby != null;
        public bool IsInGame => Session?.State == SessionState.InProgress;
        public bool IsHost => Session?.IsHost ?? false;

        // Events
        public event Action<SessionState> OnSessionStateChanged;
        public event Action<SessionPlayer> OnPlayerJoined;
        public event Action<SessionPlayer> OnPlayerLeft;
        public event Action OnGameStarting;
        public event Action OnGameStarted;
        public event Action<string> OnGameStartFailed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeComponents();
        }

        private void InitializeComponents()
        {
            Session = new GameSession();
            IdMapper = new PlayerIdMapper();
            LobbyAdapter = new LobbySessionAdapter(Session, IdMapper);
            NetcodeAdapter = new NetcodeSessionAdapter(Session, IdMapper);

            // 이벤트 구독
            Session.OnStateChanged += HandleSessionStateChanged;
            Session.OnPlayerJoined += player => OnPlayerJoined?.Invoke(player);
            Session.OnPlayerLeft += player => OnPlayerLeft?.Invoke(player);

            LobbyAdapter.OnRelayJoinCodeReceived += HandleRelayJoinCodeReceived;
        }

        private void Update()
        {
            if (_autoHeartbeat)
            {
                LobbyAdapter?.UpdateHeartbeat(Time.deltaTime);
            }

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

        #region Lobby Operations

        /// <summary>
        /// 새 로비 생성
        /// </summary>
        public async Task<bool> CreateLobbyAsync(string lobbyName, int maxPlayers, bool isPrivate = false)
        {
            return await LobbyAdapter.CreateLobbyAsync(lobbyName, maxPlayers, isPrivate);
        }

        /// <summary>
        /// 로비 코드로 참가
        /// </summary>
        public async Task<bool> JoinLobbyByCodeAsync(string lobbyCode)
        {
            return await LobbyAdapter.JoinLobbyByCodeAsync(lobbyCode);
        }

        /// <summary>
        /// 로비 ID로 참가
        /// </summary>
        public async Task<bool> JoinLobbyByIdAsync(string lobbyId)
        {
            return await LobbyAdapter.JoinLobbyByIdAsync(lobbyId);
        }

        /// <summary>
        /// 퀵 조인
        /// </summary>
        public async Task<bool> QuickJoinAsync()
        {
            return await LobbyAdapter.QuickJoinAsync();
        }

        /// <summary>
        /// 로비 나가기
        /// </summary>
        public async Task LeaveLobbyAsync()
        {
            await LobbyAdapter.LeaveLobbyAsync();
        }

        /// <summary>
        /// 현재 로비 코드 가져오기
        /// </summary>
        public string GetLobbyCode()
        {
            return LobbyAdapter?.CurrentLobby?.LobbyCode;
        }

        #endregion

        #region Game Start Operations

        /// <summary>
        /// 게임 시작 (호스트 전용)
        /// 1. Lobby 잠금
        /// 2. Relay 할당
        /// 3. NFE 서버 월드 생성 및 Relay 설정
        /// 4. Lobby에 Relay Join Code 공유
        /// 5. 호스트 클라이언트 연결
        /// </summary>
        public async Task<bool> StartGameAsHostAsync()
        {
            if (!IsHost)
            {
                Debug.LogError("[SessionManager] Only host can start the game");
                OnGameStartFailed?.Invoke("Only host can start the game");
                return false;
            }

            if (Session.State != SessionState.Waiting)
            {
                Debug.LogError("[SessionManager] Cannot start game in current state");
                OnGameStartFailed?.Invoke("Invalid session state");
                return false;
            }

            OnGameStarting?.Invoke();

            try
            {
                // 1. Lobby 잠금
                await LobbyAdapter.LockLobbyAsync();
                Debug.Log("[SessionManager] Lobby locked");

                // 2. Relay 할당 및 Join Code 획득
                var maxConnections = Session.Config.MaxPlayers - 1; // 호스트 제외
                var relayResult = await RelayServiceHelper.AllocateRelayServerAsync(maxConnections);

                if (!relayResult.HasValue)
                {
                    Debug.LogError("[SessionManager] Failed to allocate relay");
                    OnGameStartFailed?.Invoke("Failed to allocate relay server");
                    return false;
                }

                _serverRelayData = relayResult.Value.serverData;
                _currentRelayJoinCode = relayResult.Value.joinCode;
                Debug.Log($"[SessionManager] Relay allocated, join code: {_currentRelayJoinCode}");

                // 3. NFE 서버 월드 생성
                _serverWorld = NetcodeWorldHelper.CreateServerWorld("SessionServerWorld");
                if (_serverWorld == null)
                {
                    Debug.LogError("[SessionManager] Failed to create server world");
                    OnGameStartFailed?.Invoke("Failed to create server world");
                    return false;
                }

                // 4. 서버에 Relay 설정 적용
                if (!NetcodeWorldHelper.SetupServerWithRelay(_serverWorld, _serverRelayData.Value))
                {
                    Debug.LogError("[SessionManager] Failed to setup server with relay");
                    OnGameStartFailed?.Invoke("Failed to setup server with relay");
                    NetcodeWorldHelper.DisposeWorld(_serverWorld);
                    _serverWorld = null;
                    return false;
                }

                NetcodeAdapter.InitializeAsServer(_serverWorld);
                Debug.Log("[SessionManager] Server world created and listening");

                // 5. Lobby에 Relay Join Code 공유
                await LobbyAdapter.SetRelayJoinCodeAsync(_currentRelayJoinCode);
                Debug.Log("[SessionManager] Relay join code shared to lobby");

                // 6. 호스트도 클라이언트 월드 생성하여 연결
                await ConnectHostAsClient(_currentRelayJoinCode);

                // 7. GameSession 시작
                Session.StartSession();

                OnGameStarted?.Invoke();
                Debug.Log("[SessionManager] Game started successfully as host");
                return true;
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

                return false;
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

        private void HandleRelayJoinCodeReceived(string joinCode)
        {
            Debug.Log($"[SessionManager] Received relay join code: {joinCode}");

            // 호스트가 아닌 클라이언트는 자동으로 연결
            if (!IsHost && Session.State == SessionState.Waiting)
            {
                _ = ConnectToGameAsClientAsync(joinCode);
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// 세션 종료 및 정리
        /// </summary>
        public async Task EndSessionAsync()
        {
            Debug.Log("[SessionManager] Ending session...");

            // Lobby 정리
            if (IsHost)
            {
                await LobbyAdapter.DeleteLobbyAsync();
            }
            else
            {
                await LobbyAdapter.LeaveLobbyAsync();
            }

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

            Debug.Log("[SessionManager] Session ended");
        }

        private void Cleanup()
        {
            Session.OnStateChanged -= HandleSessionStateChanged;
            LobbyAdapter.OnRelayJoinCodeReceived -= HandleRelayJoinCodeReceived;

            LobbyAdapter?.Dispose();
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
            Debug.Log($"IsInLobby: {IsInLobby}");
            Debug.Log($"IsInGame: {IsInGame}");
            Debug.Log($"IsHost: {IsHost}");
            Debug.Log($"Session State: {Session?.State}");
            Debug.Log($"Player Count: {Session?.PlayerCount}");
            Debug.Log($"Lobby Code: {GetLobbyCode()}");
            Debug.Log($"Relay Join Code: {_currentRelayJoinCode}");
            Debug.Log($"Server World: {_serverWorld?.Name ?? "null"}");
            Debug.Log($"Client World: {_clientWorld?.Name ?? "null"}");
        }

        #endregion
    }
}
