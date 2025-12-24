// OctreeNodePool.cs
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

[BurstCompile]
public struct OctreeNode
{
    public int ParentIndex;
    public int ChildIndex;
    public int Depth;
    public float3 Center;
    public float Size;
    
    public FixedList64Bytes<int> Children;
    
    public bool IsLeaf
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Children[0] == -1;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetChild(int i) => Children[i];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetChild(int i, int index) => Children[i] = index;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        var node = new OctreeNode
        {
            ParentIndex = -1,
            ChildIndex = 0,
            Depth = 0,
            Center = float3.zero,
            Size = 0,
            Children = new FixedList64Bytes<int>()
        };
        
        for (int i = 0; i < 8; i++)
            node.Children.Add(-1);
            
        return node;
    }
}

[BurstCompile]
public struct OctreeNodePool : IDisposable
{
    public NativeArray<OctreeNode> Nodes;
    public NativeArray<bool> IsUsedFlags;
    private NativeArray<int> _freeStack;
    private int _freeCount;
    
    public readonly int Capacity;
    
    public int UsedCount => Capacity - _freeCount;
    public int FreeCount => _freeCount;
    
    
    public OctreeNodePool(int capacity, Allocator allocator)
    {
        Capacity = capacity;
        Nodes = new NativeArray<OctreeNode>(capacity, allocator);
        IsUsedFlags = new NativeArray<bool>(capacity, allocator);
        _freeStack = new NativeArray<int>(capacity, allocator);
        
        _freeCount = capacity;
        for (int i = 0; i < capacity; i++)
        {
            _freeStack[i] = capacity - 1 - i;
            IsUsedFlags[i] = false;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRent(out int index)
    {
        if (_freeCount == 0)
        {
            index = -1;
            return false;
        }
        
        index = _freeStack[--_freeCount];
        IsUsedFlags[index] = true;
        Nodes[index] = OctreeNode.CreateEmpty();
        return true;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(int index)
    {
        if (index < 0 || index >= Capacity) return;
        if (!IsUsedFlags[index]) return;
        
        IsUsedFlags[index] = false;
        _freeStack[_freeCount++] = index;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OctreeNode Get(int index) => Nodes[index];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int index, OctreeNode node) => Nodes[index] = node;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsUsed(int index) => IsUsedFlags[index];

    [BurstCompile]
    public bool Subdivide(int parentIndex)
    {
        var parent = Nodes[parentIndex];
        if (!parent.IsLeaf || _freeCount < 8) return false;

        float childSize = parent.Size * 0.5f;
        
        parent.GetAABB(out float3 parentMin, out float3 parentMax);
        float3 parentMid = (parentMin + parentMax) * 0.5f;

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
        
            Nodes[childIdx] = child;
        }
        Nodes[parentIndex] = parent;
        return true;
    }
    
    [BurstCompile]
    public bool Merge(int parentIndex, ref NativeArray<int> workStack)
    {
        var parent = Nodes[parentIndex];
        if (parent.IsLeaf) return false;
        
        int stackCount = 0;
        
        for (int i = 0; i < 8; i++)
        {
            int childIdx = parent.GetChild(i);
            if (childIdx != -1)
                workStack[stackCount++] = childIdx;
        }
        
        while (stackCount > 0)
        {
            int currentIdx = workStack[--stackCount];
            var current = Nodes[currentIdx];
            
            if (!current.IsLeaf)
            {
                for (int i = 0; i < 8; i++)
                {
                    int childIdx = current.GetChild(i);
                    if (childIdx != -1)
                        workStack[stackCount++] = childIdx;
                }
            }
            
            Return(currentIdx);
        }
        
        for (int i = 0; i < 8; i++)
            parent.SetChild(i, -1);
        
        Nodes[parentIndex] = parent;
        return true;
    }
    
    public void Dispose()
    {
        if (Nodes.IsCreated) Nodes.Dispose();
        if (IsUsedFlags.IsCreated) IsUsedFlags.Dispose();
        if (_freeStack.IsCreated) _freeStack.Dispose();
    }
}