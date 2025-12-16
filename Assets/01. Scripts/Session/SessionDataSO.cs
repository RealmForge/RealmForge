using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealmForge.Session
{
    /// <summary>
    /// 씬 전환 시에도 유지되어야 하는 세션 데이터를 저장하는 ScriptableObject
    /// Resources 폴더에 위치하여 런타임에 로드 가능
    /// </summary>
    [CreateAssetMenu(fileName = "SessionData", menuName = "RealmForge/Session Data")]
    public class SessionDataSO : ScriptableObject
    {
        private static SessionDataSO _instance;
        public static SessionDataSO Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<SessionDataSO>("SessionData");
                    if (_instance == null)
                    {
                        Debug.LogError("[SessionDataSO] SessionData asset not found in Resources folder!");
                    }
                }
                return _instance;
            }
        }

        [Header("Connection Info")]
        [SerializeField] private string relayJoinCode;
        [SerializeField] private string lobbyId;
        [SerializeField] private string sessionId;

        [Header("Local Player Info")]
        [SerializeField] private bool isHost;
        [SerializeField] private ulong localPlayerId;
        [SerializeField] private string localPlayerName;

        [Header("Session Config")]
        [SerializeField] private string sessionName;
        [SerializeField] private int maxPlayers;
        [SerializeField] private bool isPrivate;
        [SerializeField] private string gameMode;

        [Header("Players")]
        [SerializeField] private List<SessionPlayerData> players = new();

        [Header("State")]
        [SerializeField] private SessionState sessionState;
        [SerializeField] private bool hasValidData;
        [SerializeField] private bool pendingRestore; // 씬 전환 후 복원 대기 중

        // Properties
        public string RelayJoinCode => relayJoinCode;
        public string LobbyId => lobbyId;
        public string SessionId => sessionId;
        public bool IsHost => isHost;
        public ulong LocalPlayerId => localPlayerId;
        public string LocalPlayerName => localPlayerName;
        public SessionState SessionState => sessionState;
        public bool HasValidData => hasValidData;
        public bool PendingRestore => pendingRestore;
        public IReadOnlyList<SessionPlayerData> Players => players;

        public SessionConfig GetSessionConfig()
        {
            return new SessionConfig
            {
                SessionName = sessionName,
                MaxPlayers = maxPlayers,
                IsPrivate = isPrivate,
                GameMode = gameMode,
                CustomData = new Dictionary<string, string>()
            };
        }

        /// <summary>
        /// 세션 데이터 저장
        /// </summary>
        public void SaveSessionData(
            string relayCode,
            string lobbyId,
            string sessionId,
            bool isHost,
            ulong localPlayerId,
            string localPlayerName,
            SessionConfig config,
            IEnumerable<SessionPlayer> sessionPlayers,
            SessionState state)
        {
            this.relayJoinCode = relayCode;
            this.lobbyId = lobbyId;
            this.sessionId = sessionId;
            this.isHost = isHost;
            this.localPlayerId = localPlayerId;
            this.localPlayerName = localPlayerName;
            this.sessionName = config.SessionName;
            this.maxPlayers = config.MaxPlayers;
            this.isPrivate = config.IsPrivate;
            this.gameMode = config.GameMode;
            this.sessionState = state;

            players.Clear();
            foreach (var player in sessionPlayers)
            {
                players.Add(SessionPlayerData.FromSessionPlayer(player));
            }

            hasValidData = true;
            pendingRestore = true; // 씬 전환 후 복원 대기

            Debug.Log($"[SessionDataSO] Session data saved - RelayCode: {relayCode}, IsHost: {isHost}, Players: {players.Count}");
        }

        /// <summary>
        /// 복원 완료 후 플래그 해제
        /// </summary>
        public void MarkRestoreComplete()
        {
            pendingRestore = false;
            Debug.Log("[SessionDataSO] Restore complete, pending flag cleared");
        }

        /// <summary>
        /// 세션 데이터 초기화
        /// </summary>
        public void Clear()
        {
            relayJoinCode = null;
            lobbyId = null;
            sessionId = null;
            isHost = false;
            localPlayerId = 0;
            localPlayerName = null;
            sessionName = null;
            maxPlayers = 0;
            isPrivate = false;
            gameMode = null;
            sessionState = SessionState.None;
            players.Clear();
            hasValidData = false;
            pendingRestore = false;

            Debug.Log("[SessionDataSO] Session data cleared");
        }

        /// <summary>
        /// 플레이어 목록을 SessionPlayer Dictionary로 변환
        /// </summary>
        public Dictionary<ulong, SessionPlayer> GetPlayersAsDictionary()
        {
            var dict = new Dictionary<ulong, SessionPlayer>();
            foreach (var playerData in players)
            {
                var player = playerData.ToSessionPlayer();
                dict[player.PlayerId] = player;
            }
            return dict;
        }
    }

    /// <summary>
    /// SessionPlayer의 직렬화 가능한 버전
    /// </summary>
    [Serializable]
    public class SessionPlayerData
    {
        public ulong playerId;
        public string displayName;
        public int connectionId;
        public PlayerConnectionState state;
        public bool isHost;

        public static SessionPlayerData FromSessionPlayer(SessionPlayer player)
        {
            return new SessionPlayerData
            {
                playerId = player.PlayerId,
                displayName = player.DisplayName,
                connectionId = player.ConnectionId,
                state = player.State,
                isHost = player.IsHost
            };
        }

        public SessionPlayer ToSessionPlayer()
        {
            return new SessionPlayer
            {
                PlayerId = playerId,
                DisplayName = displayName,
                ConnectionId = connectionId,
                State = state,
                IsHost = isHost,
                LastHeartbeat = Time.realtimeSinceStartup
            };
        }
    }
}
