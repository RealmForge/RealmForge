using System;
using Unity.Collections;
using Unity.Mathematics;

public struct OctreeNode
{
    public int ParentIndex;
    public int ChildIndex;
    public int Depth;
    public float3 Center;
    public float Size;
    
    public int Child0, Child1, Child2, Child3;
    public int Child4, Child5, Child6, Child7;
    
    public bool IsLeaf => Child0 == -1;
    
    public int GetChild(int i)
    {
        return i switch
        {
            0 => Child0, 1 => Child1, 2 => Child2, 3 => Child3,
            4 => Child4, 5 => Child5, 6 => Child6, 7 => Child7,
            _ => -1
        };
    }
    
    public void SetChild(int i, int index)
    {
        switch (i)
        {
            case 0: Child0 = index; break;
            case 1: Child1 = index; break;
            case 2: Child2 = index; break;
            case 3: Child3 = index; break;
            case 4: Child4 = index; break;
            case 5: Child5 = index; break;
            case 6: Child6 = index; break;
            case 7: Child7 = index; break;
        }
    }
    
    public void GetAABB(out float3 min, out float3 max)
    {
        float dx = ((ChildIndex & 1) == 0) ? Size : -Size;
        float dy = ((ChildIndex & 2) == 0) ? Size : -Size;
        float dz = ((ChildIndex & 4) == 0) ? Size : -Size;
        
        float3 end = Center + new float3(dx, dy, dz);
        
        min = math.min(Center, end);
        max = math.max(Center, end);
    }
    
    public static OctreeNode CreateEmpty()
    {
        return new OctreeNode
        {
            ParentIndex = -1,
            ChildIndex = 0,
            Depth = 0,
            Center = float3.zero,
            Size = 0,
            Child0 = -1, Child1 = -1, Child2 = -1, Child3 = -1,
            Child4 = -1, Child5 = -1, Child6 = -1, Child7 = -1
        };
    }
}

public struct OctreeNodePool : IDisposable
{
    private NativeArray<OctreeNode> _nodes;
    private NativeList<int> _freeList;
    private NativeArray<bool> _isUsed;
    
    public readonly int Capacity;
    
    public OctreeNodePool(int capacity, Allocator allocator)
    {
        Capacity = capacity;
        _nodes = new NativeArray<OctreeNode>(capacity, allocator);
        _isUsed = new NativeArray<bool>(capacity, allocator);
        _freeList = new NativeList<int>(capacity, allocator);
        
        for (int i = capacity - 1; i >= 0; i--)
        {
            _freeList.Add(i);
            _isUsed[i] = false;
        }
    }
    
    public bool TryRent(out int index)
    {
        if (_freeList.Length == 0)
        {
            index = -1;
            return false;
        }
        
        int last = _freeList.Length - 1;
        index = _freeList[last];
        _freeList.RemoveAt(last);
        _isUsed[index] = true;
        _nodes[index] = OctreeNode.CreateEmpty();
        return true;
    }
    
    public void Return(int index)
    {
        if (index < 0 || index >= Capacity) return;
        if (!_isUsed[index]) return;
        _isUsed[index] = false;
        _freeList.Add(index);
    }
    
    public OctreeNode Get(int index) => _nodes[index];
    public void Set(int index, OctreeNode node) => _nodes[index] = node;
    public bool IsUsed(int index) => _isUsed[index];
    public int UsedCount => Capacity - _freeList.Length;

    public bool Subdivide(int parentIndex)
    {
        var parent = _nodes[parentIndex];
        if (!parent.IsLeaf || _freeList.Length < 8) return false;

        float childSize = parent.Size / 2f;
        
        parent.GetAABB(out float3 parentMin, out float3 parentMax);
        float3 parentMid = (parentMin + parentMax) / 2f;

        for (int i = 0; i < 8; i++)
        {
            if (!TryRent(out int childIdx)) return false;
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
        
            _nodes[childIdx] = child;
        }
        _nodes[parentIndex] = parent;
        return true;
    }
    
    public bool Merge(int parentIndex)
    {
        var parent = _nodes[parentIndex];
        if (parent.IsLeaf) return false;
        
        for (int i = 0; i < 8; i++)
        {
            int childIdx = parent.GetChild(i);
            if (!_nodes[childIdx].IsLeaf) Merge(childIdx);
            Return(childIdx);
        }
        
        parent.Child0 = -1; parent.Child1 = -1;
        parent.Child2 = -1; parent.Child3 = -1;
        parent.Child4 = -1; parent.Child5 = -1;
        parent.Child6 = -1; parent.Child7 = -1;
        
        _nodes[parentIndex] = parent;
        return true;
    }
    
    public void Dispose()
    {
        if (_nodes.IsCreated) _nodes.Dispose();
        if (_isUsed.IsCreated) _isUsed.Dispose();
        if (_freeList.IsCreated) _freeList.Dispose();
    }
}