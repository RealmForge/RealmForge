using Unity.Entities;
using Unity.Mathematics;

[System.Serializable]
public struct TerrainLayer
{
    public float MaxHeight;
    public float4 Color;
}

public struct TerrainLayerBuffer : IBufferElementData
{
    public float MaxHeight;
    public float4 Color;

    public static implicit operator TerrainLayerBuffer(TerrainLayer layer)
    {
        return new TerrainLayerBuffer
        {
            MaxHeight = layer.MaxHeight,
            Color = layer.Color
        };
    }
}
