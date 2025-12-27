using System.Runtime.InteropServices;
using Unity.Mathematics;

[StructLayout(LayoutKind.Sequential)]
public struct Vertex
{
    public float3 Position;
    public float3 Normal;
}
