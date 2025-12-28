using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class OctreeChunkSpawnerSystem : SystemBase
{
    private NativeHashMap<int, Entity> _nodeToEntity;
    private Entity _planetEntity;
    private bool _initialized;

    private NoiseSettings _cachedNoiseSettings;
    private PlanetData _cachedPlanetData;
    private PlanetChunkSettings _cachedChunkSettings;

    protected override void OnCreate()
    {
        _nodeToEntity = new NativeHashMap<int, Entity>(50000, Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        if (_nodeToEntity.IsCreated)
            _nodeToEntity.Dispose();
    }

    protected override void OnUpdate()
    {
        if (!World.Name.Contains("Client")) return;

        if (OctreeManager.Instance == null) return;

        if (!_initialized)
        {
            var query = GetEntityQuery(typeof(PlanetTag));
            if (query.CalculateEntityCount() == 0) return;

            _planetEntity = query.GetSingletonEntity();

            _cachedPlanetData = EntityManager.GetComponentData<PlanetData>(_planetEntity);
            _cachedChunkSettings = EntityManager.GetComponentData<PlanetChunkSettings>(_planetEntity);
            _cachedNoiseSettings = EntityManager.GetComponentData<NoiseSettings>(_planetEntity);

            _initialized = true;
            Debug.Log($"[OctreeChunkSpawner] Initialized");
        }

        var pool = OctreeManager.Instance.GetPool();
        if (pool.Capacity == 0) return;

        int newChunks = 0;

        for (int i = 0; i < pool.Capacity; i++)
        {
            if (!pool.IsUsed(i)) continue;

            var node = pool.Get(i);

            if (!node.IsLeaf) continue;
            if (_nodeToEntity.ContainsKey(i)) continue;

            var entity = CreateChunkEntity(i, node);
            _nodeToEntity.Add(i, entity);
            newChunks++;
        }

        if (newChunks > 0)
        {
            Debug.Log($"[OctreeChunkSpawner] New chunks: {newChunks}, Total: {_nodeToEntity.Count}");
        }

        var toRemove = new NativeList<int>(Allocator.Temp);

        foreach (var kvp in _nodeToEntity)
        {
            if (!pool.IsUsed(kvp.Key) || !pool.Get(kvp.Key).IsLeaf)
            {
                if (EntityManager.Exists(kvp.Value))
                    EntityManager.DestroyEntity(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var key in toRemove)
            _nodeToEntity.Remove(key);

        toRemove.Dispose();
    }

    private Entity CreateChunkEntity(int nodeIndex, OctreeNode node)
    {
        var entity = EntityManager.CreateEntity();

        node.GetAABB(out float3 min, out float3 max);

        EntityManager.AddComponentData(entity, new ChunkData
        {
            ChunkPosition = new int3(
                (int)math.floor(node.Center.x / node.Size),
                (int)math.floor(node.Center.y / node.Size),
                (int)math.floor(node.Center.z / node.Size)
            ),
            ChunkSize = _cachedChunkSettings.ChunkSize,
            NodeIndex = nodeIndex,
            Depth = node.Depth,
            Center = node.Center,
            Size = node.Size,
            Min = min,
            Max = max
        });

        EntityManager.AddComponentData(entity, _cachedPlanetData);
        EntityManager.AddComponentData(entity, _cachedNoiseSettings);

        EntityManager.AddComponent<NoiseGenerationRequest>(entity);

        EntityManager.AddComponent<MeshGenerationRequest>(entity);
        EntityManager.SetComponentEnabled<MeshGenerationRequest>(entity, false);

        EntityManager.AddComponent<NoiseVisualizationReady>(entity);
        EntityManager.SetComponentEnabled<NoiseVisualizationReady>(entity, false);

        EntityManager.AddBuffer<NoiseDataBuffer>(entity);

        return entity;
    }

    public bool TryGetChunkEntity(int nodeIndex, out Entity entity)
    {
        return _nodeToEntity.TryGetValue(nodeIndex, out entity);
    }

    public int ActiveChunkCount => _nodeToEntity.Count;
}
