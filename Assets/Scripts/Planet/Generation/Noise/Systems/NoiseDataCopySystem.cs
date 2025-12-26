using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateAfter(typeof(NoiseGenerationSystem))]
[BurstCompile]
public partial struct NoiseDataCopySystem : ISystem
{
    // NoiseGenerationSystem의 static 삭제를 위한 Handle
    private SystemHandle m_NoiseGenerationSystemHandle;

    public void OnCreate(ref SystemState state)
    {
        m_NoiseGenerationSystemHandle = state.WorldUnmanaged.GetExistingSystemState<NoiseGenerationSystem>().SystemHandle;
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        ref var noiseGenSystem = ref state.WorldUnmanaged.GetUnsafeSystemRef<NoiseGenerationSystem>(m_NoiseGenerationSystemHandle);
        ref var noiseJobsList = ref noiseGenSystem.m_NoiseJobResults;

        if (!noiseJobsList.IsCreated || noiseJobsList.Length == 0) return;

        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        var entityManager = state.EntityManager;

        // 역순으로 순회하며 처리 후 제거 (안전성 확보)
        for (int i = noiseJobsList.Length - 1; i >= 0; i--)
        {
            var jobResult = noiseJobsList[i];

            if (jobResult.JobHandle.IsCompleted)
            {
                // Job 완료 보장 및 NativeArray 데이터 복사
                jobResult.JobHandle.Complete();

                Entity entity = jobResult.Entity;
                NativeArray<float> noiseValues = jobResult.NoiseValues;

                // 데이터 복사 (NativeArray -> Dynamic Buffer)
                if (entityManager.HasBuffer<NoiseDataBuffer>(entity))
                {
                    var buffer = entityManager.GetBuffer<NoiseDataBuffer>(entity);
                    buffer.ResizeUninitialized(noiseValues.Length);
                    for (int j = 0; j < noiseValues.Length; j++)
                    {
                        buffer[j] = new NoiseDataBuffer { Value = noiseValues[j] };
                    }
                }

                // 메모리 해제
                if (noiseValues.IsCreated) noiseValues.Dispose();
                if (jobResult.NoiseLayers.IsCreated) jobResult.NoiseLayers.Dispose();

                // 리스트에서 제거 및 다음 단계 신호
                noiseJobsList.RemoveAtSwapBack(i);
                //ecb.SetComponentEnabled<NoiseVisualizationReady>(entity, true); // DebugVisualization 요청
                ecb.SetComponentEnabled<MeshGenerationRequest>(entity, true);   // MarchingCubes 요청
            }
        }
        
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}