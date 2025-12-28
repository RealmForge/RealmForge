using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

[UpdateAfter(typeof(MeshGenerationSystem))]
public partial class MeshApplySystem : SystemBase
{
    private SystemHandle meshGenerationSystemHandle;
    private UnityEngine.Material defaultMaterial;
    private List<PendingColliderJob> pendingColliderJobs;

    private struct PendingColliderJob
    {
        public JobHandle JobHandle;
        public Entity Entity;
        public NativeArray<float3> Vertices;
        public NativeArray<int3> Triangles;
        public NativeArray<BlobAssetReference<Unity.Physics.Collider>> ColliderOutput;
    }

    protected override void OnCreate()
    {
        RequireForUpdate<ChunkData>();
        meshGenerationSystemHandle = World.GetExistingSystem<MeshGenerationSystem>();
        pendingColliderJobs = new List<PendingColliderJob>();
    }

    protected override void OnStartRunning()
    {
        if (defaultMaterial == null)
        {
            var shader = Shader.Find("Custom/TerrainVertexColor");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            defaultMaterial = new UnityEngine.Material(shader);
            defaultMaterial.enableInstancing = true;
        }
    }

    protected override void OnDestroy()
    {
        if (defaultMaterial != null)
        {
            Object.Destroy(defaultMaterial);
        }

        foreach (var pending in pendingColliderJobs)
        {
            pending.JobHandle.Complete();
            if (pending.Vertices.IsCreated) pending.Vertices.Dispose();
            if (pending.Triangles.IsCreated) pending.Triangles.Dispose();
            if (pending.ColliderOutput.IsCreated)
            {
                if (pending.ColliderOutput[0].IsCreated)
                    pending.ColliderOutput[0].Dispose();
                pending.ColliderOutput.Dispose();
            }
        }
        pendingColliderJobs.Clear();
    }

    protected override void OnUpdate()
    {
        ProcessCompletedMeshJobs();
        ProcessCompletedColliderJobs();
    }

    private void ProcessCompletedMeshJobs()
    {
        ref var meshGenSystem = ref World.Unmanaged.GetUnsafeSystemRef<MeshGenerationSystem>(meshGenerationSystemHandle);
        ref var meshJobResults = ref meshGenSystem.MeshJobResults;

        if (!meshJobResults.IsCreated || meshJobResults.Length == 0) return;

        for (int i = meshJobResults.Length - 1; i >= 0; i--)
        {
            var result = meshJobResults[i];

            if (result.JobHandle.IsCompleted)
            {
                result.JobHandle.Complete();

                if (result.Vertices.Length > 0)
                {
                    CreateAndApplyMesh(result.Entity, result);
                    ScheduleColliderJob(result.Entity, result);
                }

                if (result.NoiseData.IsCreated) result.NoiseData.Dispose();
                if (result.Vertices.IsCreated) result.Vertices.Dispose();
                if (result.Normals.IsCreated) result.Normals.Dispose();
                if (result.Indices.IsCreated) result.Indices.Dispose();
                if (result.Colors.IsCreated) result.Colors.Dispose();
                if (result.TerrainLayers.IsCreated) result.TerrainLayers.Dispose();

                meshJobResults.RemoveAtSwapBack(i);
            }
        }
    }

    private void ProcessCompletedColliderJobs()
    {
        for (int i = pendingColliderJobs.Count - 1; i >= 0; i--)
        {
            var pending = pendingColliderJobs[i];

            if (pending.JobHandle.IsCompleted)
            {
                pending.JobHandle.Complete();

                if (pending.ColliderOutput[0].IsCreated)
                {
                    ApplyCollider(pending.Entity, pending.ColliderOutput[0]);
                }

                if (pending.Vertices.IsCreated) pending.Vertices.Dispose();
                if (pending.Triangles.IsCreated) pending.Triangles.Dispose();
                if (pending.ColliderOutput.IsCreated) pending.ColliderOutput.Dispose();

                pendingColliderJobs.RemoveAt(i);
            }
        }
    }

    private void ScheduleColliderJob(Entity entity, MeshJobResult result)
    {
        int vertexCount = result.Vertices.Length;
        int triangleCount = result.Indices.Length / 3;

        if (vertexCount == 0 || triangleCount == 0) return;

        var vertices = new NativeArray<float3>(vertexCount, Allocator.TempJob);
        var triangles = new NativeArray<int3>(triangleCount, Allocator.TempJob);
        var colliderOutput = new NativeArray<BlobAssetReference<Unity.Physics.Collider>>(1, Allocator.TempJob);

        NativeArray<float3>.Copy(result.Vertices.AsArray(), vertices);

        for (int i = 0; i < triangleCount; i++)
        {
            triangles[i] = new int3(
                result.Indices[i * 3],
                result.Indices[i * 3 + 1],
                result.Indices[i * 3 + 2]
            );
        }

        var job = new ColliderCreationJob
        {
            Vertices = vertices,
            Triangles = triangles,
            Output = colliderOutput
        };

        var jobHandle = job.Schedule();

        pendingColliderJobs.Add(new PendingColliderJob
        {
            JobHandle = jobHandle,
            Entity = entity,
            Vertices = vertices,
            Triangles = triangles,
            ColliderOutput = colliderOutput
        });
    }

    private void ApplyCollider(Entity entity, BlobAssetReference<Unity.Physics.Collider> collider)
    {
        if (!EntityManager.Exists(entity)) return;

        if (!EntityManager.HasComponent<PhysicsCollider>(entity))
        {
            EntityManager.AddComponentData(entity, new PhysicsCollider { Value = collider });
        }
        else
        {
            var oldCollider = EntityManager.GetComponentData<PhysicsCollider>(entity);
            if (oldCollider.Value.IsCreated)
            {
                oldCollider.Value.Dispose();
            }
            EntityManager.SetComponentData(entity, new PhysicsCollider { Value = collider });
        }
    }

    private void CreateAndApplyMesh(Entity entity, MeshJobResult result)
    {
        int vertexCount = result.Vertices.Length;
        int indexCount = result.Indices.Length;

        if (vertexCount == 0 || indexCount == 0) return;

        var mesh = new Mesh { name = $"ChunkMesh_{entity.Index}" };

        var meshDataArray = Mesh.AllocateWritableMeshData(1);
        var meshData = meshDataArray[0];

        var vertexAttributes = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Temp);
        vertexAttributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
        vertexAttributes[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);
        vertexAttributes[2] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4);

        meshData.SetVertexBufferParams(vertexCount, vertexAttributes);
        meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);

        vertexAttributes.Dispose();

        var vertices = meshData.GetVertexData<MeshVertex>();
        for (int i = 0; i < vertexCount; i++)
        {
            vertices[i] = new MeshVertex
            {
                Position = result.Vertices[i],
                Normal = result.Normals[i],
                Color = result.Colors[i]
            };
        }

        var indices = meshData.GetIndexData<uint>();
        for (int i = 0; i < indexCount; i++)
        {
            indices[i] = (uint)result.Indices[i];
        }

        meshData.subMeshCount = 1;
        meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount)
        {
            vertexCount = vertexCount
        });

        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
        mesh.RecalculateBounds();

        AddRenderingComponents(entity, mesh);
    }

    private void AddRenderingComponents(Entity entity, Mesh mesh)
    {
        float3 worldPosition = float3.zero;
        if (EntityManager.HasComponent<ChunkData>(entity))
        {
            var chunkData = EntityManager.GetComponentData<ChunkData>(entity);
            worldPosition = chunkData.Min;
        }

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

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct MeshVertex
{
    public float3 Position;
    public float3 Normal;
    public float4 Color;
}
