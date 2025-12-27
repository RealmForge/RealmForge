
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealmForge.Session
{
    public enum SessionState
    {
        None,
        Creating,
        Waiting,
        Starting,
        InProgress,
        Ending,
        Closed
    }

    public enum PlayerConnectionState
    {
        Connecting,
        Connected,
        Disconnected,
        Migrating
    }

    [Serializable]
    public struct SessionPlayer
    {
        public ulong PlayerId;
        public string DisplayName;
        public int ConnectionId;
        public PlayerConnectionState State;
        public bool IsHost;
        public float LastHeartbeat;
    }

    [Serializable]
    public struct SessionConfig
    {
        public string SessionName;
        public int MaxPlayers;
        public bool IsPrivate;
        public string GameMode;
        public Dictionary<string, string> CustomData;

        public static SessionConfig Default => new SessionConfig
        {
            SessionName = "Game Session",
            MaxPlayers = 4,
            IsPrivate = false,
            GameMode = "Default",
            CustomData = new Dictionary<string, string>()
        };
    }

    public class GameSession
    {
        public string SessionId { get; private set; }
        public SessionState State { get; private set; }
        public SessionConfig Config { get; private set; }
        public ulong HostPlayerId { get; private set; }
        public ulong LocalPlayerId { get; private set; }

        private readonly Dictionary<ulong, SessionPlayer> _players = new();
        private float _sessionStartTime;

        public event Action<SessionState> OnStateChanged;
        public event Action<SessionPlayer> OnPlayerJoined;
        public event Action<SessionPlayer> OnPlayerLeft;
        public event Action<ulong> OnHostMigrated;
        public event Action<string> OnSessionError;

        public int PlayerCount => _players.Count;
        public bool IsHost => LocalPlayerId == HostPlayerId;
        public bool IsFull => PlayerCount >= Config.MaxPlayers;
        public float SessionDuration => State == SessionState.InProgress
            ? Time.realtimeSinceStartup - _sessionStartTime
            : 0f;

        public IReadOnlyDictionary<ulong, SessionPlayer> Players => _players;

        /// 기본 생성자 - 세션을 초기 상태로 설정
        public GameSession()
        {
            State = SessionState.None;
        }

        /// 새로운 P2P 세션을 생성하고 호스트로 등록
        public bool Create(SessionConfig config, ulong localPlayerId, string displayName)
        {
            if (State != SessionState.None)
            {
                OnSessionError?.Invoke("Session already exists");
                return false;
            }

            State = SessionState.Creating;
            OnStateChanged?.Invoke(State);

            SessionId = Guid.NewGuid().ToString();
            Config = config;
            LocalPlayerId = localPlayerId;
            HostPlayerId = localPlayerId;

            var hostPlayer = new SessionPlayer
            {
                PlayerId = localPlayerId,
                DisplayName = displayName,
                ConnectionId = 0,
                State = PlayerConnectionState.Connected,
                IsHost = true,
                LastHeartbeat = Time.realtimeSinceStartup
            };

            _players[localPlayerId] = hostPlayer;

            State = SessionState.Waiting;
            OnStateChanged?.Invoke(State);
            OnPlayerJoined?.Invoke(hostPlayer);

            return true;
        }

        /// 기존 세션에 클라이언트로 참가
        public bool Join(string sessionId, ulong localPlayerId, string displayName)
        {
            if (State != SessionState.None)
            {
                OnSessionError?.Invoke("Already in a session");
                return false;
            }

            SessionId = sessionId;
            LocalPlayerId = localPlayerId;

            var player = new SessionPlayer
            {
                PlayerId = localPlayerId,
                DisplayName = displayName,
                ConnectionId = -1,
                State = PlayerConnectionState.Connecting,
                IsHost = false,
                LastHeartbeat = Time.realtimeSinceStartup
            };

            _players[localPlayerId] = player;
            State = SessionState.Waiting;
            OnStateChanged?.Invoke(State);

            return true;
        }

        /// 새로운 플레이어를 세션에 추가
        public bool AddPlayer(SessionPlayer player)
        {
            if (IsFull)
            {
                OnSessionError?.Invoke("Session is full");
                return false;
            }

            if (_players.ContainsKey(player.PlayerId))
            {
                OnSessionError?.Invoke("Player already in session");
                return false;
            }

            player.LastHeartbeat = Time.realtimeSinceStartup;
            _players[player.PlayerId] = player;
            OnPlayerJoined?.Invoke(player);

            return true;
        }

        /// 플레이어를 세션에서 제거하고 필요시 호스트 마이그레이션 수행
        public bool RemovePlayer(ulong playerId)
        {
            if (!_players.TryGetValue(playerId, out var player))
            {
                return false;
            }

            _players.Remove(playerId);
            OnPlayerLeft?.Invoke(player);

            if (player.IsHost && _players.Count > 0)
            {
                MigrateHost();
            }

            return true;
        }

        /// 플레이어의 연결 상태를 업데이트
        public void UpdatePlayerState(ulong playerId, PlayerConnectionState state)
        {
            if (!_players.TryGetValue(playerId, out var player))
            {
                return;
            }

            player.State = state;
            player.LastHeartbeat = Time.realtimeSinceStartup;
            _players[playerId] = player;
        }

        /// 플레이어의 하트비트 타임스탬프를 갱신
        public void UpdateHeartbeat(ulong playerId)
        {
            if (!_players.TryGetValue(playerId, out var player))
            {
                return;
            }

            player.LastHeartbeat = Time.realtimeSinceStartup;
            _players[playerId] = player;
        }

        /// 게임 세션을 시작 (호스트만 가능)
        public bool StartSession()
        {
            if (State != SessionState.Waiting)
            {
                OnSessionError?.Invoke("Cannot start session in current state");
                return false;
            }

            if (!IsHost)
            {
                OnSessionError?.Invoke("Only host can start session");
                return false;
            }

            State = SessionState.Starting;
            OnStateChanged?.Invoke(State);

            _sessionStartTime = Time.realtimeSinceStartup;

            State = SessionState.InProgress;
            OnStateChanged?.Invoke(State);

            return true;
        }

        /// 세션을 종료하고 모든 플레이어를 정리
        public void EndSession()
        {
            if (State == SessionState.None || State == SessionState.Closed)
            {
                return;
            }

            State = SessionState.Ending;
            OnStateChanged?.Invoke(State);

            _players.Clear();

            State = SessionState.Closed;
            OnStateChanged?.Invoke(State);
        }

        /// 플레이어 ID로 플레이어 정보를 조회
        public SessionPlayer? GetPlayer(ulong playerId)
        {
            return _players.TryGetValue(playerId, out var player) ? player : null;
        }

        /// 타임아웃된 플레이어 목록을 반환
        public List<ulong> GetDisconnectedPlayers(float timeoutSeconds)
        {
            var disconnected = new List<ulong>();
            var currentTime = Time.realtimeSinceStartup;

            foreach (var kvp in _players)
            {
                if (currentTime - kvp.Value.LastHeartbeat > timeoutSeconds)
                {
                    disconnected.Add(kvp.Key);
                }
            }

            return disconnected;
        }

        /// 호스트가 떠났을 때 새로운 호스트를 선출
        private void MigrateHost()
        {
            ulong newHostId = 0;
            float earliestJoin = float.MaxValue;

            foreach (var kvp in _players)
            {
                if (kvp.Value.State == PlayerConnectionState.Connected &&
                    kvp.Value.LastHeartbeat < earliestJoin)
                {
                    earliestJoin = kvp.Value.LastHeartbeat;
                    newHostId = kvp.Key;
                }
            }

            if (newHostId == 0)
            {
                EndSession();
                return;
            }

            HostPlayerId = newHostId;

            var newHost = _players[newHostId];
            newHost.IsHost = true;
            _players[newHostId] = newHost;

            OnHostMigrated?.Invoke(newHostId);
        }

        /// 플레이어의 네트워크 연결 ID를 설정하고 연결 상태를 Connected로 변경
        public void SetConnectionId(ulong playerId, int connectionId)
        {
            if (!_players.TryGetValue(playerId, out var player))
            {
                return;
            }

            player.ConnectionId = connectionId;
            player.State = PlayerConnectionState.Connected;
            _players[playerId] = player;
        }
    }
}
