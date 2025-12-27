using Unity.Entities;
using Unity.Mathematics;

public struct ChunkMeshData : IComponentData
{
    public int ChunkIndex;
    public int TriangleCount;
    public float3 WorldPosition;
}
