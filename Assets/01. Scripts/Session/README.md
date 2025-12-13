# Session System Documentation

RealmForge 세션 시스템은 Unity Lobby, Unity Relay, 그리고 Netcode for Entities(NFE)를 통합하여 멀티플레이어 게임 세션을 관리합니다.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                       SessionManager                             												  │
│              (MonoBehaviour, Singleton, Lifecycle)             											  │
└─────────────────────────────────────────────────────────────────┘
                              					 	│
        ┌───────────────────────┼───────────────────────┐
        ▼                       					▼                       				   ▼
┌───────────────┐       ┌───────────────┐       ┌───────────────┐
│LobbySession   		│       │  GameSession  		    │       │NetcodeSession 			│
│   Adapter    		 	│◄─►│ (State Mgmt)  		    │◄─►│   Adapter     			│
└───────────────┘       └───────────────┘       └───────────────┘
        │                       					│                       					│
        ▼                       					▼                       					▼
┌───────────────┐       ┌───────────────┐       ┌───────────────┐
│ Unity Lobby   			 │       │PlayerIdMapper 		    │       │  NFE World    		       │
│    Service    			 │       │ (ID Convert)  		    │       │   + Relay     		       │
└───────────────┘       └───────────────┘       └───────────────┘
```

---

## File Structure

```
Assets/01. Scripts/Session/
├── GameSession.cs           # Core session state management
├── PlayerIdMapper.cs        # ID mapping between systems
├── LobbySessionAdapter.cs   # Unity Lobby ↔ GameSession bridge
├── NetcodeSessionAdapter.cs # NFE ↔ GameSession bridge
├── RelayServiceHelper.cs    # Unity Relay service wrapper
├── NetcodeWorldHelper.cs    # NFE world creation/setup utilities
├── SessionManager.cs        # Top-level orchestrator
└── README.md                # This documentation
```

---

## Component Details

### 1\. GameSession.cs

**역할**: 게임 세션의 핵심 상태를 관리하는 순수 C# 클래스

**주요 기능**:

* 세션 상태 머신 관리 (`SessionState` enum)
* 플레이어 목록 관리 (`SessionPlayer` struct)
* 호스트 마이그레이션 로직
* 세션 라이프사이클 이벤트

**상태 흐름**:

```
None → Creating → Waiting → Starting → InProgress → Ending → Closed
```

**주요 타입**:

```csharp
// 세션 상태
public enum SessionState
{
    None,        // 초기 상태
    Creating,    // 세션 생성 중
    Waiting,     // 플레이어 대기 (로비)
    Starting,    // 게임 시작 중
    InProgress,  // 게임 진행 중
    Ending,      // 종료 중
    Closed       // 완전 종료
}

// 플레이어 연결 상태
public enum PlayerConnectionState
{
    Connecting,   // 연결 중
    Connected,    // 연결됨
    Disconnected, // 연결 끊김
    Migrating     // 호스트 마이그레이션 중
}

// 플레이어 정보
public struct SessionPlayer
{
    public ulong PlayerId;           // GameSession 내부 ID
    public string DisplayName;       // 표시 이름
    public int ConnectionId;         // NFE NetworkId
    public PlayerConnectionState State;
    public bool IsHost;
    public float LastHeartbeat;
}
```

**이벤트**:

```csharp
public event Action<SessionState> OnStateChanged;
public event Action<SessionPlayer> OnPlayerJoined;
public event Action<SessionPlayer> OnPlayerLeft;
public event Action<ulong> OnHostMigrated;
public event Action<string> OnSessionError;
```

---

### 2\. PlayerIdMapper.cs

**역할**: 세 가지 시스템 간의 플레이어 ID 변환을 담당

**ID 매핑**:

```
Unity Lobby AuthId (string) ↔ GameSession PlayerId (ulong) ↔ NFE NetworkId (int)
     "abc123def"           ↔           1                   ↔         7
```

**주요 메서드**:

```csharp
// Lobby → GameSession
ulong RegisterFromLobby(string authId)

// NFE 연결 바인딩
void BindNetworkId(ulong playerId, int networkId)
void UnbindNetworkId(ulong playerId)

// ID 변환
bool TryGetPlayerId(string authId, out ulong playerId)
bool TryGetPlayerId(int networkId, out ulong playerId)
bool TryGetAuthId(ulong playerId, out string authId)
bool TryGetNetworkId(ulong playerId, out int networkId)

