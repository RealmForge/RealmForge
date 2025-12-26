// OctreeManager.cs - 매 프레임 분할
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class OctreeManager : MonoBehaviour
{
    [Header("옥트리 설정")]
    public float rootSize = 800f;
    public int maxDepth = 8;
    
    [Header("타겟")]
    public Transform target;
    
    [HideInInspector] public int baseSubdivisionDepth = 3;
    [HideInInspector] public int activeTrackingDepth = 4;
    [HideInInspector] public int neighborPreloadDepth = 5;
    [HideInInspector] public float thetaThreshold = 1.0f;
    
    private OctreeNodePool _pool;
    private int _rootIndex = -1;
    private int _playerNodeIndex = -1;
    
    // 디버깅
    public int LastSubdivisions { get; private set; }
    public int PlayerNodeDepth { get; private set; }
    
    public static OctreeManager Instance { get; private set; }
    
    public int UsedNodeCount => _pool.UsedCount;
    public int FreeNodeCount => _pool.FreeCount;
    public int RootIndex => _rootIndex;
    public float ActiveCellSize => rootSize * 2f;
    public Vector3 ActiveCellMin => Vector3.zero;
    public Vector3 ActiveCellMax => Vector3.one * rootSize * 2f;
    
    public OctreeNodePool GetPool() => _pool;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        int capacity = 500000;
        _pool = new OctreeNodePool(capacity, Allocator.Persistent);
        
        if (target == null)
        {
            Debug.LogError("OctreeManager: Target을 설정해주세요!");
            return;
        }
        
        BuildOctree(target.position);
    }
    
    void BuildOctree(Vector3 centerPosition)
    {
        float3 center = new float3(centerPosition.x, centerPosition.y, centerPosition.z);
        
        if (!_pool.TryRent(out _rootIndex))
        {
            Debug.LogError("루트 노드 생성 실패");
            return;
        }
        
        var root = OctreeNode.CreateEmpty();
        root.ParentIndex = -1;
        root.ChildIndex = 0;
        root.Depth = 0;
        root.Center = center;
        root.Size = rootSize * 2f;
        _pool.Set(_rootIndex, root);
        
        Debug.Log($"Octree 빌드: 센터={center}");
    }

    void Update()
    {
        if (target == null || _rootIndex == -1) return;
        
        float3 targetPos = new float3(target.position.x, target.position.y, target.position.z);
        
        // 루트 범위 체크
        if (!IsInsideRoot(target.position))
        {
            Debug.Log($"루트 범위 벗어남 → 재구성");
            ClearAllNodes();
            BuildOctree(target.position);
            return;
        }
        
        // ★ 매 프레임 플레이어 위치로 분할
        SubdivideTowardsPlayer(targetPos);
    }
    
    /// <summary>
    /// 루트부터 플레이어 위치까지 분할 (메인 스레드에서 직접 실행)
    /// </summary>
    void SubdivideTowardsPlayer(float3 targetPos)
    {
        int currentIdx = _rootIndex;
        int subdivisions = 0;
        
        while (currentIdx != -1)
        {
            var node = _pool.Get(currentIdx);
            
            // 최대 깊이 도달
            if (node.Depth >= maxDepth)
            {
                _playerNodeIndex = currentIdx;
                PlayerNodeDepth = node.Depth;
                break;
            }
            
            // 리프면 분할
            if (node.IsLeaf)
            {
                if (!_pool.Subdivide(currentIdx))
                {
                    Debug.LogWarning($"분할 실패: depth={node.Depth}, freeCount={_pool.FreeCount}");
                    _playerNodeIndex = currentIdx;
                    PlayerNodeDepth = node.Depth;
                    break;
                }
                subdivisions++;
                node = _pool.Get(currentIdx);  // 분할 후 다시 가져오기
            }
            
            // 타겟이 포함된 자식 찾기
            int octant = GetOctant(node.Center, targetPos);
            int childIdx = node.GetChild(octant);
            
            if (childIdx == -1 || !_pool.IsUsed(childIdx))
            {
                Debug.LogWarning($"자식 없음: octant={octant}, childIdx={childIdx}");
                _playerNodeIndex = currentIdx;
                PlayerNodeDepth = node.Depth;
                break;
            }
            
            currentIdx = childIdx;
        }
        
        LastSubdivisions = subdivisions;
    }
    
    int GetOctant(float3 nodeCenter, float3 position)
    {
        int octant = 0;
        if (position.x >= nodeCenter.x) octant |= 1;
        if (position.y >= nodeCenter.y) octant |= 2;
        if (position.z >= nodeCenter.z) octant |= 4;
        return octant;
    }
    
    public bool IsInsideRoot(Vector3 pos)
    {
        if (_rootIndex == -1) return false;
        var root = _pool.Get(_rootIndex);
        root.GetAABB(out float3 rootMin, out float3 rootMax);
        
        return pos.x >= rootMin.x && pos.x <= rootMax.x &&
               pos.y >= rootMin.y && pos.y <= rootMax.y &&
               pos.z >= rootMin.z && pos.z <= rootMax.z;
    }
    
    public bool IsInsideActiveCell(Vector3 pos) => IsInsideRoot(pos);
    
    void ClearAllNodes()
    {
        for (int i = 0; i < _pool.Capacity; i++)
        {
            if (_pool.IsUsed(i))
                _pool.Return(i);
        }
        _rootIndex = -1;
        _playerNodeIndex = -1;
    }

    void OnDestroy()
    {
        _pool.Dispose();
    }
}