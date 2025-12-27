// GLRenderer.cs
using Unity.Mathematics;
using UnityEngine;

public class GLRenderer : MonoBehaviour
{
    [Header("시각화")]
    public bool showWireframe = true;
    public bool showRootBounds = true;
    
    private Material _glMaterial;
    private OctreeManager _manager;
    
    private readonly Color[] _depthColors = new Color[]
    {
        new Color(0.5f, 0.5f, 0.5f),     // 0: 회색
        new Color(1.0f, 0.2f, 0.2f),     // 1: 빨강
        new Color(1.0f, 0.6f, 0.2f),     // 2: 주황
        new Color(1.0f, 1.0f, 0.2f),     // 3: 노랑
        new Color(0.2f, 1.0f, 0.2f),     // 4: 초록
        new Color(0.2f, 1.0f, 1.0f),     // 5: 청록
        new Color(0.2f, 0.4f, 1.0f),     // 6: 파랑
        new Color(0.6f, 0.2f, 1.0f),     // 7: 보라
        new Color(1.0f, 0.2f, 1.0f),     // 8: 마젠타
    };

    void Start()
    {
        _glMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
        _glMaterial.hideFlags = HideFlags.HideAndDontSave;
        _glMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _glMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _glMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        _glMaterial.SetInt("_ZWrite", 0);
    }

    void OnRenderObject()
    {
        if (_manager == null)
        {
            _manager = OctreeManager.Instance;
            if (_manager == null) return;
        }
        
        var pool = _manager.GetPool();
        if (!pool.Nodes.IsCreated) return;
        
        _glMaterial.SetPass(0);
        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);
        
        // 리프 노드 그리기
        if (showWireframe)
        {
            GL.Begin(GL.LINES);
            
            for (int i = 0; i < pool.Capacity; i++)
            {
                if (!pool.IsUsedFlags[i]) continue;
                
                var node = pool.Nodes[i];
                if (!node.IsLeaf) continue;
                
                GL.Color(GetDepthColor(node.Depth));
                
                node.GetAABB(out float3 min, out float3 max);
                DrawCube(min, max);
            }
            
            GL.End();
        }
        
        // 루트 경계
        if (showRootBounds && _manager.RootIndex >= 0)
        {
            var root = pool.Get(_manager.RootIndex);
            root.GetAABB(out float3 rootMin, out float3 rootMax);
            
            GL.Begin(GL.LINES);
            GL.Color(Color.white);
            DrawCube(rootMin, rootMax);
            GL.End();
        }
        
        GL.PopMatrix();
    }

    Color GetDepthColor(int depth)
    {
        if (depth < 0) depth = 0;
        if (depth >= _depthColors.Length) depth = _depthColors.Length - 1;
        return _depthColors[depth];
    }

    void DrawCube(float3 min, float3 max)
    {
        // 하단
        GL.Vertex3(min.x, min.y, min.z); GL.Vertex3(max.x, min.y, min.z);
        GL.Vertex3(max.x, min.y, min.z); GL.Vertex3(max.x, min.y, max.z);
        GL.Vertex3(max.x, min.y, max.z); GL.Vertex3(min.x, min.y, max.z);
        GL.Vertex3(min.x, min.y, max.z); GL.Vertex3(min.x, min.y, min.z);
        
        // 상단
        GL.Vertex3(min.x, max.y, min.z); GL.Vertex3(max.x, max.y, min.z);
        GL.Vertex3(max.x, max.y, min.z); GL.Vertex3(max.x, max.y, max.z);
        GL.Vertex3(max.x, max.y, max.z); GL.Vertex3(min.x, max.y, max.z);
        GL.Vertex3(min.x, max.y, max.z); GL.Vertex3(min.x, max.y, min.z);
        
        // 수직
        GL.Vertex3(min.x, min.y, min.z); GL.Vertex3(min.x, max.y, min.z);
        GL.Vertex3(max.x, min.y, min.z); GL.Vertex3(max.x, max.y, min.z);
        GL.Vertex3(max.x, min.y, max.z); GL.Vertex3(max.x, max.y, max.z);
        GL.Vertex3(min.x, min.y, max.z); GL.Vertex3(min.x, max.y, max.z);
    }

    void OnDestroy()
    {
        if (_glMaterial != null)
            DestroyImmediate(_glMaterial);
    }
}