// 직접 변환 (편의 메서드)
bool TryGetNetworkId(string authId, out int networkId)
bool TryGetAuthId(int networkId, out string authId)
```

**사용 예시**:

```csharp
// Lobby에서 플레이어 등록
var playerId = mapper.RegisterFromLobby(authenticationService.PlayerId);

// NFE 연결 시 바인딩
mapper.BindNetworkId(playerId, networkId.Value);

// ID 변환
if (mapper.TryGetNetworkId(playerId, out var netId))
{
    // NFE 명령 전송
}
```

---

### 3\. LobbySessionAdapter.cs

**역할**: Unity Lobby 서비스와 GameSession 간의 브릿지

**주요 기능**:

* Lobby 생성, 참가, 나가기, 삭제
* Lobby 이벤트 → GameSession 상태 동기화
* Relay Join Code 공유 (Lobby Data)
* 호스트 하트비트 관리

**Lobby 작업**:

```csharp
// 생성
Task<bool> CreateLobbyAsync(string lobbyName, int maxPlayers, bool isPrivate)

// 참가
Task<bool> JoinLobbyByCodeAsync(string lobbyCode)
Task<bool> JoinLobbyByIdAsync(string lobbyId)
Task<bool> QuickJoinAsync()

// 나가기/삭제
Task LeaveLobbyAsync()
Task DeleteLobbyAsync()

// Relay 연동
Task SetRelayJoinCodeAsync(string joinCode)
Task LockLobbyAsync()
```

**이벤트**:

```csharp
public event Action<string> OnRelayJoinCodeReceived;  // 클라이언트가 Join Code 수신
public event Action<Lobby> OnLobbyUpdated;
public event Action OnLobbyDeleted;
```

**내부 동작**:

```
Lobby Player Joined → PlayerIdMapper.RegisterFromLobby() → GameSession.AddPlayer()
Lobby Player Left   → GameSession.RemovePlayer() → PlayerIdMapper.RemovePlayer()
Lobby Host Changed  → GameSession 호스트 마이그레이션
Lobby Data Changed  → Relay Join Code 확인 → OnRelayJoinCodeReceived
```

---

### 4\. NetcodeSessionAdapter.cs

**역할**: Netcode for Entities(NFE) 연결 상태와 GameSession 동기화

**주요 기능**:

* NFE World 초기화 (서버/클라이언트)
* 연결 상태 폴링 및 GameSession 업데이트
* NetworkId ↔ PlayerId 바인딩

**초기화**:

```csharp
// 서버 모드
void InitializeAsServer(World serverWorld)

// 클라이언트 모드
void InitializeAsClient(World clientWorld)
```

**상태 폴링 (Update에서 호출)**:

```csharp
void UpdateConnectionState()
```

**AuthId 바인딩 (RPC 핸들러에서 호출)**:

```csharp
// 클라이언트가 자신의 AuthId를 RPC로 보낸 후 서버에서 호출
void BindClientAuthId(int networkId, string authId)
```

**연결 관리**:

```csharp
void DisconnectClient(ulong playerId)  // 서버에서 클라이언트 강제 연결 해제
```

**이벤트**:

```csharp
public event Action OnServerStarted;
public event Action OnClientConnected;
public event Action<int> OnClientDisconnected;
```

---

### 5\. RelayServiceHelper.cs

**역할**: Unity Relay 서비스 API 래퍼

**주요 메서드**:

```csharp
// 호스트: Relay 할당 및 Join Code 생성
static async Task<(RelayServerData serverData, string joinCode)?>
    AllocateRelayServerAsync(int maxConnections)

// 클라이언트: Join Code로 Relay 연결
static async Task<RelayServerData?> JoinRelayAsync(string joinCode)
```

**사용 예시**:

```csharp
// 호스트
var result = await RelayServiceHelper.AllocateRelayServerAsync(3);
if (result.HasValue)
{
    var (serverData, joinCode) = result.Value;
    // serverData로 서버 설정
    // joinCode를 Lobby에 공유
}

// 클라이언트
var clientData = await RelayServiceHelper.JoinRelayAsync(joinCode);
if (clientData.HasValue)
{
    // clientData로 클라이언트 설정
}
```

**연결 타입**:

* `dtls`: 기본값, 암호화된 UDP
* `udp`: 폴백, 일반 UDP

---

### 6\. NetcodeWorldHelper.cs

**역할**: NFE World 생성, Relay 설정, 정리를 위한 유틸리티

**World 생성**:

```csharp
static World CreateServerWorld(string worldName = "ServerWorld")
static World CreateClientWorld(string worldName = "ClientWorld")
```

**Relay 설정**:

```csharp
// 서버: Relay 설정 + Listen 시작
static bool SetupServerWithRelay(World serverWorld, RelayServerData relayServerData)

