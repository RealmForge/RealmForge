using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class OctreeTest : MonoBehaviour
{
    public Vector3 pivot = Vector3.zero;
    public float rootSize = 100f;
    
    [Header("시각화 설정")]
    [Range(0.05f, 0.3f)]
    public float cornerMarkerRatio = 0.15f;  // 꼭짓점 마커 크기 비율
    public bool showCornerMarkers = true;     // 꼭짓점 마커 표시
    public bool showWireframe = true;         // 와이어프레임 표시
    public bool showFilledCubes = false;      // 채워진 큐브 표시
    
    private OctreeNodePool _pool;
    private int _rootIndex;
    private Material _glMaterial;

    void Start()
    {
        _glMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
        _pool = new OctreeNodePool(200, Allocator.Persistent);
        
        // 루트 생성
        _pool.TryRent(out _rootIndex);
        var root = OctreeNode.CreateEmpty();
        root.ParentIndex = -1;
        root.ChildIndex = 0;
        root.Depth = 1;
        root.Center = new float3(pivot.x, pivot.y, pivot.z);
        root.Size = rootSize;
        _pool.Set(_rootIndex, root);
        
        // 루트 분할 → Depth 2
        _pool.Subdivide(_rootIndex);
        
        // 자식 5번 분할 → Depth 3
        root = _pool.Get(_rootIndex);
        int child5 = root.GetChild(5);
        _pool.Subdivide(child5);
        
        // 또 분할 → Depth 4
        var level3 = _pool.Get(child5);
        int child5_5 = level3.GetChild(5);
        _pool.Subdivide(child5_5);
        
        Debug.Log($"총 노드: {_pool.UsedCount}개");
        
        // 디버그: 각 노드 정보 출력
        DebugPrintNodes();
    }

    void DebugPrintNodes()
    {
        for (int i = 0; i < _pool.Capacity; i++)
        {
            if (!_pool.IsUsed(i)) continue;
            var node = _pool.Get(i);
            if (!node.IsLeaf) continue;
            
            // 노드의 실제 AABB 계산
            GetNodeAABB(node, out Vector3 min, out Vector3 max);
            Debug.Log($"[Node {i}] Depth={node.Depth}, ChildIndex={node.ChildIndex}, " +
                      $"Center={node.Center}, Size={node.Size}, " +
                      $"AABB=({min} ~ {max})");
        }
    }

    void OnDestroy()
    {
        _pool.Dispose();
    }

    void OnRenderObject()
    {
        if (!_pool.IsUsed(0)) return;
        _glMaterial.SetPass(0);
        GL.PushMatrix();
        
        for (int i = 0; i < _pool.Capacity; i++)
        {
            if (!_pool.IsUsed(i)) continue;
            var node = _pool.Get(i);
            
            // 루트는 그리지 않음
            if (node.ParentIndex == -1) continue;
            
            // 리프만 그림
            if (!node.IsLeaf) continue;

            // 계층별 색상
            Color nodeColor = node.Depth switch {
                2 => new Color(1f, 0.3f, 0.3f),   // 빨강
                3 => new Color(0.3f, 0.5f, 1f),   // 파랑
                4 => new Color(0.3f, 1f, 0.3f),   // 초록
                _ => Color.yellow
            };

            // 노드의 실제 AABB 계산
            GetNodeAABB(node, out Vector3 min, out Vector3 max);
            
            // 1. 채워진 큐브 (선택적)
            if (showFilledCubes)
            {
                GL.Begin(GL.QUADS);
                GL.Color(new Color(nodeColor.r, nodeColor.g, nodeColor.b, 0.3f));
                DrawFilledCube(min, max);
                GL.End();
            }
            
            // 2. 와이어프레임
            if (showWireframe)
            {
                GL.Begin(GL.LINES);
                GL.Color(nodeColor);
                DrawWireframeCube(min, max);
                GL.End();
            }
            
            // 3. 꼭짓점(Center) 마커 - 부모 안쪽으로 향하는 작은 큐브
            if (showCornerMarkers)
            {
                GL.Begin(GL.QUADS);
                GL.Color(Color.white);
                
                Vector3 center = new Vector3(node.Center.x, node.Center.y, node.Center.z);
                float markerSize = node.Size * cornerMarkerRatio;
                
                // ChildIndex에 따라 안쪽 방향으로 마커 그리기
                int cIdx = node.ChildIndex;
                float dx = ((cIdx & 1) == 0) ? markerSize : -markerSize;
                float dy = ((cIdx & 2) == 0) ? markerSize : -markerSize;
                float dz = ((cIdx & 4) == 0) ? markerSize : -markerSize;
                
                Vector3 markerEnd = center + new Vector3(dx, dy, dz);
                Vector3 markerMin = Vector3.Min(center, markerEnd);
                Vector3 markerMax = Vector3.Max(center, markerEnd);
                
                DrawFilledCube(markerMin, markerMax);
                GL.End();
            }
        }
        
        // 루트 경계 표시 (회색 와이어프레임)
        if (showWireframe)
        {
            var rootNode = _pool.Get(_rootIndex);
            GetNodeAABB(rootNode, out Vector3 rootMin, out Vector3 rootMax);
            
            GL.Begin(GL.LINES);
            GL.Color(new Color(0.5f, 0.5f, 0.5f, 0.5f));
            DrawWireframeCube(rootMin, rootMax);
            GL.End();
        }
        
        GL.PopMatrix();
    }

    /// <summary>
    /// 노드의 Center와 ChildIndex를 이용해 실제 AABB 계산
    /// </summary>
    void GetNodeAABB(OctreeNode node, out Vector3 min, out Vector3 max)
    {
        Vector3 center = new Vector3(node.Center.x, node.Center.y, node.Center.z);
        float size = node.Size;
        int cIdx = node.ChildIndex;
        
        // ChildIndex 비트에 따라 Center에서 안쪽 방향으로 확장
        // 비트가 0이면 Center가 min쪽, 비트가 1이면 Center가 max쪽
        float dx = ((cIdx & 1) == 0) ? size : -size;
        float dy = ((cIdx & 2) == 0) ? size : -size;
        float dz = ((cIdx & 4) == 0) ? size : -size;
        
        Vector3 end = center + new Vector3(dx, dy, dz);
        
        min = Vector3.Min(center, end);
        max = Vector3.Max(center, end);
    }

    void DrawFilledCube(Vector3 min, Vector3 max)
    {
        // 앞면 (+Z)
        GL.Vertex3(min.x, min.y, max.z);
        GL.Vertex3(max.x, min.y, max.z);
        GL.Vertex3(max.x, max.y, max.z);
        GL.Vertex3(min.x, max.y, max.z);

        // 뒷면 (-Z)
        GL.Vertex3(min.x, min.y, min.z);
        GL.Vertex3(min.x, max.y, min.z);
        GL.Vertex3(max.x, max.y, min.z);
        GL.Vertex3(max.x, min.y, min.z);

        // 윗면 (+Y)
        GL.Vertex3(min.x, max.y, min.z);
        GL.Vertex3(min.x, max.y, max.z);
        GL.Vertex3(max.x, max.y, max.z);
        GL.Vertex3(max.x, max.y, min.z);

        // 아랫면 (-Y)
        GL.Vertex3(min.x, min.y, min.z);
        GL.Vertex3(max.x, min.y, min.z);
        GL.Vertex3(max.x, min.y, max.z);
        GL.Vertex3(min.x, min.y, max.z);

        // 오른쪽면 (+X)
        GL.Vertex3(max.x, min.y, min.z);
        GL.Vertex3(max.x, max.y, min.z);
        GL.Vertex3(max.x, max.y, max.z);
        GL.Vertex3(max.x, min.y, max.z);

        // 왼쪽면 (-X)
        GL.Vertex3(min.x, min.y, min.z);
        GL.Vertex3(min.x, min.y, max.z);
        GL.Vertex3(min.x, max.y, max.z);
        GL.Vertex3(min.x, max.y, min.z);
    }

    void DrawWireframeCube(Vector3 min, Vector3 max)
    {
        // 아래 면 4개 엣지
        GL.Vertex3(min.x, min.y, min.z); GL.Vertex3(max.x, min.y, min.z);
        GL.Vertex3(max.x, min.y, min.z); GL.Vertex3(max.x, min.y, max.z);
        GL.Vertex3(max.x, min.y, max.z); GL.Vertex3(min.x, min.y, max.z);
        GL.Vertex3(min.x, min.y, max.z); GL.Vertex3(min.x, min.y, min.z);
        
        // 위 면 4개 엣지
        GL.Vertex3(min.x, max.y, min.z); GL.Vertex3(max.x, max.y, min.z);
        GL.Vertex3(max.x, max.y, min.z); GL.Vertex3(max.x, max.y, max.z);
        GL.Vertex3(max.x, max.y, max.z); GL.Vertex3(min.x, max.y, max.z);
        GL.Vertex3(min.x, max.y, max.z); GL.Vertex3(min.x, max.y, min.z);
        
        // 수직 4개 엣지
        GL.Vertex3(min.x, min.y, min.z); GL.Vertex3(min.x, max.y, min.z);
        GL.Vertex3(max.x, min.y, min.z); GL.Vertex3(max.x, max.y, min.z);
        GL.Vertex3(max.x, min.y, max.z); GL.Vertex3(max.x, max.y, max.z);
        GL.Vertex3(min.x, min.y, max.z); GL.Vertex3(min.x, max.y, max.z);
    }
}