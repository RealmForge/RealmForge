using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using Material = UnityEngine.Material;

/// <summary>
/// Applies completed mesh generation results to entities.
/// Creates Unity Mesh and adds rendering components.
/// </summary>
[UpdateAfter(typeof(MeshGenerationSystem))]
public partial class MeshApplySystem : SystemBase
{
    private SystemHandle meshGenerationSystemHandle;
    private Material defaultMaterial;

    protected override void OnCreate()
    {
        RequireForUpdate<ChunkData>();
        meshGenerationSystemHandle = World.GetExistingSystem<MeshGenerationSystem>();
    }

    protected override void OnStartRunning()
    {
        // Create default material if not set
        if (defaultMaterial == null)
        {
            defaultMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            defaultMaterial.color = Color.gray;
        }
    }

    protected override void OnDestroy()
    {
        if (defaultMaterial != null)
        {
            Object.Destroy(defaultMaterial);
        }
    }

    protected override void OnUpdate()
    {
        ref var meshGenSystem = ref World.Unmanaged.GetUnsafeSystemRef<MeshGenerationSystem>(meshGenerationSystemHandle);
        ref var meshJobResults = ref meshGenSystem.MeshJobResults;

        if (!meshJobResults.IsCreated || meshJobResults.Length == 0) return;
        
        // Process completed jobs in reverse order
        for (int i = meshJobResults.Length - 1; i >= 0; i--)
        {
            var result = meshJobResults[i];

            if (result.JobHandle.IsCompleted)
            {
                result.JobHandle.Complete();

                if (result.Vertices.Length > 0)
                {
                    CreateAndApplyMesh(result.Entity, result);
                }

                // Dispose native collections
                if (result.NoiseData.IsCreated) result.NoiseData.Dispose();
                if (result.Vertices.IsCreated) result.Vertices.Dispose();
                if (result.Normals.IsCreated) result.Normals.Dispose();
                if (result.Indices.IsCreated) result.Indices.Dispose();

                meshJobResults.RemoveAtSwapBack(i);
            }
        }
    }

    private void CreateAndApplyMesh(Entity entity, MeshJobResult result)
    {
        int vertexCount = result.Vertices.Length;
        int indexCount = result.Indices.Length;

        if (vertexCount == 0 || indexCount == 0) return;

        // Create mesh
        var mesh = new Mesh { name = $"ChunkMesh_{entity.Index}" };

        // Allocate writable mesh data
        var meshDataArray = Mesh.AllocateWritableMeshData(1);
        var meshData = meshDataArray[0];

        // Set vertex buffer params
        var vertexAttributes = new NativeArray<VertexAttributeDescriptor>(2, Allocator.Temp);
        vertexAttributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
        vertexAttributes[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);

        meshData.SetVertexBufferParams(vertexCount, vertexAttributes);
        meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);

        vertexAttributes.Dispose();

        // Copy vertex data
        var vertices = meshData.GetVertexData<MeshVertex>();
        for (int i = 0; i < vertexCount; i++)
        {
            vertices[i] = new MeshVertex
            {
                Position = result.Vertices[i],
                Normal = result.Normals[i]
            };
        }

        // Copy index data
        var indices = meshData.GetIndexData<uint>();
        for (int i = 0; i < indexCount; i++)
        {
            indices[i] = (uint)result.Indices[i];
        }

        // Set submesh
        meshData.subMeshCount = 1;
        meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount)
        {
            vertexCount = vertexCount
        });

        // Apply mesh data
        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
        mesh.RecalculateBounds();

        // Add physics collider for collision detection
        AddMeshCollider(entity, result.Vertices, result.Indices);

        // Add rendering components to entity
        AddRenderingComponents(entity, mesh);

        Debug.Log($"Mesh created for entity {entity.Index}: {vertexCount} vertices, {indexCount / 3} triangles");
    }

    private void AddMeshCollider(Entity entity, NativeList<float3> vertexList, NativeList<int> indexList)
    {
        int vertexCount = vertexList.Length;
        int triangleCount = indexList.Length / 3;

        if (vertexCount == 0 || triangleCount == 0) return;

        // Copy vertices to NativeArray
        var vertices = new NativeArray<float3>(vertexCount, Allocator.Temp);
        for (int i = 0; i < vertexCount; i++)
        {
            vertices[i] = vertexList[i];
        }

        // Convert indices to int3 triangles
        var triangles = new NativeArray<int3>(triangleCount, Allocator.Temp);
        for (int i = 0; i < triangleCount; i++)
        {
            triangles[i] = new int3(
                indexList[i * 3],
                indexList[i * 3 + 1],
                indexList[i * 3 + 2]
            );
        }

        // Create MeshCollider
        var collider = Unity.Physics.MeshCollider.Create(vertices, triangles);

        vertices.Dispose();
        triangles.Dispose();

        // Add or set PhysicsCollider component
        if (!EntityManager.HasComponent<PhysicsCollider>(entity))
        {
            EntityManager.AddComponentData(entity, new PhysicsCollider
            {
                Value = collider
            });
        }
        else
        {
            // Dispose old collider before replacing
            var oldCollider = EntityManager.GetComponentData<PhysicsCollider>(entity);
            if (oldCollider.Value.IsCreated)
            {
                oldCollider.Value.Dispose();
            }
            EntityManager.SetComponentData(entity, new PhysicsCollider
            {
                Value = collider
            });
        }
    }

    private void AddRenderingComponents(Entity entity, Mesh mesh)
    {
        // ★ 옥트리 기반: ChunkMin을 엔티티 위치로 사용
        // (버텍스는 로컬 좌표로 생성됨)
        float3 worldPosition = float3.zero;
        if (EntityManager.HasComponent<ChunkData>(entity))
        {
            var chunkData = EntityManager.GetComponentData<ChunkData>(entity);
            worldPosition = chunkData.Min;
        }

        // Set transform with chunk position
        if (!EntityManager.HasComponent<LocalTransform>(entity))
        {
            EntityManager.AddComponentData(entity, new LocalTransform
            {
                Position = worldPosition,
                Rotation = quaternion.identity,
                Scale = 1f
            });
        }
        else
        {
            EntityManager.SetComponentData(entity, new LocalTransform
            {
                Position = worldPosition,
                Rotation = quaternion.identity,
                Scale = 1f
            });
        }

        if (!EntityManager.HasComponent<LocalToWorld>(entity))
        {
            EntityManager.AddComponentData(entity, new LocalToWorld
            {
                Value = float4x4.TRS(worldPosition, quaternion.identity, new float3(1f))
            });
        }

        // Add rendering components
        var renderMeshDescription = new RenderMeshDescription(ShadowCastingMode.On);
        var renderMeshArray = new RenderMeshArray(new[] { defaultMaterial }, new[] { mesh });

        RenderMeshUtility.AddComponents(
            entity,
            EntityManager,
            renderMeshDescription,
            renderMeshArray,
            MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)
        );
    }
}

/// <summary>
/// Vertex structure for mesh data (must match vertex attributes)
/// </summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct MeshVertex
{
    public float3 Position;
    public float3 Normal;
}
