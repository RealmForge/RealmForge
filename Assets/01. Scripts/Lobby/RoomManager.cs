using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using RealmForge.Session;

public class RoomManager : MonoBehaviour
{
    [Header("Canvas References")]
    [SerializeField] private GameObject roomCanvas;
    [SerializeField] private GameObject joinCreateCanvas;
    [SerializeField] private GameObject renameCanvas;

    [Header("Interaction Panel")]
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button exitButton;

    [Header("Lobby Panel")]
    [SerializeField] private Text lobbyNameText;

    [Header("User List")]
    [SerializeField] private Transform userListContainer;
    [SerializeField] private GameObject userPanelPrefab;

    [Header("Settings")]
    [SerializeField] private float lobbyPollInterval = 1.5f;
    [SerializeField] private float heartbeatInterval = 15f;
    [SerializeField] private string gameSceneName = "GameScene";

    private Lobby _currentLobby;
    private bool _isReady = false;
    private float _nextPollTime;
    private float _nextHeartbeatTime;
    private bool _isInRoom = false;
    private bool _isGameStarting = false;
    private string _localPlayerName;

    private void Update()
    {
        if (!_isInRoom) return;

        // 로비 상태를 주기적으로 폴링
        if (Time.time >= _nextPollTime)
        {
            _nextPollTime = Time.time + lobbyPollInterval;
            _ = PollLobbyData();
        }

        // 호스트만 하트비트 전송
        if (IsHost() && Time.time >= _nextHeartbeatTime)
        {
            _nextHeartbeatTime = Time.time + heartbeatInterval;
            _ = SendHeartbeat();
        }
    }

    private bool IsHost()
    {
        return _currentLobby != null &&
               _currentLobby.HostId == AuthenticationService.Instance.PlayerId;
    }

