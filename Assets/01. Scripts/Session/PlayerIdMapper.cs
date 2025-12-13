using System.Collections.Generic;

namespace RealmForge.Session
{
    /// <summary>
    /// Lobby AuthId(string) ↔ GameSession PlayerId(ulong) ↔ NFE NetworkId(int) 매핑 관리
    /// </summary>
    public class PlayerIdMapper
    {
        private readonly Dictionary<string, ulong> _authToPlayerId = new();
        private readonly Dictionary<ulong, string> _playerToAuthId = new();
        private readonly Dictionary<ulong, int> _playerToNetworkId = new();
        private readonly Dictionary<int, ulong> _networkToPlayerId = new();

        private ulong _nextPlayerId = 1;

        /// <summary>
        /// Lobby AuthId로 새 플레이어 등록, GameSession용 PlayerId 반환
        /// </summary>
        public ulong RegisterFromLobby(string authId)
        {
            if (_authToPlayerId.TryGetValue(authId, out var existingId))
            {
                return existingId;
            }

            var playerId = _nextPlayerId++;
            _authToPlayerId[authId] = playerId;
            _playerToAuthId[playerId] = authId;

            return playerId;
        }

        /// <summary>
        /// NFE 연결 시 NetworkId 바인딩
        /// </summary>
        public void BindNetworkId(ulong playerId, int networkId)
        {
            // 기존 바인딩 제거
            if (_playerToNetworkId.TryGetValue(playerId, out var oldNetworkId))
            {
                _networkToPlayerId.Remove(oldNetworkId);
            }

            _playerToNetworkId[playerId] = networkId;
            _networkToPlayerId[networkId] = playerId;
        }

        /// <summary>
        /// NFE 연결 해제 시 NetworkId 바인딩 제거
        /// </summary>
        public void UnbindNetworkId(ulong playerId)
        {
            if (_playerToNetworkId.TryGetValue(playerId, out var networkId))
            {
                _networkToPlayerId.Remove(networkId);
                _playerToNetworkId.Remove(playerId);
            }
        }

        /// <summary>
        /// 플레이어 완전 제거 (Lobby 퇴장 시)
        /// </summary>
        public void RemovePlayer(ulong playerId)
        {
            UnbindNetworkId(playerId);

            if (_playerToAuthId.TryGetValue(playerId, out var authId))
            {
                _authToPlayerId.Remove(authId);
                _playerToAuthId.Remove(playerId);
            }
        }

        /// <summary>
        /// AuthId → PlayerId 변환
        /// </summary>
        public bool TryGetPlayerId(string authId, out ulong playerId)
        {
            return _authToPlayerId.TryGetValue(authId, out playerId);
        }

        /// <summary>
        /// PlayerId → AuthId 변환
        /// </summary>
        public bool TryGetAuthId(ulong playerId, out string authId)
        {
            return _playerToAuthId.TryGetValue(playerId, out authId);
        }

        /// <summary>
        /// PlayerId → NetworkId 변환
        /// </summary>
        public bool TryGetNetworkId(ulong playerId, out int networkId)
        {
            return _playerToNetworkId.TryGetValue(playerId, out networkId);
        }

        /// <summary>
        /// NetworkId → PlayerId 변환
        /// </summary>
        public bool TryGetPlayerId(int networkId, out ulong playerId)
        {
            return _networkToPlayerId.TryGetValue(networkId, out playerId);
        }

        /// <summary>
        /// AuthId로 NetworkId 직접 조회
        /// </summary>
        public bool TryGetNetworkId(string authId, out int networkId)
        {
            networkId = 0;
            if (!TryGetPlayerId(authId, out var playerId))
                return false;

            return TryGetNetworkId(playerId, out networkId);
        }

        /// <summary>
        /// NetworkId로 AuthId 직접 조회
        /// </summary>
        public bool TryGetAuthId(int networkId, out string authId)
        {
            authId = null;
            if (!TryGetPlayerId(networkId, out var playerId))
                return false;

            return TryGetAuthId(playerId, out authId);
        }

        /// <summary>
        /// 모든 매핑 초기화
        /// </summary>
        public void Clear()
        {
            _authToPlayerId.Clear();
            _playerToAuthId.Clear();
            _playerToNetworkId.Clear();
            _networkToPlayerId.Clear();
            _nextPlayerId = 1;
        }
    }
}
