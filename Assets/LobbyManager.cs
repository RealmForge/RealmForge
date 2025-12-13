using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

public class LobbyManager : MonoBehaviour
{
    [Header("JOIN Tab UI")]
    [SerializeField] private Transform lobbyListContainer;
    [SerializeField] private GameObject lobbyListCellPrefab;
    [SerializeField] private InputField joinLobbyNameInput;
    [SerializeField] private InputField joinLobbyPasswordInput;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button quickJoinButton;
    [SerializeField] private Button reconnectButton;
    [SerializeField] private Text playerNameText;
    [SerializeField] private Button renameButton;
    [SerializeField] private GameObject renamePanel;
    [SerializeField] private InputField renameInput;

    [Header("CREATE Tab UI")]
    [SerializeField] private InputField createLobbyNameInput;
    [SerializeField] private InputField createLobbyPasswordInput;
    [SerializeField] private Button createButton;

    [Header("Settings")]
    [SerializeField] private int maxPlayersPerLobby = 4;
    [SerializeField] private float lobbyRefreshInterval = 2f;

    [Header("Room Manager")]
    [SerializeField] private RoomManager roomManager;

    private string _playerName = "Player";
    private Lobby _currentLobby;
    private List<Lobby> _availableLobbies = new List<Lobby>();
    private float _nextRefreshTime;

    private async void Start()
    {
        await InitializeUnityServices();
        SetupUI();
        UpdatePlayerNameDisplay();
    }

    private void Update()
    {
        // 자동으로 로비 목록 갱신
        if (Time.time >= _nextRefreshTime && _currentLobby == null)
        {
            _nextRefreshTime = Time.time + lobbyRefreshInterval;
            _ = RefreshLobbyList();
        }
    }

    #region Unity Services Initialization

