using Unity.Entities;
using Unity.Mathematics;

namespace RealmForge.Planet.Chunk.Components
{
    /// <summary>
    /// 청크의 기본 정보를 담는 컴포넌트
    /// </summary>
    public struct ChunkData : IComponentData
    {
        public int3 ChunkPosition;  // 청크 그리드 내 위치
        public int ChunkSize;       // 청크 한 변의 크기
    }
}