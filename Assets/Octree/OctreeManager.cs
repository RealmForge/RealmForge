using UnityEngine;
using System;
using Unity.Collections;
using Unity.Mathematics;

public struct OctreeNode
{
    public int val;
    public int3 pos;
}

public class OctreeManager : MonoBehaviour
{
    private Vector3 pivot;
    public float gap = 10;
    public Vector3 testPos;
    public Color pointColor = Color.green;
    private Material _glMaterial;

    private void OnRenderObject()
    {
        _glMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
        NativeArray<OctreeNode> t = CreateArray(5, Allocator.Persistent);
        DrawDots(t);
    }

    public NativeArray<OctreeNode> CreateArray(int level, Allocator allocator)
    {
        if (level < 0 || level > 8)
            throw new ArgumentOutOfRangeException(nameof(level), "Level must be 0-8");

        int resolution = 1 << level;
        
        return new NativeArray<OctreeNode>(resolution*resolution*resolution, Allocator.Persistent);
    }
    
    public Vector3 IndexToCoord(int index, int res)
    {
        int resolution = (int)math.pow(res, 1f/3f);
        int resSq = resolution * resolution;
        int x = index % resolution;
        int y = (index / resolution) % resolution;
        int z = index / resSq;
        
        return pivot + new Vector3(x*gap, y*gap, z*gap);
    }

    public void SetPivot(Vector3 pivot)
    {
        this.pivot = pivot;
    }

    public void DrawDots(NativeArray<OctreeNode> octreeNodes)
    {
        
        _glMaterial.SetPass(0);
        
        GL.PushMatrix();
        GL.Begin(GL.QUADS);
        GL.Color(pointColor);
        
        SetPivot(testPos);
        for (int i = 0; i < octreeNodes.Length; i++)
        {
            Vector3 WorldPos = IndexToCoord(i,octreeNodes.Length);
            GL.Vertex3(WorldPos.x-1, WorldPos.y-1, WorldPos.z);
            GL.Vertex3(WorldPos.x+1, WorldPos.y-1, WorldPos.z);
            GL.Vertex3(WorldPos.x+1, WorldPos.y+1, WorldPos.z);
            GL.Vertex3(WorldPos.x-1, WorldPos.y+1, WorldPos.z);
        }
        
        GL.End();
        GL.PopMatrix();
    }
}
