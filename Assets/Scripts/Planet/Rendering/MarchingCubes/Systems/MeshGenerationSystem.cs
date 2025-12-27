using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Schedules MarchingCubesJob for entities with MeshGenerationRequest flag.
/// Stores job results for MeshApplySystem to process.
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

        // Initialize lookup tables
        edgeTable = new NativeArray<int>(MarchingCubesTables.EdgeTable, Allocator.Persistent);
        triTable = new NativeArray<int>(MarchingCubesTables.TriTable, Allocator.Persistent);
    }

    public void OnDestroy(ref SystemState state)
    {
        state.Dependency.Complete();

        // Dispose all pending job results
        for (int i = 0; i < MeshJobResults.Length; i++)
        {
            var result = MeshJobResults[i];
            if (result.Vertices.IsCreated) result.Vertices.Dispose();
            if (result.Normals.IsCreated) result.Normals.Dispose();
            if (result.Indices.IsCreated) result.Indices.Dispose();
        }
        MeshJobResults.Dispose();

        // Dispose lookup tables
        if (edgeTable.IsCreated) edgeTable.Dispose();
        if (triTable.IsCreated) triTable.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var newJobs = new NativeList<MeshJobResult>(Allocator.Temp);

        // Process entities with MeshGenerationRequest flag
        foreach (var (chunkData, noiseBuffer, entity) in
            SystemAPI.Query<RefRO<ChunkData>, DynamicBuffer<NoiseDataBuffer>>()
                .WithAll<MeshGenerationRequest>()
                .WithEntityAccess())
        {
            int chunkSize = chunkData.ValueRO.ChunkSize;
            int sampleSize = chunkSize + 1;  // NoiseData is (ChunkSize+1)^3

            // Copy noise data to NativeArray for job
            var noiseData = new NativeArray<float>(noiseBuffer.Length, Allocator.TempJob);
            for (int i = 0; i < noiseBuffer.Length; i++)
            {
                noiseData[i] = noiseBuffer[i].Value;
            }

            // Allocate output lists
            // Max triangles: 5 per cube, 3 vertices per triangle
            // Now we have ChunkSize^3 cubes (not ChunkSize-1)
            int maxCubes = chunkSize * chunkSize * chunkSize;
            int maxTriangles = maxCubes * 5;
            int maxVertices = maxTriangles * 3;

            var vertices = new NativeList<float3>(maxVertices, Allocator.TempJob);
            var normals = new NativeList<float3>(maxVertices, Allocator.TempJob);
            var indices = new NativeList<int>(maxVertices, Allocator.TempJob);

            // Schedule job
            var job = new MarchingCubesJob
            {
                NoiseData = noiseData,
                EdgeTable = edgeTable,
                TriTable = triTable,
                ChunkSize = chunkSize,
                SampleSize = sampleSize,
                Threshold = 0.5f,
                VoxelSize = 1.0f,
                Vertices = vertices,
                Normals = normals,
                Indices = indices
            };

            var jobHandle = job.Schedule(state.Dependency);

            newJobs.Add(new MeshJobResult
            {
                JobHandle = jobHandle,
                Entity = entity,
                NoiseData = noiseData,
                Vertices = vertices,
                Normals = normals,
                Indices = indices
            });

            // Disable flag to prevent re-processing
            ecb.SetComponentEnabled<MeshGenerationRequest>(entity, false);
        }

        // Update dependencies and cache results
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

/// <summary>
/// Stores job handle and output data for mesh generation
/// </summary>
public struct MeshJobResult
{
    public JobHandle JobHandle;
    public Entity Entity;
    public NativeArray<float> NoiseData;
    public NativeList<float3> Vertices;
    public NativeList<float3> Normals;
    public NativeList<int> Indices;
}