    private async Task InitializeUnityServices()
    {
        try
        {
            Debug.Log("[INIT] Initializing Unity Services...");
            await UnityServices.InitializeAsync();
            Debug.Log("[INIT] Unity Services initialized successfully");

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("[INIT] Signing in anonymously...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[INIT] Signed in as: {AuthenticationService.Instance.PlayerId}");
            }
            else
            {
                Debug.Log($"[INIT] Already signed in as: {AuthenticationService.Instance.PlayerId}");
            }

            // 초기 로비 목록 로드
            Debug.Log("[INIT] Loading initial lobby list...");
            await RefreshLobbyList();
        }
        catch (Exception e)
        {
            Debug.LogError($"[ERROR] Failed to initialize Unity Services: {e.Message}");
            Debug.LogError($"[ERROR] Stack trace: {e.StackTrace}");
        }
    }

    #endregion

    #region UI Setup

    private void SetupUI()
    {
        // JOIN Tab
        joinButton.onClick.AddListener(() => _ = JoinLobbyByName());
        quickJoinButton.onClick.AddListener(() => _ = QuickJoinLobby());
        reconnectButton.onClick.AddListener(() => _ = RefreshLobbyList());
        renameButton.onClick.AddListener(OnRenameButtonClicked);

        // CREATE Tab
        createButton.onClick.AddListener(() => _ = CreateLobby());

        // 초기에는 rename panel 숨기기
        if (renamePanel != null)
            renamePanel.SetActive(false);
    }

    #endregion

    #region Nickname Management

    private void UpdatePlayerNameDisplay()
    {
        if (playerNameText != null)
            playerNameText.text = _playerName;
    }

    private void OnRenameButtonClicked()
    {
        if (renamePanel == null) return;

        if (renamePanel.activeSelf)
        {
            // Rename panel이 활성화되어 있으면 이름 변경 후 비활성화
            if (renameInput != null)
            {
                string newName = renameInput.text.Trim();
                if (!string.IsNullOrEmpty(newName))
                {
                    _playerName = newName;
                    UpdatePlayerNameDisplay();
                    Debug.Log($"[RENAME] Player name changed to: {_playerName}");
                }
            }
            renamePanel.SetActive(false);
        }
        else
        {
            // Rename panel 활성화
            if (renameInput != null)
            {
                renameInput.text = _playerName;
                renameInput.ActivateInputField();
            }
            renamePanel.SetActive(true);
        }
    }

    public void ApplyRename()
    {
        // Rename panel 내부의 확인 버튼에 연결할 함수
        if (renameInput == null) return;

        string newName = renameInput.text.Trim();
        if (!string.IsNullOrEmpty(newName))
        {
            _playerName = newName;
            UpdatePlayerNameDisplay();
            Debug.Log($"Player name changed to: {_playerName}");
        }

        if (renamePanel != null)
            renamePanel.SetActive(false);
    }

    #endregion

    #region Lobby List Management

    public async Task RefreshLobbyList()
    {
        try
        {
            Debug.Log("[REFRESH] Querying lobbies...");
            QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync();
            _availableLobbies = queryResponse.Results;
            Debug.Log($"[REFRESH] Found {_availableLobbies.Count} lobbies");

            // 각 로비 정보 출력
            foreach (var lobby in _availableLobbies)
            {
                Debug.Log($"[REFRESH] - Lobby: {lobby.Name}, Players: {lobby.Players.Count}/{lobby.MaxPlayers}, Private: {lobby.IsPrivate}");
            }

            UpdateLobbyListUI();
        }
        catch (Exception e)
        {
            Debug.LogError($"[ERROR] Failed to refresh lobby list: {e.Message}");
            Debug.LogError($"[ERROR] Stack trace: {e.StackTrace}");
        }
    }

    private void UpdateLobbyListUI()
    {
        Debug.Log($"[UI] Updating lobby list UI. Container: {(lobbyListContainer != null ? "OK" : "NULL")}, Prefab: {(lobbyListCellPrefab != null ? "OK" : "NULL")}");

        if (lobbyListContainer == null)
        {
            Debug.LogError("[UI] Lobby list container is NULL! Please assign it in the Inspector.");
            return;
        }

        if (lobbyListCellPrefab == null)
        {
            Debug.LogError("[UI] Lobby list cell prefab is NULL! Please assign it in the Inspector.");
            return;
        }

        // 기존 목록 제거
        int childCount = lobbyListContainer.childCount;
        Debug.Log($"[UI] Removing {childCount} existing lobby cells");
        foreach (Transform child in lobbyListContainer)
        {
            Destroy(child.gameObject);
        }

        // 새로운 목록 생성
        Debug.Log($"[UI] Creating {_availableLobbies.Count} lobby cells");
        foreach (Lobby lobby in _availableLobbies)
        {
            GameObject cellObj = Instantiate(lobbyListCellPrefab, lobbyListContainer);
            LobbyListCell cell = cellObj.GetComponent<LobbyListCell>();

            if (cell != null)
            {
                cell.SetLobbyInfo(lobby, OnLobbyListJoinClicked);
                Debug.Log($"[UI] Created cell for lobby: {lobby.Name}");
            }
            else
            {
                Debug.LogError($"[UI] LobbyListCell component not found on prefab!");
            }
        }
        Debug.Log("[UI] Lobby list UI update complete");
    }

    private async void OnLobbyListJoinClicked(Lobby lobby)
    {
        await JoinLobbyById(lobby.Id);
    }

    #endregion

    #region Create Lobby

    private async Task CreateLobby()
    {
        string lobbyName = createLobbyNameInput.text.Trim();
        string password = createLobbyPasswordInput.text.Trim();

        if (string.IsNullOrEmpty(lobbyName))
        {
            Debug.LogWarning("Lobby name cannot be empty!");
            return;
        }

        try
        {
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = false, // 모든 방을 Public으로 설정하여 Query에 표시
                Player = new Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, _playerName) }
                    }
                },
                Data = new Dictionary<string, DataObject>()
            };

            // 비밀번호가 있으면 데이터에 추가 (Public이지만 비밀번호로 보호)
            if (!string.IsNullOrEmpty(password))
            {
                options.Data.Add("Password", new DataObject(
                    DataObject.VisibilityOptions.Member,
                    password,
                    DataObject.IndexOptions.S1));
            }

            _currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayersPerLobby, options);

            Debug.Log($"[CREATE] Created lobby: {_currentLobby.Name} (ID: {_currentLobby.Id})");
            Debug.Log($"[CREATE] Lobby Code: {_currentLobby.LobbyCode}");
            Debug.Log($"[CREATE] IsPrivate: {_currentLobby.IsPrivate}");

            // 생성 후 입력 필드 초기화
            createLobbyNameInput.text = "";
            createLobbyPasswordInput.text = "";

            // 로비 하트비트 시작 (로비가 자동으로 제거되지 않도록)
            InvokeRepeating(nameof(SendLobbyHeartbeat), 15f, 15f);

            // RoomManager로 방 입장
            if (roomManager != null)
            {
                roomManager.EnterRoom(_currentLobby);
            }
            else
            {
                Debug.LogError("[CREATE] RoomManager is not assigned!");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to create lobby: {e.Message}");
        }
    }

    private async void SendLobbyHeartbeat()
    {
        if (_currentLobby != null)
        {
            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(_currentLobby.Id);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to send heartbeat: {e.Message}");
            }
        }
    }

    #endregion

    #region Join Lobby

    private async Task JoinLobbyByName()
    {
        string lobbyName = joinLobbyNameInput.text.Trim();
        string password = joinLobbyPasswordInput.text.Trim();

        if (string.IsNullOrEmpty(lobbyName))
        {
            Debug.LogWarning("Lobby name cannot be empty!");
            return;
        }

        try
        {
            // 이름으로 로비 찾기
            QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync();
            Lobby targetLobby = queryResponse.Results.Find(l => l.Name == lobbyName);

            if (targetLobby == null)
            {
                Debug.LogWarning($"Lobby '{lobbyName}' not found!");
                return;
            }

            // 비밀번호 확인
            if (targetLobby.Data != null && targetLobby.Data.ContainsKey("Password"))
            {
                string lobbyPassword = targetLobby.Data["Password"].Value;
                if (lobbyPassword != password)
                {
                    Debug.LogWarning("Incorrect password!");
                    return;
                }
            }

            await JoinLobbyById(targetLobby.Id, password);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to join lobby: {e.Message}");
        }
    }

    private async Task JoinLobbyById(string lobbyId, string password = "")
    {
        try
        {
            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions
            {
                Player = new Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, _playerName) }
                    }
                }
            };

            _currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, options);

            Debug.Log($"[JOIN] Joined lobby: {_currentLobby.Name} (ID: {_currentLobby.Id})");
            Debug.Log($"[JOIN] Players in lobby: {_currentLobby.Players.Count}/{_currentLobby.MaxPlayers}");

            // 입력 필드 초기화
            joinLobbyNameInput.text = "";
            joinLobbyPasswordInput.text = "";

            // RoomManager로 방 입장
            if (roomManager != null)
            {
                roomManager.EnterRoom(_currentLobby);
            }
            else
            {
                Debug.LogError("[JOIN] RoomManager is not assigned!");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to join lobby by ID: {e.Message}");
        }
    }

    private async Task QuickJoinLobby()
    {
        try
        {
            QuickJoinLobbyOptions options = new QuickJoinLobbyOptions
            {
                Player = new Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, _playerName) }
                    }
                }
            };

            _currentLobby = await LobbyService.Instance.QuickJoinLobbyAsync(options);

            Debug.Log($"[QUICKJOIN] Quick joined lobby: {_currentLobby.Name} (ID: {_currentLobby.Id})");
            Debug.Log($"[QUICKJOIN] Players in lobby: {_currentLobby.Players.Count}/{_currentLobby.MaxPlayers}");

            // RoomManager로 방 입장
            if (roomManager != null)
            {
                roomManager.EnterRoom(_currentLobby);
            }
            else
            {
                Debug.LogError("[QUICKJOIN] RoomManager is not assigned!");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to quick join lobby: {e.Message}");
        }
    }

    #endregion

    #region Leave Lobby

    public async Task LeaveLobby()
    {
        if (_currentLobby == null) return;

        try
        {
            await LobbyService.Instance.RemovePlayerAsync(_currentLobby.Id, AuthenticationService.Instance.PlayerId);
            Debug.Log($"Left lobby: {_currentLobby.Name}");

            CancelInvoke(nameof(SendLobbyHeartbeat));
            _currentLobby = null;

            await RefreshLobbyList();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to leave lobby: {e.Message}");
        }
    }

    #endregion

    private void OnDestroy()
    {
        if (_currentLobby != null)
        {
            _ = LeaveLobby();
        }
    }
}
