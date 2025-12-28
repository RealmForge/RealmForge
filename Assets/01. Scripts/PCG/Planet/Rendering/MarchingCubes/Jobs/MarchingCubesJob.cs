using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct MarchingCubesJob : IJob
{
    [ReadOnly] public NativeArray<float> NoiseData;
    [ReadOnly] public NativeArray<int> EdgeTable;
    [ReadOnly] public NativeArray<int> TriTable;
    public int ChunkSize;
    public int SampleSize;
    public float Threshold;
    public float VoxelSize;
    public float3 ChunkMin;

    public float3 PlanetCenter;
    public float PlanetRadius;
    [ReadOnly] public NativeArray<TerrainLayerBuffer> TerrainLayers;

    public NativeList<float3> Vertices;
    public NativeList<float3> Normals;
    public NativeList<int> Indices;
    public NativeList<float4> Colors;

    private static int3 GetCornerOffset(int index)
    {
        switch (index)
        {
            case 0: return new int3(0, 0, 0);
            case 1: return new int3(1, 0, 0);
            case 2: return new int3(1, 0, 1);
            case 3: return new int3(0, 0, 1);
            case 4: return new int3(0, 1, 0);
            case 5: return new int3(1, 1, 0);
            case 6: return new int3(1, 1, 1);
            case 7: return new int3(0, 1, 1);
            default: return int3.zero;
        }
    }

    private static int2 GetEdgeCorners(int edgeIndex)
    {
        switch (edgeIndex)
        {
            case 0: return new int2(0, 1);
            case 1: return new int2(1, 2);
            case 2: return new int2(2, 3);
            case 3: return new int2(3, 0);
            case 4: return new int2(4, 5);
            case 5: return new int2(5, 6);
            case 6: return new int2(6, 7);
            case 7: return new int2(7, 4);
            case 8: return new int2(0, 4);
            case 9: return new int2(1, 5);
            case 10: return new int2(2, 6);
            case 11: return new int2(3, 7);
            default: return int2.zero;
        }
    }

    public void Execute()
    {
        var cornerDensities = new NativeArray<float>(8, Allocator.Temp);
        var cornerPositions = new NativeArray<float3>(8, Allocator.Temp);
        var edgeVertices = new NativeArray<float3>(12, Allocator.Temp);

        for (int z = 0; z < ChunkSize; z++)
        {
            for (int y = 0; y < ChunkSize; y++)
            {
                for (int x = 0; x < ChunkSize; x++)
                {
                    ProcessCube(x, y, z, ref cornerDensities, ref cornerPositions, ref edgeVertices);
                }
            }
        }

        cornerDensities.Dispose();
        cornerPositions.Dispose();
        edgeVertices.Dispose();
    }

    private void ProcessCube(int x, int y, int z,
        ref NativeArray<float> cornerDensities,
        ref NativeArray<float3> cornerPositions,
        ref NativeArray<float3> edgeVertices)
    {
        for (int i = 0; i < 8; i++)
        {
            int3 corner = new int3(x, y, z) + GetCornerOffset(i);
            cornerDensities[i] = GetDensity(corner.x, corner.y, corner.z);
            cornerPositions[i] = new float3(corner.x, corner.y, corner.z) * VoxelSize;
        }

        int cubeIndex = 0;
        for (int i = 0; i < 8; i++)
        {
            if (cornerDensities[i] < Threshold)
            {
                cubeIndex |= (1 << i);
            }
        }

        if (EdgeTable[cubeIndex] == 0) return;

        int edgeFlags = EdgeTable[cubeIndex];
        for (int i = 0; i < 12; i++)
        {
            if ((edgeFlags & (1 << i)) != 0)
            {
                int2 corners = GetEdgeCorners(i);
                edgeVertices[i] = InterpolateVertex(
                    cornerPositions[corners.x], cornerPositions[corners.y],
                    cornerDensities[corners.x], cornerDensities[corners.y]);
            }
        }

        int tableIndex = cubeIndex * 16;
        for (int i = 0; TriTable[tableIndex + i] != -1; i += 3)
        {
            int e0 = TriTable[tableIndex + i];
            int e1 = TriTable[tableIndex + i + 1];
            int e2 = TriTable[tableIndex + i + 2];

            float3 v0 = edgeVertices[e0];
            float3 v1 = edgeVertices[e1];
            float3 v2 = edgeVertices[e2];

            float3 normal = math.normalize(math.cross(v1 - v0, v2 - v0));

            int baseIndex = Vertices.Length;

            float4 c0 = GetLayerColor(v0);
            float4 c1 = GetLayerColor(v1);
            float4 c2 = GetLayerColor(v2);

            Vertices.Add(v0);
            Vertices.Add(v1);
            Vertices.Add(v2);

            Normals.Add(normal);
            Normals.Add(normal);
            Normals.Add(normal);

            Colors.Add(c0);
            Colors.Add(c1);
            Colors.Add(c2);

            Indices.Add(baseIndex);
            Indices.Add(baseIndex + 1);
            Indices.Add(baseIndex + 2);
        }
    }

    private float4 GetLayerColor(float3 localPos)
    {
        float3 worldPos = ChunkMin + localPos;
        float height = math.distance(worldPos, PlanetCenter) - PlanetRadius;

        float4 color = new float4(1, 1, 1, 1);

        for (int i = 0; i < TerrainLayers.Length; i++)
        {
            if (height <= TerrainLayers[i].MaxHeight)
            {
                color = TerrainLayers[i].Color;
                break;
            }
        }

        return color;
    }

    private float GetDensity(int x, int y, int z)
    {
        x = math.clamp(x, 0, SampleSize - 1);
        y = math.clamp(y, 0, SampleSize - 1);
        z = math.clamp(z, 0, SampleSize - 1);

        int index = x + y * SampleSize + z * SampleSize * SampleSize;
        return NoiseData[index];
    }

    private float3 InterpolateVertex(float3 p0, float3 p1, float d0, float d1)
    {
        if (math.abs(Threshold - d0) < 0.00001f) return p0;
        if (math.abs(Threshold - d1) < 0.00001f) return p1;
        if (math.abs(d0 - d1) < 0.00001f) return p0;

        float t = (Threshold - d0) / (d1 - d0);
        return math.lerp(p0, p1, t);
    }
}
