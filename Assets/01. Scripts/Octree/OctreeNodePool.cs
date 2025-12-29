// OctreeNodePool.cs
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Jobs;

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
        get => Children.Length == 0 || Children[0] == -1;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetChild(int i) => Children[i];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetChild(int i, int index) => Children[i] = index;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetAllChildren(int value)
    {
        for (int i = 0; i < 8; i++)
            Children[i] = value;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetAABB(out float3 min, out float3 max)
    {
        float halfSize = Size * 0.5f;
        min = Center - new float3(halfSize);
        max = Center + new float3(halfSize);
    }
    
    // ★ in 키워드로 참조 전달
    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceToPoint(in float3 center, float size, in float3 point)
    {
        float halfSize = size * 0.5f;
        float3 min = center - new float3(halfSize);
        float3 max = center + new float3(halfSize);
        
        if (point.x >= min.x && point.x <= max.x &&
            point.y >= min.y && point.y <= max.y &&
            point.z >= min.z && point.z <= max.z)
        {
            return 0f;
        }
        
        float3 closest;
        closest.x = math.clamp(point.x, min.x, max.x);
        closest.y = math.clamp(point.y, min.y, max.y);
        closest.z = math.clamp(point.z, min.z, max.z);
        
        return math.distance(point, closest);
    }
    
    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsPoint(in float3 center, float size, in float3 point)
    {
        float halfSize = size * 0.5f;
        float3 min = center - new float3(halfSize);
        float3 max = center + new float3(halfSize);
        
        return point.x >= min.x && point.x <= max.x &&
               point.y >= min.y && point.y <= max.y &&
               point.z >= min.z && point.z <= max.z;
    }
    
    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetOctant(in float3 nodeCenter, in float3 position)
    {
        int octant = 0;
        if (position.x >= nodeCenter.x) octant |= 1;
        if (position.y >= nodeCenter.y) octant |= 2;
        if (position.z >= nodeCenter.z) octant |= 4;
        return octant;
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
    public NativeArray<int> FreeStack;
    public NativeArray<int> FreeCountArray;
    
    public readonly int Capacity;
    
    public bool IsCreated => FreeCountArray.IsCreated;
    
    public int UsedCount => Capacity - FreeCountArray[0];
    public int FreeCount => FreeCountArray[0];
    
    public OctreeNodePool(int capacity, Allocator allocator)
    {
        Capacity = capacity;
        Nodes = new NativeArray<OctreeNode>(capacity, allocator);
        IsUsedFlags = new NativeArray<bool>(capacity, allocator);
        FreeStack = new NativeArray<int>(capacity, allocator);
        FreeCountArray = new NativeArray<int>(1, allocator);
        
        FreeCountArray[0] = capacity;
        for (int i = 0; i < capacity; i++)
        {
            FreeStack[i] = capacity - 1 - i;
            IsUsedFlags[i] = false;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRent(out int index)
    {
        int freeCount = FreeCountArray[0];
        if (freeCount == 0)
        {
            index = -1;
            return false;
        }
        
        index = FreeStack[--freeCount];
        FreeCountArray[0] = freeCount;
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
        int freeCount = FreeCountArray[0];
        FreeStack[freeCount] = index;
        FreeCountArray[0] = freeCount + 1;
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
        int freeCount = FreeCountArray[0];
        
        if (!parent.IsLeaf || freeCount < 8) return false;

        float childSize = parent.Size * 0.5f;
        float3 parentCenter = parent.Center;
        float offset = childSize * 0.5f;

        for (int i = 0; i < 8; i++)
        {
            int childIdx = FreeStack[--freeCount];
            IsUsedFlags[childIdx] = true;
            
            parent.SetChild(i, childIdx);
        
            float3 childCenter;
            childCenter.x = parentCenter.x + (((i & 1) == 0) ? -offset : offset);
            childCenter.y = parentCenter.y + (((i & 2) == 0) ? -offset : offset);
            childCenter.z = parentCenter.z + (((i & 4) == 0) ? -offset : offset);
        
            var child = OctreeNode.CreateEmpty();
            child.ParentIndex = parentIndex;
            child.ChildIndex = i;
            child.Depth = parent.Depth + 1;
            child.Center = childCenter;
            child.Size = childSize;
        
            Nodes[childIdx] = child;
        }
        
        FreeCountArray[0] = freeCount;
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
        
        parent.SetAllChildren(-1);
        Nodes[parentIndex] = parent;
        return true;
    }
    
    public void Dispose()
    {
        if (Nodes.IsCreated) Nodes.Dispose();
        if (IsUsedFlags.IsCreated) IsUsedFlags.Dispose();
        if (FreeStack.IsCreated) FreeStack.Dispose();
        if (FreeCountArray.IsCreated) FreeCountArray.Dispose();
    }
}

// ============ Jobs ============

[BurstCompile(OptimizeFor = OptimizeFor.Performance)]
public struct CalculateTargetDepthJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<OctreeNode> Nodes;
    [ReadOnly] public NativeArray<bool> IsUsedFlags;
    [ReadOnly] public float3 TargetPos;
    [ReadOnly] public int MaxLodDepth;
    [ReadOnly] public int MinLodDepth;
    [ReadOnly] public float LodStepDistance;
    [ReadOnly] public float BoundaryThreshold;
    
    [WriteOnly] public NativeArray<int> TargetDepths;
    
    public void Execute(int index)
    {
        if (!IsUsedFlags[index])
        {
            TargetDepths[index] = -1;
            return;
        }
        
        var node = Nodes[index];
        float3 center = node.Center;
        float3 target = TargetPos;
        
        float distance = OctreeNode.DistanceToPoint(in center, node.Size, in target);
        
        int targetDepth;
        if (distance <= 0)
        {
            targetDepth = MaxLodDepth;
        }
        else
        {
            int steps = (int)(distance / LodStepDistance);
            targetDepth = math.clamp(MaxLodDepth - steps, MinLodDepth, MaxLodDepth);
            
            float threshold = node.Size * BoundaryThreshold;
            if (distance < threshold)
            {
                targetDepth = MaxLodDepth;
            }
        }
        
        TargetDepths[index] = targetDepth;
    }
}

[BurstCompile(OptimizeFor = OptimizeFor.Performance)]
public struct CollectLeafNodesJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<OctreeNode> Nodes;
    [ReadOnly] public NativeArray<bool> IsUsedFlags;
    
    public NativeArray<bool> IsLeafFlags;
    
    public void Execute(int index)
    {
        if (!IsUsedFlags[index])
        {
            IsLeafFlags[index] = false;
            return;
        }
        
        IsLeafFlags[index] = Nodes[index].IsLeaf;
    }
}

[BurstCompile(OptimizeFor = OptimizeFor.Performance)]
public struct GenerateNodeKeysJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<OctreeNode> Nodes;
    [ReadOnly] public NativeArray<bool> IsUsedFlags;
    [ReadOnly] public NativeArray<bool> IsLeafFlags;
    
    [WriteOnly] public NativeArray<long> NodeKeys;
    
    public void Execute(int index)
    {
        if (!IsUsedFlags[index] || !IsLeafFlags[index])
        {
            NodeKeys[index] = -1;
            return;
        }
        
        var node = Nodes[index];
        float3 pos = node.Center;
        int posHash = HashPositionInline(pos.x, pos.y, pos.z);
        long key = ((long)node.Depth << 60) | ((long)(index & 0xFFFFF) << 40) | ((long)posHash & 0xFFFFFFFFFF);
        NodeKeys[index] = key;
    }
    
    // ★ 인라인으로 직접 계산 (float3 전달 안 함)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HashPositionInline(float x, float y, float z)
    {
        int ix = (int)(x * 100);
        int iy = (int)(y * 100);
        int iz = (int)(z * 100);
        return ix ^ (iy << 10) ^ (iz << 20);
    }
}