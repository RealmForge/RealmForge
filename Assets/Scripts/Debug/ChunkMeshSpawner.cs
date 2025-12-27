using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

public class ChunkMeshSpawner : MonoBehaviour
{
    [Header("Chunk Settings")]
    [SerializeField] private int chunkCount = 4;
    [SerializeField] private int trianglesPerChunk = 100;
    [SerializeField] private float triangleSpacing = 1f;
    [SerializeField] private float chunkSpacing = 15f;

    [Header("Rendering")]
    [SerializeField] private Material material;

    private EntityManager entityManager;
    private World targetWorld;

    private void Start()
    {
        // ClientWorld 찾기 (렌더링 가능한 World)
        targetWorld = null;
        foreach (var world in World.All)
        {
            if (world.Name == "ClientWorld")
            {
                targetWorld = world;
                break;
            }
        }

        if (targetWorld == null)
        {
            targetWorld = World.DefaultGameObjectInjectionWorld;
        }

        if (targetWorld == null)
        {
            Debug.LogError("No valid World found!");
            return;
        }

        entityManager = targetWorld.EntityManager;

        SpawnChunks();
    }

    private void SpawnChunks()
    {
        int vertexCount = trianglesPerChunk * 3;
        int indexCount = trianglesPerChunk * 3;

        // 버텍스 레이아웃 설정
        var vertexAttributes = new NativeArray<VertexAttributeDescriptor>(2, Allocator.Temp);
        vertexAttributes[0] = new VertexAttributeDescriptor(
            VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
        vertexAttributes[1] = new VertexAttributeDescriptor(
            VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);

        // 4개 청크 생성
        for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            // 청크 위치 계산 (2x2 그리드)
            int gridX = chunkIndex % 2;
            int gridZ = chunkIndex / 2;
            float3 chunkPosition = new float3(gridX * chunkSpacing, 0f, gridZ * chunkSpacing);

            // 메시 생성
            var mesh = CreateChunkMesh(chunkIndex, vertexCount, indexCount, vertexAttributes);

            // ECS Entity 생성
            CreateChunkEntity(chunkIndex, mesh, chunkPosition);

            Debug.Log($"Chunk {chunkIndex} created at {chunkPosition} with {trianglesPerChunk} triangles");
        }

        vertexAttributes.Dispose();
        Debug.Log($"Total: {chunkCount} chunks spawned");
    }

    private Mesh CreateChunkMesh(int chunkIndex, int vertexCount, int indexCount,
        NativeArray<VertexAttributeDescriptor> vertexAttributes)
    {
        var mesh = new Mesh { name = $"ChunkMesh_{chunkIndex}" };

        // MeshDataArray 할당
        var meshDataArray = Mesh.AllocateWritableMeshData(1);
        var meshData = meshDataArray[0];

        meshData.SetVertexBufferParams(vertexCount, vertexAttributes);
        meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);

        // Job 실행
        var job = new TriangleMeshJob
        {
            MeshData = meshData,
            TriangleCount = trianglesPerChunk,
            Spacing = triangleSpacing,
            Seed = (uint)(chunkIndex + 1) * 12345
        };
        job.Schedule().Complete();

        // SubMesh 설정
        meshData.subMeshCount = 1;
        meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount)
        {
            vertexCount = vertexCount
        });

        // 메시에 적용
        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        return mesh;
    }

    private void CreateChunkEntity(int chunkIndex, Mesh mesh, float3 position)
    {
        var entity = entityManager.CreateEntity();

        // Transform 컴포넌트
        entityManager.AddComponentData(entity, new LocalTransform
        {
            Position = position,
            Rotation = quaternion.identity,
            Scale = 1f
        });
        entityManager.AddComponentData(entity, new LocalToWorld { Value = float4x4.identity });

        // 청크 데이터 컴포넌트
        entityManager.AddComponentData(entity, new ChunkMeshData
        {
            ChunkIndex = chunkIndex,
            TriangleCount = trianglesPerChunk,
            WorldPosition = position
        });

        // 렌더링 컴포넌트
        var renderMeshDescription = new RenderMeshDescription(ShadowCastingMode.Off);
        var renderMeshArray = new RenderMeshArray(new[] { material }, new[] { mesh });

        RenderMeshUtility.AddComponents(
            entity,
            entityManager,
            renderMeshDescription,
            renderMeshArray,
            MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)
        );

        entityManager.SetName(entity, $"ChunkEntity_{chunkIndex}");
    }
}
