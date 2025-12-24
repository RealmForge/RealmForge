// OctreeTest.cs
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct CollectLeafNodesJob : IJob
{
    [ReadOnly] public NativeArray<OctreeNode> Nodes;
    [ReadOnly] public NativeArray<bool> IsUsedFlags;
    public NativeArray<int> TraversalStack;
    public NativeArray<int> LeafIndices;
    public int StartNodeIndex;
    public NativeArray<int> LeafCount;
    
    
    public void Execute()
    {
        int stackCount = 0;
        int leafCount = 0;
        
        TraversalStack[stackCount++] = StartNodeIndex;
        
        while (stackCount > 0)
        {
            int currentIdx = TraversalStack[--stackCount];
            
            if (!IsUsedFlags[currentIdx]) continue;
            
            var node = Nodes[currentIdx];
            
            if (node.IsLeaf)
            {
                if (leafCount < LeafIndices.Length)
                    LeafIndices[leafCount++] = currentIdx;
            }
            else
            {
                for (int i = 0; i < 8; i++)
                {
                    int childIdx = node.GetChild(i);
                    if (childIdx != -1)
                        TraversalStack[stackCount++] = childIdx;
                }
            }
        }
        
        LeafCount[0] = leafCount;
    }
}

[BurstCompile]
public struct CalculateLODJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<OctreeNode> Nodes;
    [ReadOnly] public NativeArray<int> LeafIndices;
    [ReadOnly] public NativeArray<float> LodDistancesSq;
    [ReadOnly] public float3 TargetPosition;
    [ReadOnly] public int MaxDepth;
    [ReadOnly] public int MinLodDepth;
    
    [WriteOnly] public NativeArray<int> SubdivisionRequests;
    
    public void Execute(int index)
    {
        int nodeIdx = LeafIndices[index];
        var node = Nodes[nodeIdx];
        
        if (node.Depth >= MaxDepth)
        {
            SubdivisionRequests[index] = -1;
            return;
        }
        
        node.GetAABB(out float3 nodeMin, out float3 nodeMax);
        float3 nodeCenter = (nodeMin + nodeMax) * 0.5f;
        
        float distSq = math.distancesq(TargetPosition, nodeCenter);
        int targetDepth = MinLodDepth;
        
        for (int i = 0; i < LodDistancesSq.Length; i++)
        {
            if (distSq < LodDistancesSq[i])
            {
                targetDepth = MaxDepth - i;
                break;
            }
        }
        
        SubdivisionRequests[index] = (node.Depth < targetDepth) ? nodeIdx : -1;
    }
}

[BurstCompile]
public struct SubdivideUniformJob : IJob
{
    public NativeArray<OctreeNode> Nodes;
    public NativeArray<bool> IsUsedFlags;
    public NativeArray<int> FreeStack;
    public NativeArray<int> FreeCount;
    public NativeArray<int> TraversalStack;
    public int StartNodeIndex;
    public int TargetDepth;
    public int Capacity;
    
    public void Execute()
    {
        int stackCount = 0;
        TraversalStack[stackCount++] = StartNodeIndex;
        
        while (stackCount > 0)
        {
            int currentIdx = TraversalStack[--stackCount];
            var node = Nodes[currentIdx];
            
            if (node.Depth >= TargetDepth) continue;
            
            if (node.IsLeaf)
            {
                if (!SubdivideInternal(currentIdx)) continue;
                node = Nodes[currentIdx];
            }
            
            for (int i = 0; i < 8; i++)
            {
                int childIdx = node.GetChild(i);
                if (childIdx != -1)
                    TraversalStack[stackCount++] = childIdx;
            }
        }
    }
    
