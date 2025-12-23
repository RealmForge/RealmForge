using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class OctreeManager : MonoBehaviour
{
    [Header("옥트리 설정")]
    public float rootSize = 100f;
    
    [Header("타겟 설정")]
    public Transform target;
    
    [Header("시각화 설정")]
    [Range(0.05f, 0.3f)]
    public float cornerMarkerRatio = 0.15f;
    public bool showCornerMarkers = true;
    public bool showWireframe = true;
    public bool showFilledCubes = false;
    public bool showCenterCube = true;
    
    private OctreeNodePool _pool;
    private int[] _rootIndices;
    private Material _glMaterial;
    
    private Vector3 _currentPivot;
    private float3 _centerMin;
    private float3 _centerMax;
    
    public static OctreeManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        _glMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
        _pool = new OctreeNodePool(200 * 8, Allocator.Persistent);
        _rootIndices = new int[8];
        
        if (target == null)
        {
            Debug.LogError("OctreeManager: Target을 설정해주세요!");
            return;
        }
        
        _currentPivot = target.position;
        BuildOctrees(_currentPivot);
    }

    void Update()
    {
        if (target == null) return;
        
        if (!IsInsideCenterCube(target.position))
        {
            ClearAllNodes();
            BuildOctrees(target.position);
        }
    }

    public bool IsInsideCenterCube(Vector3 pos)
    {
        return pos.x >= _centerMin.x && pos.x <= _centerMax.x &&
               pos.y >= _centerMin.y && pos.y <= _centerMax.y &&
               pos.z >= _centerMin.z && pos.z <= _centerMax.z;
    }

    void BuildOctrees(Vector3 pivot)
    {
        _currentPivot = pivot;
        
        for (int i = 0; i < 8; i++)
        {
            CreateOctreeAt(i, pivot);
        }
        
        UpdateCenterCubeBounds();
    }

    void ClearAllNodes()
    {
        for (int i = 0; i < _pool.Capacity; i++)
        {
            if (_pool.IsUsed(i))
                _pool.Return(i);
        }
    }

    void CreateOctreeAt(int octreeIndex, Vector3 pivot)
    {
        float dx = ((octreeIndex & 1) == 0) ? -rootSize : rootSize;
        float dy = ((octreeIndex & 2) == 0) ? -rootSize : rootSize;
        float dz = ((octreeIndex & 4) == 0) ? -rootSize : rootSize;
        
        _pool.TryRent(out _rootIndices[octreeIndex]);
        var root = OctreeNode.CreateEmpty();
        root.ParentIndex = -1;
        root.ChildIndex = octreeIndex;
        root.Depth = 1;
        root.Center = new float3(pivot.x + dx, pivot.y + dy, pivot.z + dz);
        root.Size = rootSize;
        _pool.Set(_rootIndices[octreeIndex], root);
        
        int targetChild = 7 - octreeIndex;
        
        _pool.Subdivide(_rootIndices[octreeIndex]);
        
        root = _pool.Get(_rootIndices[octreeIndex]);
        int child1 = root.GetChild(targetChild);
        _pool.Subdivide(child1);
        
        var level2 = _pool.Get(child1);
        int child2 = level2.GetChild(targetChild);
        _pool.Subdivide(child2);
    }

    void UpdateCenterCubeBounds()
    {
        _centerMin = new float3(float.MaxValue);
        _centerMax = new float3(float.MinValue);
        
        for (int octreeIdx = 0; octreeIdx < 8; octreeIdx++)
        {
            int targetChild = 7 - octreeIdx;
            
            var root = _pool.Get(_rootIndices[octreeIdx]);
            int level2Idx = root.GetChild(targetChild);
            var level2 = _pool.Get(level2Idx);
            int level3Idx = level2.GetChild(targetChild);
            var level3 = _pool.Get(level3Idx);
            int level4Idx = level3.GetChild(targetChild);
            var level4 = _pool.Get(level4Idx);
            
            level4.GetAABB(out float3 nodeMin, out float3 nodeMax);
            
            _centerMin = math.min(_centerMin, nodeMin);
            _centerMax = math.max(_centerMax, nodeMax);
        }
    }

    public Vector3 CenterMin => new Vector3(_centerMin.x, _centerMin.y, _centerMin.z);
    public Vector3 CenterMax => new Vector3(_centerMax.x, _centerMax.y, _centerMax.z);
    public Vector3 CurrentPivot => _currentPivot;
    public float CenterCubeSize => _centerMax.x - _centerMin.x;

    void OnDestroy()
    {
        if (_pool.Capacity > 0)
            _pool.Dispose();
    }

    void OnRenderObject()
    {
        if (_rootIndices == null) return;
        
        _glMaterial.SetPass(0);
        GL.PushMatrix();
        
        for (int i = 0; i < _pool.Capacity; i++)
        {
            if (!_pool.IsUsed(i)) continue;
            var node = _pool.Get(i);
            
            if (node.ParentIndex == -1) continue;
            if (!node.IsLeaf) continue;

            Color nodeColor = node.Depth switch {
                2 => new Color(1f, 0.3f, 0.3f),
                3 => new Color(0.3f, 0.5f, 1f),
                4 => new Color(0.3f, 1f, 0.3f),
                _ => Color.yellow
            };

            GetNodeAABB(node, out Vector3 min, out Vector3 max);
            
            if (showFilledCubes)
            {
                GL.Begin(GL.QUADS);
                GL.Color(new Color(nodeColor.r, nodeColor.g, nodeColor.b, 0.3f));
                DrawFilledCube(min, max);
                GL.End();
            }
            
            if (showWireframe)
            {
                GL.Begin(GL.LINES);
                GL.Color(nodeColor);
                DrawWireframeCube(min, max);
                GL.End();
            }
            
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
        
        if (showCenterCube)
        {
            GL.Begin(GL.LINES);
            GL.Color(Color.yellow);
            DrawWireframeCube(CenterMin, CenterMax);
            GL.End();
            
            GL.Begin(GL.QUADS);
            GL.Color(new Color(1f, 1f, 0f, 0.15f));
            DrawFilledCube(CenterMin, CenterMax);
            GL.End();
        }
        
        if (showWireframe)
        {
            for (int i = 0; i < 8; i++)
            {
                var rootNode = _pool.Get(_rootIndices[i]);
                GetNodeAABB(rootNode, out Vector3 rootMin, out Vector3 rootMax);
                
                GL.Begin(GL.LINES);
                GL.Color(new Color(0.5f, 0.5f, 0.5f, 0.3f));
                DrawWireframeCube(rootMin, rootMax);
                GL.End();
            }
        }
        
        GL.Begin(GL.QUADS);
        GL.Color(Color.white);
        float pivotSize = 2f;
        DrawFilledCube(
            _currentPivot - Vector3.one * pivotSize,
            _currentPivot + Vector3.one * pivotSize
        );
        GL.End();
        
        GL.PopMatrix();
    }

    void GetNodeAABB(OctreeNode node, out Vector3 min, out Vector3 max)
    {
        Vector3 center = new Vector3(node.Center.x, node.Center.y, node.Center.z);
        float size = node.Size;
        int cIdx = node.ChildIndex;
        
        float dx = ((cIdx & 1) == 0) ? size : -size;
        float dy = ((cIdx & 2) == 0) ? size : -size;
        float dz = ((cIdx & 4) == 0) ? size : -size;
        
        Vector3 end = center + new Vector3(dx, dy, dz);
        
        min = Vector3.Min(center, end);
        max = Vector3.Max(center, end);
    }

    void DrawFilledCube(Vector3 min, Vector3 max)
    {
        GL.Vertex3(min.x, min.y, max.z);
        GL.Vertex3(max.x, min.y, max.z);
        GL.Vertex3(max.x, max.y, max.z);
        GL.Vertex3(min.x, max.y, max.z);

        GL.Vertex3(min.x, min.y, min.z);
        GL.Vertex3(min.x, max.y, min.z);
        GL.Vertex3(max.x, max.y, min.z);
        GL.Vertex3(max.x, min.y, min.z);

        GL.Vertex3(min.x, max.y, min.z);
        GL.Vertex3(min.x, max.y, max.z);
        GL.Vertex3(max.x, max.y, max.z);
        GL.Vertex3(max.x, max.y, min.z);

        GL.Vertex3(min.x, min.y, min.z);
        GL.Vertex3(max.x, min.y, min.z);
        GL.Vertex3(max.x, min.y, max.z);
        GL.Vertex3(min.x, min.y, max.z);

        GL.Vertex3(max.x, min.y, min.z);
        GL.Vertex3(max.x, max.y, min.z);
        GL.Vertex3(max.x, max.y, max.z);
        GL.Vertex3(max.x, min.y, max.z);

        GL.Vertex3(min.x, min.y, min.z);
        GL.Vertex3(min.x, min.y, max.z);
        GL.Vertex3(min.x, max.y, max.z);
        GL.Vertex3(min.x, max.y, min.z);
    }

    void DrawWireframeCube(Vector3 min, Vector3 max)
    {
        GL.Vertex3(min.x, min.y, min.z); GL.Vertex3(max.x, min.y, min.z);
        GL.Vertex3(max.x, min.y, min.z); GL.Vertex3(max.x, min.y, max.z);
        GL.Vertex3(max.x, min.y, max.z); GL.Vertex3(min.x, min.y, max.z);
        GL.Vertex3(min.x, min.y, max.z); GL.Vertex3(min.x, min.y, min.z);
        
        GL.Vertex3(min.x, max.y, min.z); GL.Vertex3(max.x, max.y, min.z);
        GL.Vertex3(max.x, max.y, min.z); GL.Vertex3(max.x, max.y, max.z);
        GL.Vertex3(max.x, max.y, max.z); GL.Vertex3(min.x, max.y, max.z);
        GL.Vertex3(min.x, max.y, max.z); GL.Vertex3(min.x, max.y, min.z);
        
        GL.Vertex3(min.x, min.y, min.z); GL.Vertex3(min.x, max.y, min.z);
        GL.Vertex3(max.x, min.y, min.z); GL.Vertex3(max.x, max.y, min.z);
        GL.Vertex3(max.x, min.y, max.z); GL.Vertex3(max.x, max.y, max.z);
        GL.Vertex3(min.x, min.y, max.z); GL.Vertex3(min.x, max.y, max.z);
    }
}