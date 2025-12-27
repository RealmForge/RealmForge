using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct TriangleMeshJob : Unity.Jobs.IJob
{
    public Mesh.MeshData MeshData;
    public int TriangleCount;
    public float Spacing;
    public uint Seed;

    public void Execute()
    {
        var vertices = MeshData.GetVertexData<Vertex>();
        var indices = MeshData.GetIndexData<uint>();

        var random = new Unity.Mathematics.Random(Seed);

        for (int i = 0; i < TriangleCount; i++)
        {
            int vertexOffset = i * 3;
            int indexOffset = i * 3;

            // 삼각형 중심 위치 (그리드 배치)
            int gridSize = (int)math.ceil(math.sqrt(TriangleCount));
            int gridX = i % gridSize;
            int gridZ = i / gridSize;
            float3 center = new float3(gridX * Spacing, 0f, gridZ * Spacing);

            // 약간의 랜덤 오프셋
            center += random.NextFloat3(-0.2f, 0.2f);

            // 삼각형 크기
            float size = 0.4f;

            // 삼각형 정점 3개
            vertices[vertexOffset + 0] = new Vertex
            {
                Position = center + new float3(0f, size, 0f),
                Normal = new float3(0f, 0f, -1f)
            };
            vertices[vertexOffset + 1] = new Vertex
            {
                Position = center + new float3(-size, -size, 0f),
                Normal = new float3(0f, 0f, -1f)
            };
            vertices[vertexOffset + 2] = new Vertex
            {
                Position = center + new float3(size, -size, 0f),
                Normal = new float3(0f, 0f, -1f)
            };

            // 인덱스 (시계방향)
            indices[indexOffset + 0] = (uint)(vertexOffset + 0);
            indices[indexOffset + 1] = (uint)(vertexOffset + 2);
            indices[indexOffset + 2] = (uint)(vertexOffset + 1);
        }
    }
}