// 클라이언트: Relay 설정 + 서버 연결
static bool SetupClientWithRelay(World clientWorld, RelayServerData relayClientData)
```

**정리**:

```csharp
static void DisposeWorld(World world)
static void DisposeAllNetcodeWorlds()
```

**내부 동작 (SetupServerWithRelay)**:

1. NetworkStreamDriver 싱글톤 획득
2. IPC 드라이버 등록 (로컬 연결용)
3. Relay 드라이버 등록 (외부 연결용)
4. DriverStore 리셋
5. Listen 시작

---

### 7\. SessionManager.cs

**역할**: 전체 세션 시스템의 통합 관리자 (MonoBehaviour Singleton)

**프로퍼티**:

```csharp
public static SessionManager Instance { get; }
public GameSession Session { get; }
public PlayerIdMapper IdMapper { get; }
public LobbySessionAdapter LobbyAdapter { get; }
public NetcodeSessionAdapter NetcodeAdapter { get; }

public bool IsInLobby { get; }
public bool IsInGame { get; }
public bool IsHost { get; }
```

**Lobby 작업**:

```csharp
Task<bool> CreateLobbyAsync(string lobbyName, int maxPlayers, bool isPrivate = false)
Task<bool> JoinLobbyByCodeAsync(string lobbyCode)
Task<bool> JoinLobbyByIdAsync(string lobbyId)
Task<bool> QuickJoinAsync()
Task LeaveLobbyAsync()
string GetLobbyCode()
```

**게임 시작**:

```csharp
// 호스트 전용: 전체 게임 시작 프로세스 실행
Task<bool> StartGameAsHostAsync()

// 클라이언트: Relay Join Code로 연결
Task<bool> ConnectToGameAsClientAsync(string relayJoinCode)
```

**세션 종료**:

```csharp
Task EndSessionAsync()
```

**이벤트**:

```csharp
public event Action<SessionState> OnSessionStateChanged;
public event Action<SessionPlayer> OnPlayerJoined;
public event Action<SessionPlayer> OnPlayerLeft;
public event Action OnGameStarting;
public event Action OnGameStarted;
public event Action<string> OnGameStartFailed;
```

---

## Session Lifecycle

### 1\. Lobby Creation (Host)

```csharp
await SessionManager.Instance.CreateLobbyAsync("My Game", 4);
```

```
SessionManager.CreateLobbyAsync()
    ↓
LobbyAdapter.CreateLobbyAsync()
    ↓
Unity Lobby Service: CreateLobbyAsync()
    ↓
PlayerIdMapper.RegisterFromLobby(authId)
    ↓
GameSession.Create(config, playerId, displayName)
    ↓
Subscribe to Lobby events
```

### 2\. Lobby Join (Client)

```csharp
await SessionManager.Instance.JoinLobbyByCodeAsync("ABC123");
```

```
SessionManager.JoinLobbyByCodeAsync()
    ↓
LobbyAdapter.JoinLobbyByCodeAsync()
    ↓
Unity Lobby Service: JoinLobbyByCodeAsync()
    ↓
PlayerIdMapper.RegisterFromLobby(authId)
    ↓
GameSession.Join(sessionId, playerId, displayName)
    ↓
Sync existing players
    ↓
Subscribe to Lobby events
    ↓
Check for existing Relay Join Code
```

### 3\. Game Start (Host)

```csharp
await SessionManager.Instance.StartGameAsHostAsync();
```

```
SessionManager.StartGameAsHostAsync()
    ↓
1. LobbyAdapter.LockLobbyAsync()
    ↓
2. RelayServiceHelper.AllocateRelayServerAsync()
    ↓
3. NetcodeWorldHelper.CreateServerWorld()
    ↓
4. NetcodeWorldHelper.SetupServerWithRelay()
    ↓
5. NetcodeAdapter.InitializeAsServer()
    ↓
6. LobbyAdapter.SetRelayJoinCodeAsync()
    ↓
7. ConnectHostAsClient() - 호스트도 클라이언트로 연결
    ↓
8. GameSession.StartSession()
```

### 4\. Game Connect (Client - Automatic)

```
LobbyAdapter: Relay Join Code received via Lobby Data
    ↓
