using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;

[UnityEngine.Scripting.Preserve]
public class NetCodeBootstrap : ClientServerBootstrap
{
    public override bool Initialize(string defaultWorldName)
    {
        // Relay 사용 시 자동 연결 비활성화 (수동으로 Listen/Connect 호출)
        AutoConnectPort = 0;
        return base.Initialize(defaultWorldName);
    }
}