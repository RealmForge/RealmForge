// OctreeManager.cs - Burst + Job System
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;

public class OctreeManager : MonoBehaviour
{
    [Header("옥트리 설정")]
    public float rootSize = 800f;
    public int maxDepth = 8;
    
    [Header("타겟")]
    public Transform target;
    
    [Header("거리 기반 LOD")]
    [Range(1, 12)]
    public int maxLodDepth = 6;
    [Range(1, 8)]
    public int minLodDepth = 2;
    public float lodStepDistance = 50f;
    
    [Header("경계 설정")]
    [Range(0.01f, 0.5f)]
    public float boundaryThreshold = 0.2f;
    
    private OctreeNodePool _pool;
    private int _rootIndex = -1;
    private int _playerNodeIndex = -1;
    
    // Job용 버퍼
    private NativeArray<int> _targetDepths;
    private NativeList<int> _traversalStack;
    private NativeArray<int> _mergeWorkStack;
    private NativeList<int> _nodesToSubdivide;
    private NativeList<int> _nodesToMerge;
    
    public int LastSubdivisions { get; private set; }
    public int LastMerges { get; private set; }
    public int PlayerNodeDepth { get; private set; }
    
    public static OctreeManager Instance { get; private set; }
    
    public int UsedNodeCount => _pool.UsedCount;
    public int FreeNodeCount => _pool.FreeCount;
    public int RootIndex => _rootIndex;
    public OctreeNodePool GetPool() => _pool;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    
    void OnValidate()
    {
        if (minLodDepth > maxLodDepth) minLodDepth = maxLodDepth;
        if (maxLodDepth > maxDepth) maxLodDepth = maxDepth;
    }

    public void RegisterTarget(Transform target)
    {
        this.target = target;
        
        Initiallize();
    }

    void Initiallize()
    {
        int capacity = 500000;
        _pool = new OctreeNodePool(capacity, Allocator.Persistent);
        _targetDepths = new NativeArray<int>(capacity, Allocator.Persistent);
        _traversalStack = new NativeList<int>(4096, Allocator.Persistent);
        _mergeWorkStack = new NativeArray<int>(4096, Allocator.Persistent);
        _nodesToSubdivide = new NativeList<int>(1024, Allocator.Persistent);
        _nodesToMerge = new NativeList<int>(1024, Allocator.Persistent);
        
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
    }

    void Update()
    {
        if (target == null || _rootIndex == -1) return;
        
        float3 targetPos = new float3(target.position.x, target.position.y, target.position.z);
        
        if (!IsInsideRoot(target.position))
        {
            Debug.Log("루트 범위 벗어남 → 재구성");
            ClearAllNodes();
            BuildOctree(target.position);
            return;
        }
        
        UpdateWithJobs(targetPos);
    }
    
    void UpdateWithJobs(float3 targetPos)
    {
        // 1단계: 병렬로 모든 노드의 목표 깊이 계산
        var calcJob = new CalculateTargetDepthJob
        {
            Nodes = _pool.Nodes,
            IsUsedFlags = _pool.IsUsedFlags,
            TargetPos = targetPos,
            MaxLodDepth = maxLodDepth,
            MinLodDepth = minLodDepth,
            LodStepDistance = lodStepDistance,
            BoundaryThreshold = boundaryThreshold,
            TargetDepths = _targetDepths
        };
        
        var calcHandle = calcJob.Schedule(_pool.Capacity, 256);
        calcHandle.Complete();
        
        // 2단계: 메인 스레드에서 분할/병합 결정 및 실행
        ProcessNodesMainThread(targetPos);
    }
    
