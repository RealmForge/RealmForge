using System;
using Unity.Collections;
using Unity.Mathematics;

public struct OctreeNode
{
    public int ParentIndex;
    public int Depth;
    public float3 Center;
    public float Size;
    
    // 자식 8개 인덱스
    public int Child0, Child1, Child2, Child3;
    public int Child4, Child5, Child6, Child7;
    
    public bool IsLeaf => Child0 == -1;
    
    public int GetChild(int i)
    {
        return i switch
        {
            0 => Child0,
            1 => Child1,
            2 => Child2,
            3 => Child3,
            4 => Child4,
            5 => Child5,
            6 => Child6,
            7 => Child7,
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
    
    public static OctreeNode CreateEmpty()
    {
        return new OctreeNode
        {
            ParentIndex = -1,
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
        
        if (!parent.IsLeaf) return false;
        if (_freeList.Length < 8) return false;
        
        float childSize = parent.Size / 2f;
        float offset = childSize / 2f;
        
        for (int i = 0; i < 8; i++)
        {
            if (!TryRent(out int childIdx)) return false;
            
            // 부모에 자식 인덱스 저장
            parent.SetChild(i, childIdx);
            
            float3 childCenter = parent.Center + new float3(
                ((i & 1) == 0) ? -offset : +offset,
                ((i & 2) == 0) ? -offset : +offset,
                ((i & 4) == 0) ? -offset : +offset
            );
            
            var child = OctreeNode.CreateEmpty();
            child.ParentIndex = parentIndex;
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
            
            // 손자 있으면 먼저 병합
            if (!_nodes[childIdx].IsLeaf)
            {
                Merge(childIdx);
            }
            
            Return(childIdx);
        }
        
        // 자식 연결 해제
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