    bool SubdivideInternal(int parentIndex)
    {
        var parent = Nodes[parentIndex];
        int freeCount = FreeCount[0];
        
        if (!parent.IsLeaf || freeCount < 8) return false;

        float childSize = parent.Size * 0.5f;
        
        parent.GetAABB(out float3 parentMin, out float3 parentMax);
        float3 parentMid = (parentMin + parentMax) * 0.5f;

        for (int i = 0; i < 8; i++)
        {
            int childIdx = FreeStack[--freeCount];
            IsUsedFlags[childIdx] = true;
            
            parent.SetChild(i, childIdx);
        
            float3 childMin, childMax;
            childMin.x = ((i & 1) == 0) ? parentMin.x : parentMid.x;
            childMax.x = ((i & 1) == 0) ? parentMid.x : parentMax.x;
            childMin.y = ((i & 2) == 0) ? parentMin.y : parentMid.y;
            childMax.y = ((i & 2) == 0) ? parentMid.y : parentMax.y;
            childMin.z = ((i & 4) == 0) ? parentMin.z : parentMid.z;
            childMax.z = ((i & 4) == 0) ? parentMid.z : parentMax.z;
            
            float3 childCenter;
            childCenter.x = ((i & 1) == 0) ? childMin.x : childMax.x;
            childCenter.y = ((i & 2) == 0) ? childMin.y : childMax.y;
            childCenter.z = ((i & 4) == 0) ? childMin.z : childMax.z;
        
            var child = OctreeNode.CreateEmpty();
            child.ParentIndex = parentIndex;
            child.ChildIndex = i;
            child.Depth = parent.Depth + 1;
            child.Center = childCenter;
            child.Size = childSize;
        
            Nodes[childIdx] = child;
        }
        
        Nodes[parentIndex] = parent;
        FreeCount[0] = freeCount;
        return true;
    }
}

public class OctreeManager : MonoBehaviour
{
    [Header("옥트리 설정")]
    public float rootSize = 800f;
    
    [Header("깊이 설정")]
    public int baseSubdivisionDepth = 3;
    public int activeTrackingDepth = 4;
    public int neighborPreloadDepth = 5;
    public int maxDepth = 8;
    
    [Header("LOD 설정")]
    public float[] lodDistances = { 5f, 10f, 20f, 40f, 80f };
    
    [Header("최적화 설정")]
    public int lodUpdateInterval = 3;
    public float positionChangeThreshold = 2f;
    public int maxSubdivisionsPerFrame = 50;
    
    [Header("타겟 (베이킹 시 float3로 대체)")]
    public Transform target;
    
    private OctreeNodePool _pool;
    private NativeArray<float> _lodDistancesSq;
    private NativeArray<int> _traversalStack;
    private NativeArray<int> _leafIndices;
    private NativeArray<int> _leafCount;
    private NativeArray<int> _subdivisionRequests;
    private NativeArray<int> _mergeWorkStack;
    private NativeArray<int> _freeCountWrapper;
    
    private int _rootIndex;
    private int _activeCellIndex;
    private float3 _activeCellMin;
    private float3 _activeCellMax;
    private float3 _lastUpdatePosition;
    private int _frameCounter;
    private int _cachedLeafCount;
    private bool _leafCacheDirty;
    
    public static OctreeManager Instance { get; private set; }
    
    public int UsedNodeCount => _pool.UsedCount;
    public int MaxDepthInActiveCell => _cachedLeafCount > 0 ? maxDepth : 0;
    public float ActiveCellSize => _activeCellMax.x - _activeCellMin.x;
    public Vector3 ActiveCellMin => new Vector3(_activeCellMin.x, _activeCellMin.y, _activeCellMin.z);
    public Vector3 ActiveCellMax => new Vector3(_activeCellMax.x, _activeCellMax.y, _activeCellMax.z);
    
    public OctreeNodePool GetPool() => _pool;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        InitializeNativeCollections();
        
        if (target == null)
        {
            Debug.LogError("OctreeManager: Target을 설정해주세요!");
            return;
        }
        