OnRelayJoinCodeReceived event
    ↓
SessionManager.ConnectToGameAsClientAsync()
    ↓
RelayServiceHelper.JoinRelayAsync()
    ↓
NetcodeWorldHelper.CreateClientWorld()
    ↓
NetcodeWorldHelper.SetupClientWithRelay()
    ↓
NetcodeAdapter.InitializeAsClient()
```

### 5\. Session End

```csharp
await SessionManager.Instance.EndSessionAsync();
```

```
SessionManager.EndSessionAsync()
    ↓
Host: LobbyAdapter.DeleteLobbyAsync()
 -or-
Client: LobbyAdapter.LeaveLobbyAsync()
    ↓
GameSession.EndSession()
    ↓
PlayerIdMapper.Clear()
    ↓
NetcodeAdapter.Dispose()
    ↓
NetcodeWorldHelper.DisposeWorld(clientWorld)
    ↓
NetcodeWorldHelper.DisposeWorld(serverWorld)
```

---

## Usage Example

### Basic Flow

```csharp
using RealmForge.Session;
using Unity.Services.Authentication;
using Unity.Services.Core;

public class GameLobbyUI : MonoBehaviour
{
    async void Start()
    {
        // 1. Unity Services 초기화
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        // 2. SessionManager 이벤트 구독
        SessionManager.Instance.OnPlayerJoined += OnPlayerJoined;
        SessionManager.Instance.OnGameStarted += OnGameStarted;
    }

    // 호스트: 로비 생성
    public async void OnCreateLobbyClicked()
    {
        bool success = await SessionManager.Instance.CreateLobbyAsync("My Game", 4);
        if (success)
        {
            string code = SessionManager.Instance.GetLobbyCode();
            Debug.Log($"Lobby created! Code: {code}");
        }
    }

    // 클라이언트: 로비 참가
    public async void OnJoinLobbyClicked(string code)
    {
        bool success = await SessionManager.Instance.JoinLobbyByCodeAsync(code);
        if (success)
        {
            Debug.Log("Joined lobby!");
            // Relay Join Code 수신 시 자동으로 게임 연결됨
        }
    }

    // 호스트: 게임 시작
    public async void OnStartGameClicked()
    {
        bool success = await SessionManager.Instance.StartGameAsHostAsync();
        if (success)
        {
            Debug.Log("Game started!");
        }
    }

    // 게임 종료
    public async void OnLeaveClicked()
    {
        await SessionManager.Instance.EndSessionAsync();
    }

    void OnPlayerJoined(SessionPlayer player)
    {
        Debug.Log($"Player joined: {player.DisplayName}");
    }

    void OnGameStarted()
    {
        // 게임 씬으로 전환
        SceneManager.LoadScene("GameScene");
    }
}
```

---

## ID System Reference

| System | ID Type | Example | When Assigned |
|--------|---------|---------|---------------|
| Unity Lobby | `string` (AuthId) | `"abc123def456"` | Authentication 로그인 시 |
| GameSession | `ulong` (PlayerId) | `1`, `2`, `3` | Lobby 참가 시 (PlayerIdMapper) |
| NFE | `int` (NetworkId) | `1`, `7`, `12` | NFE 연결 완료 시 |

---

## Dependencies

### Required Packages

* `com.unity.services.lobby` (1.2.2+)
* `com.unity.services.multiplayer` (1.1.8+) - Relay 포함
* `com.unity.netcode` (1.9.0+) - Netcode for Entities
* `com.unity.services.authentication` - Unity Services 인증

### Unity Services Setup

1. Unity Dashboard에서 프로젝트 생성
2. Lobby, Relay 서비스 활성화
3. Project Settings > Services에서 프로젝트 연결

---

## Notes

### NFE Bootstrap 설정

기본 `ClientServerBootstrap`의 자동 월드 생성을 비활성화해야 할 수 있습니다.
씬에 `OverrideAutomaticNetcodeBootstrap` 컴포넌트를 추가하여 제어하세요.

### 호스트 마이그레이션

현재 `GameSession`에 기본적인 호스트 마이그레이션 로직이 있으나,
NFE 서버 월드 재생성은 포함되어 있지 않습니다.
실제 호스트 마이그레이션 구현 시 추가 작업이 필요합니다.

### 에러 처리

프로덕션에서는 각 async 작업에 대한 재시도 로직과
더 세밀한 에러 처리를 추가하는 것을 권장합니다.

