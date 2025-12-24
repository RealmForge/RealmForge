# Lobby & Room System Documentation

RealmForge 로비 시스템은 Unity Lobby Service를 활용하여 방 탐색, 생성, 대기 기능을 제공합니다.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                      LobbyManager                                │
│            (방 탐색, 생성, 참여 - 메인 메뉴)                      │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ EnterRoom(lobby, playerName)
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      RoomManager                                 │
│      (방 대기, Ready 상태, 하트비트, 게임 시작 트리거)            │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ InitializeSessionFromLobby()
                              │ StartGameAsHostAsync()
                              │ ConnectToGameAsClientAsync()
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                     SessionManager                               │
│              (인게임 네트워크 - Session 폴더)                     │
└─────────────────────────────────────────────────────────────────┘
```

---

## 계층별 책임

| 계층 | 클래스 | 역할 | 시점 |
|------|--------|------|------|
| **Lobby** | LobbyManager | 방 목록 조회, 생성, 참여 | 메인 메뉴 |
| **Room** | RoomManager | 플레이어 대기, Ready 상태, 게임 시작 트리거 | 게임 시작 전 |
| **Session** | SessionManager | Netcode 연결, 인게임 동기화 | 인게임 |

---

## File Structure

```
Assets/01. Scripts/Lobby/
├── LobbyManager.cs      # 로비 탐색 및 방 생성/참여
├── RoomManager.cs       # 방 대기 및 게임 시작 관리
├── LobbyListCell.cs     # 로비 리스트 UI 아이템
├── UserPanel.cs         # 플레이어 패널 UI
├── JoinCreateLobby.cs   # 탭 전환 UI
└── README.md            # This documentation
```

---

## Component Details

### 1. LobbyManager.cs

**역할**: 로비 탐색, 방 생성, 방 참여를 담당하는 UI 컨트롤러

**주요 기능**:
* Unity Services 초기화 및 익명 로그인
* 로비 목록 자동 갱신 (2초 간격)
* 방 생성 (이름, 비밀번호)
* 방 참여 (이름 검색, ID, 퀵조인)
* 플레이어 이름 변경

**UI 구성**:

```
[JOIN Tab]
├── Lobby List Container (로비 목록)
├── Join Lobby Name Input
├── Join Lobby Password Input
├── Join Button
├── Quick Join Button
├── Reconnect Button
└── Player Name / Rename

[CREATE Tab]
├── Create Lobby Name Input
├── Create Lobby Password Input
└── Create Button
```

**주요 메서드**:

```csharp
// 로비 목록 갱신
Task RefreshLobbyList()

// 프로퍼티
string PlayerName { get; }  // 현재 플레이어 이름
```

---

### 2. RoomManager.cs

**역할**: 방 대기 상태 관리 및 게임 시작을 트리거하는 컨트롤러

**주요 기능**:
* 방 입장/퇴장
* 플레이어 Ready 상태 관리
* 로비 하트비트 (15초 간격, 호스트만)
* 로비 폴링 (1.5초 간격)
* 게임 시작 트리거 (호스트)
* 게임 참가 감지 (클라이언트)

**UI 구성**:

```
[Room Canvas]
├── Lobby Name Text
├── User List Container (플레이어 목록)
├── Ready Button
├── Start Button (호스트만 활성화)
└── Exit Button
```

**주요 메서드**:

```csharp
// 방 입장 (LobbyManager에서 호출)
void EnterRoom(Lobby lobby, string playerName)
void EnterRoom(Lobby lobby)  // 오버로드 (호환성)

// 방 퇴장
Task ExitRoom()
```

**게임 시작 플로우 (호스트)**:

```csharp
OnStartButtonClicked()
    ↓
1. LockLobby()                           // 새 참가 방지
    ↓
2. SessionManager.InitializeSessionFromLobby()  // 플레이어 자동 등록
    ↓
3. SessionManager.StartGameAsHostAsync()        // Relay + Netcode 시작
    ↓
4. SetRelayJoinCode(joinCode)            // Lobby에 코드 공유
    ↓
5. LoadGameScene()                       // 게임 씬 전환
```

**게임 참가 플로우 (클라이언트)**:

```csharp
PollLobbyData()  // 1.5초마다 폴링
    ↓
RelayJoinCode 감지
    ↓
1. SessionManager.InitializeSessionFromLobby()  // 플레이어 자동 등록
    ↓
2. SessionManager.ConnectToGameAsClientAsync()  // Relay 연결
    ↓
