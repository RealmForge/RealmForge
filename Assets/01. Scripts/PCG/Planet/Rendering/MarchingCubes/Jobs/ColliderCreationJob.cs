using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

[BurstCompile]
public struct ColliderCreationJob : IJob
{
    [ReadOnly] public NativeArray<float3> Vertices;
    [ReadOnly] public NativeArray<int3> Triangles;

    [WriteOnly] public NativeArray<BlobAssetReference<Unity.Physics.Collider>> Output;

    public void Execute()
    {
        if (Vertices.Length == 0 || Triangles.Length == 0)
        {
            Output[0] = default;
            return;
        }

        Output[0] = MeshCollider.Create(Vertices, Triangles);
    }
}
