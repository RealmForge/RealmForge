# Session System Documentation

RealmForge 세션 시스템은 Unity Relay와 Netcode for Entities(NFE)를 통합하여 인게임 멀티플레이어 세션을 관리합니다.

> **Note**: Lobby 관련 기능은 `Lobby` 폴더의 `LobbyManager`와 `RoomManager`가 담당합니다.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                       SessionManager                            │
│              (MonoBehaviour, Singleton, 인게임 전용)            │
└─────────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
┌───────────────┐       ┌───────────────┐       ┌───────────────┐
│  GameSession  │       │PlayerIdMapper │       │NetcodeSession │
│ (State Mgmt)  │       │ (ID Convert)  │       │   Adapter     │
└───────────────┘       └───────────────┘       └───────────────┘
        │                     │                       │
        ▼                     ▼                       ▼
┌───────────────┐       ┌───────────────┐       ┌───────────────┐
│   Players     │       │  ID Mapping   │       │  NFE World    │
│   (Runtime)   │       │ Auth↔Play↔Net │       │   + Relay     │
└───────────────┘       └───────────────┘       └───────────────┘
```

---

## 계층 구조 (3-Tier Architecture)

```
┌─────────────┐
│   Lobby     │  LobbyManager: 방 목록 조회, 생성, 참여
│  (탐색)     │  → Unity Lobby Service
└─────┬───────┘
      │ EnterRoom()
      ▼
┌─────────────┐
│    Room     │  RoomManager: 방 대기, Ready 상태, 게임 시작 트리거
│   (대기)    │  → Lobby 폴링, 하트비트 관리
└─────┬───────┘
      │ StartGame / JoinGame
      ▼
┌─────────────┐
│   Session   │  SessionManager: 인게임 네트워크, 플레이어 동기화
│  (인게임)   │  → Relay + Netcode for Entities
└─────────────┘
```

---

## File Structure

```
Assets/01. Scripts/Session/
├── GameSession.cs           # Core session state management
├── PlayerIdMapper.cs        # ID mapping between systems
├── NetcodeSessionAdapter.cs # NFE ↔ GameSession bridge
├── RelayServiceHelper.cs    # Unity Relay service wrapper
├── NetcodeWorldHelper.cs    # NFE world creation/setup utilities
├── SessionManager.cs        # Top-level orchestrator (인게임 전용)
└── README.md                # This documentation
```

---

## Component Details

### 1. SessionManager.cs

**역할**: 인게임 세션을 관리하는 최상위 오케스트레이터

**주요 기능**:
* Relay 할당 및 연결
* Netcode 월드 생성
* 인게임 플레이어 동기화

**프로퍼티**:

```csharp
public static SessionManager Instance { get; }
public GameSession Session { get; }
public PlayerIdMapper IdMapper { get; }
public NetcodeSessionAdapter NetcodeAdapter { get; }

public bool IsInGame { get; }      // 게임 진행 중 여부
public bool IsHost { get; }        // 호스트 여부
public string CurrentRelayJoinCode { get; }
```

**주요 메서드**:

```csharp
// Room에서 게임 시작 시 호출 - Lobby 플레이어들을 Session에 자동 등록
void InitializeSessionFromLobby(Lobby lobby, string localPlayerName, bool isHost)

// 호스트: 게임 시작 (Relay 할당 + Netcode 서버 생성)
// 반환값: Relay Join Code (RoomManager가 Lobby에 공유)
Task<string> StartGameAsHostAsync()

// 클라이언트: 게임 연결
Task<bool> ConnectToGameAsClientAsync(string relayJoinCode)

// 세션 종료 및 정리
void EndSession()
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

### 2. GameSession.cs

**역할**: 게임 세션의 핵심 상태를 관리하는 순수 C# 클래스

**상태 흐름**:

```
None → Creating → Waiting → Starting → InProgress → Ending → Closed
```

**주요 타입**:

```csharp
public enum SessionState
{
    None,        // 초기 상태
    Creating,    // 세션 생성 중
    Waiting,     // 플레이어 대기
    Starting,    // 게임 시작 중
    InProgress,  // 게임 진행 중
    Ending,      // 종료 중
    Closed       // 완전 종료
}

public enum PlayerConnectionState
{
    Connecting,   // 연결 중
    Connected,    // 연결됨
    Disconnected, // 연결 끊김
    Migrating     // 호스트 마이그레이션 중
}

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

**인게임에서 플레이어 조회**:

```csharp
// 모든 플레이어 순회
foreach (var kvp in SessionManager.Instance.Session.Players)
{
    var player = kvp.Value;
    Debug.Log($"{player.DisplayName}, Host: {player.IsHost}");
}

// 특정 플레이어 조회
var player = SessionManager.Instance.Session.GetPlayer(playerId);

// NetworkId로 PlayerId 찾기
if (SessionManager.Instance.IdMapper.TryGetPlayerId(networkId, out var playerId))
{
    // 처리
}
```

---

### 3. PlayerIdMapper.cs

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

// ID 변환
bool TryGetPlayerId(string authId, out ulong playerId)
bool TryGetPlayerId(int networkId, out ulong playerId)
bool TryGetNetworkId(ulong playerId, out int networkId)
bool TryGetAuthId(ulong playerId, out string authId)
```

---

### 4. NetcodeSessionAdapter.cs

**역할**: Netcode for Entities(NFE) 연결 상태와 GameSession 동기화

**초기화**:

```csharp
void InitializeAsServer(World serverWorld)
void InitializeAsClient(World clientWorld)
```

