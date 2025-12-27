using System;
using System.Threading.Tasks;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace RealmForge.Session
{
    /// <summary>
    /// Unity Relay 서비스 래퍼
    /// Allocation 생성 및 RelayServerData 변환 처리
    /// </summary>
    public static class RelayServiceHelper
    {
        /// <summary>
        /// Host용 Relay Allocation 생성 및 Join Code 반환
        /// </summary>
        public static async Task<(RelayServerData serverData, string joinCode)?> AllocateRelayServerAsync(int maxConnections)
        {
            try
            {
                // Relay 서버 할당
                var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

                // Join Code 생성
                var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                // RelayServerData 생성 (서버용)
                var serverData = CreateRelayServerData(allocation, "dtls");

                Debug.Log($"[RelayServiceHelper] Relay allocated. Join code: {joinCode}");
                return (serverData, joinCode);
            }
            catch (RelayServiceException e)
            {
                Debug.LogError($"[RelayServiceHelper] Failed to allocate relay: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Client용 Join Code로 Relay 연결
        /// </summary>
        public static async Task<RelayServerData?> JoinRelayAsync(string joinCode)
        {
            try
            {
                var joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

                // RelayServerData 생성 (클라이언트용)
                var clientData = CreateRelayServerData(joinAllocation, "dtls");

                Debug.Log($"[RelayServiceHelper] Joined relay with code: {joinCode}");
                return clientData;
            }
            catch (RelayServiceException e)
            {
                Debug.LogError($"[RelayServiceHelper] Failed to join relay: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Allocation에서 RelayServerData 생성 (Host용)
        /// </summary>
        private static RelayServerData CreateRelayServerData(Allocation allocation, string connectionType)
        {
            // 연결 타입에 맞는 엔드포인트 찾기
            RelayServerEndpoint endpoint = default;
            foreach (var ep in allocation.ServerEndpoints)
            {
                if (ep.ConnectionType == connectionType)
                {
                    endpoint = ep;
                    break;
                }
            }

            if (string.IsNullOrEmpty(endpoint.Host))
            {
                // fallback to udp
                foreach (var ep in allocation.ServerEndpoints)
                {
                    if (ep.ConnectionType == "udp")
                    {
                        endpoint = ep;
                        break;
                    }
                }
            }

            return new RelayServerData(
                host: endpoint.Host,
                port: (ushort)endpoint.Port,
                allocationId: allocation.AllocationIdBytes,
                connectionData: allocation.ConnectionData,
                hostConnectionData: allocation.ConnectionData,
                key: allocation.Key,
                isSecure: connectionType == "dtls"
            );
        }

        /// <summary>
        /// JoinAllocation에서 RelayServerData 생성 (Client용)
        /// </summary>
        private static RelayServerData CreateRelayServerData(JoinAllocation joinAllocation, string connectionType)
        {
            // 연결 타입에 맞는 엔드포인트 찾기
            RelayServerEndpoint endpoint = default;
            foreach (var ep in joinAllocation.ServerEndpoints)
            {
                if (ep.ConnectionType == connectionType)
                {
                    endpoint = ep;
                    break;
                }
            }

            if (string.IsNullOrEmpty(endpoint.Host))
            {
                // fallback to udp
                foreach (var ep in joinAllocation.ServerEndpoints)
                {
                    if (ep.ConnectionType == "udp")
                    {
                        endpoint = ep;
                        break;
                    }
                }
            }

            return new RelayServerData(
                host: endpoint.Host,
                port: (ushort)endpoint.Port,
                allocationId: joinAllocation.AllocationIdBytes,
                connectionData: joinAllocation.ConnectionData,
                hostConnectionData: joinAllocation.HostConnectionData,
                key: joinAllocation.Key,
                isSecure: connectionType == "dtls"
            );
        }
    }
}
