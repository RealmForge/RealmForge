using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;

[UnityEngine.Scripting.Preserve]
public class NetCodeBootstrap : ClientServerBootstrap
{
    public override bool Initialize(string defaultWorldName)
    {
        AutoConnectPort = 7979;
        return base.Initialize(defaultWorldName);
    }
}