    void ProcessNodesMainThread(float3 targetPos)
    {
        int subdivisions = 0;
        int merges = 0;
        
        _traversalStack.Clear();
        _traversalStack.Add(_rootIndex);
        
        while (_traversalStack.Length > 0)
        {
            int currentIdx = _traversalStack[_traversalStack.Length - 1];
            _traversalStack.RemoveAt(_traversalStack.Length - 1);
            
            if (!_pool.IsUsed(currentIdx)) continue;
            
            var node = _pool.Get(currentIdx);
            int targetDepth = _targetDepths[currentIdx];
            
            if (targetDepth < 0) continue;
            
            // 플레이어 노드 추적
            if (OctreeNode.ContainsPoint(node.Center, node.Size, targetPos) && node.IsLeaf)
            {
                _playerNodeIndex = currentIdx;
                PlayerNodeDepth = node.Depth;
            }
            
            // 분할/병합 결정
            if (node.Depth < targetDepth)
            {
                if (node.IsLeaf)
                {
                    if (_pool.Subdivide(currentIdx))
                    {
                        subdivisions++;
                        node = _pool.Get(currentIdx);
                    }
                }
                
                if (!node.IsLeaf)
                {
                    AddChildrenToStack(node);
                }
            }
            else if (node.Depth > targetDepth && !node.IsLeaf)
            {
                if (TryMergeCompletely(currentIdx, targetPos))
                {
                    merges++;
                    _traversalStack.Add(currentIdx);
                }
                else
                {
                    AddChildrenToStack(node);
                }
            }
            else if (node.Depth == targetDepth)
            {
                if (!node.IsLeaf)
                {
                    if (TryMergeCompletely(currentIdx, targetPos))
                    {
                        merges++;
                    }
                    else
                    {
                        AddChildrenToStack(node);
                    }
                }
            }
        }
        
        LastSubdivisions = subdivisions;
        LastMerges = merges;
    }
    
    bool TryMergeCompletely(int nodeIdx, float3 targetPos)
    {
        if (!_pool.IsUsed(nodeIdx)) return false;
        
        var node = _pool.Get(nodeIdx);
        if (node.IsLeaf) return true;
        
        if (OctreeNode.ContainsPoint(node.Center, node.Size, targetPos))
        {
            int playerOctant = OctreeNode.GetOctant(node.Center, targetPos);
            int playerChildIdx = node.GetChild(playerOctant);
            
            if (playerChildIdx != -1 && _pool.IsUsed(playerChildIdx))
            {
                var playerChild = _pool.Get(playerChildIdx);
                int childTargetDepth = _targetDepths[playerChildIdx];
                
                if (playerChild.Depth < childTargetDepth)
                {
                    return false;
                }
            }
        }
        
        for (int i = 0; i < 8; i++)
        {
            int childIdx = node.GetChild(i);
            if (childIdx != -1 && _pool.IsUsed(childIdx))
            {
                var child = _pool.Get(childIdx);
                if (!child.IsLeaf)
                {
                    int childTarget = _targetDepths[childIdx];
                    if (child.Depth >= childTarget)
                    {
                        TryMergeCompletely(childIdx, targetPos);
                    }
                }
            }
        }
        
        node = _pool.Get(nodeIdx);
        for (int i = 0; i < 8; i++)
        {
            int childIdx = node.GetChild(i);
            if (childIdx != -1 && _pool.IsUsed(childIdx))
            {
                if (!_pool.Get(childIdx).IsLeaf)
                {
                    return false;
                }
            }
        }
        
        return _pool.Merge(nodeIdx, ref _mergeWorkStack);
    }
    
    void AddChildrenToStack(OctreeNode node)
    {
        for (int i = 0; i < 8; i++)
        {
            int childIdx = node.GetChild(i);
            if (childIdx != -1 && _pool.IsUsed(childIdx))
            {
                _traversalStack.Add(childIdx);
            }
        }
    }
    
    public bool IsInsideRoot(Vector3 pos)
    {
        if (_rootIndex == -1) return false;
        var root = _pool.Get(_rootIndex);
        return OctreeNode.ContainsPoint(root.Center, root.Size, new float3(pos.x, pos.y, pos.z));
    }
    
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
        if (_targetDepths.IsCreated) _targetDepths.Dispose();
        if (_traversalStack.IsCreated) _traversalStack.Dispose();
        if (_mergeWorkStack.IsCreated) _mergeWorkStack.Dispose();
        if (_nodesToSubdivide.IsCreated) _nodesToSubdivide.Dispose();
        if (_nodesToMerge.IsCreated) _nodesToMerge.Dispose();
    }
    
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 150));
        GUILayout.Label($"Player Depth: {PlayerNodeDepth}");
        GUILayout.Label($"Subdivisions: {LastSubdivisions}");
        GUILayout.Label($"Merges: {LastMerges}");
        GUILayout.Label($"Used Nodes: {UsedNodeCount}");
        GUILayout.EndArea();
    }
}