using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class RelayManager : MonoBehaviour
{
    private static RelayManager _instance;
    public static RelayManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("RelayManager");
                _instance = go.AddComponent<RelayManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Host로서 Relay 서버를 생성하고 Join Code를 반환합니다.
    /// </summary>
    /// <param name="maxPlayers">최대 플레이어 수</param>
    /// <returns>Join Code</returns>
    public async Task<string> CreateRelayAsHost(int maxPlayers = 4)
    {
        try
        {
            // Relay Allocation 생성
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);

            // Join Code 생성
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // Unity Transport 설정
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
                transport.SetRelayServerData(relayServerData);
            }

            Debug.Log($"[RELAY] Created relay as host. Join Code: {joinCode}");
            return joinCode;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RELAY] Failed to create relay as host: {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// Client로서 Join Code를 사용하여 Relay에 연결합니다.
    /// </summary>
    /// <param name="joinCode">Host의 Join Code</param>
    public async Task JoinRelayAsClient(string joinCode)
    {
        try
        {
            // Join Code로 Relay 연결
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            // Unity Transport 설정
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");
                transport.SetRelayServerData(relayServerData);
            }

            Debug.Log($"[RELAY] Joined relay as client with code: {joinCode}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RELAY] Failed to join relay as client: {e.Message}");
            throw;
        }
    }
}