**이벤트**:

```csharp
public event Action OnServerStarted;
public event Action OnClientConnected;
public event Action<int> OnClientDisconnected;
```

---

### 5. RelayServiceHelper.cs

**역할**: Unity Relay 서비스 API 래퍼

```csharp
// 호스트: Relay 할당 및 Join Code 생성
static async Task<(RelayServerData serverData, string joinCode)?> AllocateRelayServerAsync(int maxConnections)

// 클라이언트: Join Code로 Relay 연결
static async Task<RelayServerData?> JoinRelayAsync(string joinCode)
```

---

### 6. NetcodeWorldHelper.cs

**역할**: NFE World 생성, Relay 설정, 정리를 위한 유틸리티

```csharp
static World CreateServerWorld(string worldName = "ServerWorld")
static World CreateClientWorld(string worldName = "ClientWorld")
static bool SetupServerWithRelay(World serverWorld, RelayServerData relayServerData)
static bool SetupClientWithRelay(World clientWorld, RelayServerData relayClientData)
static void DisposeWorld(World world)
```

---

## Session Lifecycle

### 1. Game Start (Host)

```
RoomManager.OnStartButtonClicked()
    ↓
1. LobbyService.LockLobby()           // 새 참가 방지
    ↓
2. SessionManager.InitializeSessionFromLobby()
   → Lobby 플레이어들을 GameSession에 자동 등록
   → PlayerIdMapper에 AuthId → PlayerId 매핑
    ↓
3. SessionManager.StartGameAsHostAsync()
   → RelayServiceHelper.AllocateRelayServerAsync()
   → NetcodeWorldHelper.CreateServerWorld()
   → NetcodeWorldHelper.SetupServerWithRelay()
   → 호스트 클라이언트 연결
   → GameSession.StartSession()
   → Relay Join Code 반환
    ↓
4. LobbyService.UpdateLobby(RelayJoinCode)
    ↓
5. SceneManager.LoadScene(gameScene)
```

### 2. Game Connect (Client)

```
RoomManager.PollLobbyData()
    ↓
Relay Join Code 감지
    ↓
1. SessionManager.InitializeSessionFromLobby()
   → Lobby 플레이어들을 GameSession에 자동 등록
    ↓
2. SessionManager.ConnectToGameAsClientAsync()
   → RelayServiceHelper.JoinRelayAsync()
   → NetcodeWorldHelper.CreateClientWorld()
   → NetcodeWorldHelper.SetupClientWithRelay()
    ↓
3. SceneManager.LoadScene(gameScene)
```

### 3. Session End

```
SessionManager.EndSession()
    ↓
GameSession.EndSession()
    ↓
PlayerIdMapper.Clear()
    ↓
NetcodeAdapter.Dispose()
    ↓
NetcodeWorldHelper.DisposeWorld()
```

---

## Usage Example

### 인게임에서 세션 사용

```csharp
using RealmForge.Session;

public class GameController : MonoBehaviour
{
    void Start()
    {
        // 이벤트 구독
        SessionManager.Instance.OnPlayerJoined += OnPlayerJoined;
        SessionManager.Instance.OnPlayerLeft += OnPlayerLeft;

        // 현재 세션 정보 출력
        Debug.Log($"IsHost: {SessionManager.Instance.IsHost}");
        Debug.Log($"Player Count: {SessionManager.Instance.Session.PlayerCount}");
    }

    void OnPlayerJoined(SessionPlayer player)
    {
        Debug.Log($"Player joined: {player.DisplayName}");
    }

    void OnPlayerLeft(SessionPlayer player)
    {
        Debug.Log($"Player left: {player.DisplayName}");
    }

    // 특정 플레이어에게 명령 전송 (예: 킥)
    public void KickPlayer(ulong playerId)
    {
        if (SessionManager.Instance.IsHost)
        {
            if (SessionManager.Instance.IdMapper.TryGetNetworkId(playerId, out var networkId))
            {
                SessionManager.Instance.NetcodeAdapter.DisconnectClient(playerId);
            }
        }
    }

    // 게임 종료
    public void EndGame()
    {
        SessionManager.Instance.EndSession();
    }
}
```

### NetworkId로 플레이어 찾기

```csharp
// Netcode 이벤트에서 NetworkId를 받았을 때
void OnNetworkEvent(int networkId)
{
    var idMapper = SessionManager.Instance.IdMapper;

    if (idMapper.TryGetPlayerId(networkId, out var playerId))
    {
        var player = SessionManager.Instance.Session.GetPlayer(playerId);
        if (player.HasValue)
        {
            Debug.Log($"Event from: {player.Value.DisplayName}");
        }
    }
}
```

---

## ID System Reference

| System | ID Type | Example | When Assigned |
|--------|---------|---------|---------------|
| Unity Lobby | `string` (AuthId) | `"abc123def456"` | Authentication 로그인 시 |
| GameSession | `ulong` (PlayerId) | `1`, `2`, `3` | 게임 시작 시 (InitializeSessionFromLobby) |
| NFE | `int` (NetworkId) | `1`, `7`, `12` | NFE 연결 완료 시 |

---

## Dependencies

### Required Packages

* `com.unity.services.multiplayer` (1.1.8+) - Relay 포함
* `com.unity.netcode` (1.9.0+) - Netcode for Entities
* `com.unity.services.authentication` - Unity Services 인증

### Unity Services Setup

1. Unity Dashboard에서 프로젝트 생성
2. Relay 서비스 활성화
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
