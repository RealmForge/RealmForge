using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// SessionClientWorld의 로컬 플레이어 엔티티 위치를 OctreeManager의 Target Transform에 동기화하는 시스템
/// </summary>
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class OctreeTargetSyncSystem : SystemBase
{
    private GameObject _targetObject;
    private Transform _targetTransform;
    private bool _initialized;

    protected override void OnCreate()
    {
        RequireForUpdate<NetworkId>();
    }
    
    protected override void OnUpdate()
    {
    // OctreeManager가 없으면 리턴
    if (OctreeManager.Instance == null)
        return;

    // Target GameObject 생성 (한 번만)
    if (!_initialized)
    {
        _targetObject = new GameObject("OctreeTarget");
        _targetTransform = _targetObject.transform;
        OctreeManager.Instance.target = _targetTransform;
        _initialized = true;
        Debug.Log("[OctreeTargetSync] Target GameObject created and assigned to OctreeManager");
    }

    // SessionClientWorld에서 로컬 플레이어 찾기
    foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>()
                 .WithAll<GhostOwnerIsLocal, PlayerComponent>())
    {
        // 플레이어 위치를 Target Transform에 동기화
        _targetTransform.position = transform.ValueRO.Position;
        return; // 첫 번째 로컬 플레이어만 사용
    }
}

protected override void OnDestroy()
{
    if (_targetObject != null)
    {
        Object.Destroy(_targetObject);
    }
}
}