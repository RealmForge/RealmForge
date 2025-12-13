using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Netcode;

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
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private int maxPlayers = 4;

    private Lobby _currentLobby;
    private bool _isReady = false;
    private float _nextPollTime;
    private bool _isInRoom = false;

    private void Update()
    {
        // 방에 있을 때만 로비 상태를 주기적으로 폴링
        if (_isInRoom && Time.time >= _nextPollTime)
        {
            _nextPollTime = Time.time + lobbyPollInterval;
            _ = PollLobbyData();
        }
    }

    #region Room Entry

    public async void EnterRoom(Lobby lobby)
    {
        _currentLobby = lobby;
        _isReady = false;
        _isInRoom = true;

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
        if (_currentLobby == null) return;

        try
        {
            _currentLobby = await LobbyService.Instance.GetLobbyAsync(_currentLobby.Id);
            UpdateRoomUI();

            // 게임 시작 확인 (Host가 RelayJoinCode를 설정했는지)
            if (_currentLobby.Data != null && _currentLobby.Data.ContainsKey("RelayJoinCode"))
            {
                string joinCode = _currentLobby.Data["RelayJoinCode"].Value;

                // 이미 게임이 시작되었으면 참가
                if (!string.IsNullOrEmpty(joinCode))
                {
                    Debug.Log($"[ROOM] Game started detected. Joining with code: {joinCode}");
                    await JoinGame(joinCode);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ROOM] Failed to poll lobby data: {e.Message}");
        }
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
        if (AuthenticationService.Instance.PlayerId != _currentLobby.HostId)
        {
            Debug.LogWarning("[ROOM] Only host can start the game!");
            return;
        }

        Debug.Log("[ROOM] Starting game...");

        try
        {
            // Relay 생성 및 Join Code 획득
            string joinCode = await RelayManager.Instance.CreateRelayAsHost(maxPlayers);

            // Lobby 데이터에 Join Code 저장
            UpdateLobbyOptions options = new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, joinCode) }
                }
            };

            await LobbyService.Instance.UpdateLobbyAsync(_currentLobby.Id, options);

            Debug.Log($"[ROOM] Relay created and join code stored: {joinCode}");

            // Host로 네트워크 시작
            NetworkManager.Singleton.StartHost();

            // 씬 전환
            _isInRoom = false;
            SceneManager.LoadScene(gameSceneName);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ROOM] Failed to start game: {e.Message}");
        }
    }

    private async void OnExitButtonClicked()
    {
        await ExitRoom();
    }

    #endregion

    #region Game Start

    private async Task JoinGame(string joinCode)
    {
        // 중복 참가 방지
        if (!_isInRoom) return;

        _isInRoom = false;

        try
        {
            // Relay에 연결
            await RelayManager.Instance.JoinRelayAsClient(joinCode);

            // Client로 네트워크 시작
            NetworkManager.Singleton.StartClient();

            // 씬 전환
            SceneManager.LoadScene(gameSceneName);

            Debug.Log($"[ROOM] Joined game with relay code: {joinCode}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ROOM] Failed to join game: {e.Message}");
            _isInRoom = true;
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