3. LoadGameScene()                       // 게임 씬 전환
```

---

## Complete Flow

### 1. 호스트 - 방 생성

```
LobbyManager.CreateLobby()
    ↓
1. 입력값 검증 (방 이름)
    ↓
2. CreateLobbyOptions 생성
   - IsPrivate = false
   - Player.Data["PlayerName"]
   - Data["RelayJoinCode"] = ""  (빈 값으로 초기화)
   - Data["Password"] (선택)
    ↓
3. LobbyService.Instance.CreateLobbyAsync()
    ↓
4. RoomManager.EnterRoom(lobby, playerName)
```

### 2. 클라이언트 - 방 참가

```
LobbyManager.JoinLobbyById() / QuickJoinLobby()
    ↓
1. 비밀번호 확인 (필요시)
    ↓
2. JoinLobbyByIdOptions 생성
   - Player.Data["PlayerName"]
    ↓
3. LobbyService.Instance.JoinLobbyByIdAsync()
    ↓
4. RoomManager.EnterRoom(lobby, playerName)
```

### 3. 호스트 - 게임 시작

```
RoomManager.OnStartButtonClicked()
    ↓
1. 호스트 권한 확인
    ↓
2. LockLobby() - 새 참가 방지
    ↓
3. SessionManager.InitializeSessionFromLobby()
   → Lobby 플레이어들을 Session에 자동 등록
    ↓
4. SessionManager.StartGameAsHostAsync()
   → Relay 할당 + Netcode 서버 생성
   → Relay Join Code 반환
    ↓
5. SetRelayJoinCode() - Lobby에 코드 공유
    ↓
6. LoadGameScene() - 게임 씬 전환
```

### 4. 클라이언트 - 게임 참가 (자동)

```
RoomManager.PollLobbyData() (1.5초마다)
    ↓
_currentLobby.Data["RelayJoinCode"] 감지
    ↓
1. SessionManager.InitializeSessionFromLobby()
   → Lobby 플레이어들을 Session에 자동 등록
    ↓
2. SessionManager.ConnectToGameAsClientAsync()
   → Relay 연결 + Netcode 클라이언트 생성
    ↓
3. LoadGameScene() - 게임 씬 전환
```

---

## Start 버튼 활성화 조건

```csharp
bool canStart =
    IsHost &&                           // 현재 플레이어가 호스트
    AllOthersReady &&                   // 호스트 제외 모든 플레이어 Ready
    _currentLobby.Players.Count > 1;    // 최소 2명 이상
```

---

## Lobby Data Structure

| Key | Visibility | 용도 |
|-----|------------|------|
| `PlayerName` | Member | 플레이어 표시 이름 |
| `IsReady` | Member | Ready 상태 (true/false) |
| `Password` | Member | 방 비밀번호 (선택) |
| `RelayJoinCode` | Member | 게임 시작 시 설정되는 Relay 코드 |

---

## Settings (Inspector)

### LobbyManager

| Field | Default | Description |
|-------|---------|-------------|
| maxPlayersPerLobby | 4 | 최대 플레이어 수 |
| lobbyRefreshInterval | 2f | 로비 목록 갱신 간격 (초) |

### RoomManager

| Field | Default | Description |
|-------|---------|-------------|
| lobbyPollInterval | 1.5f | 로비 상태 폴링 간격 (초) |
| heartbeatInterval | 15f | 하트비트 전송 간격 (초) |
| gameSceneName | "GameScene" | 게임 시작 시 로드할 씬 이름 |

---

## Dependencies

### Required Packages

* `com.unity.services.lobby` (1.2.2+)
* `com.unity.services.authentication`
* `com.unity.services.core`

### Unity Services Setup

1. Unity Dashboard에서 프로젝트 생성
2. Lobby 서비스 활성화
3. Project Settings > Services에서 프로젝트 연결

---

## Notes

### 하트비트

- 호스트만 15초마다 하트비트 전송
- RoomManager에서 관리 (LobbyManager가 아님)
- 하트비트 없으면 Unity Lobby에서 자동으로 로비 삭제

### 폴링 vs 이벤트

현재 구현은 폴링 방식을 사용합니다:
- 장점: 단순한 구현, 안정적
- 단점: 약간의 지연 (최대 1.5초)

Unity Lobby Events API를 사용하면 실시간 업데이트가 가능합니다.

### 에러 처리

- `LobbyNotFound`: 로비가 삭제된 경우 자동으로 로비 목록으로 복귀
- 비밀번호 오류: 경고 로그 출력
- 네트워크 오류: 에러 로그 출력
