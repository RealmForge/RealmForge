using Unity.Entities;
using Unity.Scenes;
using UnityEngine;

/// <summary>
/// SubScene이 로드되었는지 확인하고 로드되지 않았으면 로드 요청
/// </summary>
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(InitializationSystemGroup))]
partial struct SubSceneLoadSystem : ISystem
{
    private bool _logged;

    public void OnCreate(ref SystemState state)
    {
        _logged = false;
    }

    public void OnUpdate(ref SystemState state)
    {
        // EntitiesReference가 이미 있으면 SubScene 로드 완료
        if (SystemAPI.HasSingleton<EntitiesReference>())
        {
            if (!_logged)
            {
                Debug.Log($"[SubSceneLoad] EntitiesReference found in {state.WorldUnmanaged.Name}");
                _logged = true;
            }
            return;
        }

        // SubScene 엔티티 찾기 및 로드 요청
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (sceneRef, entity) in SystemAPI.Query<RefRO<SceneReference>>()
            .WithNone<RequestSceneLoaded>()
            .WithEntityAccess())
        {
            Debug.Log($"[SubSceneLoad] Requesting scene load in {state.WorldUnmanaged.Name}");
            ecb.AddComponent<RequestSceneLoaded>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
