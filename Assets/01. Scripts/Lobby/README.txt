# Lobby & Game Start Flow Documentation

## 1. 방 입장 플로우

### 호스트 (방 생성)

- 함수: `LobbyManager.CreateLobby()` → `RoomManager.EnterRoom()`

1. 사용자가 방 이름과 비밀번호를 입력하고 Create 버튼 클릭
2. `LobbyManager.CreateLobby()` 실행:
   - 입력값 검증 (방 이름이 비어있지 않은지)
   - `CreateLobbyOptions` 생성:
     - `IsPrivate = false` 설정 (로비 목록에 표시되도록)
   - `Player.Data["PlayerName"]` 설정 (호스트 닉네임)
   - `LobbyService.Instance.CreateLobbyAsync()` 호출하여 Unity Lobbies에 방 생성
   - `_currentLobby`에 생성된 로비 정보 저장
   - 15초마다 하트비트 전송 시작 (로비가 자동 삭제되지 않도록)
3. `RoomManager.EnterRoom(_currentLobby)` 호출

---

### 클라이언트 (방 참가)

- 함수: `LobbyManager.JoinLobbyById()` → `RoomManager.EnterRoom()`

1. 사용자가 로비 리스트에서 방 선택 후 Join 버튼 클릭
2. `OnLobbyListJoinClicked(lobby)` 실행:
   - `JoinLobbyById(lobby.Id)` 호출
3. `LobbyManager.JoinLobbyById(lobbyId, password)` 실행:
   - `LobbyService.Instance.GetLobbyAsync(lobbyId)`로 로비 정보 조회
   - `JoinLobbyByIdOptions` 생성:
     - `Player.Data["PlayerName"]` 설정 (클라이언트 닉네임)
   - `LobbyService.Instance.JoinLobbyByIdAsync()` 호출하여 방 참가
   - `_currentLobby`에 참가한 로비 정보 저장
4. `RoomManager.EnterRoom(_currentLobby)` 호출
   - 호스트와 동일한 과정
   - 단, Start 버튼은 비활성화됨 (호스트만 사용 가능)

---

## 2. 게임 시작 플로우

### 호스트

- 함수: RoomManager.OnStartButtonClicked()

1. 사용자가 Start 버튼 클릭
2. `OnStartButtonClicked()` 실행:
   - 호스트 권한 확인  
     (`AuthenticationService.Instance.PlayerId == _currentLobby.HostId`)
   - 권한 없을 경우 `"Only host can start the game!"` 경고 출력
3. 현재 내용 없음 – 향후 구현 필요:
   - Relay 서버 할당 코드 추가
   - `_currentLobby.Data["RelayJoinCode"]` 설정하여 클라이언트가 감지하도록
   - 게임 씬 전환 또는 네트워크 서버 시작

#### Start 버튼 활성화 조건
- 현재 플레이어가 호스트
- 호스트를 제외한 모든 플레이어가 Ready 상태
- 최소 2명 이상 (혼자서는 시작 불가)

---

### 클라이언트

- 함수: `RoomManager.JoinGame()
- 자동 감지 함수: `RoomManager.PollLobbyData()`

1. 자동 폴링 (Update에서 1.5초마다 실행):
   - `_isInRoom == true`일 때만 실행
   - `PollLobbyData()` 호출
2. `PollLobbyData()` 실행:
   - `LobbyService.Instance.GetLobbyAsync(_currentLobby.Id)`로 최신 로비 정보 가져오기
   - 플레이어 목록 UI 업데이트
   - 게임 시작 감지:
     - `_currentLobby.Data.ContainsKey("RelayJoinCode")` 확인
     - `RelayJoinCode`가 존재하고 비어있지 않으면 → 게임 시작으로 간주
     - `JoinGame(joinCode)` 호출
3. `JoinGame(joinCode)` 실행