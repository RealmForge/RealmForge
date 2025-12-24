using Unity.Mathematics;
using UnityEngine;

public class GLRenderer : MonoBehaviour
{
    [Header("시각화 설정")]
    public bool showWireframe = true;
    public bool showActiveCellBounds = true;
    public bool showRootBounds = true;
    public bool showCornerMarkers = false;
    
    [Range(0.05f, 0.3f)]
    public float cornerMarkerRatio = 0.15f;
    
    private Material _glMaterial;
    private OctreeManager _manager;

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
        
        _glMaterial.SetPass(0);
        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);
        
        // 리프 노드 와이어프레임
        if (showWireframe)
        {
            DrawLeafNodes();
        }
        
        // 활성 셀 경계 (노란색)
        if (showActiveCellBounds)
        {
            GL.Begin(GL.LINES);
            GL.Color(Color.yellow);
            DrawWireframeCube(_manager.ActiveCellMin, _manager.ActiveCellMax);
            GL.End();
        }
        
        // 루트 경계 (회색)
        if (showRootBounds)
        {
            var rootMin = _manager.ActiveCellMin - Vector3.one * _manager.rootSize;
            var rootMax = _manager.ActiveCellMax + Vector3.one * _manager.rootSize;
            
            // 실제 루트 AABB 계산
            if (_manager.IsInsideRoot(Vector3.zero) || _manager.UsedNodeCount > 0)
            {
                GL.Begin(GL.LINES);
                GL.Color(new Color(0.5f, 0.5f, 0.5f, 0.5f));
                
                Vector3 rootCenter = _manager.ActiveCellMin + (_manager.ActiveCellMax - _manager.ActiveCellMin) * 0.5f;
                // 대략적인 루트 바운드 (정확한 값은 Pool 접근 필요)
                float size = _manager.rootSize;
                DrawWireframeCube(
                    new Vector3(-size, -size, -size) + rootCenter,
                    new Vector3(size, size, size) + rootCenter
                );
                GL.End();
            }
        }
        
        GL.PopMatrix();
    }

    void DrawLeafNodes()
    {
        var pool = GetPool();
        if (!pool.Nodes.IsCreated) return;
        
        for (int i = 0; i < pool.Capacity; i++)
        {
            if (!pool.IsUsedFlags[i]) continue;
            
            var node = pool.Nodes[i];
            if (!node.IsLeaf) continue;
            
            Color nodeColor = GetDepthColor(node.Depth);
            node.GetAABB(out float3 min, out float3 max);
            
            Vector3 vMin = new Vector3(min.x, min.y, min.z);
            Vector3 vMax = new Vector3(max.x, max.y, max.z);
            
            GL.Begin(GL.LINES);
            GL.Color(nodeColor);
            DrawWireframeCube(vMin, vMax);
            GL.End();
            
            if (showCornerMarkers)
            {
                GL.Begin(GL.QUADS);
                GL.Color(Color.white);
                
                Vector3 center = new Vector3(node.Center.x, node.Center.y, node.Center.z);
                float markerSize = node.Size * cornerMarkerRatio;
                
                int cIdx = node.ChildIndex;
                float mdx = ((cIdx & 1) == 0) ? markerSize : -markerSize;
                float mdy = ((cIdx & 2) == 0) ? markerSize : -markerSize;
                float mdz = ((cIdx & 4) == 0) ? markerSize : -markerSize;
                
                Vector3 markerEnd = center + new Vector3(mdx, mdy, mdz);
                Vector3 markerMin = Vector3.Min(center, markerEnd);
                Vector3 markerMax = Vector3.Max(center, markerEnd);
                
                DrawFilledCube(markerMin, markerMax);
                GL.End();
            }
        }
    }

    Color GetDepthColor(int depth)
    {
        return depth switch
        {
            0 => new Color(0.2f, 0.2f, 0.2f),
            1 => new Color(0.3f, 0.3f, 0.3f),
            2 => new Color(0.4f, 0.4f, 0.4f),
            3 => new Color(0.6f, 0.3f, 0.3f),
            4 => new Color(0.3f, 0.4f, 0.7f),
            5 => new Color(0.3f, 0.7f, 0.3f),
            6 => new Color(0.7f, 0.7f, 0.2f),
            7 => new Color(0.7f, 0.4f, 0.7f),
            8 => new Color(0.4f, 0.7f, 0.7f),
            9 => new Color(1f, 0.5f, 0.2f),
            10 => new Color(1f, 0.2f, 0.5f),
            _ => Color.white
        };
    }

    // OctreeManager에서 Pool 접근용 메서드 필요
    OctreeNodePool GetPool()
    {
        return _manager.GetPool();
    }

    void DrawWireframeCube(Vector3 min, Vector3 max)
    {
        // 하단 면
        GL.Vertex3(min.x, min.y, min.z); GL.Vertex3(max.x, min.y, min.z);
        GL.Vertex3(max.x, min.y, min.z); GL.Vertex3(max.x, min.y, max.z);
        GL.Vertex3(max.x, min.y, max.z); GL.Vertex3(min.x, min.y, max.z);
        GL.Vertex3(min.x, min.y, max.z); GL.Vertex3(min.x, min.y, min.z);
        
        // 상단 면
        GL.Vertex3(min.x, max.y, min.z); GL.Vertex3(max.x, max.y, min.z);
        GL.Vertex3(max.x, max.y, min.z); GL.Vertex3(max.x, max.y, max.z);
        GL.Vertex3(max.x, max.y, max.z); GL.Vertex3(min.x, max.y, max.z);
        GL.Vertex3(min.x, max.y, max.z); GL.Vertex3(min.x, max.y, min.z);
        
        // 수직 엣지
        GL.Vertex3(min.x, min.y, min.z); GL.Vertex3(min.x, max.y, min.z);
        GL.Vertex3(max.x, min.y, min.z); GL.Vertex3(max.x, max.y, min.z);
        GL.Vertex3(max.x, min.y, max.z); GL.Vertex3(max.x, max.y, max.z);
        GL.Vertex3(min.x, min.y, max.z); GL.Vertex3(min.x, max.y, max.z);
    }

    void DrawFilledCube(Vector3 min, Vector3 max)
    {
        // 앞면
        GL.Vertex3(min.x, min.y, max.z);
        GL.Vertex3(max.x, min.y, max.z);
        GL.Vertex3(max.x, max.y, max.z);
        GL.Vertex3(min.x, max.y, max.z);

        // 뒷면
        GL.Vertex3(min.x, min.y, min.z);
        GL.Vertex3(min.x, max.y, min.z);
        GL.Vertex3(max.x, max.y, min.z);
        GL.Vertex3(max.x, min.y, min.z);

        // 상단
        GL.Vertex3(min.x, max.y, min.z);
        GL.Vertex3(min.x, max.y, max.z);
        GL.Vertex3(max.x, max.y, max.z);
        GL.Vertex3(max.x, max.y, min.z);

        // 하단
        GL.Vertex3(min.x, min.y, min.z);
        GL.Vertex3(max.x, min.y, min.z);
        GL.Vertex3(max.x, min.y, max.z);
        GL.Vertex3(min.x, min.y, max.z);

        // 오른쪽
        GL.Vertex3(max.x, min.y, min.z);
        GL.Vertex3(max.x, max.y, min.z);
        GL.Vertex3(max.x, max.y, max.z);
        GL.Vertex3(max.x, min.y, max.z);

        // 왼쪽
        GL.Vertex3(min.x, min.y, min.z);
        GL.Vertex3(min.x, min.y, max.z);
        GL.Vertex3(min.x, max.y, max.z);
        GL.Vertex3(min.x, max.y, min.z);
    }

    void OnDestroy()
    {
        if (_glMaterial != null)
            DestroyImmediate(_glMaterial);
    }
}