        ValidateDepthSettings();
        BuildOctree(target.position);
    }
    
    void ValidateDepthSettings()
    {
        if (activeTrackingDepth < baseSubdivisionDepth)
            activeTrackingDepth = baseSubdivisionDepth;
        if (neighborPreloadDepth < activeTrackingDepth)
            neighborPreloadDepth = activeTrackingDepth + 1;
        if (maxDepth < neighborPreloadDepth)
            maxDepth = neighborPreloadDepth + 2;
    }
    
    void InitializeNativeCollections()
    {
        _pool = new OctreeNodePool(100000, Allocator.Persistent);
        _lodDistancesSq = new NativeArray<float>(lodDistances.Length, Allocator.Persistent);
        _traversalStack = new NativeArray<int>(1024, Allocator.Persistent);
        _leafIndices = new NativeArray<int>(50000, Allocator.Persistent);
        _leafCount = new NativeArray<int>(1, Allocator.Persistent);
        _subdivisionRequests = new NativeArray<int>(50000, Allocator.Persistent);
        _mergeWorkStack = new NativeArray<int>(10000, Allocator.Persistent);
        _freeCountWrapper = new NativeArray<int>(1, Allocator.Persistent);
        
        for (int i = 0; i < lodDistances.Length; i++)
            _lodDistancesSq[i] = lodDistances[i] * lodDistances[i];
    }
    
    void BuildOctree(Vector3 position)
    {
        _pool.TryRent(out _rootIndex);
        
        var root = OctreeNode.CreateEmpty();
        root.ParentIndex = -1;
        root.ChildIndex = 0;
        root.Depth = 0;
        root.Center = new float3(position.x, position.y, position.z);
        root.Size = rootSize;
        _pool.Set(_rootIndex, root);
        
        SubdivideUniformly(_rootIndex, baseSubdivisionDepth);
        
        float3 targetPos = new float3(position.x, position.y, position.z);
        SubdivideTowardsTarget(_rootIndex, targetPos, activeTrackingDepth);
        FindAndSetActiveCell(targetPos);
        FindAndPreloadNeighbors(targetPos);
        
        _leafCacheDirty = true;
        _lastUpdatePosition = targetPos;
        
        Debug.Log($"[빌드 완료] 노드 수: {_pool.UsedCount}");
    }
    
    void SubdivideUniformly(int startIndex, int targetDepth)
    {
        int stackCount = 0;
        _traversalStack[stackCount++] = startIndex;
        
        while (stackCount > 0)
        {
            int currentIdx = _traversalStack[--stackCount];
            var node = _pool.Get(currentIdx);
            
            if (node.Depth >= targetDepth) continue;
            
            if (node.IsLeaf)
            {
                if (!_pool.Subdivide(currentIdx)) continue;
                node = _pool.Get(currentIdx);
            }
            
            for (int i = 0; i < 8; i++)
            {
                int childIdx = node.GetChild(i);
                if (childIdx != -1)
                    _traversalStack[stackCount++] = childIdx;
            }
        }
    }
    
    void SubdivideTowardsTarget(int startIndex, float3 targetPos, int targetDepth)
    {
        int currentIdx = startIndex;
        
        while (true)
        {
            var node = _pool.Get(currentIdx);
            if (node.Depth >= targetDepth) break;
            
            if (node.IsLeaf)
            {
                if (!_pool.Subdivide(currentIdx)) break;
                node = _pool.Get(currentIdx);
            }
            
            int octant = GetOctantForPosition(currentIdx, targetPos);
            int childIdx = node.GetChild(octant);
            
            if (childIdx == -1) break;
            currentIdx = childIdx;
        }
    }
    
    int GetOctantForPosition(int nodeIndex, float3 position)
    {
        var node = _pool.Get(nodeIndex);
        node.GetAABB(out float3 nodeMin, out float3 nodeMax);
        float3 nodeMid = (nodeMin + nodeMax) * 0.5f;
        
        int octant = 0;
        if (position.x >= nodeMid.x) octant |= 1;
        if (position.y >= nodeMid.y) octant |= 2;
        if (position.z >= nodeMid.z) octant |= 4;
        
        return octant;
    }
    
    void FindAndSetActiveCell(float3 targetPos)
    {
        _activeCellIndex = FindCellAtDepth(_rootIndex, targetPos, activeTrackingDepth);
        
        if (_activeCellIndex != -1)
        {
            var cell = _pool.Get(_activeCellIndex);
            cell.GetAABB(out _activeCellMin, out _activeCellMax);
        }
    }
    
    int FindCellAtDepth(int startIndex, float3 position, int targetDepth)
    {
        int currentIdx = startIndex;
        
        while (true)
        {
            var node = _pool.Get(currentIdx);
            
            if (node.Depth >= targetDepth || node.IsLeaf)
                return currentIdx;
            
            int octant = GetOctantForPosition(currentIdx, position);
            int childIdx = node.GetChild(octant);
            
            if (childIdx == -1)
                return currentIdx;
            
            currentIdx = childIdx;
        }
    }
    
    void FindAndPreloadNeighbors(float3 targetPos)
    {
        if (_activeCellIndex == -1) return;
        
        var activeCell = _pool.Get(_activeCellIndex);
        activeCell.GetAABB(out float3 activeMin, out float3 activeMax);
        float3 activeCenter = (activeMin + activeMax) * 0.5f;
        float cellSize = activeMax.x - activeMin.x;
        
        var root = _pool.Get(_rootIndex);
        root.GetAABB(out float3 rootMin, out float3 rootMax);
        
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dy == 0 && dz == 0) continue;
                    
                    float3 neighborCenter = activeCenter + new float3(dx, dy, dz) * cellSize;
                    
                    if (!IsInsideBounds(neighborCenter, rootMin, rootMax)) continue;
                    
                    int neighborIdx = FindCellAtDepth(_rootIndex, neighborCenter, activeTrackingDepth);
                    
                    if (neighborIdx != -1 && neighborIdx != _activeCellIndex)
                    {
                        SubdivideUniformly(neighborIdx, neighborPreloadDepth);
                    }
                }
            }
        }
    }
    
    bool IsInsideBounds(float3 pos, float3 min, float3 max)
    {
        return pos.x >= min.x && pos.x <= max.x &&
               pos.y >= min.y && pos.y <= max.y &&
               pos.z >= min.z && pos.z <= max.z;
    }
    
    public bool IsInsideActiveCell(Vector3 pos)
    {
        return pos.x >= _activeCellMin.x && pos.x <= _activeCellMax.x &&
               pos.y >= _activeCellMin.y && pos.y <= _activeCellMax.y &&
               pos.z >= _activeCellMin.z && pos.z <= _activeCellMax.z;
    }
    
    public bool IsInsideRoot(Vector3 pos)
    {
        if (_rootIndex == -1) return false;
        var root = _pool.Get(_rootIndex);
        root.GetAABB(out float3 rootMin, out float3 rootMax);
        return IsInsideBounds(new float3(pos.x, pos.y, pos.z), rootMin, rootMax);
    }

    void Update()
    {
        if (target == null) return;
        
        float3 targetPos = new float3(target.position.x, target.position.y, target.position.z);
        
        if (!IsInsideActiveCell(target.position))
        {
            UpdateActiveRegion(targetPos);
            _leafCacheDirty = true;
        }
        
        _frameCounter++;
        float distSq = math.distancesq(targetPos, _lastUpdatePosition);
        
        if (_frameCounter >= lodUpdateInterval && distSq > positionChangeThreshold * positionChangeThreshold)
        {
            UpdateLODWithJobs(targetPos);
            _lastUpdatePosition = targetPos;
            _frameCounter = 0;
        }
    }
    
    void UpdateActiveRegion(float3 newTargetPos)
    {
        if (_activeCellIndex != -1 && _pool.IsUsed(_activeCellIndex))
        {
            MergeCellToDepth(_activeCellIndex, neighborPreloadDepth);
        }
        
        var root = _pool.Get(_rootIndex);
        root.GetAABB(out float3 rootMin, out float3 rootMax);
        
        if (!IsInsideBounds(newTargetPos, rootMin, rootMax))
        {
            Debug.Log("루트 범위 벗어남 → 전체 재구성");
            ClearAllNodes();
            BuildOctree(new Vector3(newTargetPos.x, newTargetPos.y, newTargetPos.z));
            return;
        }
        
        SubdivideTowardsTarget(_rootIndex, newTargetPos, activeTrackingDepth);
        FindAndSetActiveCell(newTargetPos);
        FindAndPreloadNeighbors(newTargetPos);
        
        Debug.Log($"활성 영역 이동: Cell Index = {_activeCellIndex}");
    }
    
    void MergeCellToDepth(int nodeIndex, int keepDepth)
    {
        int stackCount = 0;
        _traversalStack[stackCount++] = nodeIndex;
        
        while (stackCount > 0)
        {
            int currentIdx = _traversalStack[--stackCount];
            if (!_pool.IsUsed(currentIdx)) continue;
            
            var node = _pool.Get(currentIdx);
            
            if (node.IsLeaf) continue;
            
            if (node.Depth >= keepDepth)
            {
                _pool.Merge(currentIdx, ref _mergeWorkStack);
            }
            else
            {
                for (int i = 0; i < 8; i++)
                {
                    int childIdx = node.GetChild(i);
                    if (childIdx != -1)
                        _traversalStack[stackCount++] = childIdx;
                }
            }
        }
    }
    
    void ClearAllNodes()
    {
        for (int i = 0; i < _pool.Capacity; i++)
        {
            if (_pool.IsUsed(i))
                _pool.Return(i);
        }
        _activeCellIndex = -1;
    }
    
    void UpdateLODWithJobs(float3 targetPos)
    {
        if (_activeCellIndex == -1) return;
        
        if (_leafCacheDirty)
        {
            var collectJob = new CollectLeafNodesJob
            {
                Nodes = _pool.Nodes,
                IsUsedFlags = _pool.IsUsedFlags,
                TraversalStack = _traversalStack,
                LeafIndices = _leafIndices,
                StartNodeIndex = _activeCellIndex,
                LeafCount = _leafCount
            };
            collectJob.Schedule().Complete();
            _cachedLeafCount = _leafCount[0];
            _leafCacheDirty = false;
        }
        
        if (_cachedLeafCount == 0) return;
        
        var lodJob = new CalculateLODJob
        {
            Nodes = _pool.Nodes,
            LeafIndices = _leafIndices,
            LodDistancesSq = _lodDistancesSq,
            TargetPosition = targetPos,
            MaxDepth = maxDepth,
            MinLodDepth = neighborPreloadDepth,
            SubdivisionRequests = _subdivisionRequests
        };
        
        lodJob.Schedule(_cachedLeafCount, 64).Complete();
        
        int subdivideCount = 0;
        for (int i = 0; i < _cachedLeafCount && subdivideCount < maxSubdivisionsPerFrame; i++)
        {
            int nodeIdx = _subdivisionRequests[i];
            if (nodeIdx >= 0 && _pool.IsUsed(nodeIdx))
            {
                var node = _pool.Get(nodeIdx);
                if (node.IsLeaf && _pool.Subdivide(nodeIdx))
                {
                    subdivideCount++;
                    _leafCacheDirty = true;
                }
            }
        }
    }

    void OnDestroy()
    {
        _pool.Dispose();
        if (_lodDistancesSq.IsCreated) _lodDistancesSq.Dispose();
        if (_traversalStack.IsCreated) _traversalStack.Dispose();
        if (_leafIndices.IsCreated) _leafIndices.Dispose();
        if (_leafCount.IsCreated) _leafCount.Dispose();
        if (_subdivisionRequests.IsCreated) _subdivisionRequests.Dispose();
        if (_mergeWorkStack.IsCreated) _mergeWorkStack.Dispose();
        if (_freeCountWrapper.IsCreated) _freeCountWrapper.Dispose();
    }
}