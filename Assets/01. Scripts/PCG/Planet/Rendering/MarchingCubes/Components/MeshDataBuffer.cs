using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Vertex position buffer for generated mesh
/// </summary>
public struct MeshVertexBuffer : IBufferElementData
{
    public float3 Value;
}

/// <summary>
/// Vertex normal buffer for generated mesh
/// </summary>
public struct MeshNormalBuffer : IBufferElementData
{
    public float3 Value;
}

/// <summary>
/// Triangle index buffer for generated mesh
/// </summary>
public struct MeshIndexBuffer : IBufferElementData
{
    public int Value;
}
