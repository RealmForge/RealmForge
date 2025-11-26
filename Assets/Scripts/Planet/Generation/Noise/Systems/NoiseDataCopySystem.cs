// Systems/NoiseDataCopySystem.cs
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using RealmForge.Planet.Generation.Noise.Components;

[UpdateAfter(typeof(NoiseGenerationSystem))]
[BurstCompile]
public partial  struct NoiseDataCopySystem : ISystem
{
    // 💡 참고: 실제로는 NoiseGenerationSystem의 NativeList 필드에 접근하는 별도의 방법이 필요함
    // 여기서는 편의상 m_PerlinJobResults를 static/global하게 접근 가능하다고 가정합니다.
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // ❌ 실제 코드로 컴파일하려면 m_PerlinJobResults 접근 방식을 변경해야 함
        // 예시를 위해 m_PerlinJobResults가 static으로 접근 가능하다고 가정
        
        // 현재는 로직 흐름 설명을 위해 코드 생략

        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        var entityManager = state.EntityManager;
        
        var perlinJobsList = NoiseGenerationSystem.m_PerlinJobResults;
        if (!NoiseGenerationSystem.m_PerlinJobResults.IsCreated || NoiseGenerationSystem.m_PerlinJobResults.Length == 0) return;

        // 💡 주의: 이 코드는 NoiseGenerationSystem의 m_PerlinJobResults에 대한 참조를 직접 가져와야 작동합니다.
        // 역순으로 순회하며 처리 후 제거 (안전성 확보)
        
        for (int i = perlinJobsList.Length - 1; i >= 0; i--)
        {
            var jobResult = perlinJobsList[i];
            
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
                
                // 리스트에서 제거 및 다음 단계 신호
                perlinJobsList.RemoveAtSwapBack(i); 
                ecb.SetComponentEnabled<NoiseVisualizationReady>(entity, true); // 다음 단계 요청 활성화
            }
        }
        
        
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}