using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// 청크의 기본 정보를 담는 컴포넌트
/// ★ 옥트리 연동을 위해 월드 좌표 정보 추가
/// </summary>
public struct ChunkData : IComponentData
{
    public int3 ChunkPosition;  // 청크 그리드 내 위치 (호환용)
    public int ChunkSize;       // 청크 한 변의 복셀 수
    
    // ★ 옥트리 노드 정보
    public int NodeIndex;       // OctreeNodePool 내 인덱스
    public int Depth;           // 옥트리 깊이 (LOD)
    public float3 Center;       // 노드 중심 (월드)
    public float Size;          // 노드 크기 (월드)
    public float3 Min;          // AABB 최소점
    public float3 Max;          // AABB 최대점
}