using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class OctreeTest : MonoBehaviour
{
    public Vector3 pivot = Vector3.zero;
    public float rootSize = 100f;
    
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
        root.Depth = 1;
        root.Center = new float3(pivot.x, pivot.y, pivot.z);
        root.Size = rootSize;
        _pool.Set(_rootIndex, root);
        
        // 루트 분할 → Depth 2
        _pool.Subdivide(_rootIndex);
        
        // 자식 5번 분할 → Depth 3
        root = _pool.Get(_rootIndex);
        int child5 = root.GetChild(5);  // ← GetChild 사용
        _pool.Subdivide(child5);
        
        // 또 분할 → Depth 4
        var level3 = _pool.Get(child5);
        int child5_5 = level3.GetChild(5);  // ← GetChild 사용
        _pool.Subdivide(child5_5);
        
        Debug.Log($"총 노드: {_pool.UsedCount}개");
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
        GL.Begin(GL.QUADS);
        
        for (int i = 0; i < _pool.Capacity; i++)
        {
            if (!_pool.IsUsed(i)) continue;
            
            var node = _pool.Get(i);
            
            // 리프만 그리기
            if (!node.IsLeaf) continue;
            
            // 깊이별 색상
            Color color = node.Depth switch
            {
                2 => Color.red,
                3 => Color.blue,
                4 => Color.green,
                _ => Color.white
            };
            GL.Color(color);
            
            float size = node.Size * 0.1f;
            Vector3 p = new Vector3(node.Center.x, node.Center.y, node.Center.z);
            
            // 앞면
            GL.Vertex3(p.x - size, p.y - size, p.z + size);
            GL.Vertex3(p.x + size, p.y - size, p.z + size);
            GL.Vertex3(p.x + size, p.y + size, p.z + size);
            GL.Vertex3(p.x - size, p.y + size, p.z + size);
            
            // 뒷면
            GL.Vertex3(p.x - size, p.y - size, p.z - size);
            GL.Vertex3(p.x - size, p.y + size, p.z - size);
            GL.Vertex3(p.x + size, p.y + size, p.z - size);
            GL.Vertex3(p.x + size, p.y - size, p.z - size);
            
            // 윗면
            GL.Vertex3(p.x - size, p.y + size, p.z - size);
            GL.Vertex3(p.x - size, p.y + size, p.z + size);
            GL.Vertex3(p.x + size, p.y + size, p.z + size);
            GL.Vertex3(p.x + size, p.y + size, p.z - size);
            
            // 아랫면
            GL.Vertex3(p.x - size, p.y - size, p.z - size);
            GL.Vertex3(p.x + size, p.y - size, p.z - size);
            GL.Vertex3(p.x + size, p.y - size, p.z + size);
            GL.Vertex3(p.x - size, p.y - size, p.z + size);
            
            // 오른면
            GL.Vertex3(p.x + size, p.y - size, p.z - size);
            GL.Vertex3(p.x + size, p.y + size, p.z - size);
            GL.Vertex3(p.x + size, p.y + size, p.z + size);
            GL.Vertex3(p.x + size, p.y - size, p.z + size);
            
            // 왼면
            GL.Vertex3(p.x - size, p.y - size, p.z - size);
            GL.Vertex3(p.x - size, p.y - size, p.z + size);
            GL.Vertex3(p.x - size, p.y + size, p.z + size);
            GL.Vertex3(p.x - size, p.y + size, p.z - size);
        }
        
        GL.End();
        GL.PopMatrix();
    }
}