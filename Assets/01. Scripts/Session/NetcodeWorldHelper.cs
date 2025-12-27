using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using UnityEngine;

namespace RealmForge.Session
{
    /// <summary>
    /// NFE(Netcode for Entities) 월드 생성 및 네트워크 연결 헬퍼
    /// </summary>
    public static class NetcodeWorldHelper
    {
        /// <summary>
        /// 서버 월드 생성
        /// </summary>
        public static World CreateServerWorld(string worldName = "ServerWorld")
        {
            var world = ClientServerBootstrap.CreateServerWorld(worldName);
            Debug.Log($"[NetcodeWorldHelper] Server world created: {worldName}");
            return world;
        }

        /// <summary>
        /// 클라이언트 월드 생성
        /// </summary>
        public static World CreateClientWorld(string worldName = "ClientWorld")
        {
            var world = ClientServerBootstrap.CreateClientWorld(worldName);
            Debug.Log($"[NetcodeWorldHelper] Client world created: {worldName}");
            return world;
        }

        /// <summary>
        /// 서버 월드에 Relay 설정 적용 및 Listen 시작
        /// </summary>
        public static bool SetupServerWithRelay(World serverWorld, RelayServerData relayServerData)
        {
            if (serverWorld == null || !serverWorld.IsCreated)
            {
                Debug.LogError("[NetcodeWorldHelper] Server world is null or not created");
                return false;
            }

            var entityManager = serverWorld.EntityManager;

            // NetworkStreamDriver 싱글톤 가져오기
            using var query = entityManager.CreateEntityQuery(typeof(NetworkStreamDriver));
            if (query.IsEmpty)
            {
                Debug.LogError("[NetcodeWorldHelper] NetworkStreamDriver not found in server world");
                return false;
            }

            // 기존 드라이버 스토어 리셋 (Relay 설정 적용)
            var driverStore = new NetworkDriverStore();

            using var netDebugQuery = entityManager.CreateEntityQuery(typeof(NetDebug));
            var netDebug = netDebugQuery.GetSingleton<NetDebug>();

            // IPC 드라이버 (로컬 연결용)
            var ipcSettings = DefaultDriverBuilder.GetNetworkServerSettings();
            DefaultDriverBuilder.RegisterServerIpcDriver(serverWorld, ref driverStore, netDebug, ipcSettings);

            // Relay 드라이버 (외부 연결용)
            var relaySettings = DefaultDriverBuilder.GetNetworkServerSettings();
            var relayData = relayServerData;
            relaySettings = relaySettings.WithRelayParameters(ref relayData);

#if !UNITY_WEBGL || UNITY_EDITOR
            DefaultDriverBuilder.RegisterServerUdpDriver(serverWorld, ref driverStore, netDebug, relaySettings);
#else
            DefaultDriverBuilder.RegisterServerWebSocketDriver(serverWorld, ref driverStore, netDebug, relaySettings);
#endif

            // 드라이버 스토어 리셋
            var networkStreamDriver = query.GetSingletonRW<NetworkStreamDriver>();
            networkStreamDriver.ValueRW.ResetDriverStore(serverWorld.Unmanaged, ref driverStore);

            // Relay를 통해 Listen (포트 0 = Relay가 할당)
            var listenEndpoint = NetworkEndpoint.AnyIpv4.WithPort(0);
            networkStreamDriver.ValueRW.Listen(listenEndpoint);

            Debug.Log("[NetcodeWorldHelper] Server setup with Relay completed, listening...");
            return true;
        }

        /// <summary>
        /// 클라이언트 월드에 Relay 설정 적용 및 서버 연결
        /// </summary>
        public static bool SetupClientWithRelay(World clientWorld, RelayServerData relayClientData)
        {
            if (clientWorld == null || !clientWorld.IsCreated)
            {
                Debug.LogError("[NetcodeWorldHelper] Client world is null or not created");
                return false;
            }

            var entityManager = clientWorld.EntityManager;

            // NetworkStreamDriver 싱글톤 가져오기
            using var query = entityManager.CreateEntityQuery(typeof(NetworkStreamDriver));
            if (query.IsEmpty)
            {
                Debug.LogError("[NetcodeWorldHelper] NetworkStreamDriver not found in client world");
                return false;
            }

            // 기존 드라이버 스토어 리셋 (Relay 설정 적용)
            var driverStore = new NetworkDriverStore();

            using var netDebugQuery = entityManager.CreateEntityQuery(typeof(NetDebug));
            var netDebug = netDebugQuery.GetSingleton<NetDebug>();

            var settings = DefaultDriverBuilder.GetNetworkClientSettings();
            var relayData = relayClientData;
            settings = settings.WithRelayParameters(ref relayData);

#if !UNITY_WEBGL || UNITY_EDITOR
            DefaultDriverBuilder.RegisterClientUdpDriver(clientWorld, ref driverStore, netDebug, settings);
#else
            DefaultDriverBuilder.RegisterClientWebSocketDriver(clientWorld, ref driverStore, netDebug, settings);
#endif

            // 드라이버 스토어 리셋
            var networkStreamDriver = query.GetSingletonRW<NetworkStreamDriver>();
            networkStreamDriver.ValueRW.ResetDriverStore(clientWorld.Unmanaged, ref driverStore);

            // Relay를 통해 서버에 연결 - RelayServerData의 Endpoint 사용
            var relayEndpoint = relayClientData.Endpoint;
            networkStreamDriver.ValueRW.Connect(entityManager, relayEndpoint);

            Debug.Log($"[NetcodeWorldHelper] Client setup with Relay completed, connecting to {relayEndpoint}...");
            return true;
        }

        /// <summary>
        /// 월드 정리 및 삭제
        /// </summary>
        public static void DisposeWorld(World world)
        {
            if (world == null || !world.IsCreated) return;

            var worldName = world.Name;
            world.Dispose();
            Debug.Log($"[NetcodeWorldHelper] World disposed: {worldName}");
        }

        /// <summary>
        /// 모든 NetCode 월드 정리
        /// </summary>
        public static void DisposeAllNetcodeWorlds()
        {
            // 서버 월드들 정리
            foreach (var serverWorld in ClientServerBootstrap.ServerWorlds.ToArray())
            {
                if (serverWorld != null && serverWorld.IsCreated)
                {
                    serverWorld.Dispose();
                }
            }
            ClientServerBootstrap.ServerWorlds.Clear();

            // 클라이언트 월드들 정리
            foreach (var clientWorld in ClientServerBootstrap.ClientWorlds.ToArray())
            {
                if (clientWorld != null && clientWorld.IsCreated)
                {
                    clientWorld.Dispose();
                }
            }
            ClientServerBootstrap.ClientWorlds.Clear();

            // Thin 클라이언트 월드들 정리
            foreach (var thinWorld in ClientServerBootstrap.ThinClientWorlds.ToArray())
            {
                if (thinWorld != null && thinWorld.IsCreated)
                {
                    thinWorld.Dispose();
                }
            }
            ClientServerBootstrap.ThinClientWorlds.Clear();

            Debug.Log("[NetcodeWorldHelper] All NetCode worlds disposed");
        }
    }
}
