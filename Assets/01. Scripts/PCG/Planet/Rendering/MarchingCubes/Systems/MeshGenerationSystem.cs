using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// ★ 옥트리 기반: ChunkData.Min, ChunkData.Size로 VoxelSize 계산
/// </summary>
[UpdateAfter(typeof(NoiseDataCopySystem))]
[BurstCompile]
public partial struct MeshGenerationSystem : ISystem
{
    private NativeArray<int> edgeTable;
    private NativeArray<int> triTable;

    public NativeList<MeshJobResult> MeshJobResults;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ChunkData>();

        MeshJobResults = new NativeList<MeshJobResult>(Allocator.Persistent);

        edgeTable = new NativeArray<int>(MarchingCubesTables.EdgeTable, Allocator.Persistent);
        triTable = new NativeArray<int>(MarchingCubesTables.TriTable, Allocator.Persistent);
    }

    public void OnDestroy(ref SystemState state)
    {
        state.Dependency.Complete();

        for (int i = 0; i < MeshJobResults.Length; i++)
        {
            var result = MeshJobResults[i];
            if (result.Vertices.IsCreated) result.Vertices.Dispose();
            if (result.Normals.IsCreated) result.Normals.Dispose();
            if (result.Indices.IsCreated) result.Indices.Dispose();
            if (result.Colors.IsCreated) result.Colors.Dispose();
            if (result.TerrainLayers.IsCreated) result.TerrainLayers.Dispose();
        }
        MeshJobResults.Dispose();

        if (edgeTable.IsCreated) edgeTable.Dispose();
        if (triTable.IsCreated) triTable.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var newJobs = new NativeList<MeshJobResult>(Allocator.Temp);

        foreach (var (chunkData, planetData, noiseBuffer, terrainBuffer, entity) in
            SystemAPI.Query<RefRO<ChunkData>, RefRO<PlanetData>, DynamicBuffer<NoiseDataBuffer>, DynamicBuffer<TerrainLayerBuffer>>()
                .WithAll<MeshGenerationRequest>()
                .WithEntityAccess())
        {
            int chunkSize = chunkData.ValueRO.ChunkSize;
            int sampleSize = chunkSize + 1;

            var noiseData = new NativeArray<float>(noiseBuffer.Length, Allocator.TempJob);
            for (int i = 0; i < noiseBuffer.Length; i++)
            {
                noiseData[i] = noiseBuffer[i].Value;
            }

            int maxCubes = chunkSize * chunkSize * chunkSize;
            int maxTriangles = maxCubes * 5;
            int maxVertices = maxTriangles * 3;

            var vertices = new NativeList<float3>(maxVertices, Allocator.TempJob);
            var normals = new NativeList<float3>(maxVertices, Allocator.TempJob);
            var indices = new NativeList<int>(maxVertices, Allocator.TempJob);
            var colors = new NativeList<float4>(maxVertices, Allocator.TempJob);

            var terrainLayers = new NativeArray<TerrainLayerBuffer>(terrainBuffer.Length, Allocator.TempJob);
            for (int i = 0; i < terrainBuffer.Length; i++)
            {
                terrainLayers[i] = terrainBuffer[i];
            }

            float voxelSize = chunkData.ValueRO.Size / chunkSize;

            var job = new MarchingCubesJob
            {
                NoiseData = noiseData,
                EdgeTable = edgeTable,
                TriTable = triTable,
                ChunkSize = chunkSize,
                SampleSize = sampleSize,
                Threshold = 0f,
                VoxelSize = voxelSize,
                ChunkMin = chunkData.ValueRO.Min,

                PlanetCenter = planetData.ValueRO.Center,
                PlanetRadius = planetData.ValueRO.Radius,
                TerrainLayers = terrainLayers,

                Vertices = vertices,
                Normals = normals,
                Indices = indices,
                Colors = colors
            };

            var jobHandle = job.Schedule(state.Dependency);

            newJobs.Add(new MeshJobResult
            {
                JobHandle = jobHandle,
                Entity = entity,
                NoiseData = noiseData,
                Vertices = vertices,
                Normals = normals,
                Indices = indices,
                Colors = colors,
                TerrainLayers = terrainLayers
            });

            ecb.SetComponentEnabled<MeshGenerationRequest>(entity, false);
        }

        if (newJobs.Length > 0)
        {
            var jobHandles = new NativeArray<JobHandle>(newJobs.Length, Allocator.Temp);
            for (int i = 0; i < newJobs.Length; i++)
            {
                jobHandles[i] = newJobs[i].JobHandle;
            }

            state.Dependency = JobHandle.CombineDependencies(jobHandles);
            MeshJobResults.AddRange(newJobs.AsArray());

            jobHandles.Dispose();
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        newJobs.Dispose();
    }
}

public struct MeshJobResult
{
    public JobHandle JobHandle;
    public Entity Entity;
    public NativeArray<float> NoiseData;
    public NativeList<float3> Vertices;
    public NativeList<float3> Normals;
    public NativeList<int> Indices;
    public NativeList<float4> Colors;
    public NativeArray<TerrainLayerBuffer> TerrainLayers;
}