    private async Task SendHeartbeat()
    {
        if (_currentLobby == null) return;

        try
        {
            await LobbyService.Instance.SendHeartbeatPingAsync(_currentLobby.Id);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ROOM] Heartbeat failed: {e.Message}");
        }
    }

    #region Room Entry

    public void EnterRoom(Lobby lobby, string playerName)
    {
        _currentLobby = lobby;
        _localPlayerName = playerName;
        _isReady = false;
        _isInRoom = true;
        _isGameStarting = false;

        // Canvas 전환
        if (roomCanvas != null) roomCanvas.SetActive(true);
        if (joinCreateCanvas != null) joinCreateCanvas.SetActive(false);
        if (renameCanvas != null) renameCanvas.SetActive(false);

        // UI 초기화
        SetupRoomUI();
        UpdateRoomUI();

        // 버튼 리스너 설정
        if (readyButton != null)
            readyButton.onClick.AddListener(OnReadyButtonClicked);
        if (startButton != null)
            startButton.onClick.AddListener(OnStartButtonClicked);
        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitButtonClicked);

        Debug.Log($"[ROOM] Entered room: {lobby.Name}");
    }

    // 기존 호환성을 위한 오버로드
    public void EnterRoom(Lobby lobby)
    {
        var playerName = GetLocalPlayerName();
        EnterRoom(lobby, playerName);
    }

    private string GetLocalPlayerName()
    {
        // Lobby에서 로컬 플레이어 이름 가져오기
        if (_currentLobby != null)
        {
            var localPlayerId = AuthenticationService.Instance.PlayerId;
            foreach (var player in _currentLobby.Players)
            {
                if (player.Id == localPlayerId)
                {
                    return GetPlayerName(player);
                }
            }
        }
        return $"Player_{AuthenticationService.Instance.PlayerId[..6]}";
    }

    private void SetupRoomUI()
    {
        // 로비 이름 표시
        if (lobbyNameText != null && _currentLobby != null)
            lobbyNameText.text = _currentLobby.Name;
    }

    #endregion

    #region Lobby Polling

    private async Task PollLobbyData()
    {
        if (_currentLobby == null || _isGameStarting) return;

        try
        {
            _currentLobby = await LobbyService.Instance.GetLobbyAsync(_currentLobby.Id);
            UpdateRoomUI();

            // 클라이언트만: 게임 시작 확인 (Host가 RelayJoinCode를 설정했는지)
            if (!IsHost() && _currentLobby.Data != null && _currentLobby.Data.ContainsKey("RelayJoinCode"))
            {
                string joinCode = _currentLobby.Data["RelayJoinCode"].Value;

                // 이미 게임이 시작되었으면 참가
                if (!string.IsNullOrEmpty(joinCode))
                {
                    Debug.Log($"[ROOM] Game started detected. Joining with code: {joinCode}");
                    _isGameStarting = true;
                    await JoinGameAsClient(joinCode);
                }
            }
        }
        catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyNotFound)
        {
            Debug.Log("[ROOM] Lobby no longer exists");
            await HandleLobbyDeleted();
        }
        catch (Exception e)
        {
            Debug.LogError($"[ROOM] Failed to poll lobby data: {e.Message}");
        }
    }

    private async Task HandleLobbyDeleted()
    {
        _isInRoom = false;
        _currentLobby = null;

        // Canvas 전환
        if (roomCanvas != null) roomCanvas.SetActive(false);
        if (joinCreateCanvas != null) joinCreateCanvas.SetActive(true);

        // LobbyManager의 목록 갱신
        LobbyManager lobbyManager = FindObjectOfType<LobbyManager>();
        if (lobbyManager != null)
            await lobbyManager.RefreshLobbyList();
    }

    #endregion

    #region UI Update

    private void UpdateRoomUI()
    {
        if (_currentLobby == null) return;

        UpdatePlayerList();
        UpdateStartButton();
    }

    private void UpdatePlayerList()
    {
        if (userListContainer == null || userPanelPrefab == null) return;

        // 기존 목록 제거
        foreach (Transform child in userListContainer)
        {
            Destroy(child.gameObject);
        }

        // 플레이어 목록 생성
        string myPlayerId = AuthenticationService.Instance.PlayerId;
        foreach (Player player in _currentLobby.Players)
        {
            GameObject panelObj = Instantiate(userPanelPrefab, userListContainer);
            UserPanel userPanel = panelObj.GetComponent<UserPanel>();

            if (userPanel != null)
            {
                // 플레이어 데이터 가져오기
                string playerName = GetPlayerName(player);
                bool isHost = player.Id == _currentLobby.HostId;
                bool isReady = GetPlayerReadyStatus(player);

                userPanel.SetPlayerInfo(playerName, isHost, isReady);
            }
        }
    }

    private void UpdateStartButton()
    {
        if (startButton == null) return;

        // Host만 Start 버튼 사용 가능
        bool isHost = AuthenticationService.Instance.PlayerId == _currentLobby.HostId;

        if (!isHost)
        {
            startButton.interactable = false;
            return;
        }

        // 모든 플레이어가 Ready인지 확인 (Host 제외)
        bool allReady = true;
        foreach (Player player in _currentLobby.Players)
        {
            // Host는 체크하지 않음
            if (player.Id == _currentLobby.HostId)
                continue;

            if (!GetPlayerReadyStatus(player))
            {
                allReady = false;
                break;
            }
        }

        startButton.interactable = allReady && _currentLobby.Players.Count > 1;
    }

    #endregion

    #region Button Handlers

    private async void OnReadyButtonClicked()
    {
        _isReady = !_isReady;

        try
        {
            // 자신의 Ready 상태 업데이트
            string playerId = AuthenticationService.Instance.PlayerId;
            UpdatePlayerOptions options = new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, _isReady.ToString()) }
                }
            };

            _currentLobby = await LobbyService.Instance.UpdatePlayerAsync(_currentLobby.Id, playerId, options);

            Debug.Log($"[ROOM] Ready status changed to: {_isReady}");

            UpdateRoomUI();
        }
        catch (Exception e)
        {
            Debug.LogError($"[ROOM] Failed to update ready status: {e.Message}");
            _isReady = !_isReady; // 실패 시 되돌림
        }
    }

    private async void OnStartButtonClicked()
    {
        // Host만 호출 가능
        if (!IsHost())
        {
            Debug.LogWarning("[ROOM] Only host can start the game!");
            return;
        }

        if (_isGameStarting)
        {
            Debug.LogWarning("[ROOM] Game is already starting!");
            return;
        }

        Debug.Log("[ROOM] Starting game...");
        _isGameStarting = true;

        try
        {
            // 1. Lobby 잠금
            await LockLobby();

            // 2. SessionManager에 Lobby 플레이어 등록
            if (_localPlayerName == null)
            {
                _localPlayerName = GetLocalPlayerName();
            }
            SessionManager.Instance.InitializeSessionFromLobby(_currentLobby, _localPlayerName, true);

            // 3. SessionManager로 게임 시작 (Relay 할당 + Netcode 서버 생성)
            string relayJoinCode = await SessionManager.Instance.StartGameAsHostAsync();

            if (string.IsNullOrEmpty(relayJoinCode))
            {
                Debug.LogError("[ROOM] Failed to start game - no relay join code");
                _isGameStarting = false;
                return;
            }

            // 4. Lobby에 Relay Join Code 공유 (클라이언트들이 감지)
            await SetRelayJoinCode(relayJoinCode);

            Debug.Log($"[ROOM] Game started! Relay code: {relayJoinCode}");

            // 5. 게임 씬으로 전환
            LoadGameScene();
        }
        catch (Exception e)
        {
            Debug.LogError($"[ROOM] Failed to start game: {e.Message}");
            _isGameStarting = false;
        }
    }

    private async Task LockLobby()
    {
        if (_currentLobby == null) return;

        try
        {
            var options = new UpdateLobbyOptions
            {
                IsLocked = true
            };
            _currentLobby = await LobbyService.Instance.UpdateLobbyAsync(_currentLobby.Id, options);
            Debug.Log("[ROOM] Lobby locked");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ROOM] Failed to lock lobby: {e.Message}");
        }
    }

    private async Task SetRelayJoinCode(string joinCode)
    {
        if (_currentLobby == null) return;

        try
        {
            var options = new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, joinCode) }
                }
            };
            _currentLobby = await LobbyService.Instance.UpdateLobbyAsync(_currentLobby.Id, options);
            Debug.Log($"[ROOM] Relay join code set: {joinCode}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ROOM] Failed to set relay join code: {e.Message}");
        }
    }

    private async void OnExitButtonClicked()
    {
        await ExitRoom();
    }

    #endregion

    #region Game Start

    private async Task JoinGameAsClient(string joinCode)
    {
        try
        {
            // 1. SessionManager에 Lobby 플레이어 등록
            if (_localPlayerName == null)
            {
                _localPlayerName = GetLocalPlayerName();
            }
            SessionManager.Instance.InitializeSessionFromLobby(_currentLobby, _localPlayerName, false);

            // 2. SessionManager로 게임 연결
            bool connected = await SessionManager.Instance.ConnectToGameAsClientAsync(joinCode);

            if (!connected)
            {
                Debug.LogError("[ROOM] Failed to connect to game");
                _isGameStarting = false;
                return;
            }

            Debug.Log("[ROOM] Connected to game as client!");

            // 3. 게임 씬으로 전환
            LoadGameScene();
        }
        catch (Exception e)
        {
            Debug.LogError($"[ROOM] Failed to join game: {e.Message}");
            _isGameStarting = false;
        }
    }

    private void LoadGameScene()
    {
        if (!string.IsNullOrEmpty(gameSceneName))
        {
            // 씬 전환 전에 세션 데이터 저장
            SessionManager.Instance.SaveSessionToData();

            SceneManager.LoadScene(gameSceneName);
        }
        else
        {
            Debug.LogWarning("[ROOM] Game scene name is not set!");
        }
    }

    #endregion

    #region Exit Room

    public async Task ExitRoom()
    {
        if (_currentLobby == null) return;

        try
        {
            string playerId = AuthenticationService.Instance.PlayerId;
            await LobbyService.Instance.RemovePlayerAsync(_currentLobby.Id, playerId);

            Debug.Log($"[ROOM] Left room: {_currentLobby.Name}");

            _currentLobby = null;
            _isReady = false;
            _isInRoom = false;

            // Canvas 전환
            if (roomCanvas != null) roomCanvas.SetActive(false);
            if (joinCreateCanvas != null) joinCreateCanvas.SetActive(true);

            // LobbyManager의 목록 갱신
            LobbyManager lobbyManager = FindObjectOfType<LobbyManager>();
            if (lobbyManager != null)
                await lobbyManager.RefreshLobbyList();
        }
        catch (Exception e)
        {
            Debug.LogError($"[ROOM] Failed to exit room: {e.Message}");
        }
    }

    #endregion

    #region Helper Methods

    private string GetPlayerName(Player player)
    {
        if (player.Data != null && player.Data.ContainsKey("PlayerName"))
        {
            return player.Data["PlayerName"].Value;
        }
        return "Unknown";
    }

    private bool GetPlayerReadyStatus(Player player)
    {
        if (player.Data != null && player.Data.ContainsKey("IsReady"))
        {
            bool.TryParse(player.Data["IsReady"].Value, out bool isReady);
            return isReady;
        }
        return false;
    }

    #endregion

    private void OnDestroy()
    {
        if (_currentLobby != null)
        {
            _ = ExitRoom();
        }
    }
}
